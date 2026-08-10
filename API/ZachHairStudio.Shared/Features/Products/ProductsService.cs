using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;

namespace ZachHairStudio.Shared.Features.Products;

// This class owns ALL Product BookingDbContext access (PLAT-01).
public class ProductsService
{
    private readonly BookingDbContext _dbContext;

    public ProductsService(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ProductResponseDto>> GetProductsAsync()
        => await _dbContext.Products
            .Where(product => product.IsActive)
            .OrderBy(product => product.Name)
            .Select(product => product.ToDto())
            .ToListAsync();

    public async Task<Result<ProductResponseDto>> GetBySlugAsync(string slug)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(product => product.Slug == slug && product.IsActive);

        return product is null
            ? Result<ProductResponseDto>.NotFoundError($"Product '{slug}' not found.")
            : Result<ProductResponseDto>.Success(product.ToDto());
    }

    /// <summary>
    /// SHOP-07 / D-07 — suggest add-ons via ServiceRecommendedProduct join:
    /// find services that recommend any cart product, then return other active
    /// products linked to those services (excluding cart ids), max 4 by name.
    /// Empty cart or empty join → empty list (UI omits chips).
    /// </summary>
    public async Task<IEnumerable<ProductResponseDto>> GetRecommendedForCheckoutAsync(
        IReadOnlyCollection<int> cartProductIds)
    {
        if (cartProductIds is null || cartProductIds.Count == 0)
        {
            return [];
        }

        var cartIds = cartProductIds.Distinct().ToList();

        var serviceIds = await _dbContext.Set<ServiceRecommendedProduct>()
            .Where(link => cartIds.Contains(link.ProductId))
            .Select(link => link.ServiceId)
            .Distinct()
            .ToListAsync();

        if (serviceIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Set<ServiceRecommendedProduct>()
            .Where(link => serviceIds.Contains(link.ServiceId) && !cartIds.Contains(link.ProductId))
            .Join(
                _dbContext.Products.Where(product => product.IsActive),
                link => link.ProductId,
                product => product.Id,
                (link, product) => product)
            .Distinct()
            .OrderBy(product => product.Name)
            .Take(4)
            .Select(product => product.ToDto())
            .ToListAsync();
    }
}
