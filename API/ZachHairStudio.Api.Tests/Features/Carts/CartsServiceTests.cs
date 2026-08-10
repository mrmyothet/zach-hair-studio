using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Carts;
using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Api.Tests.Features.Carts;

public class CartsServiceTests
{
    [Fact]
    public async Task UpsertThenGet_EnrichesUnitPriceAndLineTotalFromCatalog()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Products.Add(CreateProduct(id: 1, slug: "leave-in", name: "Leave-In", price: 24.00m, stock: 40));
        await dbContext.SaveChangesAsync();

        var service = CreateServiceLayer(dbContext);
        const string sessionKey = "11111111-1111-1111-1111-111111111111";

        var upsert = await service.UpsertItemAsync(sessionKey, new CartItemUpsertDto
        {
            ProductId = 1,
            Quantity = 2,
        });

        Assert.True(upsert.IsSuccess);

        var get = await service.GetCartAsync(sessionKey);

        Assert.True(get.IsSuccess);
        Assert.Equal(sessionKey, get.Data.SessionKey);
        var line = Assert.Single(get.Data.Items);
        Assert.Equal(1, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(24.00m, line.UnitPrice);
        Assert.Equal(48.00m, line.LineTotal);
        Assert.Equal(48.00m, get.Data.Subtotal);
    }

    [Fact]
    public async Task GetCartAsync_UnknownSession_ReturnsEmptyItemsList()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateServiceLayer(dbContext);

        var result = await service.GetCartAsync("22222222-2222-2222-2222-222222222222");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data.Items);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0m, result.Data.Subtotal);
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesLineByProductId()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Products.Add(CreateProduct(id: 1, slug: "leave-in", name: "Leave-In", price: 24.00m, stock: 40));
        await dbContext.SaveChangesAsync();

        var service = CreateServiceLayer(dbContext);
        const string sessionKey = "33333333-3333-3333-3333-333333333333";

        await service.UpsertItemAsync(sessionKey, new CartItemUpsertDto { ProductId = 1, Quantity = 1 });
        var removed = await service.RemoveItemAsync(sessionKey, productId: 1);

        Assert.True(removed.IsSuccess);
        Assert.Empty(removed.Data.Items);

        var get = await service.GetCartAsync(sessionKey);
        Assert.Empty(get.Data.Items);
    }

    [Fact]
    public void CartItemUpsertDtoValidator_RejectsQuantityBelowOne()
    {
        var validator = new CartItemUpsertDtoValidator();
        var result = validator.TestValidate(new CartItemUpsertDto { ProductId = 1, Quantity = 0 });
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void ConflictError_MessageOnly_IsConflictWithNullConflicts()
    {
        var result = Result<string>.ConflictError("Sorry, only 2 left.");

        Assert.True(result.IsConflict());
        Assert.Equal("Sorry, only 2 left.", result.Message);
        Assert.True(result.Conflicts is null || result.Conflicts.Count == 0);
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase($"CartsServiceTests-{Guid.NewGuid()}")
            .Options;

        return new BookingDbContext(options);
    }

    private static CartsService CreateServiceLayer(BookingDbContext dbContext)
        => new CartsService(dbContext);

    private static Product CreateProduct(
        int id,
        string slug,
        string name,
        decimal price,
        int stock,
        bool isActive = true)
        => new Product
        {
            Id = id,
            Slug = slug,
            Name = name,
            ShortDescription = "A stylist-recommended hair care product.",
            LongDescription = "A stylist-recommended hair care product for testing the cart feature.",
            Category = "Hair Care",
            Price = price,
            Stock = stock,
            IsActive = isActive,
        };
}
