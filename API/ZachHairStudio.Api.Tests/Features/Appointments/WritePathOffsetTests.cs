using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZachHairStudio.Api.Tests.TestSupport;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Appointments;

/// <summary>
/// Closes the SC5/BOOK-05 behavior_unverified gap: proves the SHIPPED create path
/// (POST /api/appointments -> FluentValidation -> AppointmentsService.CreateAsync ->
/// SlotService candidate matching -> real unfiltered-unique-index insert) persists
/// Appointment.StartsAt (and its AppointmentSlot.SlotStart rows) at the salon's
/// resolved offset, on real SQL Server LocalDB. The deployed zone (Asia/Yangon) is
/// fixed UTC+06:30 and has never observed DST, so this proves offset correctness for
/// the deployed reality without a DST date or a clock seam — DstRoundTripTests covers
/// the generic-DST round-trip proof separately (descoped for this deployment; see
/// 02-VALIDATION.md).
/// </summary>
public class WritePathOffsetTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private readonly SqlServerWebApplicationFactory _factory;

    public WritePathOffsetTests(SqlServerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithNoOpEmail()
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService, NoOpEmailService>();
            });
        }).CreateClient();

    [Fact]
    public async Task Post_ValidBooking_PersistsSalonOffsetThroughTheShippedCreatePath()
    {
        var startsAt = BookingDates.NextBookableSlot(10);
        var expectedOffset = SalonTimeZone.FromOptions(new SalonOptions())
            .ToSalonInstant(startsAt.DateTime)!.Value.Offset;

        var request = new AppointmentCreateDto
        {
            ServiceId = 1, // Precision Cut, 45 min (seeded).
            StylistId = null, // "Any" — exercises the shipped assignment path too.
            StartsAt = startsAt,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            Phone = null,
        };

        var client = CreateClientWithNoOpEmail();
        var response = await client.PostAsJsonAsync("/api/appointments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AppointmentResponseDto>();
        Assert.NotNull(dto);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var reloaded = await db.Appointments
            .AsNoTracking()
            .Include(a => a.Slots)
            .SingleAsync(a => a.Id == dto!.Id);

        Assert.Equal(expectedOffset, reloaded.StartsAt.Offset);
        // datetimeoffset compares by UTC instant — the persisted instant must equal the submitted one.
        Assert.Equal(startsAt.ToUniversalTime(), reloaded.StartsAt.ToUniversalTime());

        Assert.NotEmpty(reloaded.Slots);
        Assert.Equal(expectedOffset, reloaded.Slots[0].SlotStart.Offset);
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendConfirmationAsync(Appointment appointment, ServiceResponseDto service, string stylistName)
            => Task.CompletedTask;
    }
}
