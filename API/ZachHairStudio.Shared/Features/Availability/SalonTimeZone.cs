namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// The single wall-clock -> DateTimeOffset conversion for the salon's configured
/// IANA timezone. Every appointment/availability instant in the booking domain is
/// constructed through this helper — never re-derived independently (Pitfall 5).
/// Never hardcode an offset; the offset is always resolved per-instant via
/// <see cref="TimeZoneInfo"/>.
/// </summary>
public class SalonTimeZone
{
    private readonly TimeZoneInfo _timeZoneInfo;

    public SalonTimeZone(string ianaTimeZoneId)
    {
        _timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
    }

    public static SalonTimeZone FromOptions(SalonOptions options) => new(options.IanaTimeZoneId);

    /// <summary>
    /// Resolves a salon-local wall-clock time to its DateTimeOffset instant.
    /// Returns null when <paramref name="localWallClock"/> falls inside a
    /// spring-forward gap (a local time that does not exist). An ambiguous
    /// fall-back local time (one that occurs twice) deterministically resolves
    /// to the standard-time offset — the numerically smaller of the two.
    /// </summary>
    public DateTimeOffset? ToSalonInstant(DateTime localWallClock)
    {
        var unspecified = DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified);

        if (_timeZoneInfo.IsInvalidTime(unspecified))
        {
            return null;
        }

        if (_timeZoneInfo.IsAmbiguousTime(unspecified))
        {
            var standardOffset = _timeZoneInfo.GetAmbiguousTimeOffsets(unspecified).Min();
            return new DateTimeOffset(unspecified, standardOffset);
        }

        var offset = _timeZoneInfo.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset);
    }

    /// <summary>
    /// Resolves a UTC/offset instant to its salon-local wall-clock time — the
    /// inverse of <see cref="ToSalonInstant"/>. Every AppointmentSlot.SlotStart
    /// comparison against a DayOfWeek/TimeOnly working-hours or time-off
    /// boundary MUST go through this helper — never a raw .DayOfWeek/.TimeOfDay
    /// on the DateTimeOffset itself, which reflects the offset baked into the
    /// instant, not the salon's configured zone (Pitfall 2).
    /// </summary>
    public DateTime ToSalonLocal(DateTimeOffset instant)
    {
        var converted = TimeZoneInfo.ConvertTime(instant, _timeZoneInfo);
        return DateTime.SpecifyKind(converted.DateTime, DateTimeKind.Unspecified);
    }
}
