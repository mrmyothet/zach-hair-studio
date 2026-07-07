namespace ZachHairStudio.Shared.Features.Services;

public class ServiceResponseDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string ShortDescription { get; set; } = null!;
    public string LongDescription { get; set; } = null!;
    public string Category { get; set; } = null!;
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
}
