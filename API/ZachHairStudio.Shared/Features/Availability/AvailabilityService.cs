using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;

namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// Availability write path (MGMT-02). Writes ONLY to StylistWorkingHours /
/// StylistTimeOff — the exact tables SlotService.GetOpenSlotsAsync reads — so
/// staff edits are reflected immediately with no second/parallel availability
/// store (D-08). Any authenticated staff may target any stylist (D-13); there is
/// no per-stylist ownership check here or in the controller. No conflict check
/// against Confirmed appointments yet — that arrives in Plan 05 / MGMT-03.
/// </summary>
public class AvailabilityService
{
    private readonly BookingDbContext _dbContext;
    private readonly IValidator<WorkingHoursReplaceDto> _workingHoursValidator;
    private readonly IValidator<TimeOffCreateDto> _timeOffValidator;

    public AvailabilityService(
        BookingDbContext dbContext,
        IValidator<WorkingHoursReplaceDto> workingHoursValidator,
        IValidator<TimeOffCreateDto> timeOffValidator)
    {
        _dbContext = dbContext;
        _workingHoursValidator = workingHoursValidator;
        _timeOffValidator = timeOffValidator;
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

        var existing = await _dbContext.StylistWorkingHours
            .Where(hours => hours.StylistId == stylistId)
            .ToListAsync();
        _dbContext.StylistWorkingHours.RemoveRange(existing);

        // Stable order (DayOfWeek then StartTime) so equal-compare rows never
        // silently reorder between saves.
        var replacement = request.Segments
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

        _dbContext.StylistWorkingHours.AddRange(replacement);
        await _dbContext.SaveChangesAsync();

        return Result<IReadOnlyList<StylistWorkingHours>>.Success(replacement);
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

        var timeOff = new StylistTimeOff
        {
            StylistId = stylistId,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Reason = request.Reason,
        };

        _dbContext.StylistTimeOff.Add(timeOff);
        await _dbContext.SaveChangesAsync();

        return Result<StylistTimeOff>.Success(timeOff);
    }

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
}
