using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Loyalty;
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
    private readonly IValidator<ClientRescheduleRequestDto> _rescheduleValidator;
    private readonly SlotService _slotService;
    private readonly IEmailService _emailService;
    private readonly SalonTimeZone _salonTimeZone;
    private readonly LoyaltyService _loyaltyService;

    // Confirmed -> {Completed, Cancelled, NoShow}; the three terminal statuses have no
    // outbound entries. This map is the ONLY place a status transition is decided
    // (03-PATTERNS: no second transition table/endpoint).
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitions = new()
    {
        [AppointmentStatus.Confirmed] = new[] { AppointmentStatus.Completed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow },
        [AppointmentStatus.Completed] = Array.Empty<AppointmentStatus>(),
        [AppointmentStatus.Cancelled] = Array.Empty<AppointmentStatus>(),
        [AppointmentStatus.NoShow] = Array.Empty<AppointmentStatus>(),
    };

    public AppointmentsService(
        BookingDbContext dbContext,
        IValidator<AppointmentCreateDto> validator,
        IValidator<ClientRescheduleRequestDto> rescheduleValidator,
        SlotService slotService,
        IEmailService emailService,
        SalonOptions salonOptions,
        LoyaltyService loyaltyService)
    {
        _dbContext = dbContext;
        _validator = validator;
        _rescheduleValidator = rescheduleValidator;
        _slotService = slotService;
        _emailService = emailService;
        _salonTimeZone = SalonTimeZone.FromOptions(salonOptions);
        _loyaltyService = loyaltyService;
    }

    public async Task<Result<AppointmentResponseDto>> CreateAsync(
        AppointmentCreateDto request,
        int? clientUserId = null)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return Result<AppointmentResponseDto>.ValidationError(
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        var bookResult = await TryBookNewAsync(request, clientUserId);
        if (!bookResult.IsSuccess)
        {
            if (bookResult.IsValidationError())
            {
                return Result<AppointmentResponseDto>.ValidationError(bookResult.Message);
            }

            if (bookResult.IsNotFound())
            {
                return Result<AppointmentResponseDto>.NotFoundError(bookResult.Message);
            }

            if (bookResult.IsDuplicateRecord())
            {
                return Result<AppointmentResponseDto>.DuplicateRecordError(bookResult.Message);
            }

            return Result<AppointmentResponseDto>.SystemError(bookResult.Message);
        }

        var (appointment, service, stylist) = bookResult.Data;
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

    /// <summary>
    /// Staff schedule read (DASH-01/DASH-04). The window bound resolves through the
    /// single salon-zone helper — never a hardcoded offset (D-16 pattern) — so a day
    /// spanning a DST transition still yields correct UTC bounds. Terminal
    /// (Cancelled/NoShow) appointments are included; hiding them by default is the
    /// dashboard's D-08 concern, not this query's. Filtering is always on the explicit
    /// AppointmentStatus enum value, never a derived boolean (DASH-04).
    /// </summary>
    public async Task<Result<IReadOnlyList<AppointmentResponseDto>>> ListByDateRangeAsync(
        DateOnly from, DateOnly to, AppointmentStatus? status)
    {
        var start = _salonTimeZone.ToSalonInstant(from.ToDateTime(TimeOnly.MinValue));
        var end = _salonTimeZone.ToSalonInstant(to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        if (start is null || end is null)
        {
            return Result<IReadOnlyList<AppointmentResponseDto>>.ValidationError(
                "The requested date range falls on an invalid salon-local time.");
        }

        var query = _dbContext.Appointments
            .Where(appointment => appointment.StartsAt >= start.Value && appointment.StartsAt < end.Value);

        if (status is not null)
        {
            query = query.Where(appointment => appointment.Status == status.Value);
        }

        var appointments = await query
            .OrderBy(appointment => appointment.StartsAt)
            .ToListAsync();

        if (appointments.Count == 0)
        {
            return Result<IReadOnlyList<AppointmentResponseDto>>.Success(Array.Empty<AppointmentResponseDto>());
        }

        var serviceIds = appointments.Select(appointment => appointment.ServiceId).Distinct().ToList();
        var stylistIds = appointments.Select(appointment => appointment.StylistId).Distinct().ToList();

        var services = await _dbContext.Services
            .Where(service => serviceIds.Contains(service.Id))
            .ToDictionaryAsync(service => service.Id);
        var stylists = await _dbContext.Stylists
            .Where(stylist => stylistIds.Contains(stylist.Id))
            .ToDictionaryAsync(stylist => stylist.Id);

        // No FK constraints back Appointment.ServiceId/StylistId, so a missing referenced
        // row must surface as a controlled SystemError — never a KeyNotFoundException 500.
        var dtos = new List<AppointmentResponseDto>(appointments.Count);
        foreach (var appointment in appointments)
        {
            if (!services.TryGetValue(appointment.ServiceId, out var service)
                || !stylists.TryGetValue(appointment.StylistId, out var stylist))
            {
                return Result<IReadOnlyList<AppointmentResponseDto>>.SystemError(
                    $"Appointment {appointment.Id} references a missing service or stylist.");
            }

            dtos.Add(appointment.ToDto(service, stylist));
        }

        return Result<IReadOnlyList<AppointmentResponseDto>>.Success(dtos);
    }

    /// <summary>One appointment's full detail, including the status-audit line (DASH-02, D-12).</summary>
    public async Task<Result<AppointmentResponseDto>> GetByIdAsync(int id)
    {
        var appointment = await _dbContext.Appointments.FindAsync(id);
        if (appointment is null)
        {
            return Result<AppointmentResponseDto>.NotFoundError("Appointment not found.");
        }

        var service = await _dbContext.Services.FindAsync(appointment.ServiceId);
        var stylist = await _dbContext.Stylists.FindAsync(appointment.StylistId);
        if (service is null || stylist is null)
        {
            return Result<AppointmentResponseDto>.SystemError(
                $"Appointment {appointment.Id} references a missing service or stylist.");
        }

        return Result<AppointmentResponseDto>.Success(appointment.ToDto(service, stylist));
    }

    /// <summary>
    /// Constrained, server-enforced status transitions (D-10). The CURRENT status is
    /// always re-read from the DB and checked against the single <see cref="AllowedTransitions"/>
    /// map — a stale/forged client-echoed status can never force a disallowed transition.
    /// Cancel/NoShow remove the appointment's AppointmentSlot rows here; this method IS the
    /// single reusable slot-release path (D-04/D-11), not a copy of the booking-time logic.
    /// StatusChangedAt/StatusChangedBy record the minimal audit line (D-12).
    /// </summary>
    public async Task<Result<AppointmentResponseDto>> UpdateStatusAsync(
        int id, AppointmentStatus newStatus, string staffDisplayName)
    {
        var appointment = await _dbContext.Appointments
            .Include(appointment => appointment.Slots)
            .FirstOrDefaultAsync(appointment => appointment.Id == id);

        if (appointment is null)
        {
            return Result<AppointmentResponseDto>.NotFoundError("Appointment not found.");
        }

        if (!IsAllowedTransition(appointment.Status, newStatus))
        {
            return Result<AppointmentResponseDto>.ValidationError(
                $"Cannot move an appointment from {appointment.Status} to {newStatus}.");
        }

        // Resolve the referenced rows BEFORE mutating — no FK constraints back these ids,
        // and a missing row must not commit the status change and then fail the response.
        var service = await _dbContext.Services.FindAsync(appointment.ServiceId);
        var stylist = await _dbContext.Stylists.FindAsync(appointment.StylistId);
        if (service is null || stylist is null)
        {
            return Result<AppointmentResponseDto>.SystemError(
                $"Appointment {appointment.Id} references a missing service or stylist.");
        }

        if (newStatus is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
        {
            _dbContext.AppointmentSlots.RemoveRange(appointment.Slots);
        }

        appointment.Status = newStatus;
        appointment.StatusChangedAt = DateTimeOffset.UtcNow;
        appointment.StatusChangedBy = staffDisplayName;

        await _dbContext.SaveChangesAsync();

        // D-13: earn +1 when staff marks Completed for an owned appointment (idempotent).
        if (newStatus == AppointmentStatus.Completed && appointment.ClientUserId is int clientUserId)
        {
            await _loyaltyService.EarnForCompletedAsync(appointment.Id, clientUserId);
        }

        return Result<AppointmentResponseDto>.Success(appointment.ToDto(service, stylist));
    }

    /// <summary>
    /// Client self-service cancel (ACCT-04 / D-09 / D-11 / D-12). Ownership from
    /// <paramref name="clientUserId"/> only; Confirmed→Cancelled via the shared
    /// <see cref="AllowedTransitions"/> map + AppointmentSlots RemoveRange path.
    /// </summary>
    public async Task<Result<AppointmentResponseDto>> CancelForClientAsync(
        int appointmentId, int clientUserId, string actorDisplayName)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.Slots)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment is null || appointment.ClientUserId != clientUserId)
        {
            return Result<AppointmentResponseDto>.NotFoundError("Appointment not found.");
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return Result<AppointmentResponseDto>.ValidationError(
                $"Cannot cancel an appointment with status {appointment.Status}.");
        }

        if (HasAppointmentStarted(appointment.StartsAt))
        {
            return Result<AppointmentResponseDto>.ValidationError(
                "This appointment has already started and can no longer be cancelled online.");
        }

        if (!IsAllowedTransition(appointment.Status, AppointmentStatus.Cancelled))
        {
            return Result<AppointmentResponseDto>.ValidationError(
                $"Cannot move an appointment from {appointment.Status} to {AppointmentStatus.Cancelled}.");
        }

        var service = await _dbContext.Services.FindAsync(appointment.ServiceId);
        var stylist = await _dbContext.Stylists.FindAsync(appointment.StylistId);
        if (service is null || stylist is null)
        {
            return Result<AppointmentResponseDto>.SystemError(
                $"Appointment {appointment.Id} references a missing service or stylist.");
        }

        _dbContext.AppointmentSlots.RemoveRange(appointment.Slots);
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.StatusChangedAt = DateTimeOffset.UtcNow;
        appointment.StatusChangedBy = actorDisplayName;

        await _dbContext.SaveChangesAsync();

        return Result<AppointmentResponseDto>.Success(appointment.ToDto(service, stylist));
    }

    /// <summary>
    /// Client self-service reschedule (ACCT-04 / D-10 / D-11 / D-12). Book-new then
    /// cancel-old inside CreateExecutionStrategy + BeginTransactionAsync so a
    /// unique-index failure rolls back and the old booking stays Confirmed (Pitfall 4).
    /// Never cancel-first.
    /// </summary>
    public async Task<Result<AppointmentResponseDto>> RescheduleForClientAsync(
        int appointmentId,
        int clientUserId,
        ClientRescheduleRequestDto request,
        string actorDisplayName)
    {
        var validation = await _rescheduleValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return Result<AppointmentResponseDto>.ValidationError(
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var oldAppointment = await _dbContext.Appointments
                .Include(a => a.Slots)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (oldAppointment is null || oldAppointment.ClientUserId != clientUserId)
            {
                return Result<AppointmentResponseDto>.NotFoundError("Appointment not found.");
            }

            if (oldAppointment.Status != AppointmentStatus.Confirmed)
            {
                return Result<AppointmentResponseDto>.ValidationError(
                    $"Cannot reschedule an appointment with status {oldAppointment.Status}.");
            }

            if (HasAppointmentStarted(oldAppointment.StartsAt))
            {
                return Result<AppointmentResponseDto>.ValidationError(
                    "This appointment has already started and can no longer be rescheduled online.");
            }

            if (!IsAllowedTransition(oldAppointment.Status, AppointmentStatus.Cancelled))
            {
                return Result<AppointmentResponseDto>.ValidationError(
                    $"Cannot move an appointment from {oldAppointment.Status} to {AppointmentStatus.Cancelled}.");
            }

            var createRequest = new AppointmentCreateDto
            {
                ServiceId = oldAppointment.ServiceId,
                StylistId = request.StylistId ?? oldAppointment.StylistId,
                StartsAt = request.StartsAt,
                FirstName = oldAppointment.FirstName,
                LastName = oldAppointment.LastName,
                Email = oldAppointment.Email,
                Phone = oldAppointment.Phone,
            };

            var bookResult = await TryBookNewAsync(createRequest, clientUserId);
            if (!bookResult.IsSuccess)
            {
                await transaction.RollbackAsync();
                if (bookResult.IsDuplicateRecord())
                {
                    return Result<AppointmentResponseDto>.DuplicateRecordError(bookResult.Message);
                }

                if (bookResult.IsNotFound())
                {
                    return Result<AppointmentResponseDto>.NotFoundError(bookResult.Message);
                }

                if (bookResult.IsValidationError())
                {
                    return Result<AppointmentResponseDto>.ValidationError(bookResult.Message);
                }

                return Result<AppointmentResponseDto>.SystemError(bookResult.Message);
            }

            var (newAppointment, service, stylist) = bookResult.Data;

            _dbContext.AppointmentSlots.RemoveRange(oldAppointment.Slots);
            oldAppointment.Status = AppointmentStatus.Cancelled;
            oldAppointment.StatusChangedAt = DateTimeOffset.UtcNow;
            oldAppointment.StatusChangedBy = actorDisplayName;

            try
            {
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
            {
                await transaction.RollbackAsync();
                return Result<AppointmentResponseDto>.DuplicateRecordError(
                    "This slot was just booked by someone else. Please choose another time.");
            }

            var dto = newAppointment.ToDto(service, stylist);

            try
            {
                await _emailService.SendConfirmationAsync(newAppointment, service.ToDto(), stylist.Name);
            }
            catch
            {
                // Best-effort AFTER commit — never roll back for email (mirror CreateAsync).
            }

            return Result<AppointmentResponseDto>.Success(dto);
        });
    }

    private static bool HasAppointmentStarted(DateTimeOffset startsAt)
        => startsAt <= DateTimeOffset.UtcNow;

    /// <summary>
    /// Shared open-slot + unique-index insert used by guest create and client reschedule.
    /// Caller owns the ambient transaction (reschedule) or relies on the single SaveChanges
    /// implicit transaction (create). Does not send email.
    /// </summary>
    private async Task<Result<(Appointment Appointment, Service Service, Stylist Stylist)>> TryBookNewAsync(
        AppointmentCreateDto request,
        int? clientUserId)
    {
        var service = await _dbContext.Services.FindAsync(request.ServiceId);
        if (service is null || !service.IsActive)
        {
            return Result<(Appointment, Service, Stylist)>.NotFoundError("Service not found.");
        }

        var date = DateOnly.FromDateTime(request.StartsAt.DateTime);

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
            var candidateStylistIds = activeStylists.Select(stylist => stylist.Id).ToList();
            var alreadyBooked = await _dbContext.AppointmentSlots
                .AnyAsync(slot => candidateStylistIds.Contains(slot.StylistId)
                                  && slot.SlotStart == request.StartsAt);

            return alreadyBooked
                ? Result<(Appointment, Service, Stylist)>.DuplicateRecordError(
                    "This slot was just booked by someone else. Please choose another time.")
                : Result<(Appointment, Service, Stylist)>.NotFoundError("That time is not an available slot.");
        }

        var cellsNeeded = (int)Math.Ceiling(service.DurationMinutes / (double)GridMinutes);

        foreach (var (stylist, instant) in freeCandidates)
        {
            var appointment = BuildAppointment(request, stylist.Id, instant, cellsNeeded);
            if (clientUserId.HasValue)
            {
                appointment.ClientUserId = clientUserId.Value;
            }

            _dbContext.Appointments.Add(appointment);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
            {
                _dbContext.Entry(appointment).State = EntityState.Detached;
                foreach (var slot in appointment.Slots)
                {
                    _dbContext.Entry(slot).State = EntityState.Detached;
                }

                continue;
            }

            return Result<(Appointment, Service, Stylist)>.Success((appointment, service, stylist));
        }

        return Result<(Appointment, Service, Stylist)>.DuplicateRecordError(
            "This slot was just booked by someone else. Please choose another time.");
    }

    private static bool IsAllowedTransition(AppointmentStatus current, AppointmentStatus next)
        => AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);

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
