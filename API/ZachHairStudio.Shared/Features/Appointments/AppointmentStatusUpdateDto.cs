namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>Incoming contract for PATCH /api/schedule/{id}/status (DASH-03).</summary>
public class AppointmentStatusUpdateDto
{
    public AppointmentStatus NewStatus { get; set; }
}
