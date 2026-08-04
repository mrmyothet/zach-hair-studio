namespace ZachHairStudio.Shared.Features.Services;

public static class ServiceExtensions
{
    public static ServiceResponseDto ToDto(this Service service, bool includeStatus = false)
        => new ServiceResponseDto
        {
            Id = service.Id,
            Slug = service.Slug,
            Name = service.Name,
            ShortDescription = service.ShortDescription,
            LongDescription = service.LongDescription,
            Category = service.Category,
            DurationMinutes = service.DurationMinutes,
            Price = service.Price,
            ImageUrl = service.ImageUrl,
            IsActive = includeStatus ? service.IsActive : null,
            DisplayOrder = service.DisplayOrder,
        };

    public static Service ToEntity(this ServiceCreateDto createDto)
        => new Service
        {
            Slug = createDto.Slug,
            Name = createDto.Name,
            ShortDescription = createDto.ShortDescription,
            LongDescription = createDto.LongDescription,
            Category = createDto.Category,
            DurationMinutes = createDto.DurationMinutes,
            Price = createDto.Price,
            ImageUrl = createDto.ImageUrl,
            IsActive = true,
            DisplayOrder = createDto.DisplayOrder,
        };

    public static void ApplyTo(this ServiceUpdateDto updateDto, Service service)
    {
        service.Slug = updateDto.Slug;
        service.Name = updateDto.Name;
        service.ShortDescription = updateDto.ShortDescription;
        service.LongDescription = updateDto.LongDescription;
        service.Category = updateDto.Category;
        service.DurationMinutes = updateDto.DurationMinutes;
        service.Price = updateDto.Price;
        service.ImageUrl = updateDto.ImageUrl;
        service.IsActive = updateDto.IsActive;
        service.DisplayOrder = updateDto.DisplayOrder;
    }
}
