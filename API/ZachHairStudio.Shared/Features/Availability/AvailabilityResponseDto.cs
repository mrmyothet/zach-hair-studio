namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// Read-side shape for GET /api/availability/{stylistId} — a stylist's current
/// weekly working-hours segments plus their upcoming/active time-off blocks.
/// Reads straight from the same StylistWorkingHours/StylistTimeOff tables the
/// write path (WorkingHoursReplaceDto/TimeOffCreateDto) targets (D-08); this DTO
/// introduces no new store, only a response shape for the dashboard editor.
/// </summary>
public class AvailabilityResponseDto
{
    public List<WorkingHoursSegmentDto> WorkingHours { get; set; } = new();

    public List<TimeOffResponseDto> TimeOff { get; set; } = new();
}

/// <summary>
/// A persisted time-off block, including its Id so the client can target
/// DELETE /api/availability/{stylistId}/time-off/{timeOffId}.
/// </summary>
public class TimeOffResponseDto
{
    public int Id { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public string? Reason { get; set; }
}
