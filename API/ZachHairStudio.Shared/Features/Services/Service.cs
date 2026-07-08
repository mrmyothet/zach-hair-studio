using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Services;

public class Service
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Slug { get; set; } = null!;

    [Required, StringLength(150)]
    public string Name { get; set; } = null!;

    [Required, StringLength(200)]
    public string ShortDescription { get; set; } = null!;

    [Required, StringLength(2000)]
    public string LongDescription { get; set; } = null!;

    [Required, StringLength(50)]
    public string Category { get; set; } = null!;

    public int DurationMinutes { get; set; }

    public decimal Price { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}
