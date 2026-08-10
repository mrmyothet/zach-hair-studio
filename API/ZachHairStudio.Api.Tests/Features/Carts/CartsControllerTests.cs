using System.Net;
using System.Net.Http.Json;
using ZachHairStudio.Api.Controllers;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Carts;

namespace ZachHairStudio.Api.Tests.Features.Carts;

public class CartsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string SessionHeaderName = "X-Cart-Session-Id";

    private readonly CustomWebApplicationFactory _factory;

    public CartsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpsertThenGet_ReturnsServerEnrichedLineFromSeededCatalog()
    {
        var client = _factory.CreateClient();
        var sessionKey = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add(SessionHeaderName, sessionKey);

        var upsertResponse = await client.PutAsJsonAsync(
            "/api/carts/items",
            new CartItemUpsertDto { ProductId = 1, Quantity = 2 });

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/carts");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var cart = await getResponse.Content.ReadFromJsonAsync<CartResponseDto>();
        Assert.NotNull(cart);
        Assert.Equal(sessionKey, cart.SessionKey);
        var line = Assert.Single(cart.Items);
        Assert.Equal(1, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(24.00m, line.UnitPrice);
        Assert.Equal(48.00m, line.LineTotal);
        Assert.Equal(48.00m, cart.Subtotal);
    }

    [Fact]
    public async Task GetCart_UnknownSession_ReturnsEmptyItems()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(SessionHeaderName, Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/carts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = await response.Content.ReadFromJsonAsync<CartResponseDto>();
        Assert.NotNull(cart);
        Assert.NotNull(cart.Items);
        Assert.Empty(cart.Items);
    }

    [Fact]
    public async Task Upsert_QuantityBelowOne_ReturnsValidationProblem()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(SessionHeaderName, Guid.NewGuid().ToString());

        var response = await client.PutAsJsonAsync(
            "/api/carts/items",
            new CartItemUpsertDto { ProductId = 1, Quantity = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void CartsController_DoesNotDependOnBookingDbContext()
    {
        var ctorParams = typeof(CartsController)
            .GetConstructors()
            .SelectMany(ctor => ctor.GetParameters());

        Assert.DoesNotContain(ctorParams, parameter => parameter.ParameterType == typeof(BookingDbContext));
    }
}
