using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Loyalty;
using ZachHairStudio.Shared.Features.Payments;
using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Shared.Features.Orders;

/// <summary>
/// Guest/authenticated checkout write path (SHOP-02/03/04/06 + ACCT-07). Recomputes money from
/// <see cref="Product.Price"/>, optionally applies server loyalty dollars after catalog
/// recompute (D-15), decrements stock with conditional <c>ExecuteUpdateAsync</c> inside
/// CreateExecutionStrategy + transaction (D-04/D-05), then creates a payment session.
/// </summary>
public class OrdersService
{
    private readonly BookingDbContext _dbContext;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IValidator<CheckoutRequestDto> _validator;
    private readonly LoyaltyService _loyaltyService;

    public OrdersService(
        BookingDbContext dbContext,
        IPaymentProvider paymentProvider,
        IValidator<CheckoutRequestDto> validator,
        LoyaltyService loyaltyService)
    {
        _dbContext = dbContext;
        _paymentProvider = paymentProvider;
        _validator = validator;
        _loyaltyService = loyaltyService;
    }

    public async Task<Result<CheckoutResponseDto>> CreateCheckoutAsync(
        CheckoutRequestDto request,
        int? clientUserId = null,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CheckoutResponseDto>.ValidationError(
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        var redeemPoints = request.RedeemPoints ?? 0;
        if (redeemPoints > 0 && !clientUserId.HasValue)
        {
            return Result<CheckoutResponseDto>.ValidationError(
                "Sign in to redeem loyalty points.");
        }

        var catalog = await LoadCatalogLinesAsync(request, cancellationToken);
        if (!catalog.IsSuccess)
        {
            return Result<CheckoutResponseDto>.NotFoundError(catalog.Message);
        }

        var merchandiseSubtotal = catalog.Data.Sum(line => line.LineTotal);
        decimal loyaltyDiscount = 0m;
        var pointsRedeemed = 0;

        if (redeemPoints > 0)
        {
            var quote = await _loyaltyService.QuoteRedeemAsync(
                clientUserId!.Value,
                redeemPoints,
                merchandiseSubtotal,
                cancellationToken);
            if (!quote.IsSuccess)
            {
                return Result<CheckoutResponseDto>.ValidationError(quote.Message);
            }

            loyaltyDiscount = quote.Data.LoyaltyDiscount;
            pointsRedeemed = quote.Data.PointsRedeemed;
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var order = new Order
            {
                ClientId = clientUserId,
                Status = OrderStatus.Pending,
                Email = request.Email.Trim(),
                CustomerName = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
                PlacedAtUtc = DateTimeOffset.UtcNow,
            };

            foreach (var line in catalog.Data)
            {
                var updated = await _dbContext.Products
                    .Where(p => p.Id == line.ProductId && p.Stock >= line.Quantity)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(p => p.Stock, p => p.Stock - line.Quantity),
                        cancellationToken);

                if (updated == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    var currentStock = await _dbContext.Products
                        .Where(p => p.Id == line.ProductId)
                        .Select(p => p.Stock)
                        .SingleAsync(cancellationToken);
                    return Result<CheckoutResponseDto>.ConflictError(
                        $"Sorry, only {currentStock} left of {line.ProductName}.");
                }

                order.Items.Add(new OrderItem
                {
                    ProductId = line.ProductId,
                    ProductName = line.ProductName,
                    UnitPrice = line.UnitPrice,
                    Quantity = line.Quantity,
                    LineTotal = line.LineTotal,
                });
            }

            // Catalog recompute first, then server loyalty dollars (D-15).
            var subtotal = order.Items.Sum(item => item.LineTotal);
            order.TotalAmount = subtotal - loyaltyDiscount;

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (pointsRedeemed > 0 && clientUserId.HasValue)
            {
                // Re-check balance inside the transaction before appending redeem.
                var balance = await _dbContext.LoyaltyLedgers
                    .Where(row => row.ClientUserId == clientUserId.Value)
                    .SumAsync(row => (int?)row.Delta, cancellationToken) ?? 0;
                if (pointsRedeemed > balance)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CheckoutResponseDto>.ValidationError("Insufficient loyalty balance.");
                }

                _loyaltyService.AppendRedeem(clientUserId.Value, pointsRedeemed, order.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            try
            {
                var session = await _paymentProvider.CreateCheckoutSessionAsync(
                    new CheckoutSessionRequest(
                        order.Id,
                        order.TotalAmount,
                        order.Email,
                        order.Items
                            .Select(item => new CheckoutLine(item.ProductName, item.UnitPrice, item.Quantity))
                            .ToList()),
                    cancellationToken);

                order.StripeSessionId = session.SessionId;
                order.StripeSessionUrl = session.Url;
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result<CheckoutResponseDto>.Success(
                    order.ToCheckoutResponseDto(session.Url, subtotal, loyaltyDiscount, pointsRedeemed));
            }
            catch (Exception)
            {
                await CompensateFailedPaymentAsync(order, pointsRedeemed, clientUserId, cancellationToken);
                return Result<CheckoutResponseDto>.SystemError(
                    "Checkout could not start payment. Please try again.");
            }
        });
    }

    /// <summary>
    /// Catalog recompute + loyalty quote without stock decrement or Stripe (Apply Points preview).
    /// </summary>
    public async Task<Result<LoyaltyQuoteDto>> QuoteCheckoutAsync(
        CheckoutRequestDto request,
        int? clientUserId = null,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<LoyaltyQuoteDto>.ValidationError(
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        var redeemPoints = request.RedeemPoints ?? 0;
        if (redeemPoints > 0 && !clientUserId.HasValue)
        {
            return Result<LoyaltyQuoteDto>.ValidationError(
                "Sign in to redeem loyalty points.");
        }

        var catalog = await LoadCatalogLinesAsync(request, cancellationToken);
        if (!catalog.IsSuccess)
        {
            return Result<LoyaltyQuoteDto>.NotFoundError(catalog.Message);
        }

        var merchandiseSubtotal = catalog.Data.Sum(line => line.LineTotal);

        if (clientUserId.HasValue)
        {
            return await _loyaltyService.QuoteRedeemAsync(
                clientUserId.Value,
                redeemPoints,
                merchandiseSubtotal,
                cancellationToken);
        }

        return Result<LoyaltyQuoteDto>.Success(new LoyaltyQuoteDto
        {
            Subtotal = merchandiseSubtotal,
            LoyaltyDiscount = 0m,
            TotalAmount = merchandiseSubtotal,
            PointsRedeemed = 0,
            Balance = 0,
        });
    }

    /// <summary>
    /// Thin idempotent Pending→Fulfilled flip for Plan 05 webhook wiring.
    /// Already Fulfilled is a success no-op. Does not touch stock.
    /// </summary>
    public async Task<Result<Order>> MarkFulfilledAsync(
        string? orderIdOrClientReference,
        string? stripeSessionId,
        CancellationToken cancellationToken = default)
    {
        Order? order = null;

        if (!string.IsNullOrWhiteSpace(stripeSessionId))
        {
            order = await _dbContext.Orders
                .FirstOrDefaultAsync(o => o.StripeSessionId == stripeSessionId, cancellationToken);
        }

        if (order is null
            && !string.IsNullOrWhiteSpace(orderIdOrClientReference)
            && int.TryParse(orderIdOrClientReference, out var orderId))
        {
            order = await _dbContext.Orders.FindAsync([orderId], cancellationToken);
        }

        if (order is null)
        {
            return Result<Order>.NotFoundError("Order not found.");
        }

        if (order.Status == OrderStatus.Fulfilled)
        {
            return Result<Order>.Success(order);
        }

        if (order.Status != OrderStatus.Pending)
        {
            return Result<Order>.ValidationError(
                $"Order {order.Id} cannot be fulfilled from status {order.Status}.");
        }

        order.Status = OrderStatus.Fulfilled;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<Order>.Success(order);
    }

    /// <summary>
    /// ACCT-06: guest read for /checkout/success. The order id alone is enumerable,
    /// so the unguessable payment-session id is required as a second factor. A wrong
    /// or missing session yields NotFound — never a distinguishable "exists but
    /// forbidden", which would leak which order ids are real.
    /// Authenticated users are served by the owner-scoped /api/account/orders/{id}.
    /// </summary>
    public async Task<Result<OrderResponseDto>> GetByIdAsync(
        int orderId,
        string? sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Result<OrderResponseDto>.NotFoundError($"Order {orderId} not found.");
        }

        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null
            || string.IsNullOrEmpty(order.StripeSessionId)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(order.StripeSessionId),
                Encoding.UTF8.GetBytes(sessionId)))
        {
            return Result<OrderResponseDto>.NotFoundError($"Order {orderId} not found.");
        }

        return Result<OrderResponseDto>.Success(order.ToResponseDto());
    }

    private async Task<Result<List<CatalogLine>>> LoadCatalogLinesAsync(
        CheckoutRequestDto request,
        CancellationToken cancellationToken)
    {
        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id) && product.IsActive)
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        foreach (var line in request.Items)
        {
            if (!products.ContainsKey(line.ProductId))
            {
                return Result<List<CatalogLine>>.NotFoundError(
                    $"Product {line.ProductId} not found.");
            }
        }

        var lines = request.Items.Select(line =>
        {
            var product = products[line.ProductId];
            return new CatalogLine(
                product.Id,
                product.Name,
                product.Price,
                line.Quantity,
                product.Price * line.Quantity);
        }).ToList();

        return Result<List<CatalogLine>>.Success(lines);
    }

    private async Task CompensateFailedPaymentAsync(
        Order order,
        int pointsRedeemed,
        int? clientUserId,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            foreach (var item in order.Items)
            {
                await _dbContext.Products
                    .Where(p => p.Id == item.ProductId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(p => p.Stock, p => p.Stock + item.Quantity),
                        cancellationToken);
            }

            if (pointsRedeemed > 0 && clientUserId.HasValue)
            {
                // Reverse redeem: append compensating Earn-style positive delta with Redeem reason
                // is wrong — append a positive offset linked to the failed order instead.
                _dbContext.LoyaltyLedgers.Add(new LoyaltyLedger
                {
                    ClientUserId = clientUserId.Value,
                    Delta = pointsRedeemed,
                    Reason = LoyaltyReasons.Redeem,
                    OrderId = order.Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                });
            }

            var tracked = await _dbContext.Orders.FirstAsync(o => o.Id == order.Id, cancellationToken);
            tracked.Status = OrderStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            order.Status = OrderStatus.Failed;
        });
    }

    private sealed record CatalogLine(
        int ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity,
        decimal LineTotal);
}
