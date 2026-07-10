using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Tests.Features.Availability;

/// <summary>
/// Proves SalonTimeZone.ToSalonInstant resolves DST-correct offsets across the
/// salon's configured IANA zone (America/New_York) for the real 2026 US DST
/// transition dates (BOOK-05, D-16, Pitfall 5). Spring forward: Sun Mar 8 2026,
/// 2:00 AM -> 3:00 AM. Fall back: Sun Nov 1 2026, 2:00 AM -> 1:00 AM.
/// </summary>
public class DstBoundaryTests
{
    private static readonly SalonTimeZone SalonTz = new("America/New_York");

    [Theory]
    [InlineData("2026-03-07", "-05:00")] // day before spring-forward: still EST
    [InlineData("2026-03-08", "-04:00")] // spring-forward day itself: business hours are already EDT
    [InlineData("2026-10-31", "-04:00")] // day before fall-back: still EDT
    [InlineData("2026-11-01", "-05:00")] // fall-back day itself: business hours are already EST
    public void ToSalonInstant_ResolvesCorrectOffsetAcrossDstBoundary(string dateStr, string expectedOffset)
    {
        var localWallClock = DateOnly.Parse(dateStr).ToDateTime(new TimeOnly(10, 0));

        var instant = SalonTz.ToSalonInstant(localWallClock);

        Assert.NotNull(instant);
        Assert.Equal(TimeSpan.Parse(expectedOffset), instant!.Value.Offset);
    }

    [Fact]
    public void ToSalonInstant_SpringForwardGap_ReturnsNull()
    {
        // 2026-03-08 02:00-02:59 America/New_York does not exist (clocks jump 2:00 -> 3:00).
        var localWallClock = new DateTime(2026, 3, 8, 2, 30, 0);

        var instant = SalonTz.ToSalonInstant(localWallClock);

        Assert.Null(instant);
    }

    [Fact]
    public void ToSalonInstant_FallBackAmbiguousTime_ResolvesToStandardOffset()
    {
        // 2026-11-01 01:00-01:59 America/New_York occurs twice; the documented
        // deterministic policy resolves to the standard-time (EST, -05:00) offset.
        var localWallClock = new DateTime(2026, 11, 1, 1, 30, 0);

        var instant = SalonTz.ToSalonInstant(localWallClock);

        Assert.NotNull(instant);
        Assert.Equal(TimeSpan.FromHours(-5), instant!.Value.Offset);
    }
}
