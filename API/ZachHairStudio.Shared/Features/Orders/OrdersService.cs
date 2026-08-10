using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Payments;
using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Shared.Features.Orders;

/// <summary>
/// Guest checkout write path (SHOP-02/03/04/06). Recomputes money from
/// <see cref="Product.Price"/>, decrements stock with conditional
/// <c>ExecuteUpdateAsync</c> inside CreateExecutionStrategy + transaction (D-04/D-05),
/// then creates a payment session. Fulfillment is a separate thin flip (SHOP-05).
/// </summary>
public class OrdersService
{
    private readonly BookingDbContext _dbContext;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IValidator<CheckoutRequestDto> _validator;

    public OrdersService(
        BookingDbContext dbContext,
        IPaymentProvider paymentProvider,
        IValidator<CheckoutRequestDto> validator)
    {
        _dbContext = dbContext;
        _paymentProvider = paymentProvider;
        _validator = validator;
    }

    public async Task<Result<CheckoutResponseDto>> CreateCheckoutAsync(
        CheckoutRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CheckoutResponseDto>.ValidationError(
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .Where(product => productIds.Contains(product.Id) && product.IsActive)
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        foreach (var line in request.Items)
        {
            if (!products.ContainsKey(line.ProductId))
            {
                return Result<CheckoutResponseDto>.NotFoundError(
                    $"Product {line.ProductId} not found.");
            }
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var order = new Order
            {
                ClientId = null,
                Status = OrderStatus.Pending,
                Email = request.Email.Trim(),
                CustomerName = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
                PlacedAtUtc = DateTimeOffset.UtcNow,
            };

            foreach (var line in request.Items)
            {
                var product = products[line.ProductId];

                var updated = await _dbContext.Products
                    .Where(p => p.Id == line.ProductId && p.Stock >= line.Quantity)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(p => p.Stock, p => p.Stock - line.Quantity),
                        cancellationToken);

                if (updated == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    // Reload display stock for a helpful message (may still race; message is best-effort).
                    var currentStock = await _dbContext.Products
                        .Where(p => p.Id == line.ProductId)
                        .Select(p => p.Stock)
                        .SingleAsync(cancellationToken);
                    return Result<CheckoutResponseDto>.ConflictError(
                        $"Sorry, only {currentStock} left of {product.Name}.");
                }

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = line.Quantity,
                    LineTotal = product.Price * line.Quantity,
                });
            }

            order.TotalAmount = order.Items.Sum(item => item.LineTotal);
            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);
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

                return Result<CheckoutResponseDto>.Success(order.ToCheckoutResponseDto(session.Url));
            }
            catch (Exception)
            {
                await CompensateFailedPaymentAsync(order, cancellationToken);
                return Result<CheckoutResponseDto>.SystemError(
                    "Checkout could not start payment. Please try again.");
            }
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

    public async Task<Result<OrderResponseDto>> GetByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return Result<OrderResponseDto>.NotFoundError($"Order {orderId} not found.");
        }

        return Result<OrderResponseDto>.Success(order.ToResponseDto());
    }

    private async Task CompensateFailedPaymentAsync(Order order, CancellationToken cancellationToken)
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

            // Re-attach status update if the instance is still tracked; otherwise reload.
            var tracked = await _dbContext.Orders.FirstAsync(o => o.Id == order.Id, cancellationToken);
            tracked.Status = OrderStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            order.Status = OrderStatus.Failed;
        });
    }
}
