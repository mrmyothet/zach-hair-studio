using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;

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

        return service is null
            ? Result<ServiceResponseDto>.NotFoundError($"Service '{slug}' not found.")
            : Result<ServiceResponseDto>.Success(service.ToDto());
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
