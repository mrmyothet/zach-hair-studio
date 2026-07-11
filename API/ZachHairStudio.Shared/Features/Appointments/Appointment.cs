using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Appointments;

public class Appointment
{
    public int Id { get; set; }

    public int ServiceId { get; set; }

    public int StylistId { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;

    [Required, StringLength(100)]
    public string FirstName { get; set; } = null!;

    [Required, StringLength(100)]
    public string LastName { get; set; } = null!;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = null!;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Minimal status audit (D-12) — the acting staff member's DisplayName, not a full
    // AppointmentStatusHistory table.
    public DateTimeOffset? StatusChangedAt { get; set; }

    public string? StatusChangedBy { get; set; }

    public List<AppointmentSlot> Slots { get; set; } = new();
}
