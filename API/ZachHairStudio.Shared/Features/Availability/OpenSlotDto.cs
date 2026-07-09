namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// A single open appointment start time returned by SlotService.GetOpenSlotsAsync.
/// StylistId/StylistName are populated only when the query is filtered to a
/// specific stylist; the "Any stylist" union view leaves them null — concrete
/// stylist assignment happens at confirm time (D-07).
/// </summary>
public class OpenSlotDto
{
    public DateTimeOffset StartsAt { get; set; }

    public int? StylistId { get; set; }

    public string? StylistName { get; set; }
}
