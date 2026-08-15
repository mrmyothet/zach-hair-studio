using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Orders;

namespace ZachHairStudio.Api.Tests.Features.Payments;

/// <summary>
/// SHOP-05 / LAUNCH-04 regression: the webhook must distinguish a transient miss
/// (retry) from a terminal state (ack). Returning 200 for everything let Stripe
/// treat a dropped fulfillment as handled, so paid-but-unfulfilled orders piled up
/// invisibly; returning 5xx for everything would make Stripe retry a state that no
/// redelivery can fix.
/// </summary>
public class StripeWebhookRetryTests : IClassFixture<SqliteWebApplicationFactory>
{
    private readonly SqliteWebApplicationFactory _factory;

    public StripeWebhookRetryTests(SqliteWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Webhook_UnknownOrder_Returns503SoStripeRetries()
    {
        // No order with this session id — models the webhook outrunning the
        // checkout transaction's commit.
        var json = StripeWebhookTests.BuildCheckoutSessionCompletedJson(
            sessionId: "cs_test_never_persisted",
            clientReferenceId: "999999",
            paymentStatus: "paid");

        var client = _factory.CreateClient();
        var response = await StripeWebhookTests.PostSignedWebhookAsync(client, json);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_TerminalStatusOrder_Returns200AndDoesNotRetry()
    {
        var orderId = await SeedOrderAsync("cs_test_terminal_1", OrderStatus.Failed);
        var json = StripeWebhookTests.BuildCheckoutSessionCompletedJson(
            sessionId: "cs_test_terminal_1",
            clientReferenceId: orderId.ToString(),
            paymentStatus: "paid");

        var client = _factory.CreateClient();
        var response = await StripeWebhookTests.PostSignedWebhookAsync(client, json);

        // Ack: retrying a Failed order forever cannot fulfill it. The LogError is
        // the alarm; the 200 stops Stripe hammering the endpoint for days.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var status = await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
        Assert.Equal(OrderStatus.Failed, status);
    }

    private async Task<int> SeedOrderAsync(string stripeSessionId, OrderStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var order = new Order
        {
            ClientId = null,
            Status = status,
            TotalAmount = 25m,
            Email = "guest@example.com",
            StripeSessionId = stripeSessionId,
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
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}
