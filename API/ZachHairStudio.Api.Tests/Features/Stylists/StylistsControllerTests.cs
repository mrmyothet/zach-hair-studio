using System.Net;
using System.Net.Http.Json;
using ZachHairStudio.Api.Controllers;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Stylists;

namespace ZachHairStudio.Api.Tests.Features.Stylists;

public class StylistsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StylistsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetStylists_ReturnsOkWithSeededStylistsOrderedByDisplayOrder()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/stylists");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stylists = await response.Content.ReadFromJsonAsync<List<StylistResponseDto>>();
        Assert.NotNull(stylists);
        Assert.Equal(
            [
                "zin-min",
                "may-yoon",
                "thiri-cho",
                "sai-min-htet"
            ],
            stylists.Select(stylist => stylist.Slug));
    }

    [Fact]
    public void StylistsController_DoesNotDependOnBookingDbContext()
    {
        var ctorParams = typeof(StylistsController)
            .GetConstructors()
            .SelectMany(ctor => ctor.GetParameters());

        Assert.DoesNotContain(ctorParams, parameter => parameter.ParameterType == typeof(BookingDbContext));
    }
}
