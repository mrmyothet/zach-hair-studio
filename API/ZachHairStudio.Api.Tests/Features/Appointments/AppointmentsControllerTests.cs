using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Appointments;

/// <summary>
/// Proves POST /api/appointments end-to-end over the InMemory host (PATT-01 — no
/// unique-constraint semantics exercised here; SC4 lives in ConcurrencyTests on real
/// SQL). A fake IEmailService is registered ONLY to assert send call behavior (attempt
/// after commit, and that a throwing send does not fail the 201) — this is not the
/// D-12 real-send path. Recipients are RFC 2606 @example.com addresses.
/// </summary>
public class AppointmentsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    // 2026-07-15 is a Wednesday covered by the seeded Tue-Sat working hours and within
    // the 60-day horizon of the test env's "today" (2026-07-10).
    private const int ServiceId = 1; // Precision Cut, 45 min (seeded).

    public AppointmentsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static AppointmentCreateDto ValidRequest(DateTimeOffset startsAt) => new()
    {
        ServiceId = ServiceId,
        StylistId = null,
        StartsAt = startsAt,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        Phone = null,
    };

    private static readonly SalonTimeZone SalonTz = SalonTimeZone.FromOptions(new SalonOptions());

    // Resolved through the configured salon zone rather than a hardcoded offset, so these
    // slots follow Salon:IanaTimeZoneId instead of drifting off-grid when it changes.
    private static DateTimeOffset Slot(int hour, int minute = 0)
        => SalonTz.ToSalonInstant(new DateTime(2026, 7, 15, hour, minute, 0))!.Value;

    [Fact]
    public async Task Post_ValidBooking_Returns201WithFullDetails()
    {
        var email = new RecordingEmailService();
        var client = CreateClientWithEmail(email);

        var response = await client.PostAsJsonAsync("/api/appointments", ValidRequest(Slot(10)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AppointmentResponseDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.Id > 0);
        Assert.Equal(ServiceId, dto.ServiceId);
        Assert.False(string.IsNullOrWhiteSpace(dto.ServiceName));
        Assert.True(dto.StylistId > 0);
        Assert.False(string.IsNullOrWhiteSpace(dto.StylistName));
        Assert.NotEqual("Any", dto.StylistName);
        Assert.Equal(45, dto.DurationMinutes);
        Assert.Equal("Confirmed", dto.Status);
        Assert.Equal("jane.doe@example.com", dto.Email);
    }

    [Fact]
    public async Task Post_OffGridStartsAt_Returns400()
    {
        var client = CreateClientWithEmail(new RecordingEmailService());

        var response = await client.PostAsJsonAsync("/api/appointments", ValidRequest(Slot(13, 7)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_PastStartsAt_Returns400()
    {
        var client = CreateClientWithEmail(new RecordingEmailService());

        var past = SalonTz.ToSalonInstant(new DateTime(2020, 1, 7, 10, 0, 0))!.Value;
        var response = await client.PostAsJsonAsync("/api/appointments", ValidRequest(past));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidBooking_AttemptsConfirmationEmailAfterCommit()
    {
        var email = new RecordingEmailService();
        var client = CreateClientWithEmail(email);

        var response = await client.PostAsJsonAsync("/api/appointments", ValidRequest(Slot(11)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, email.CallCount);
    }

    [Fact]
    public async Task Post_ValidBooking_EmailThrows_StillReturns201()
    {
        var client = CreateClientWithEmail(new ThrowingEmailService());

        var response = await client.PostAsJsonAsync("/api/appointments", ValidRequest(Slot(12)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private HttpClient CreateClientWithEmail(IEmailService email)
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton(email);
            });
        }).CreateClient();

    private sealed class RecordingEmailService : IEmailService
    {
        public int CallCount { get; private set; }

        public Task SendConfirmationAsync(Appointment appointment, ServiceResponseDto service, string stylistName)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingEmailService : IEmailService
    {
        public Task SendConfirmationAsync(Appointment appointment, ServiceResponseDto service, string stylistName)
            => throw new InvalidOperationException("Simulated Resend outage.");
    }
}
