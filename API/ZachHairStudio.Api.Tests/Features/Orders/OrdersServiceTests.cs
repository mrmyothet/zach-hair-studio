using FluentValidation.TestHelper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Loyalty;
using ZachHairStudio.Shared.Features.Orders;
using ZachHairStudio.Shared.Features.Payments;

namespace ZachHairStudio.Api.Tests.Features.Orders;

public class OrdersServiceTests
{
    [Fact]
    public async Task PriceAuthority_CreateCheckoutAsync_UsesCatalogPriceIgnoringClientMoneyAbsence()
    {
        await using var harness = await CreateSqliteHarnessAsync();
        await SetCatalogAsync(harness.Db, productId: 1, price: 25.00m, stock: 10, name: "Serum");

        var service = CreateService(harness.Db, new FakePaymentProvider());
        var result = await service.CreateCheckoutAsync(new CheckoutRequestDto
        {
            Email = "guest@example.com",
            Items = [new CheckoutLineItemDto { ProductId = 1, Quantity = 2 }],
        });

        Assert.True(result.IsSuccess, result.Message);
        var order = await harness.Db.Orders.Include(o => o.Items).SingleAsync();
        Assert.Equal(50.00m, order.TotalAmount);
        var line = Assert.Single(order.Items);
        Assert.Equal(25.00m, line.UnitPrice);
        Assert.Equal(50.00m, line.LineTotal);
        Assert.Equal("Serum", line.ProductName);
    }

    [Fact]
    public async Task GuestCheckout_CreateCheckoutAsync_SetsClientIdNullAndStatusPending()
    {
        await using var harness = await CreateSqliteHarnessAsync();
        await SetCatalogAsync(harness.Db, productId: 1, price: 25.00m, stock: 10, name: "Serum");

        var service = CreateService(harness.Db, new FakePaymentProvider());
        var result = await service.CreateCheckoutAsync(new CheckoutRequestDto
        {
            Email = "guest@example.com",
            Name = "Guest",
            Items = [new CheckoutLineItemDto { ProductId = 1, Quantity = 1 }],
        });

        Assert.True(result.IsSuccess, result.Message);
        var order = await harness.Db.Orders.SingleAsync();
        Assert.Null(order.ClientId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal($"https://example.test/checkout/{order.Id}", result.Data.CheckoutUrl);
        Assert.Equal($"fake-{order.Id}", order.StripeSessionId);
    }

    [Fact]
    public async Task CreateCheckoutAsync_InsufficientStock_IsConflictAndStockUnchanged()
    {
        await using var harness = await CreateSqliteHarnessAsync();
        await SetCatalogAsync(harness.Db, productId: 1, price: 25.00m, stock: 1, name: "Serum");

        var service = CreateService(harness.Db, new FakePaymentProvider());
        var result = await service.CreateCheckoutAsync(new CheckoutRequestDto
        {
            Email = "guest@example.com",
            Items = [new CheckoutLineItemDto { ProductId = 1, Quantity = 2 }],
        });

        Assert.True(result.IsConflict());
        Assert.False(await harness.Db.Orders.AnyAsync());
        Assert.Equal(1, await harness.Db.Products.Where(p => p.Id == 1).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task CreateCheckoutAsync_PaymentProviderFailure_RestoresStockAndMarksFailed()
    {
        await using var harness = await CreateSqliteHarnessAsync();
        await SetCatalogAsync(harness.Db, productId: 1, price: 25.00m, stock: 5, name: "Serum");

        var service = CreateService(harness.Db, new ThrowingPaymentProvider());
        var result = await service.CreateCheckoutAsync(new CheckoutRequestDto
        {
            Email = "guest@example.com",
            Items = [new CheckoutLineItemDto { ProductId = 1, Quantity = 2 }],
        });

        Assert.True(result.IsError || result.IsSystemError());
        var order = await harness.Db.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal(5, await harness.Db.Products.Where(p => p.Id == 1).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task MarkFulfilledAsync_PendingToFulfilled_IsIdempotent()
    {
        await using var harness = await CreateSqliteHarnessAsync();
        var order = new Order
        {
            ClientId = null,
            Status = OrderStatus.Pending,
            TotalAmount = 25m,
            Email = "guest@example.com",
            StripeSessionId = "fake-99",
            PlacedAtUtc = DateTimeOffset.UtcNow,
            Items =
            [
                new OrderItem
                {
                    ProductId = 1,
                    ProductName = "Serum",
                    UnitPrice = 25m,
                    Quantity = 1,
                    LineTotal = 25m,
                },
            ],
        };
        harness.Db.Orders.Add(order);
        await harness.Db.SaveChangesAsync();

        var service = CreateService(harness.Db, new FakePaymentProvider());
        var first = await service.MarkFulfilledAsync(order.Id.ToString(), "fake-99");
        var second = await service.MarkFulfilledAsync(order.Id.ToString(), "fake-99");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(OrderStatus.Fulfilled, await harness.Db.Orders.Where(o => o.Id == order.Id).Select(o => o.Status).SingleAsync());
    }

    [Fact]
    public void CheckoutRequestDtoValidator_RejectsEmptyEmailAndBadQuantity()
    {
        var validator = new CheckoutRequestDtoValidator();
        var result = validator.TestValidate(new CheckoutRequestDto
        {
            Email = "",
            Items = [new CheckoutLineItemDto { ProductId = 1, Quantity = 0 }],
        });

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor("Items[0].Quantity");
    }

    [Fact]
    public void CheckoutRequestDto_HasNoPriceOrTotalProperties()
    {
        var names = typeof(CheckoutRequestDto).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Price", names);
        Assert.DoesNotContain("UnitPrice", names);
        Assert.DoesNotContain("LineTotal", names);
        Assert.DoesNotContain("Total", names);
        Assert.DoesNotContain("TotalAmount", names);
    }

    private static OrdersService CreateService(BookingDbContext db, IPaymentProvider paymentProvider)
        => new OrdersService(db, paymentProvider, new CheckoutRequestDtoValidator(), new LoyaltyService(db));

    private static async Task SetCatalogAsync(
        BookingDbContext db,
        int productId,
        decimal price,
        int stock,
        string name)
    {
        var product = await db.Products.SingleAsync(p => p.Id == productId);
        product.Price = price;
        product.Stock = stock;
        product.Name = name;
        product.IsActive = true;
        await db.SaveChangesAsync();
    }

    private static async Task<SqliteHarness> CreateSqliteHarnessAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new BookingDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return new SqliteHarness(connection, db);
    }

    private sealed class ThrowingPaymentProvider : IPaymentProvider
    {
        public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
            CheckoutSessionRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated payment provider failure.");
    }

    private sealed class SqliteHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public SqliteHarness(SqliteConnection connection, BookingDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public BookingDbContext Db { get; }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
