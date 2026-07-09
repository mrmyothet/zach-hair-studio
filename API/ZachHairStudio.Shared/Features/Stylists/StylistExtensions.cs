namespace ZachHairStudio.Shared.Features.Stylists;

public static class StylistExtensions
{
    public static StylistResponseDto ToDto(this Stylist stylist)
        => new StylistResponseDto
        {
            Id = stylist.Id,
            Slug = stylist.Slug,
            Name = stylist.Name,
            DisplayOrder = stylist.DisplayOrder,
        };
}
