using System.Globalization;
using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Tests.Features.Availability;

/// <summary>
/// Proves SalonTimeZone.ToSalonLocal resolves the correct salon-local
/// weekday/time for the salon's REAL configured zone (Asia/Yangon, fixed
/// UTC+06:30, never observes DST — Phase 2 Plan 07's SC5 descope). This is the
/// conflict scan's local-time correctness proof (RESEARCH Pitfall 2): every
/// AppointmentSlot.SlotStart comparison against a DayOfWeek/TimeOnly
/// working-hours boundary MUST go through this helper, mirroring
/// DstBoundaryTests' offset-assertion style for the (unrelated, DST-observing)
/// America/New_York fixture used there. RED until Task 2 adds
/// SalonTimeZone.ToSalonLocal — the whole test assembly will not compile until
/// then, which is the expected RED signal for this file.
/// </summary>
public class ConflictCheckLocalTimeTests
{
    private static readonly SalonTimeZone SalonTz = SalonTimeZone.FromOptions(new SalonOptions());

    [Theory]
    // UTC 02:30 + 06:30 = 09:00 same salon-local day.
    [InlineData("2026-08-05T02:30:00Z", "2026-08-05", 9, 0)]
    // UTC 17:35 + 06:30 rolls past local midnight into the next salon-local day.
    [InlineData("2026-08-05T17:35:00Z", "2026-08-06", 0, 5)]
    // UTC 23:45 + 06:30 = 06:15 the next salon-local day.
    [InlineData("2026-08-05T23:45:00Z", "2026-08-06", 6, 15)]
    public void ToSalonLocal_ConvertsUtcInstantToCorrectSalonWeekdayAndTime(
        string utcInstant, string expectedLocalDate, int expectedHour, int expectedMinute)
    {
        var instant = DateTimeOffset.Parse(
            utcInstant, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        var local = SalonTz.ToSalonLocal(instant);

        var expectedDate = DateOnly.Parse(expectedLocalDate, CultureInfo.InvariantCulture);
        Assert.Equal(expectedDate.DayOfWeek, local.DayOfWeek);
        Assert.Equal(expectedHour, local.Hour);
        Assert.Equal(expectedMinute, local.Minute);
    }

    [Fact]
    public void ToSalonLocal_RoundTripsWithToSalonInstant()
    {
        var localWallClock = new DateTime(2026, 8, 5, 14, 15, 0);
        var instant = SalonTz.ToSalonInstant(localWallClock);
        Assert.NotNull(instant);

        var roundTripped = SalonTz.ToSalonLocal(instant!.Value);

        Assert.Equal(localWallClock.DayOfWeek, roundTripped.DayOfWeek);
        Assert.Equal(localWallClock.TimeOfDay, roundTripped.TimeOfDay);
    }

    [Fact]
    public void ToSalonLocal_NeverAppliesDst_SameUtcDeltaMapsIdenticallyAcrossTheYear()
    {
        // Asia/Yangon never observes DST — the same UTC wall-clock delta must
        // map to the same salon-local time in January as in July.
        var winter = SalonTz.ToSalonLocal(new DateTimeOffset(2026, 1, 15, 3, 30, 0, TimeSpan.Zero));
        var summer = SalonTz.ToSalonLocal(new DateTimeOffset(2026, 7, 15, 3, 30, 0, TimeSpan.Zero));

        Assert.Equal(new TimeOnly(10, 0), TimeOnly.FromDateTime(winter));
        Assert.Equal(new TimeOnly(10, 0), TimeOnly.FromDateTime(summer));
    }
}
