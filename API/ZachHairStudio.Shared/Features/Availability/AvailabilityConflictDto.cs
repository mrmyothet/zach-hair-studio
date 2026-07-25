namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// One Confirmed appointment whose AppointmentSlot cell(s) would fall outside
/// the newly proposed working hours or inside newly proposed time off
/// (MGMT-03, D-09). Carries ONLY the fields the staff-facing conflict panel
/// needs — client name, service, stylist, and the appointment's salon-local
/// instant, plus AppointmentId for a future deep link (D-11). No email/phone or
/// any other PII (T-04-09).
/// </summary>
public class AvailabilityConflictDto
{
    public int AppointmentId { get; set; }

    public string ClientName { get; set; } = null!;

    public string ServiceName { get; set; } = null!;

    public string StylistName { get; set; } = null!;

    /// <summary>
    /// The appointment's UTC instant (Appointment.StartsAt) — the client
    /// formats this into salon-local wall-clock text the same way the
    /// existing schedule/detail panel does (formatSalonDateTime), never a
    /// server-pre-formatted string.
    /// </summary>
    public DateTimeOffset SalonLocalTime { get; set; }
}
