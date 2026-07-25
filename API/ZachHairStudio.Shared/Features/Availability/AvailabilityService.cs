using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;

namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// Availability write path (MGMT-02) plus the hard-blocking conflict check
/// (MGMT-03, D-09). Writes ONLY to StylistWorkingHours / StylistTimeOff — the
/// exact tables SlotService.GetOpenSlotsAsync reads — so staff edits are
/// reflected immediately with no second/parallel availability store (D-08). Any
/// authenticated staff may target any stylist (D-13); there is no per-stylist
/// ownership check here or in the controller.
///
/// Before EITHER write persists, a conflict scan evaluates the FULL proposed
/// final state — not an old-vs-new diff (RESEARCH Pitfall 1) — against every
/// Confirmed appointment for the stylist: a working-hours write checks the
/// submitted hours against the CURRENTLY persisted time off (unaffected by this
/// write); a time-off write checks the CURRENTLY persisted hours (unaffected)
/// against the existing time-off set plus the new range being added. The scan
/// and the persist share one execution-strategy-wrapped transaction, so a
/// non-empty conflict set rolls back cleanly and nothing partially applies
/// (D-09). Removing time off is never conflict-checked — it can only ever
/// widen availability.
/// </summary>
public class AvailabilityService
{
    private const int GridMinutes = 15;

    private readonly BookingDbContext _dbContext;
    private readonly IValidator<WorkingHoursReplaceDto> _workingHoursValidator;
    private readonly IValidator<TimeOffCreateDto> _timeOffValidator;
    private readonly SalonTimeZone _salonTimeZone;

    public AvailabilityService(
        BookingDbContext dbContext,
        IValidator<WorkingHoursReplaceDto> workingHoursValidator,
        IValidator<TimeOffCreateDto> timeOffValidator,
        SalonOptions salonOptions)
    {
        _dbContext = dbContext;
        _workingHoursValidator = workingHoursValidator;
        _timeOffValidator = timeOffValidator;
        _salonTimeZone = SalonTimeZone.FromOptions(salonOptions);
    }

    /// <summary>
    /// Read-side for the dashboard editor: a stylist's current working-hours
    /// segments plus their time-off blocks, straight from the same tables the
    /// write path targets (D-08) — no derived/cached second store.
    /// </summary>
    public async Task<Result<AvailabilityResponseDto>> GetAvailabilityAsync(int stylistId)
    {
        var stylist = await _dbContext.Stylists.FindAsync(stylistId);
        if (stylist is null)
        {
            return Result<AvailabilityResponseDto>.NotFoundError($"Stylist '{stylistId}' not found.");
        }

        var workingHours = await _dbContext.StylistWorkingHours
            .Where(hours => hours.StylistId == stylistId)
            .OrderBy(hours => hours.DayOfWeek)
            .ThenBy(hours => hours.StartTime)
            .Select(hours => new WorkingHoursSegmentDto
            {
                DayOfWeek = hours.DayOfWeek,
                StartTime = hours.StartTime,
                EndTime = hours.EndTime,
            })
            .ToListAsync();

        var timeOff = await _dbContext.StylistTimeOff
            .Where(off => off.StylistId == stylistId)
            .OrderBy(off => off.StartsAt)
            .Select(off => new TimeOffResponseDto
            {
                Id = off.Id,
                StartsAt = off.StartsAt,
                EndsAt = off.EndsAt,
                Reason = off.Reason,
            })
            .ToListAsync();

        return Result<AvailabilityResponseDto>.Success(new AvailabilityResponseDto
        {
            WorkingHours = workingHours,
            TimeOff = timeOff,
        });
    }

    /// <summary>
    /// Whole-week replace: delete every existing StylistWorkingHours row for this
    /// stylist, then insert the submitted segments. A single SaveChangesAsync
    /// commits both halves atomically — no manual BeginTransaction, mirroring
    /// AppointmentsService's EnableRetryOnFailure-safe implicit-transaction
    /// pattern. An empty Segments list is a valid "all days closed" result.
    /// </summary>
    public async Task<Result<IReadOnlyList<StylistWorkingHours>>> ReplaceWorkingHoursAsync(
        int stylistId, WorkingHoursReplaceDto request)
    {
        var stylist = await _dbContext.Stylists.FindAsync(stylistId);
        if (stylist is null)
        {
            return Result<IReadOnlyList<StylistWorkingHours>>.NotFoundError($"Stylist '{stylistId}' not found.");
        }

        var validation = await _workingHoursValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return Result<IReadOnlyList<StylistWorkingHours>>.ValidationError(
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        // Stable order (DayOfWeek then StartTime) so equal-compare rows never
        // silently reorder between saves.
        var proposedHours = request.Segments
            .OrderBy(segment => segment.DayOfWeek)
            .ThenBy(segment => segment.StartTime)
            .Select(segment => new StylistWorkingHours
            {
                StylistId = stylistId,
                DayOfWeek = segment.DayOfWeek,
                StartTime = segment.StartTime,
                EndTime = segment.EndTime,
            })
            .ToList();

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // Time off is unaffected by this write — the currently persisted set
            // IS the proposed final set for it (full-proposed-final-state).
            var currentTimeOff = await _dbContext.StylistTimeOff
                .Where(off => off.StylistId == stylistId)
                .Select(off => new TimeOffRange(off.StartsAt, off.EndsAt))
                .ToListAsync();

            var conflicts = await FindConflictsAsync(stylistId, proposedHours, currentTimeOff);
            if (conflicts.Count > 0)
            {
                await transaction.RollbackAsync();
                return Result<IReadOnlyList<StylistWorkingHours>>.ConflictError(ConflictMessage, conflicts);
            }

            var existing = await _dbContext.StylistWorkingHours
                .Where(hours => hours.StylistId == stylistId)
                .ToListAsync();
            _dbContext.StylistWorkingHours.RemoveRange(existing);
            _dbContext.StylistWorkingHours.AddRange(proposedHours);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result<IReadOnlyList<StylistWorkingHours>>.Success(proposedHours);
        });
    }

    public async Task<Result<StylistTimeOff>> AddTimeOffAsync(int stylistId, TimeOffCreateDto request)
    {
        var stylist = await _dbContext.Stylists.FindAsync(stylistId);
        if (stylist is null)
        {
            return Result<StylistTimeOff>.NotFoundError($"Stylist '{stylistId}' not found.");
        }

        var validation = await _timeOffValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return Result<StylistTimeOff>.ValidationError(
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // Hours are unaffected by this write — the currently persisted set IS
            // the proposed final set for it.
            var currentHours = await _dbContext.StylistWorkingHours
                .Where(hours => hours.StylistId == stylistId)
                .ToListAsync();

            var currentTimeOff = await _dbContext.StylistTimeOff
                .Where(off => off.StylistId == stylistId)
                .Select(off => new TimeOffRange(off.StartsAt, off.EndsAt))
                .ToListAsync();

            // Proposed final time-off set = everything already persisted PLUS the
            // new range being added right now (full-proposed-final-state).
            var proposedTimeOff = currentTimeOff
                .Append(new TimeOffRange(request.StartsAt, request.EndsAt))
                .ToList();

            var conflicts = await FindConflictsAsync(stylistId, currentHours, proposedTimeOff);
            if (conflicts.Count > 0)
            {
                await transaction.RollbackAsync();
                return Result<StylistTimeOff>.ConflictError(ConflictMessage, conflicts);
            }

            var timeOff = new StylistTimeOff
            {
                StylistId = stylistId,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Reason = request.Reason,
            };

            _dbContext.StylistTimeOff.Add(timeOff);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result<StylistTimeOff>.Success(timeOff);
        });
    }

    /// <summary>
    /// No conflict scan here (intentional): removing time off can only ever
    /// WIDEN a stylist's availability, never shrink it, so it can never orphan
    /// a Confirmed appointment.
    /// </summary>
    public async Task<Result<bool>> RemoveTimeOffAsync(int stylistId, int timeOffId)
    {
        var timeOff = await _dbContext.StylistTimeOff.FindAsync(timeOffId);
        if (timeOff is null || timeOff.StylistId != stylistId)
        {
            return Result<bool>.NotFoundError($"Time off '{timeOffId}' not found for stylist '{stylistId}'.");
        }

        _dbContext.StylistTimeOff.Remove(timeOff);
        await _dbContext.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    private const string ConflictMessage =
        "These confirmed appointments fall outside the new hours or inside the new time off. " +
        "Cancel or reschedule them first, then try again.";

    /// <summary>
    /// Every Confirmed appointment for this stylist whose AppointmentSlot
    /// cell(s) fall outside <paramref name="proposedHours"/> or inside
    /// <paramref name="proposedTimeOff"/> — evaluated against the FULL proposed
    /// final state, never an old-vs-new diff (RESEARCH Pitfall 1). Only
    /// Appointment.Status == Confirmed is ever considered: AppointmentSlot
    /// itself carries no status column, and a Completed appointment still
    /// retains its slot rows (RESEARCH Pitfall 3), so the join against
    /// Appointment.Status must be explicit here, not inferred from slot
    /// presence.
    /// </summary>
    private async Task<IReadOnlyList<AvailabilityConflictDto>> FindConflictsAsync(
        int stylistId,
        IReadOnlyList<StylistWorkingHours> proposedHours,
        IReadOnlyList<TimeOffRange> proposedTimeOff)
    {
        var confirmedAppointments = await _dbContext.Appointments
            .Include(appointment => appointment.Slots)
            .Where(appointment => appointment.StylistId == stylistId
                                && appointment.Status == AppointmentStatus.Confirmed)
            .OrderBy(appointment => appointment.StartsAt)
            .ToListAsync();

        if (confirmedAppointments.Count == 0)
        {
            return Array.Empty<AvailabilityConflictDto>();
        }

        var conflictingAppointments = confirmedAppointments
            .Where(appointment => appointment.Slots.Any(
                slot => SlotConflicts(slot.SlotStart, proposedHours, proposedTimeOff)))
            .ToList();

        if (conflictingAppointments.Count == 0)
        {
            return Array.Empty<AvailabilityConflictDto>();
        }

        var serviceIds = conflictingAppointments.Select(appointment => appointment.ServiceId).Distinct().ToList();
        var services = await _dbContext.Services
            .Where(service => serviceIds.Contains(service.Id))
            .ToDictionaryAsync(service => service.Id);
        var stylist = await _dbContext.Stylists.FindAsync(stylistId);

        // Stable order (by slot start time, RESEARCH Ordering backstop) so
        // equal-compare rows never silently reorder between requests.
        return conflictingAppointments
            .OrderBy(appointment => appointment.StartsAt)
            .Select(appointment => new AvailabilityConflictDto
            {
                AppointmentId = appointment.Id,
                ClientName = $"{appointment.FirstName} {appointment.LastName}",
                ServiceName = services.TryGetValue(appointment.ServiceId, out var service)
                    ? service.Name
                    : "Unknown service",
                StylistName = stylist?.Name ?? "Unknown stylist",
                SalonLocalTime = appointment.StartsAt,
            })
            .ToList();
    }

    /// <summary>
    /// One AppointmentSlot cell conflicts if it falls inside any proposed
    /// time-off range OR outside every proposed working-hours segment for its
    /// salon-local weekday.
    /// </summary>
    private bool SlotConflicts(
        DateTimeOffset slotStart,
        IReadOnlyList<StylistWorkingHours> proposedHours,
        IReadOnlyList<TimeOffRange> proposedTimeOff)
    {
        var slotEnd = slotStart.AddMinutes(GridMinutes);

        // Time off is compared on the instant directly — mirrors
        // SlotService.AllCellsFree's identical cellStart < off.EndsAt &&
        // cellEnd > off.StartsAt overlap test (Pitfall 2: reuse the
        // cell-matching math, never re-derive it).
        if (proposedTimeOff.Any(off => slotStart < off.EndsAt && slotEnd > off.StartsAt))
        {
            return true;
        }

        // Working hours are DayOfWeek/TimeOnly salon-local wall-clock values —
        // the slot instant MUST be converted via ToSalonLocal before comparison
        // (Pitfall 2), never read via a raw .DayOfWeek/.TimeOfDay on the
        // DateTimeOffset itself.
        var localStart = _salonTimeZone.ToSalonLocal(slotStart);
        var localWeekday = localStart.DayOfWeek;
        var localStartMinutes = (localStart.Hour * 60) + localStart.Minute;
        var localEndMinutes = localStartMinutes + GridMinutes;

        if (localEndMinutes > 24 * 60)
        {
            // Crosses the local midnight boundary — no same-day working-hours
            // segment can ever cover it.
            return true;
        }

        var coveredByHours = proposedHours.Any(hours =>
            hours.DayOfWeek == localWeekday
            && localStartMinutes >= (hours.StartTime.Hour * 60) + hours.StartTime.Minute
            && localEndMinutes <= (hours.EndTime.Hour * 60) + hours.EndTime.Minute);

        return !coveredByHours;
    }

    private readonly record struct TimeOffRange(DateTimeOffset StartsAt, DateTimeOffset EndsAt);
}
