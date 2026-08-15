using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ZachHairStudio.Shared.Features.Orders;

namespace ZachHairStudio.Api.Tests.Features.Orders;

/// <summary>
/// ACCT-06 regression: GET /api/orders/{id} is anonymous by design (guest success
/// page) but must require the payment-session id as a second factor. Without it the
/// order id is an enumerable handle to another customer's email, name, and items.
/// </summary>
public class OrderReadScopingTests : IClassFixture<SqliteWebApplicationFactory>
{
    private const string SessionHeaderName = "X-Cart-Session-Id";

    private readonly SqliteWebApplicationFactory _factory;

    public OrderReadScopingTests(SqliteWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Places a real guest order; returns its id and payment-session id.</summary>
    private async Task<(int OrderId, string SessionId)> PlaceOrderAsync(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(SessionHeaderName, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync(
            "/api/orders/checkout",
            new CheckoutRequestDto
            {
                Email = "guest@example.com",
                Items = [new CheckoutLineItemDto { ProductId = 1, Quantity = 1 }],
            });

        var body = await response.Content.ReadFromJsonAsync<CheckoutResponseDto>();
        Assert.NotNull(body);

        // FakePaymentProvider (test host) derives the session id from the order id.
        return (body.OrderId, $"fake-{body.OrderId}");
    }

    [Fact]
    public async Task GetById_CorrectSession_ReturnsOrder()
    {
        var client = _factory.CreateClient();
        var (orderId, sessionId) = await PlaceOrderAsync(client);

        var response = await client.GetAsync($"/api/orders/{orderId}?session={sessionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Read as a document: the API serializes OrderStatus as a string, and this
        // test asserts on identity/PII presence, not on the enum's wire form.
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(orderId, doc.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("guest@example.com", doc.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task GetById_WrongSession_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var (orderId, _) = await PlaceOrderAsync(client);

        var response = await client.GetAsync($"/api/orders/{orderId}?session=fake-999999");

        // NotFound, not Forbidden — a distinguishable response would confirm the
        // order id is real and turn enumeration into a working oracle.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NoSession_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var (orderId, _) = await PlaceOrderAsync(client);

        var response = await client.GetAsync($"/api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_BlankSession_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var (orderId, _) = await PlaceOrderAsync(client);

        var response = await client.GetAsync($"/api/orders/{orderId}?session=%20");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
