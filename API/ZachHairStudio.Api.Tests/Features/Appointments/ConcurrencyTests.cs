using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Api.Tests.TestSupport;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;

namespace ZachHairStudio.Api.Tests.Features.Appointments;

/// <summary>
/// Proves SC4 / BOOK-04 against REAL SQL Server LocalDB: two near-simultaneous POSTs
/// for the same (stylistId, slot) yield exactly one 201 and one 409, and exactly one
/// AppointmentSlot row exists for the contested cell afterward. This can only be proven
/// on real SQL Server — the InMemory provider does not enforce the unfiltered unique
/// index that IS the double-booking guarantee (Pitfall 3), so this uses
/// SqlServerWebApplicationFactory, NOT CustomWebApplicationFactory.
/// </summary>
public class ConcurrencyTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private readonly SqlServerWebApplicationFactory _factory;

    // Resolved through the shared BookingDates helper (relative-to-now, seeded working day),
    // so this slot stays future/in-horizon regardless of the calendar date.
    private static readonly DateTimeOffset SlotInstant = BookingDates.NextBookableSlot(10);

    private const int StylistId = 1; // Mr. Zachary (seeded, active).

    public ConcurrencyTests(SqlServerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TwoSimultaneousRequestsForSameSlot_ExactlyOne201AndOne409()
    {
        // RFC 2606 reserved recipient — the real Testing-env Resend send (D-12) is
        // rejected best-effort (D-11) and never affects the booking or these assertions.
        var request = new AppointmentCreateDto
        {
            ServiceId = 1, // Precision Cut, 45 min → 3 consecutive cells.
            StylistId = StylistId,
            StartsAt = SlotInstant,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            Phone = null,
        };

        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        var task1 = client1.PostAsJsonAsync("/api/appointments", request);
        var task2 = client2.PostAsJsonAsync("/api/appointments", request);
        var responses = await Task.WhenAll(task1, task2);

        var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToList();
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, statusCodes);

        // The guarantee is the DB index, not an app-level check: exactly one slot row.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var slotCount = await db.AppointmentSlots
            .CountAsync(s => s.StylistId == StylistId && s.SlotStart == SlotInstant);

        Assert.Equal(1, slotCount);
    }
}
