namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>
/// Outgoing contract for a confirmed appointment. Carries every detail the
/// on-screen confirmation needs, because the email is best-effort (D-11).
/// StylistName is always the concrete assigned stylist — never "Any".
/// </summary>
public class AppointmentResponseDto
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = null!;
    public int StylistId { get; set; }
    public string StylistName { get; set; } = null!;
    public DateTimeOffset StartsAt { get; set; }
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
}
