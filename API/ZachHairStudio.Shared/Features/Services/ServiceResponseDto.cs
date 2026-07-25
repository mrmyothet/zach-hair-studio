using System.Text.Json.Serialization;

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

    // Populated only on the Owner-gated includeInactive listing path
    // (ServicesController.GetServices); omitted from the wire otherwise so the
    // default, anonymous catalog response body stays byte-identical (DD-2).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsActive { get; set; }

    public int DisplayOrder { get; set; }
}
