using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;

namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// Computes open appointment slots on a fixed 15-minute grid: working hours
/// minus time off minus already-booked cells (D-01, D-02, D-06). Data access
/// is server-evaluated (stylists, working hours, time off, booked cells); the
/// grid math itself runs in memory because it does not translate to SQL.
/// </summary>
public class SlotService
{
    private const int GridMinutes = 15;

    private readonly BookingDbContext _dbContext;
    private readonly SalonTimeZone _salonTimeZone;

    public SlotService(BookingDbContext dbContext, SalonOptions salonOptions)
    {
        _dbContext = dbContext;
        _salonTimeZone = SalonTimeZone.FromOptions(salonOptions);
    }

    public async Task<IReadOnlyList<OpenSlotDto>> GetOpenSlotsAsync(int serviceId, int? stylistId, DateOnly date)
    {
        var dayStartLocal = date.ToDateTime(TimeOnly.MinValue);
        var dayStartUtc = _salonTimeZone.ToSalonInstant(dayStartLocal);
        var dayEndUtc = _salonTimeZone.ToSalonInstant(dayStartLocal.AddDays(1));

        if (dayStartUtc is null || dayEndUtc is null)
        {
            // Midnight boundary itself fell in a DST gap for this configured zone —
            // no candidates can be safely generated for the day.
            return Array.Empty<OpenSlotDto>();
        }

        var service = await _dbContext.Services.FindAsync(serviceId);
        if (service is null)
        {
            return Array.Empty<OpenSlotDto>();
        }

        var cellsNeeded = (int)Math.Ceiling(service.DurationMinutes / (double)GridMinutes);

        var stylists = await _dbContext.Stylists
            .Where(stylist => stylist.IsActive && (stylistId == null || stylist.Id == stylistId))
            .ToListAsync();

        if (stylists.Count == 0)
        {
            return Array.Empty<OpenSlotDto>();
        }

        var stylistIds = stylists.Select(stylist => stylist.Id).ToList();

        var workingHours = await _dbContext.StylistWorkingHours
            .Where(hours => hours.DayOfWeek == date.DayOfWeek && stylistIds.Contains(hours.StylistId))
            .ToListAsync();

        var timeOff = await _dbContext.StylistTimeOff
            .Where(off => off.EndsAt > dayStartUtc.Value && off.StartsAt < dayEndUtc.Value
                       && stylistIds.Contains(off.StylistId))
            .ToListAsync();

        var bookedCells = await _dbContext.AppointmentSlots
            .Where(slot => slot.SlotStart >= dayStartUtc.Value && slot.SlotStart < dayEndUtc.Value
                        && stylistIds.Contains(slot.StylistId))
            .Select(slot => new { slot.StylistId, slot.SlotStart })
            .ToListAsync();

        var isUnionView = stylistId is null;
        var unionStarts = new SortedSet<DateTimeOffset>();
        var filteredSlots = new List<OpenSlotDto>();

        foreach (var stylist in stylists)
        {
            var stylistBookedStarts = bookedCells
                .Where(cell => cell.StylistId == stylist.Id)
                .Select(cell => cell.SlotStart)
                .ToHashSet();

            var stylistTimeOff = timeOff
                .Where(off => off.StylistId == stylist.Id)
                .ToList();

            foreach (var hours in workingHours.Where(hours => hours.StylistId == stylist.Id))
            {
                foreach (var candidateStart in GenerateCandidateStarts(
                    dayStartLocal, hours, stylistTimeOff, stylistBookedStarts, cellsNeeded))
                {
                    if (isUnionView)
                    {
                        unionStarts.Add(candidateStart);
                    }
                    else
                    {
                        filteredSlots.Add(new OpenSlotDto
                        {
                            StartsAt = candidateStart,
                            StylistId = stylist.Id,
                            StylistName = stylist.Name,
                        });
                    }
                }
            }
        }

        if (isUnionView)
        {
            return unionStarts
                .Select(start => new OpenSlotDto { StartsAt = start, StylistId = null, StylistName = null })
                .ToList();
        }

        return filteredSlots
            .OrderBy(slot => slot.StartsAt)
            .ToList();
    }

    private IEnumerable<DateTimeOffset> GenerateCandidateStarts(
        DateTime dayStartLocal,
        StylistWorkingHours hours,
        IReadOnlyList<StylistTimeOff> timeOff,
        HashSet<DateTimeOffset> bookedCellStarts,
        int cellsNeeded)
    {
        var windowStartLocal = dayStartLocal.Add(hours.StartTime.ToTimeSpan());
        var windowEndLocal = dayStartLocal.Add(hours.EndTime.ToTimeSpan());
        var spanNeeded = TimeSpan.FromMinutes(GridMinutes * cellsNeeded);

        for (var candidateStartLocal = windowStartLocal;
             candidateStartLocal.Add(spanNeeded) <= windowEndLocal;
             candidateStartLocal = candidateStartLocal.AddMinutes(GridMinutes))
        {
            var candidateInstant = _salonTimeZone.ToSalonInstant(candidateStartLocal);
            if (candidateInstant is null)
            {
                // Candidate start itself falls in a spring-forward gap — skip it.
                continue;
            }

            if (AllCellsFree(candidateInstant.Value, cellsNeeded, timeOff, bookedCellStarts))
            {
                yield return candidateInstant.Value;
            }
        }
    }

    private static bool AllCellsFree(
        DateTimeOffset candidateStart,
        int cellsNeeded,
        IReadOnlyList<StylistTimeOff> timeOff,
        HashSet<DateTimeOffset> bookedCellStarts)
    {
        for (var cellIndex = 0; cellIndex < cellsNeeded; cellIndex++)
        {
            var cellStart = candidateStart.AddMinutes(GridMinutes * cellIndex);
            var cellEnd = cellStart.AddMinutes(GridMinutes);

            if (bookedCellStarts.Contains(cellStart))
            {
                return false;
            }

            if (timeOff.Any(off => cellStart < off.EndsAt && cellEnd > off.StartsAt))
            {
                return false;
            }
        }

        return true;
    }
}
