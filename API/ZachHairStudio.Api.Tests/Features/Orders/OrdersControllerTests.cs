using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using ZachHairStudio.Api.Controllers;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Orders;

namespace ZachHairStudio.Api.Tests.Features.Orders;

public class OrdersControllerTests : IClassFixture<SqliteWebApplicationFactory>
{
    private const string SessionHeaderName = "X-Cart-Session-Id";

    private readonly SqliteWebApplicationFactory _factory;

    public OrdersControllerTests(SqliteWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Checkout_AnonymousWithSessionHeader_ReturnsCheckoutUrl()
    {
        var client = _factory.CreateClient();
        var sessionKey = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add(SessionHeaderName, sessionKey);

        var response = await client.PostAsJsonAsync(
            "/api/orders/checkout",
            new CheckoutRequestDto
            {
                Email = "guest@example.com",
                Items = [new CheckoutLineItemDto { ProductId = 1, Quantity = 1 }],
            });

        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Unexpected status {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<CheckoutResponseDto>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.CheckoutUrl));
        Assert.True(body.OrderId > 0);
        Assert.Contains($"/checkout/{body.OrderId}", body.CheckoutUrl);
    }

    [Fact]
    public async Task Checkout_MissingSessionHeader_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/orders/checkout",
            new CheckoutRequestDto
            {
                Email = "guest@example.com",
                Items = [new CheckoutLineItemDto { ProductId = 1, Quantity = 1 }],
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_BodySessionKeyMismatch_ReturnsValidationProblem()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(SessionHeaderName, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync(
            "/api/orders/checkout",
            new CheckoutRequestDto
            {
                SessionKey = "different-session-key",
                Email = "guest@example.com",
                Items = [new CheckoutLineItemDto { ProductId = 1, Quantity = 1 }],
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void OrdersController_DoesNotDependOnBookingDbContext()
    {
        var ctorParams = typeof(OrdersController)
            .GetConstructors()
            .SelectMany(ctor => ctor.GetParameters());

        Assert.DoesNotContain(ctorParams, parameter => parameter.ParameterType == typeof(BookingDbContext));
    }

    [Fact]
    public void OrdersController_RequiresCartSessionHeaderConstant()
    {
        var field = typeof(OrdersController).GetField(
            "SessionHeaderName",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        Assert.NotNull(field);
        Assert.Equal(CartsController.SessionHeaderName, field.GetValue(null));
    }
}
