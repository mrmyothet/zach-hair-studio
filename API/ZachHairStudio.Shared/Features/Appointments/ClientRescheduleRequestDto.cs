namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>
/// Client self-service reschedule body (ACCT-04 / D-10). Contact fields and
/// ServiceId come from the owned appointment — never from the request.
/// </summary>
public class ClientRescheduleRequestDto
{
    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Optional; null keeps "any free stylist" assignment like guest create.</summary>
    public int? StylistId { get; set; }
}
