namespace ZachHairStudio.Shared.Features.Products;

public static class ProductExtensions
{
    public static ProductResponseDto ToDto(this Product product)
        => new ProductResponseDto
        {
            Id = product.Id,
            Slug = product.Slug,
            Name = product.Name,
            ShortDescription = product.ShortDescription,
            LongDescription = product.LongDescription,
            Category = product.Category,
            Price = product.Price,
            Stock = product.Stock,
            ImageUrl = product.ImageUrl,
        };

    public static Product ToEntity(this ProductCreateDto createDto)
        => new Product
        {
            Slug = createDto.Slug,
            Name = createDto.Name,
            ShortDescription = createDto.ShortDescription,
            LongDescription = createDto.LongDescription,
            Category = createDto.Category,
            Price = createDto.Price,
            Stock = createDto.Stock,
            ImageUrl = createDto.ImageUrl,
            IsActive = true,
        };
}
