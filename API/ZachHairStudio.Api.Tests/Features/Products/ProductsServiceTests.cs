using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Api.Tests.Features.Products;

public class ProductsServiceTests
{
    [Fact]
    public async Task GetProductsAsync_ReturnsOnlyActiveProductsOrderedByName()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Products.AddRange(
            CreateProduct(id: 1, slug: "z-product", name: "Z Product"),
            CreateProduct(id: 2, slug: "a-product", name: "A Product"),
            CreateProduct(id: 3, slug: "inactive-product", name: "M Product", isActive: false));
        await dbContext.SaveChangesAsync();

        var service = CreateServiceLayer(dbContext);

        var results = (await service.GetProductsAsync()).ToList();

        Assert.Equal(["a-product", "z-product"], results.Select(result => result.Slug));
    }

    [Fact]
    public async Task GetProductsAsync_ReturnsEmptyArrayWhenNoActiveProducts()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Products.Add(CreateProduct(id: 1, slug: "inactive-product", name: "Inactive", isActive: false));
        await dbContext.SaveChangesAsync();

        var service = CreateServiceLayer(dbContext);

        var results = await service.GetProductsAsync();

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNotFoundForUnknownSlug()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateServiceLayer(dbContext);

        var result = await service.GetBySlugAsync("missing-product");

        Assert.True(result.IsNotFound());
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsSuccessForActiveSlug()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Products.Add(CreateProduct(id: 1, slug: "leave-in-treatment", name: "Leave-In Treatment"));
        await dbContext.SaveChangesAsync();
        var service = CreateServiceLayer(dbContext);

        var result = await service.GetBySlugAsync("leave-in-treatment");

        Assert.True(result.IsSuccess);
        Assert.Equal("leave-in-treatment", result.Data.Slug);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNotFoundForInactiveSlug()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Products.Add(CreateProduct(id: 1, slug: "discontinued-wax", name: "Discontinued Wax", isActive: false));
        await dbContext.SaveChangesAsync();
        var service = CreateServiceLayer(dbContext);

        var result = await service.GetBySlugAsync("discontinued-wax");

        Assert.True(result.IsNotFound());
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase($"ProductsServiceTests-{Guid.NewGuid()}")
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
}
