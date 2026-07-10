using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Availability;

public class StylistTimeOff
{
    public int Id { get; set; }

    public int StylistId { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    [StringLength(200)]
    public string? Reason { get; set; }
}
