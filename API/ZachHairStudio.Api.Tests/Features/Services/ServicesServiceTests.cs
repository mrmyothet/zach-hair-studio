using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Services;

public class ServicesServiceTests
{
    [Fact]
    public async Task GetServicesAsync_ReturnsOnlyActiveServicesOrderedByDisplayOrder()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Services.AddRange(
            CreateService(id: 1, slug: "third-active", displayOrder: 3),
            CreateService(id: 2, slug: "first-active", displayOrder: 1),
            CreateService(id: 3, slug: "inactive", displayOrder: 2, isActive: false));
        await dbContext.SaveChangesAsync();

        var service = CreateServiceLayer(dbContext);

        var results = (await service.GetServicesAsync()).ToList();

        Assert.Equal(["first-active", "third-active"], results.Select(result => result.Slug));
    }

    [Fact]
    public async Task GetServicesAsync_WithIncludeInactive_ReturnsActiveAndInactiveOrderedByDisplayOrder()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Services.AddRange(
            CreateService(id: 1, slug: "third-active", displayOrder: 3),
            CreateService(id: 2, slug: "second-inactive", displayOrder: 2, isActive: false),
            CreateService(id: 3, slug: "first-active", displayOrder: 1));
        await dbContext.SaveChangesAsync();

        var service = CreateServiceLayer(dbContext);

        var results = (await service.GetServicesAsync(includeInactive: true)).ToList();

        Assert.Equal(
            ["first-active", "second-inactive", "third-active"],
            results.Select(result => result.Slug));

        var inactiveResult = results.Single(result => result.Slug == "second-inactive");
        var activeResult = results.Single(result => result.Slug == "first-active");
        Assert.False(inactiveResult.IsActive);
        Assert.True(activeResult.IsActive);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNotFoundForUnknownSlug()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateServiceLayer(dbContext);

        var result = await service.GetBySlugAsync("missing-service");

        Assert.True(result.IsNotFound());
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsSuccessForActiveSlug()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Services.Add(CreateService(id: 1, slug: "precision-cut", displayOrder: 1));
        await dbContext.SaveChangesAsync();
        var service = CreateServiceLayer(dbContext);

        var result = await service.GetBySlugAsync("precision-cut");

        Assert.True(result.IsSuccess);
        Assert.Equal("precision-cut", result.Data.Slug);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNotFoundForInactiveSlug()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Services.Add(CreateService(id: 1, slug: "inactive-service", displayOrder: 1, isActive: false));
        await dbContext.SaveChangesAsync();
        var service = CreateServiceLayer(dbContext);

        var result = await service.GetBySlugAsync("inactive-service");

        Assert.True(result.IsNotFound());
    }

    [Fact]
    public async Task CreateAsync_WithInvalidDto_ReturnsValidationErrorAndDoesNotWriteRow()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateServiceLayer(dbContext);
        var request = CreateDto(name: string.Empty, price: -1);

        var result = await service.CreateAsync(request);

        Assert.True(result.IsValidationError());
        Assert.Empty(dbContext.Services);
    }

    [Fact]
    public async Task CreateAsync_WithValidDto_ReturnsSuccessAndPersistsRow()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateServiceLayer(dbContext);
        var request = CreateDto(slug: "signature-blowout", name: "Signature Blowout");

        var result = await service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("signature-blowout", result.Data.Slug);
        Assert.Single(dbContext.Services);
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase($"ServicesServiceTests-{Guid.NewGuid()}")
            .Options;

        return new BookingDbContext(options);
    }

    private static ServicesService CreateServiceLayer(BookingDbContext dbContext)
        => new ServicesService(
            dbContext,
            new ServiceCreateDtoValidator(),
            new ServiceUpdateDtoValidator());

    private static Service CreateService(
        int id,
        string slug,
        int displayOrder,
        bool isActive = true)
        => new Service
        {
            Id = id,
            Slug = slug,
            Name = $"Service {id}",
            ShortDescription = "A polished service for testing.",
            LongDescription = "A polished service for testing the service catalog feature.",
            Category = "Cuts",
            DurationMinutes = 45,
            Price = 35,
            IsActive = isActive,
            DisplayOrder = displayOrder,
        };

    private static ServiceCreateDto CreateDto(
        string slug = "precision-cut",
        string name = "Precision Cut",
        decimal price = 35)
        => new ServiceCreateDto
        {
            Slug = slug,
            Name = name,
            ShortDescription = "A tailored cut.",
            LongDescription = "A tailored cut designed around your style and routine.",
            Category = "Cuts",
            DurationMinutes = 45,
            Price = price,
            DisplayOrder = 1,
        };
}
