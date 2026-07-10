using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>
/// Incoming contract for POST /api/appointments. Guest booking only — no account
/// fields (D-15). StylistId is optional: null means "Any stylist" and the server
/// deterministically assigns a concrete free stylist on confirm (D-07).
/// </summary>
public class AppointmentCreateDto
{
    public int ServiceId { get; set; }

    public int? StylistId { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    [Required, StringLength(100)]
    public string FirstName { get; set; } = null!;

    [Required, StringLength(100)]
    public string LastName { get; set; } = null!;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = null!;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }
}
