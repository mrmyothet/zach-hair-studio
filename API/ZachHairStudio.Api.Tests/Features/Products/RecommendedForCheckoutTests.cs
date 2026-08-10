using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Products;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Products;

public class RecommendedForCheckoutTests
{
    [Fact]
    public async Task GetRecommendedForCheckoutAsync_ExcludesInCartProducts()
    {
        await using var dbContext = CreateDbContext();
        SeedServiceAndProducts(dbContext);
        // Service 1 recommends products 1, 2, 3 — cart has product 1 → expect 2 and 3 only.
        dbContext.Set<ServiceRecommendedProduct>().AddRange(
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 1 },
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 2 },
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 3 });
        await dbContext.SaveChangesAsync();

        var service = new ProductsService(dbContext);
        var results = (await service.GetRecommendedForCheckoutAsync([1])).ToList();

        Assert.Equal([2, 3], results.Select(p => p.Id).OrderBy(id => id));
        Assert.DoesNotContain(results, p => p.Id == 1);
    }

    [Fact]
    public async Task GetRecommendedForCheckoutAsync_TakesAtMostFourOrderedByName()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Services.Add(CreateService(id: 1));
        dbContext.Products.AddRange(
            CreateProduct(id: 1, name: "Cart Anchor", slug: "cart-anchor"),
            CreateProduct(id: 2, name: "Echo Serum", slug: "echo-serum"),
            CreateProduct(id: 3, name: "Delta Oil", slug: "delta-oil"),
            CreateProduct(id: 4, name: "Charlie Mist", slug: "charlie-mist"),
            CreateProduct(id: 5, name: "Bravo Cream", slug: "bravo-cream"),
            CreateProduct(id: 6, name: "Alpha Spray", slug: "alpha-spray"));
        dbContext.Set<ServiceRecommendedProduct>().AddRange(
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 1 },
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 2 },
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 3 },
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 4 },
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 5 },
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 6 });
        await dbContext.SaveChangesAsync();

        var service = new ProductsService(dbContext);
        var results = (await service.GetRecommendedForCheckoutAsync([1])).ToList();

        Assert.Equal(4, results.Count);
        Assert.Equal(
            ["Alpha Spray", "Bravo Cream", "Charlie Mist", "Delta Oil"],
            results.Select(p => p.Name));
    }

    [Fact]
    public async Task GetRecommendedForCheckoutAsync_EmptyCart_ReturnsEmptyList()
    {
        await using var dbContext = CreateDbContext();
        SeedServiceAndProducts(dbContext);
        dbContext.Set<ServiceRecommendedProduct>().Add(
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 1 });
        await dbContext.SaveChangesAsync();

        var service = new ProductsService(dbContext);
        var results = await service.GetRecommendedForCheckoutAsync([]);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetRecommendedForCheckoutAsync_NoJoinMatches_ReturnsEmptyList()
    {
        await using var dbContext = CreateDbContext();
        SeedServiceAndProducts(dbContext);
        // Cart product 1 has no ServiceRecommendedProduct rows.
        await dbContext.SaveChangesAsync();

        var service = new ProductsService(dbContext);
        var results = await service.GetRecommendedForCheckoutAsync([1]);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetRecommendedForCheckoutAsync_ExcludesInactiveProducts()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Services.Add(CreateService(id: 1));
        dbContext.Products.AddRange(
            CreateProduct(id: 1, name: "In Cart", slug: "in-cart"),
            CreateProduct(id: 2, name: "Active Add-On", slug: "active-add-on"),
            CreateProduct(id: 3, name: "Retired Add-On", slug: "retired-add-on", isActive: false));
        dbContext.Set<ServiceRecommendedProduct>().AddRange(
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 1 },
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 2 },
            new ServiceRecommendedProduct { ServiceId = 1, ProductId = 3 });
        await dbContext.SaveChangesAsync();

        var service = new ProductsService(dbContext);
        var results = (await service.GetRecommendedForCheckoutAsync([1])).ToList();

        Assert.Equal([2], results.Select(p => p.Id));
        Assert.DoesNotContain(results, p => p.Id == 3);
    }

    private static void SeedServiceAndProducts(BookingDbContext dbContext)
    {
        dbContext.Services.Add(CreateService(id: 1));
        dbContext.Products.AddRange(
            CreateProduct(id: 1, name: "Product A", slug: "product-a"),
            CreateProduct(id: 2, name: "Product B", slug: "product-b"),
            CreateProduct(id: 3, name: "Product C", slug: "product-c"));
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase($"RecommendedForCheckoutTests-{Guid.NewGuid()}")
            .Options;

        return new BookingDbContext(options);
    }

    private static Service CreateService(int id)
        => new Service
        {
            Id = id,
            Slug = $"service-{id}",
            Name = $"Service {id}",
            ShortDescription = "A salon service for recommendation join tests.",
            LongDescription = "A salon service used to seed ServiceRecommendedProduct links in checkout recommendation tests.",
            Category = "Cuts",
            DurationMinutes = 60,
            Price = 35,
            DisplayOrder = id,
            IsActive = true,
        };

    private static Product CreateProduct(int id, string name, string slug, bool isActive = true)
        => new Product
        {
            Id = id,
            Slug = slug,
            Name = name,
            ShortDescription = "A stylist-recommended hair care product.",
            LongDescription = "A stylist-recommended hair care product for testing checkout recommendations.",
            Category = "Hair Care",
            Price = 25,
            Stock = 10,
            IsActive = isActive,
        };
}
