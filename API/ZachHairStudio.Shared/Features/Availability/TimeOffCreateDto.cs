namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// A one-off / date-range time-off block for a stylist (D-07) — vacation, sick,
/// holiday, ad-hoc. Not a recurring weekly pattern; that is StylistWorkingHours'
/// job (D-06).
/// </summary>
public class TimeOffCreateDto
{
    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public string? Reason { get; set; }
}
