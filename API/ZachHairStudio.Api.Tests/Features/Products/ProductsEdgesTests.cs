using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Api.Tests.Features.Products;

// Uncovered-behavior edges from 05-PLAN.md 05-01 must_haves — no duplicate coverage.
// Each test targets a requirement the dedicated ProductsServiceTests / ProductsControllerTests
// / ServicesServiceTests files do not assert.
public class ProductsEdgesTests
{
    [Fact]
    public async Task GetProductsAsync_NameOrderingIsStableForIdenticalNames()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Products.AddRange(
            CreateProduct(id: 1, slug: "duplicate-a", name: "Identical Name"),
            CreateProduct(id: 2, slug: "duplicate-b", name: "Identical Name"));
        await dbContext.SaveChangesAsync();
        var service = CreateServiceLayer(dbContext);

        var first = (await service.GetProductsAsync()).Select(product => product.Slug).ToList();
        var second = (await service.GetProductsAsync()).Select(product => product.Slug).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetProducts_ListResponseOmitsRecommendedProductsField()
    {
        var factory = new CustomWebApplicationFactory();
        await using var host = factory;
        var client = host.CreateClient();

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var element in body.RootElement.EnumerateArray())
        {
            Assert.False(
                element.TryGetProperty("recommendedProducts", out _),
                "GET /api/products items must not carry a recommendedProducts key.");
        }
    }

    [Fact]
    public async Task GetService_WithLinkedService_IncludesRecommendedProductsInBody()
    {
        var factory = new CustomWebApplicationFactory();
        await using var host = factory;
        var client = host.CreateClient();

        var response = await client.GetAsync("/api/services/blowout-and-styling");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("recommendedProducts", out var recommended));
        Assert.Equal(JsonValueKind.Array, recommended.ValueKind);
        Assert.Equal(2, recommended.GetArrayLength());
    }

    [Fact]
    public async Task GetService_WithUnlinkedService_RendersNothingWithoutRecommendedProducts()
    {
        var factory = new CustomWebApplicationFactory();
        await using var host = factory;
        var client = host.CreateClient();

        var response = await client.GetAsync("/api/services/precision-cut");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The wire emits an empty recommendedProducts array (unit test
        // GetBySlugAsync_RecommendedProducts_ReturnsEmptyListWhenUnlinked asserts the same
        // non-null empty-list contract). The user-visible PROD-03/D-14 rule — a service with
        // no recommendations renders NO Recommended Products section — holds because the
        // frontend guard `service.recommendedProducts && service.recommendedProducts.length > 0`
        // renders nothing for an empty array (verified in landing-page/app/services/[slug]/page.tsx).
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("recommendedProducts", out var recommended));
        Assert.Equal(JsonValueKind.Array, recommended.ValueKind);
        Assert.Equal(0, recommended.GetArrayLength());
    }

    [Fact]
    public async Task ProductCreateDto_DoesNotExposeClientSettableIsActiveOrId()
    {
        var createProperties = typeof(ProductCreateDto).GetProperties();
        Assert.DoesNotContain(createProperties, property => property.Name == "IsActive");
        Assert.DoesNotContain(createProperties, property => property.Name == "Id");
    }

    [Fact]
    public void ProductToEntity_DefaultsIsActiveToTrue()
    {
        var entity = new ProductCreateDto
        {
            Slug = "leave-in-repair-serum",
            Name = "Leave-In Repair Serum",
            ShortDescription = "A lightweight leave-in serum.",
            LongDescription = "A lightweight leave-in serum formulated to extend smoothing between salon visits.",
            Category = "Hair Care",
            Price = 24m,
            Stock = 40,
        }.ToEntity();

        Assert.True(entity.IsActive);
    }

    [Fact]
    public async Task ProductToDto_DoesNotExposeIsActiveToClients()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Products.Add(CreateProduct(id: 1, slug: "leave-in-serum", name: "Leave-In Serum"));
        await dbContext.SaveChangesAsync();
        var service = CreateServiceLayer(dbContext);

        var result = await service.GetBySlugAsync("leave-in-serum");

        var responseProperties = result.Data.GetType().GetProperties();
        Assert.DoesNotContain(responseProperties, property => property.Name == "IsActive");
    }

    [Theory]
    [InlineData(75)] // 75 astral emoji = 150 UTF-16 units — exactly at the MaximumLength(150) limit
    public void Validate_MultiByteNameExactlyAtUnitLimit_HasNoValidationError(int count)
    {
        var validator = new ProductCreateDtoValidator();
        var dto = CreateValidDto();
        dto.Name = string.Concat(Enumerable.Repeat("😀", count)); // 😀 = U+1F600, one surrogate pair

        var result = validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(76)] // 76 astral emoji = 152 UTF-16 units — one past the 150 limit despite only 76 chars
    [InlineData(151)] // 151 ASCII chars = 151 UTF-16 units — one past the limit
    public void Validate_NameOverUnitLimit_HasValidationError(int count)
    {
        var validator = new ProductCreateDtoValidator();
        var dto = CreateValidDto();
        dto.Name = string.Concat(Enumerable.Repeat("😀", count));

        var result = validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase($"ProductsEdgesTests-{Guid.NewGuid()}")
            .Options;

        return new BookingDbContext(options);
    }

    private static ProductsService CreateServiceLayer(BookingDbContext dbContext)
        => new ProductsService(dbContext);

    private static Product CreateProduct(
        int id,
        string slug,
        string name,
        bool isActive = true)
        => new Product
        {
            Id = id,
            Slug = slug,
            Name = name,
            ShortDescription = "A stylist-recommended hair care product.",
            LongDescription = "A stylist-recommended hair care product for testing the product catalog feature.",
            Category = "Hair Care",
            Price = 25,
            Stock = 10,
            IsActive = isActive,
        };

    private static ProductCreateDto CreateValidDto()
        => new()
        {
            Slug = "leave-in-repair-serum",
            Name = "Leave-In Repair Serum",
            ShortDescription = "A lightweight leave-in serum that locks in smoothness.",
            LongDescription = "A lightweight leave-in serum formulated to extend the smoothing effects of a keratin treatment between salon visits.",
            Category = "Hair Care",
            Price = 24.00m,
            Stock = 40,
            ImageUrl = "/images/products/leave-in-repair-serum.jpg",
        };
}
