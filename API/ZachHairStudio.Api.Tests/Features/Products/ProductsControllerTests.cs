using System.Net;
using System.Net.Http.Json;
using ZachHairStudio.Api.Controllers;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Api.Tests.Features.Products;

public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProducts_ReturnsOkWithSeededActiveProductsOrderedByName()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<ProductResponseDto>>();
        Assert.NotNull(products);
        Assert.DoesNotContain(products, product => product.Slug == "discontinued-styling-wax");
        Assert.Equal(products.OrderBy(product => product.Name).Select(product => product.Slug), products.Select(product => product.Slug));
    }

    [Fact]
    public async Task GetProduct_WithUnknownSlug_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products/unknown-product");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProduct_WithInactiveSlug_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products/discontinued-styling-wax");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void ProductsController_DoesNotDependOnBookingDbContext()
    {
        var ctorParams = typeof(ProductsController)
            .GetConstructors()
            .SelectMany(ctor => ctor.GetParameters());

        Assert.DoesNotContain(ctorParams, parameter => parameter.ParameterType == typeof(BookingDbContext));
    }
}
