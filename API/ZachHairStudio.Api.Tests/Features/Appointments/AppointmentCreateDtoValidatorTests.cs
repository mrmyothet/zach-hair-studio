using ZachHairStudio.Shared.Features.Appointments;

namespace ZachHairStudio.Api.Tests.Features.Appointments;

/// <summary>
/// Unit tests for AppointmentCreateDtoValidator. Proves the guest-booking input
/// contract: required name/email, bounded field lengths, valid email, and — the
/// booking-specific rules — a future, on-15-minute-grid StartsAt within the
/// owner-reviewable booking horizon (BOOK-02). Pure validator unit tests, no host.
/// </summary>
public class AppointmentCreateDtoValidatorTests
{
    private readonly AppointmentCreateDtoValidator _validator = new();

    // A future, on-grid (minutes 0), within-horizon instant. Test env "today" is 2026-07-10.
    private static DateTimeOffset ValidFutureOnGrid =>
        new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(14), TimeSpan.Zero);

    private static AppointmentCreateDto ValidRequest() => new()
    {
        ServiceId = 1,
        StylistId = null,
        StartsAt = ValidFutureOnGrid,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        Phone = "+1 555 010 1234",
    };

    [Fact]
    public void Accepts_WellFormedGuestBooking()
    {
        var result = _validator.Validate(ValidRequest());
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Accepts_WhenOptionalStylistIdProvidedAndPositive()
    {
        var request = ValidRequest();
        request.StylistId = 2;
        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_NonPositiveServiceId()
    {
        var request = ValidRequest();
        request.ServiceId = 0;
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_StylistIdPresentButNonPositive()
    {
        var request = ValidRequest();
        request.StylistId = 0;
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_EmptyFirstName()
    {
        var request = ValidRequest();
        request.FirstName = "";
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_EmptyLastName()
    {
        var request = ValidRequest();
        request.LastName = "";
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_OverLengthFirstName()
    {
        var request = ValidRequest();
        request.FirstName = new string('a', 101);
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_OverLengthLastName()
    {
        var request = ValidRequest();
        request.LastName = new string('b', 101);
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_NonEmailEmail()
    {
        var request = ValidRequest();
        request.Email = "not-an-email";
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_OverLengthPhone()
    {
        var request = ValidRequest();
        request.Phone = new string('9', 31);
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_StartsAtInThePast()
    {
        var request = ValidRequest();
        request.StartsAt = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1).AddHours(14), TimeSpan.Zero);
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_StartsAtOffTheFifteenMinuteGrid()
    {
        var request = ValidRequest();
        request.StartsAt = ValidFutureOnGrid.AddMinutes(7); // 07 minutes past the hour — off grid
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_StartsAtWithNonZeroSeconds()
    {
        var request = ValidRequest();
        request.StartsAt = ValidFutureOnGrid.AddSeconds(30);
        Assert.False(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_StartsAtBeyondBookingHorizon()
    {
        var request = ValidRequest();
        // Owner-reviewable horizon default is 60 days; 90 days out must be rejected.
        request.StartsAt = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(90).AddHours(14), TimeSpan.Zero);
        Assert.False(_validator.Validate(request).IsValid);
    }
}
