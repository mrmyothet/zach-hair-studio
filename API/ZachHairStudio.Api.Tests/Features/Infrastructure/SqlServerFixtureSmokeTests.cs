using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Stylists;

namespace ZachHairStudio.Api.Tests.Features.Infrastructure;

// Class name intentionally contains "SqlServer" so it matches the
// `FullyQualifiedName~SqlServer` filter used by the quick-run exclusion.
public class SqlServerFixtureSmokeTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private readonly SqlServerWebApplicationFactory _factory;

    public SqlServerFixtureSmokeTests(SqlServerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MigratedDatabase_ExposesFourSeededStylists()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/stylists");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stylists = await response.Content.ReadFromJsonAsync<List<StylistResponseDto>>();
        Assert.NotNull(stylists);
        Assert.Equal(4, stylists.Count);
    }

    [Fact]
    public async Task AppointmentSlot_RoundTripsDateTimeOffset()
    {
        // A pre-DST-transition instant (US Eastern springs forward 2026-03-08) with an
        // explicit -05:00 offset — the offset must survive the SQL Server datetimeoffset round-trip.
        var slotStart = new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.FromHours(-5));

        int slotId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            var appointment = new Appointment
            {
                ServiceId = 1,
                StylistId = 1,
                StartsAt = slotStart,
                FirstName = "Smoke",
                LastName = "Test",
                Email = "smoke@example.com",
                Slots =
                {
                    new AppointmentSlot { StylistId = 1, SlotStart = slotStart },
                },
            };

            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            slotId = appointment.Slots[0].Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            var reloaded = await db.AppointmentSlots
                .AsNoTracking()
                .SingleAsync(s => s.Id == slotId);

            Assert.Equal(slotStart, reloaded.SlotStart);
            Assert.Equal(TimeSpan.FromHours(-5), reloaded.SlotStart.Offset);
        }
    }

    [Fact]
    public void ResendApiKey_ResolvesInTestingEnvironment()
    {
        // D-12: real Resend sends occur in the Testing environment too, so the key MUST resolve here.
        // Program.cs registers AddUserSecrets unconditionally; without it the default host loads
        // user secrets only in Development and dotnet test (env "Testing") would find no key.
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();

        var resendApiKey = configuration["RESEND_API_KEY"];

        // Assert non-empty only — never assert on, log, or print the value.
        Assert.False(
            string.IsNullOrWhiteSpace(resendApiKey),
            "RESEND_API_KEY not configured — set it via `dotnet user-secrets set RESEND_API_KEY <key> --project API/ZachHairStudio.Api` (D-12/D-13).");
    }
}
