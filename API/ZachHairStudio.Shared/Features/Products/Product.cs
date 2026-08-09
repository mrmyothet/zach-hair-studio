using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Products;

public class Product
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

    public decimal Price { get; set; }

    public int Stock { get; set; } // D-06 — display-only this phase, no reservation logic

    [StringLength(500)]
    public string? ImageUrl { get; set; } // D-07

    public bool IsActive { get; set; } = true; // D-08
}
