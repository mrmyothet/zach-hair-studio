using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Orders;
using ZachHairStudio.Shared.Features.Payments;

namespace ZachHairStudio.Api.Tests.Features.Orders;

/// <summary>
/// Plan 05 RED/GREEN coverage for the thin Pending→Fulfilled flip (SHOP-05).
/// Already Fulfilled is a success no-op; stock is never touched.
/// </summary>
public class MarkFulfilledTests
{
    [Fact]
    public async Task MarkFulfilledAsync_AlreadyFulfilled_IsNoOpSuccess()
    {
        await using var harness = await CreateSqliteHarnessAsync();
        var order = new Order
        {
            ClientId = null,
            Status = OrderStatus.Fulfilled,
            TotalAmount = 25m,
            Email = "guest@example.com",
            StripeSessionId = "cs_already_fulfilled",
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

        var productStockBefore = await harness.Db.Products
            .Where(p => p.Id == 1)
            .Select(p => p.Stock)
            .SingleAsync();

        var service = new OrdersService(
            harness.Db,
            new FakePaymentProvider(),
            new CheckoutRequestDtoValidator(),
            new ZachHairStudio.Shared.Features.Loyalty.LoyaltyService(harness.Db));
        var result = await service.MarkFulfilledAsync(order.Id.ToString(), "cs_already_fulfilled");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(OrderStatus.Fulfilled, result.Data.Status);
        Assert.Equal(
            productStockBefore,
            await harness.Db.Products.Where(p => p.Id == 1).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task MarkFulfilledAsync_Pending_FlipsToFulfilled()
    {
        await using var harness = await CreateSqliteHarnessAsync();
        var order = new Order
        {
            ClientId = null,
            Status = OrderStatus.Pending,
            TotalAmount = 25m,
            Email = "guest@example.com",
            StripeSessionId = "cs_pending_flip",
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

        var service = new OrdersService(
            harness.Db,
            new FakePaymentProvider(),
            new CheckoutRequestDtoValidator(),
            new ZachHairStudio.Shared.Features.Loyalty.LoyaltyService(harness.Db));
        var result = await service.MarkFulfilledAsync(order.Id.ToString(), "cs_pending_flip");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(
            OrderStatus.Fulfilled,
            await harness.Db.Orders.Where(o => o.Id == order.Id).Select(o => o.Status).SingleAsync());
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
