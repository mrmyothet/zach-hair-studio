using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Shared.Features.Services;

public class ServicesService
{
    private readonly BookingDbContext _dbContext;
    private readonly IValidator<ServiceCreateDto> _createValidator;
    private readonly IValidator<ServiceUpdateDto> _updateValidator;

    public ServicesService(
        BookingDbContext dbContext,
        IValidator<ServiceCreateDto> createValidator,
        IValidator<ServiceUpdateDto> updateValidator)
    {
        _dbContext = dbContext;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IEnumerable<ServiceResponseDto>> GetServicesAsync(bool includeInactive = false)
    {
        IQueryable<Service> query = _dbContext.Services;
        if (!includeInactive)
        {
            query = query.Where(service => service.IsActive);
        }

        return await query
            .OrderBy(service => service.DisplayOrder)
            .Select(service => service.ToDto(includeInactive))
            .ToListAsync();
    }

    public async Task<Result<ServiceResponseDto>> GetBySlugAsync(string slug)
    {
        var service = await _dbContext.Services
            .FirstOrDefaultAsync(service => service.Slug == slug && service.IsActive);

        if (service is null)
        {
            return Result<ServiceResponseDto>.NotFoundError($"Service '{slug}' not found.");
        }

        // Only active linked products are surfaced (RESEARCH Pitfall 3, T-05-02).
        var recommendedProducts = await _dbContext.Set<ServiceRecommendedProduct>()
            .Where(link => link.ServiceId == service.Id)
            .Join(
                _dbContext.Products.Where(product => product.IsActive),
                link => link.ProductId,
                product => product.Id,
                (link, product) => product)
            .Select(product => product.ToDto())
            .ToListAsync();

        var dto = service.ToDto();
        dto.RecommendedProducts = recommendedProducts;
        return Result<ServiceResponseDto>.Success(dto);
    }

    public async Task<Result<ServiceResponseDto>> CreateAsync(ServiceCreateDto request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return Result<ServiceResponseDto>.ValidationError(
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        var service = request.ToEntity();
        _dbContext.Services.Add(service);
        await _dbContext.SaveChangesAsync();

        return Result<ServiceResponseDto>.Success(service.ToDto());
    }

    public async Task<Result<ServiceResponseDto>> UpdateAsync(int id, ServiceUpdateDto request)
    {
        var service = await _dbContext.Services.FindAsync(id);
        if (service is null)
        {
            return Result<ServiceResponseDto>.NotFoundError($"Service '{id}' not found.");
        }

        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return Result<ServiceResponseDto>.ValidationError(
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        request.ApplyTo(service);
        await _dbContext.SaveChangesAsync();

        return Result<ServiceResponseDto>.Success(service.ToDto());
    }

    /// <summary>
    /// The service's currently-persisted ImageUrl (or null if the service doesn't
    /// exist / has none) — read before an image upload overwrites it, so the
    /// caller can best-effort delete the now-orphaned physical file (WR-03).
    /// </summary>
    public async Task<string?> GetImageUrlAsync(int id)
    {
        var service = await _dbContext.Services.FindAsync(id);
        return service?.ImageUrl;
    }

    public async Task<Result<ServiceResponseDto>> SetImageAsync(int id, string imageUrl)
    {
        var service = await _dbContext.Services.FindAsync(id);
        if (service is null)
        {
            return Result<ServiceResponseDto>.NotFoundError($"Service '{id}' not found.");
        }

        service.ImageUrl = imageUrl;
        await _dbContext.SaveChangesAsync();

        return Result<ServiceResponseDto>.Success(service.ToDto());
    }
}
