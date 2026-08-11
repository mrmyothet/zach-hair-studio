using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Api.Tests.TestSupport;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Services;
using ZachHairStudio.Shared.Features.Stylists;

namespace ZachHairStudio.Api.Tests.Features.Appointments;

/// <summary>
/// Proves the "Any stylist" deterministic assignment (D-07, BOOK-06): with no
/// stylist chosen, CreateAsync assigns the lowest-Id free active stylist and the
/// response names that concrete stylist; when the lowest-Id stylist's cell is
/// already taken the loop falls through to the next candidate. Runs against the
/// EF Core InMemory provider — this exercises the assignment/availability logic,
/// NOT the unique-index concurrency guarantee (that is ConcurrencyTests on real SQL).
/// </summary>
public class AnyStylistAssignmentTests
{
    // Resolved through the shared BookingDates helper (relative-to-now, seeded working day),
    // so this suite stays future/in-horizon regardless of the calendar date. This test calls
    // AppointmentsService.CreateAsync directly with a real AppointmentCreateDtoValidator, so
    // it DOES cross the future-gated validator and is date-bombed the same as the HTTP-path
    // tests (correcting the plan's original exclusion rationale for this file).
    private static readonly DateOnly BookingDate = BookingDates.NextBookableDate();

    // 10:00 salon-local is the authoritative instant, resolved through the configured salon
    // zone rather than a hardcoded offset (follows Salon:IanaTimeZoneId).
    private static readonly SalonTimeZone SalonTz = SalonTimeZone.FromOptions(new SalonOptions());

    private static readonly DateTimeOffset SlotInstant = BookingDates.NextBookableSlot(10);

    private static AppointmentCreateDto AnyStylistRequest() => new()
    {
        ServiceId = 1,
        StylistId = null,
        StartsAt = SlotInstant,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        Phone = null,
    };

    [Fact]
    public async Task AnyStylist_AssignsLowestIdFreeStylist()
    {
        await using var db = BuildSeededContext();
        var service = BuildService(db);

        var result = await service.CreateAsync(AnyStylistRequest());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(1, result.Data.StylistId);
        Assert.Equal("Ada Lowest", result.Data.StylistName);
        Assert.NotEqual("Any", result.Data.StylistName);
    }

    [Fact]
    public async Task AnyStylist_FallsThroughWhenLowestIdStylistTaken()
    {
        await using var db = BuildSeededContext();
        // Pre-book stylist 1's cell at the slot so only stylist 2 is free.
        db.Appointments.Add(new Appointment
        {
            ServiceId = 1,
            StylistId = 1,
            StartsAt = SlotInstant,
            FirstName = "Prior",
            LastName = "Guest",
            Email = "prior.guest@example.com",
            Slots = { new AppointmentSlot { StylistId = 1, SlotStart = SlotInstant } },
        });
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var result = await service.CreateAsync(AnyStylistRequest());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, result.Data.StylistId);
        Assert.Equal("Ben Next", result.Data.StylistName);
    }

    [Fact]
    public async Task AnyStylist_AllCandidatesTaken_ReturnsDuplicateRecordError()
    {
        await using var db = BuildSeededContext();
        // Pre-book BOTH stylists' cells at the slot.
        foreach (var stylistId in new[] { 1, 2 })
        {
            db.Appointments.Add(new Appointment
            {
                ServiceId = 1,
                StylistId = stylistId,
                StartsAt = SlotInstant,
                FirstName = "Prior",
                LastName = "Guest",
                Email = "prior.guest@example.com",
                Slots = { new AppointmentSlot { StylistId = stylistId, SlotStart = SlotInstant } },
            });
        }
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var result = await service.CreateAsync(AnyStylistRequest());

        Assert.False(result.IsSuccess);
        Assert.True(result.IsDuplicateRecord());
    }

    [Fact]
    public async Task RequestedTimeNotAnOpenSlot_ReturnsNotFound()
    {
        await using var db = BuildSeededContext();
        var service = BuildService(db);

        var request = AnyStylistRequest();
        // 03:00 salon-local is outside the seeded 09:00-18:00 working hours — not a real slot.
        request.StartsAt = BookingDates.NextBookableSlot(3);

        var result = await service.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsNotFound());
    }

    [Fact]
    public async Task SuccessfulCreate_AttemptsConfirmationEmailAfterCommit()
    {
        await using var db = BuildSeededContext();
        var email = new RecordingEmailService();
        var service = BuildService(db, email);

        var result = await service.CreateAsync(AnyStylistRequest());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(1, email.CallCount);
    }

    private static AppointmentsService BuildService(BookingDbContext db, IEmailService? email = null)
    {
        var salonOptions = new SalonOptions();
        var slotService = new SlotService(db, salonOptions);
        return new AppointmentsService(
            db,
            new AppointmentCreateDtoValidator(),
            new ClientRescheduleRequestDtoValidator(),
            slotService,
            email ?? new RecordingEmailService(),
            salonOptions,
            new ZachHairStudio.Shared.Features.Loyalty.LoyaltyService(db),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AppointmentsService>.Instance);
    }

    private static BookingDbContext BuildSeededContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase($"AnyStylist-{Guid.NewGuid()}")
            .Options;

        var db = new BookingDbContext(options);

        db.Services.Add(new Service
        {
            Id = 1,
            Slug = "precision-cut",
            Name = "Precision Cut",
            ShortDescription = "s",
            LongDescription = "l",
            Category = "Cuts",
            DurationMinutes = 45,
            Price = 35m,
            IsActive = true,
            DisplayOrder = 1,
        });

        db.Stylists.AddRange(
            new Stylist { Id = 1, Slug = "ada-lowest", Name = "Ada Lowest", IsActive = true, DisplayOrder = 1 },
            new Stylist { Id = 2, Slug = "ben-next", Name = "Ben Next", IsActive = true, DisplayOrder = 2 });

        db.StylistWorkingHours.AddRange(
            new StylistWorkingHours { Id = 1, StylistId = 1, DayOfWeek = BookingDate.DayOfWeek, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
            new StylistWorkingHours { Id = 2, StylistId = 2, DayOfWeek = BookingDate.DayOfWeek, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) });

        db.SaveChanges();
        return db;
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public int CallCount { get; private set; }

        public Task SendConfirmationAsync(Appointment appointment, ServiceResponseDto service, string stylistName)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
