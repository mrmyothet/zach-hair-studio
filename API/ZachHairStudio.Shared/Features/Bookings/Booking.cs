using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Bookings;

public class Booking
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string FirstName { get; set; } = null!;

    [Required, StringLength(100)]
    public string LastName { get; set; } = null!;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = null!;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required, StringLength(200)]
    public string Service { get; set; } = null!;

    public DateTime PreferredDate { get; set; }

    [StringLength(1000)]
    public string? Message { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string CustomerName => $"{FirstName} {LastName}";
}
