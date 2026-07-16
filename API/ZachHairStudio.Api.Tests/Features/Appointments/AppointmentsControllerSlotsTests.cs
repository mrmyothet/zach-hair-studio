using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Api.Controllers;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Tests.Features.Appointments;

/// <summary>
/// Proves GET /api/appointments/slots is wired end-to-end: SlotService resolved
/// via DI, offset-carrying DateTimeOffset start times, and stylistId narrowing
/// the result set (BOOK-01, BOOK-06). Uses a Sunday date and a hand-seeded
/// working-hours row so the test does not depend on the placeholder Tue-Sat
/// HasData seed shared with other tests (PATT-01: InMemory fixture is fine —
/// no unique-constraint semantics are exercised here).
/// </summary>
public class AppointmentsControllerSlotsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    // A Sunday with no seeded StylistWorkingHours (seed data covers Tue-Sat only),
    // so only the rows this test explicitly adds are in play. Anchored to today so it
    // always lands on a current-or-future Sunday rather than a fixed past date.
    private static readonly DateOnly TestSunday = NextSunday(DateOnly.FromDateTime(DateTime.UtcNow));

    public AppointmentsControllerSlotsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSlots_ReturnsOkWithOffsetCarryingStartTimesWithinWorkingHours()
    {
        await SeedWorkingHoursAsync(stylistId: 1, TestSunday.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(10, 0));

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/appointments/slots?serviceId=1&date={TestSunday:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var slots = await response.Content.ReadFromJsonAsync<List<OpenSlotDto>>();
        Assert.NotNull(slots);
        Assert.NotEmpty(slots);
        Assert.All(slots, slot =>
        {
            Assert.NotEqual(default, slot.StartsAt.Offset);
            var localTime = TimeOnly.FromDateTime(slot.StartsAt.DateTime);
            Assert.True(localTime >= new TimeOnly(9, 0) && localTime < new TimeOnly(10, 0));
        });
    }

    [Fact]
    public async Task GetSlots_StylistIdFilter_NarrowsResultSet()
    {
        // Only stylist 1 works this Sunday; stylist 2 has no working-hours row for it.
        await SeedWorkingHoursAsync(stylistId: 1, TestSunday.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(9, 30));

        var client = _factory.CreateClient();

        var unfilteredResponse = await client.GetAsync($"/api/appointments/slots?serviceId=1&date={TestSunday:yyyy-MM-dd}");
        var filteredOutResponse = await client.GetAsync($"/api/appointments/slots?serviceId=1&stylistId=2&date={TestSunday:yyyy-MM-dd}");
        var filteredInResponse = await client.GetAsync($"/api/appointments/slots?serviceId=1&stylistId=1&date={TestSunday:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, unfilteredResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, filteredOutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, filteredInResponse.StatusCode);

        var unfilteredSlots = await unfilteredResponse.Content.ReadFromJsonAsync<List<OpenSlotDto>>();
        var filteredOutSlots = await filteredOutResponse.Content.ReadFromJsonAsync<List<OpenSlotDto>>();
        var filteredInSlots = await filteredInResponse.Content.ReadFromJsonAsync<List<OpenSlotDto>>();

        Assert.NotNull(unfilteredSlots);
        Assert.NotEmpty(unfilteredSlots);

        // stylistId=2 narrows the result to empty (stylist 2 has no hours this Sunday).
        Assert.NotNull(filteredOutSlots);
        Assert.Empty(filteredOutSlots);

        // stylistId=1 narrows/keeps the same candidates, now with a concrete stylist assigned.
        Assert.NotNull(filteredInSlots);
        Assert.NotEmpty(filteredInSlots);
        Assert.All(filteredInSlots, slot => Assert.Equal(1, slot.StylistId));
    }

    private async Task SeedWorkingHoursAsync(int stylistId, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        dbContext.StylistWorkingHours.Add(new StylistWorkingHours
        {
            StylistId = stylistId,
            DayOfWeek = dayOfWeek,
            StartTime = start,
            EndTime = end,
        });

        await dbContext.SaveChangesAsync();
    }

    private static DateOnly NextSunday(DateOnly from)
    {
        var date = from;
        while (date.DayOfWeek != DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
