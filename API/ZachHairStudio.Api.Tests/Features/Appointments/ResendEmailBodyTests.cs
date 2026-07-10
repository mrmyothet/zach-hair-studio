using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Appointments;

/// <summary>
/// Pins the confirmation email body to the five fields 02-VALIDATION.md requires for
/// BOOK-03: service, stylist, salon-local time WITH an explicit zone label, duration,
/// and price. These regressed silently once because nothing asserted the HTML.
///
/// No network: a stub handler captures the outgoing Resend payload. This is not the
/// D-12 real-send path.
/// </summary>
public class ResendEmailBodyTests
{
    private static readonly SalonTimeZone SalonTz = SalonTimeZone.FromOptions(new SalonOptions());

    private static Appointment BuildAppointment() => new()
    {
        Id = 1,
        ServiceId = 1,
        StylistId = 1,
        // 10:00 salon-local, resolved through the configured zone (never a hardcoded offset).
        StartsAt = SalonTz.ToSalonInstant(new DateTime(2026, 7, 15, 10, 0, 0))!.Value,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
    };

    private static ServiceResponseDto BuildService() => new()
    {
        Id = 1,
        Slug = "precision-cut",
        Name = "Precision Cut",
        ShortDescription = "s",
        LongDescription = "l",
        Category = "Cuts",
        DurationMinutes = 45,
        Price = 85m,
    };

    private static async Task<string> CaptureHtmlAsync()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var service = new ResendEmailService(
            httpClient,
            new ResendOptions(),
            NullLogger<ResendEmailService>.Instance);

        await service.SendConfirmationAsync(BuildAppointment(), BuildService(), "Mr. Zachary");

        Assert.NotNull(handler.Payload);
        using var doc = JsonDocument.Parse(handler.Payload!);
        return doc.RootElement.GetProperty("html").GetString()!;
    }

    [Fact]
    public async Task ConfirmationEmail_CarriesServiceStylistDurationAndPrice()
    {
        var html = await CaptureHtmlAsync();

        Assert.Contains("Precision Cut", html);
        Assert.Contains("Mr. Zachary", html);
        Assert.Contains("45 min", html);
        Assert.Contains("$85", html);
    }

    [Fact]
    public async Task ConfirmationEmail_LabelsTheSalonZoneExplicitly()
    {
        var html = await CaptureHtmlAsync();

        // The stored offset is the salon offset; the email must name it so the time is
        // unambiguous for a client reading it in a different zone (D-16).
        var expectedOffset = BuildAppointment().StartsAt.Offset;
        var sign = expectedOffset < TimeSpan.Zero ? "-" : "+";
        var expectedLabel = $"GMT{sign}{expectedOffset.Duration():hh\\:mm}";

        Assert.Contains(expectedLabel, html);
        Assert.Contains("salon local time", html);
    }

    [Fact]
    public async Task ConfirmationEmail_RendersSalonWallClockNotUtc()
    {
        var html = await CaptureHtmlAsync();

        // 10:00 salon-local must read as 10:00, never converted to UTC.
        Assert.Contains("10:00 AM", html);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Payload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Payload = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"stub\"}"),
            };
        }
    }
}
