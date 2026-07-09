namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// Salon-wide configuration bound from the "Salon" section (appsettings.json).
/// The IANA timezone id is the single source of truth every DateTimeOffset
/// conversion in the booking domain resolves against (D-16).
/// </summary>
public class SalonOptions
{
    public string IanaTimeZoneId { get; set; } = "America/New_York";
}
