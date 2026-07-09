namespace ZachHairStudio.Shared.Features.Stylists;

public class StylistResponseDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int DisplayOrder { get; set; }
}
