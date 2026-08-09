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
}
