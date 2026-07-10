using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Services;
using ZachHairStudio.Shared.Features.Stylists;

namespace ZachHairStudio.Api.Tests.Features.Availability;

/// <summary>
/// Proves SlotService.GetOpenSlotsAsync's grid math: cell-count rounding (D-02,
/// Pitfall 4), booked-cell and time-off exclusion (D-06), and union-vs-filtered
/// stylist behavior (D-07). Uses the EF Core InMemory provider with hand-seeded
/// rows — no unique-index/concurrency semantics are needed here (that's the
/// real-SQL-Server fixture's job in a later plan).
/// </summary>
public class SlotServiceTests
{
    private static readonly SalonOptions SalonOptions = new() { IanaTimeZoneId = "America/New_York" };

    // A plain midweek Tuesday, safely inside standard time (no DST edge nearby).
    private static readonly DateOnly WorkingTuesday = new(2026, 7, 14);

    private static BookingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BookingDbContext(options);
    }

    private static Service SeedService(BookingDbContext dbContext, int durationMinutes, int id = 1)
    {
        var service = new Service
        {
            Id = id,
            Slug = $"service-{id}",
            Name = $"Service {id}",
            ShortDescription = "Short description.",
            LongDescription = "Long description.",
            Category = "Cuts",
            DurationMinutes = durationMinutes,
            Price = 10m,
            IsActive = true,
            DisplayOrder = id,
        };
        dbContext.Services.Add(service);
        return service;
    }

    private static Stylist SeedStylist(BookingDbContext dbContext, int id, string name)
    {
        var stylist = new Stylist { Id = id, Slug = name.ToLowerInvariant(), Name = name, IsActive = true, DisplayOrder = id };
        dbContext.Stylists.Add(stylist);
        return stylist;
    }

    private static void SeedWorkingHours(BookingDbContext dbContext, int stylistId, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end)
        => dbContext.StylistWorkingHours.Add(new StylistWorkingHours
        {
            StylistId = stylistId,
            DayOfWeek = dayOfWeek,
            StartTime = start,
            EndTime = end,
        });

    private static void SeedBookedCell(BookingDbContext dbContext, int stylistId, DateTimeOffset slotStart)
    {
        var appointment = new Appointment
        {
            ServiceId = 1,
            StylistId = stylistId,
            StartsAt = slotStart,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
        };
        appointment.Slots.Add(new AppointmentSlot { StylistId = stylistId, SlotStart = slotStart });
        dbContext.Appointments.Add(appointment);
    }

    [Fact]
    public async Task GetOpenSlotsAsync_ScalpTreatment40Minutes_Reserves3CellsNot2()
    {
        using var dbContext = CreateContext();
        var service = SeedService(dbContext, durationMinutes: 40);
        var stylist = SeedStylist(dbContext, 1, "Aria");
        SeedWorkingHours(dbContext, stylist.Id, WorkingTuesday.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(10, 0));
        await dbContext.SaveChangesAsync();

        var slotService = new SlotService(dbContext, SalonOptions);
        var slots = await slotService.GetOpenSlotsAsync(service.Id, stylist.Id, WorkingTuesday);

        // 09:00-10:00 window; a 3-cell (45min, rounded up from 40) service fits at
        // 09:00 (+45=09:45) and 09:15 (+45=10:00), but NOT 09:30 (+45=10:15 > 10:00).
        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(9, 15)],
            slots.Select(slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime)));
    }

    [Fact]
    public async Task GetOpenSlotsAsync_NinetyMinuteService_Reserves6Cells()
    {
        using var dbContext = CreateContext();
        var service = SeedService(dbContext, durationMinutes: 90);
        var stylist = SeedStylist(dbContext, 1, "Aria");
        SeedWorkingHours(dbContext, stylist.Id, WorkingTuesday.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(11, 0));
        await dbContext.SaveChangesAsync();

        var slotService = new SlotService(dbContext, SalonOptions);
        var slots = await slotService.GetOpenSlotsAsync(service.Id, stylist.Id, WorkingTuesday);

        // 09:00-11:00 window (120min); a 6-cell (90min) service's last valid start is
        // 09:30 (+90=11:00). Off-by-one cell math (e.g. 5 or 7 cells) would shift this.
        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(9, 15), new TimeOnly(9, 30)],
            slots.Select(slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime)));
    }

    [Fact]
    public async Task GetOpenSlotsAsync_BookedCell_RemovesOverlappingCandidateStarts()
    {
        using var dbContext = CreateContext();
        var service = SeedService(dbContext, durationMinutes: 15);
        var stylist = SeedStylist(dbContext, 1, "Aria");
        SeedWorkingHours(dbContext, stylist.Id, WorkingTuesday.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(10, 0));
        await dbContext.SaveChangesAsync();

        var salonTz = new SalonTimeZone(SalonOptions.IanaTimeZoneId);
        var bookedStart = salonTz.ToSalonInstant(WorkingTuesday.ToDateTime(new TimeOnly(9, 15)))!.Value;
        SeedBookedCell(dbContext, stylist.Id, bookedStart);
        await dbContext.SaveChangesAsync();

        var slotService = new SlotService(dbContext, SalonOptions);
        var slots = await slotService.GetOpenSlotsAsync(service.Id, stylist.Id, WorkingTuesday);

        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(9, 30), new TimeOnly(9, 45)],
            slots.Select(slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime)));
    }

    [Fact]
    public async Task GetOpenSlotsAsync_TimeOffInterval_RemovesOverlappingCandidateStarts()
    {
        using var dbContext = CreateContext();
        var service = SeedService(dbContext, durationMinutes: 15);
        var stylist = SeedStylist(dbContext, 1, "Aria");
        SeedWorkingHours(dbContext, stylist.Id, WorkingTuesday.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(10, 0));
        await dbContext.SaveChangesAsync();

        var salonTz = new SalonTimeZone(SalonOptions.IanaTimeZoneId);
        var timeOffStart = salonTz.ToSalonInstant(WorkingTuesday.ToDateTime(new TimeOnly(9, 15)))!.Value;
        var timeOffEnd = salonTz.ToSalonInstant(WorkingTuesday.ToDateTime(new TimeOnly(9, 45)))!.Value;
        dbContext.StylistTimeOff.Add(new StylistTimeOff
        {
            StylistId = stylist.Id,
            StartsAt = timeOffStart,
            EndsAt = timeOffEnd,
            Reason = "Lunch",
        });
        await dbContext.SaveChangesAsync();

        var slotService = new SlotService(dbContext, SalonOptions);
        var slots = await slotService.GetOpenSlotsAsync(service.Id, stylist.Id, WorkingTuesday);

        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(9, 45)],
            slots.Select(slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime)));
    }

    [Fact]
    public async Task GetOpenSlotsAsync_NoStylistId_ReturnsUnionAcrossActiveStylists_StylistIdFiltersToOne()
    {
        using var dbContext = CreateContext();
        var service = SeedService(dbContext, durationMinutes: 15);
        var stylistA = SeedStylist(dbContext, 1, "Aria");
        var stylistB = SeedStylist(dbContext, 2, "Marcus");
        SeedWorkingHours(dbContext, stylistA.Id, WorkingTuesday.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(9, 30));
        SeedWorkingHours(dbContext, stylistB.Id, WorkingTuesday.DayOfWeek, new TimeOnly(9, 15), new TimeOnly(9, 45));
        await dbContext.SaveChangesAsync();

        var slotService = new SlotService(dbContext, SalonOptions);
        var unionSlots = await slotService.GetOpenSlotsAsync(service.Id, stylistId: null, WorkingTuesday);
        var filteredSlots = await slotService.GetOpenSlotsAsync(service.Id, stylistA.Id, WorkingTuesday);

        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(9, 15), new TimeOnly(9, 30)],
            unionSlots.Select(slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime)));
        Assert.All(unionSlots, slot => Assert.Null(slot.StylistId));
        Assert.All(unionSlots, slot => Assert.Null(slot.StylistName));

        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(9, 15)],
            filteredSlots.Select(slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime)));
        Assert.All(filteredSlots, slot => Assert.Equal(stylistA.Id, slot.StylistId));
        Assert.All(filteredSlots, slot => Assert.Equal(stylistA.Name, slot.StylistName));
    }
}
