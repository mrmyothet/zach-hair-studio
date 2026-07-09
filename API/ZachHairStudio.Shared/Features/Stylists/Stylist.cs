using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Stylists;

public class Stylist
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Slug { get; set; } = null!;

    [Required, StringLength(150)]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}
