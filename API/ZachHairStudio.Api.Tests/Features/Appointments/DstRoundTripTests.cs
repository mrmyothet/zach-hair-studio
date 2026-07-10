using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Tests.Features.Appointments;

/// <summary>
/// Proves SC5 / BOOK-05 against REAL SQL Server LocalDB: an appointment whose StartsAt
/// instant is resolved by the salon's <see cref="SalonTimeZone"/> — the very same helper
/// the create path uses — stores as datetimeoffset and round-trips the correct offset
/// across both 2026 US-Eastern DST transitions: -04:00 (EDT) on the spring-forward day
/// (2026-03-08) and -05:00 (EST) on the fall-back day (2026-11-01), each preserving the
/// exact UTC instant.
///
/// Deviation from the plan's "book through the HTTP create path": both DST transition
/// dates are calendar-fixed and fall OUTSIDE the create-path booking window relative to
/// the test clock (2026-07-10) — 2026-03-08 is in the past and 2026-11-01 is beyond the
/// 60-day horizon — so the (correct, legitimate) future/horizon validator rejects them
/// with 400 before persistence is ever reached. A past date can never be booked via the
/// public API by design. SC5 is therefore proven at the layer the create path relies on
/// for its stored offset (SalonTimeZone resolution) plus the real datetimeoffset column
/// round-trip, building and persisting the Appointment + AppointmentSlots exactly as
/// AppointmentsService does. Uses SqlServerWebApplicationFactory (real SQL), NOT InMemory.
/// </summary>
public class DstRoundTripTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private readonly SqlServerWebApplicationFactory _factory;

    private const int StylistId = 1; // Mr. Zachary (seeded, active).
    private const int GridMinutes = 15;

    public DstRoundTripTests(SqlServerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    // Spring forward: 2026-03-08 — 10:00 salon-local is already EDT (-04:00).
    [InlineData(2026, 3, 8, -4)]
    // Fall back: 2026-11-01 — 10:00 salon-local is already EST (-05:00).
    [InlineData(2026, 11, 1, -5)]
    public async Task Booking_AcrossDstBoundary_PersistsCorrectOffsetAndInstant(
        int year, int month, int day, int expectedOffsetHours)
    {
        // Resolve the stored instant exactly as the create path does — via SalonTimeZone,
        // never a hardcoded offset (Pitfall 5 / D-16).
        var salonTimeZone = new SalonTimeZone("America/New_York");
        var localTenAm = new DateTime(year, month, day, 10, 0, 0);
        var instant = salonTimeZone.ToSalonInstant(localTenAm);

        Assert.NotNull(instant); // 10:00 is never in a spring-forward gap.
        Assert.Equal(TimeSpan.FromHours(expectedOffsetHours), instant!.Value.Offset);

        // Persist an Appointment + its slots exactly as AppointmentsService.BuildAppointment
        // would (Precision Cut: 45 min → 3 consecutive 15-min cells).
        var service = new { DurationMinutes = 45 };
        var cellsNeeded = (int)Math.Ceiling(service.DurationMinutes / (double)GridMinutes);

        int appointmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            var appointment = new Appointment
            {
                ServiceId = 1,
                StylistId = StylistId,
                StartsAt = instant.Value,
                Status = AppointmentStatus.Confirmed,
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane.doe@example.com",
            };

            for (var cell = 0; cell < cellsNeeded; cell++)
            {
                appointment.Slots.Add(new AppointmentSlot
                {
                    StylistId = StylistId,
                    SlotStart = instant.Value.AddMinutes(GridMinutes * cell),
                });
            }

            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var reloaded = await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointmentId);

            Assert.Equal(TimeSpan.FromHours(expectedOffsetHours), reloaded.StartsAt.Offset);
            // datetimeoffset compares by UTC instant — the persisted instant must equal the submitted one.
            Assert.Equal(instant.Value.ToUniversalTime(), reloaded.StartsAt.ToUniversalTime());
        }
    }
}
