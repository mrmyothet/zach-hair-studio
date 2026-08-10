using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Orders;

namespace ZachHairStudio.Api.Tests.Features.Payments;

/// <summary>
/// SHOP-05: signature reject + paid→Fulfilled + idempotent redelivery.
/// Uses synthetic Stripe-Signature headers (no Stripe CLI required).
/// </summary>
public class StripeWebhookTests : IClassFixture<SqliteWebApplicationFactory>
{
    internal const string TestWebhookSecret = "whsec_test_zach_hair_studio_webhook";

    private readonly SqliteWebApplicationFactory _factory;

    public StripeWebhookTests(SqliteWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Webhook_MissingStripeSignature_Returns400()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/stripe/webhook", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_InvalidStripeSignature_Returns400()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", "t=1,v1=deadbeef");

        var response = await client.PostAsync("/api/stripe/webhook", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_ValidCheckoutSessionCompletedPaid_MarksOrderFulfilled()
    {
        var orderId = await SeedPendingOrderAsync("cs_test_paid_1");
        var json = BuildCheckoutSessionCompletedJson(
            sessionId: "cs_test_paid_1",
            clientReferenceId: orderId.ToString(),
            paymentStatus: "paid");

        var client = _factory.CreateClient();
        var response = await PostSignedWebhookAsync(client, json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var status = await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
        Assert.Equal(OrderStatus.Fulfilled, status);
    }

    [Fact]
    public async Task Webhook_IdenticalRedelivery_Returns200AndStaysFulfilledOnce()
    {
        var orderId = await SeedPendingOrderAsync("cs_test_paid_2");
        var json = BuildCheckoutSessionCompletedJson(
            sessionId: "cs_test_paid_2",
            clientReferenceId: orderId.ToString(),
            paymentStatus: "paid");

        var client = _factory.CreateClient();
        var first = await PostSignedWebhookAsync(client, json);
        var second = await PostSignedWebhookAsync(client, json);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var order = await db.Orders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
        Assert.Equal(1, await db.Orders.CountAsync(o => o.Id == orderId && o.Status == OrderStatus.Fulfilled));
    }

    private async Task<int> SeedPendingOrderAsync(string stripeSessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var order = new Order
        {
            ClientId = null,
            Status = OrderStatus.Pending,
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

    internal static async Task<HttpResponseMessage> PostSignedWebhookAsync(HttpClient client, string json)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = EventUtility.ComputeSignature(TestWebhookSecret, timestamp, json);
        var header = $"t={timestamp},v1={signature}";

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", header);
        return await client.PostAsync("/api/stripe/webhook", content);
    }

    internal static string BuildCheckoutSessionCompletedJson(
        string sessionId,
        string clientReferenceId,
        string paymentStatus)
    {
        // Minimal checkout.session.completed payload; ConstructEvent is called with
        // throwOnApiVersionMismatch: false so synthetic fixtures stay stable across SDK bumps.
        return $$"""
            {
              "id": "evt_test_{{sessionId}}",
              "object": "event",
              "api_version": "2024-06-20",
              "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
              "type": "checkout.session.completed",
              "livemode": false,
              "pending_webhooks": 1,
              "request": { "id": null, "idempotency_key": null },
              "data": {
                "object": {
                  "id": "{{sessionId}}",
                  "object": "checkout.session",
                  "client_reference_id": "{{clientReferenceId}}",
                  "payment_status": "{{paymentStatus}}",
                  "mode": "payment",
                  "status": "complete"
                }
              }
            }
            """;
    }
}
