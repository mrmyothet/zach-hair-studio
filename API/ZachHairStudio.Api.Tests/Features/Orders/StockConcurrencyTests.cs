using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Orders;

namespace ZachHairStudio.Api.Tests.Features.Orders;

/// <summary>
/// Proves SHOP-04 / D-04 against REAL SQL Server: two near-simultaneous checkouts
/// for the last unit yield exactly one success and one 409, and Stock ends at 0.
/// Must not run on EF InMemory (ExecuteUpdateAsync / row locking).
/// </summary>
public class StockConcurrencyTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string SessionHeaderName = "X-Cart-Session-Id";
    private const int ProductId = 1;

    private readonly SqlServerWebApplicationFactory _factory;

    public StockConcurrencyTests(SqlServerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TwoParallelCheckoutsForLastUnit_ExactlyOneSuccessAndOne409()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var product = await db.Products.SingleAsync(p => p.Id == ProductId);
            product.Stock = 1;
            product.IsActive = true;
            await db.SaveChangesAsync();
        }

        var request = new CheckoutRequestDto
        {
            Email = "guest@example.com",
            Items = [new CheckoutLineItemDto { ProductId = ProductId, Quantity = 1 }],
        };

        var client1 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Add(SessionHeaderName, Guid.NewGuid().ToString());
        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Add(SessionHeaderName, Guid.NewGuid().ToString());

        var task1 = client1.PostAsJsonAsync("/api/orders/checkout", request);
        var task2 = client2.PostAsJsonAsync("/api/orders/checkout", request);
        var responses = await Task.WhenAll(task1, task2);

        var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToList();
        Assert.Contains(statusCodes, code => (int)code is >= 200 and < 300);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);
        Assert.Equal(2, statusCodes.Count);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var stock = await db.Products.Where(p => p.Id == ProductId).Select(p => p.Stock).SingleAsync();
            Assert.Equal(0, stock);
        }
    }
}
