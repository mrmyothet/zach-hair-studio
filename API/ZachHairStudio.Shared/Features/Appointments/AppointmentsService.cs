using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Services;
using ZachHairStudio.Shared.Features.Stylists;

namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>
/// The booking write path. Validates the create request, re-validates the requested
/// slot server-side against the SlotService grid (never trusts the echoed slot,
/// T-02-09), resolves candidate stylists (D-07 deterministic "Any stylist"), and
/// try-inserts one candidate at a time. Atomicity for the Appointment + N
/// AppointmentSlot rows comes from a single SaveChangesAsync per candidate — there is
/// deliberately NO manual transaction (Pitfall 2: incompatible with EnableRetryOnFailure).
/// A duplicate-key violation (SQL 2601/2627) means another booking won the race for
/// that stylist's cell; the loop detaches and tries the next candidate. The
/// confirmation email is best-effort AFTER commit and can never roll back the booking (D-11).
/// </summary>
public class AppointmentsService
{
    private const int GridMinutes = 15;

    private readonly BookingDbContext _dbContext;
    private readonly IValidator<AppointmentCreateDto> _validator;
    private readonly SlotService _slotService;
    private readonly IEmailService _emailService;

    public AppointmentsService(
        BookingDbContext dbContext,
        IValidator<AppointmentCreateDto> validator,
        SlotService slotService,
        IEmailService emailService)
    {
        _dbContext = dbContext;
        _validator = validator;
        _slotService = slotService;
        _emailService = emailService;
    }

    public async Task<Result<AppointmentResponseDto>> CreateAsync(AppointmentCreateDto request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return Result<AppointmentResponseDto>.ValidationError(
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        var service = await _dbContext.Services.FindAsync(request.ServiceId);
        if (service is null || !service.IsActive)
        {
            return Result<AppointmentResponseDto>.NotFoundError("Service not found.");
        }

        // Salon-local date of the requested instant. The echoed offset only selects the
        // query day; the authoritative trust anchor is instant-equality against the
        // server-recomputed open-slot grid below — a wrong/forged offset fails closed.
        var date = DateOnly.FromDateTime(request.StartsAt.DateTime);

        // Candidate stylists, deterministically ordered by Id (D-07). A specific
        // requested stylist restricts the set to just that one.
        var activeStylists = await _dbContext.Stylists
            .Where(stylist => stylist.IsActive && (request.StylistId == null || stylist.Id == request.StylistId))
            .OrderBy(stylist => stylist.Id)
            .ToListAsync();

        var requestedInstantUtc = request.StartsAt.ToUniversalTime();

        var freeCandidates = new List<(Stylist Stylist, DateTimeOffset Instant)>();
        foreach (var stylist in activeStylists)
        {
            var openSlots = await _slotService.GetOpenSlotsAsync(request.ServiceId, stylist.Id, date);
            var match = openSlots.FirstOrDefault(slot => slot.StartsAt.ToUniversalTime() == requestedInstantUtc);
            if (match is not null)
            {
                freeCandidates.Add((stylist, match.StartsAt));
            }
        }

        if (freeCandidates.Count == 0)
        {
            // Distinguish "already booked" (a real grid slot someone else took → 409-worthy)
            // from "never an open slot" (off working hours / invalid time → 404).
            var candidateStylistIds = activeStylists.Select(stylist => stylist.Id).ToList();
            var alreadyBooked = await _dbContext.AppointmentSlots
                .AnyAsync(slot => candidateStylistIds.Contains(slot.StylistId)
                                  && slot.SlotStart == request.StartsAt);

            return alreadyBooked
                ? Result<AppointmentResponseDto>.DuplicateRecordError(
                    "This slot was just booked by someone else. Please choose another time.")
                : Result<AppointmentResponseDto>.NotFoundError("That time is not an available slot.");
        }

        var cellsNeeded = (int)Math.Ceiling(service.DurationMinutes / (double)GridMinutes);

        foreach (var (stylist, instant) in freeCandidates)
        {
            var appointment = BuildAppointment(request, stylist.Id, instant, cellsNeeded);
            _dbContext.Appointments.Add(appointment);

            try
            {
                // Single implicit transaction — all rows commit or none do. No manual
                // BeginTransaction (Pitfall 2).
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
            {
                // Lost the race for this stylist's cell — detach and try the next candidate.
                _dbContext.Entry(appointment).State = EntityState.Detached;
                foreach (var slot in appointment.Slots)
                {
                    _dbContext.Entry(slot).State = EntityState.Detached;
                }

                continue;
            }

            var dto = appointment.ToDto(service, stylist);

            // Best-effort confirmation AFTER commit (D-11). Guarded so no IEmailService
            // implementation — including a Resend outage — can cost the client their slot.
            try
            {
                await _emailService.SendConfirmationAsync(appointment, service.ToDto(), stylist.Name);
            }
            catch
            {
                // Swallow: the booking is already committed and the email is best-effort.
                // ResendEmailService logs its own failures; a throwing double must not roll back.
            }

            return Result<AppointmentResponseDto>.Success(dto);
        }

        return Result<AppointmentResponseDto>.DuplicateRecordError(
            "This slot was just booked by someone else. Please choose another time.");
    }

    private static Appointment BuildAppointment(
        AppointmentCreateDto request, int stylistId, DateTimeOffset instant, int cellsNeeded)
    {
        var appointment = new Appointment
        {
            ServiceId = request.ServiceId,
            StylistId = stylistId,
            StartsAt = instant,
            Status = AppointmentStatus.Confirmed,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
        };

        for (var cell = 0; cell < cellsNeeded; cell++)
        {
            appointment.Slots.Add(new AppointmentSlot
            {
                StylistId = stylistId,
                SlotStart = instant.AddMinutes(GridMinutes * cell),
            });
        }

        return appointment;
    }

    // Landmine 1 (02-RESEARCH): EF Core's HasIndex().IsUnique() emits CREATE UNIQUE INDEX,
    // which SQL Server flags as error 2601 — NOT 2627 (a named UNIQUE CONSTRAINT). Catch both
    // so the 409 mapping holds regardless of how the index is later expressed.
    private static bool IsDuplicateKeyViolation(DbUpdateException ex)
        => ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
           && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
}
