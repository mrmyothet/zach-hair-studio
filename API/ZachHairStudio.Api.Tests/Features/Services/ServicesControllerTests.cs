using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ZachHairStudio.Api.Controllers;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Services;

public class ServicesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ServicesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetServices_ReturnsOkWithJsonArray()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/services");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetService_WithUnknownSlug_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/services/unknown-service");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateService_WithEmptyName_ReturnsBadRequestWithErrorsBody()
    {
        var client = _factory.CreateClient();
        var request = CreateDto(name: string.Empty);

        var response = await client.PostAsJsonAsync("/api/services", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("errors", out var errors));
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
    }

    [Fact]
    public void ServicesController_DoesNotDependOnBookingDbContext()
    {
        var ctorParams = typeof(ServicesController)
            .GetConstructors()
            .SelectMany(ctor => ctor.GetParameters());

        Assert.DoesNotContain(ctorParams, parameter => parameter.ParameterType == typeof(BookingDbContext));
    }

    private static ServiceCreateDto CreateDto(string name = "Precision Cut")
        => new ServiceCreateDto
        {
            Slug = "precision-cut",
            Name = name,
            ShortDescription = "A tailored cut.",
            LongDescription = "A tailored cut designed around your style and routine.",
            Category = "Cuts",
            DurationMinutes = 45,
            Price = 35,
            DisplayOrder = 1,
        };
}
