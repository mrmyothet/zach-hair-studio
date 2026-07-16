using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Tests.TestSupport;

/// <summary>
/// Single source of every create-path test's booking instant. Every date is computed
/// relative to <see cref="DateTime.UtcNow"/> so the suite never re-hardcodes an absolute
/// date that ages past <see cref="AppointmentCreateDtoValidator"/>'s future/horizon gate.
/// Instants are always resolved through <see cref="SalonTimeZone.FromOptions"/> — never a
/// hardcoded offset (Pitfall 5 / D-16), matching the production create path.
/// </summary>
public static class BookingDates
{
    private static readonly SalonTimeZone SalonTz = SalonTimeZone.FromOptions(new SalonOptions());

    /// <summary>
    /// Today + 7 days, advanced to the next Wednesday: always strictly future, comfortably
    /// inside the 60-day booking horizon, and a seeded working day (seed covers Tue-Sat for
    /// all four stylists, with no seeded StylistTimeOff to collide with).
    /// </summary>
    public static DateOnly NextBookableDate()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        while (date.DayOfWeek != DayOfWeek.Wednesday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    /// <summary>Companion for tests needing more than one working day (e.g. week-range queries).
    /// Stays within the seeded Tue-Sat window for small offsets from <see cref="NextBookableDate"/>.</summary>
    public static DateOnly NextBookableDate(int dayOffset) => NextBookableDate().AddDays(dayOffset);

    public static DateTimeOffset NextBookableSlot(int hour, int minute = 0)
        => SlotOn(NextBookableDate(), hour, minute);

    public static DateTimeOffset SlotOn(DateOnly date, int hour, int minute = 0)
        => SalonTz.ToSalonInstant(date.ToDateTime(new TimeOnly(hour, minute)))!.Value;
}
