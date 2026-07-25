namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// The full replacement set of weekly working-hours segments for one stylist
/// (D-05). Write-time semantics are delete-existing-then-insert against the SAME
/// StylistWorkingHours table SlotService reads (D-08) — an empty Segments list is
/// a valid "all days closed" result, not a no-op.
/// </summary>
public class WorkingHoursReplaceDto
{
    public List<WorkingHoursSegmentDto> Segments { get; set; } = new();
}

/// <summary>
/// One weekday time range. Multiple segments may share the same DayOfWeek — a gap
/// between two segments on the same day models a recurring break (D-06); there is
/// no separate Break entity.
/// </summary>
public class WorkingHoursSegmentDto
{
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}
