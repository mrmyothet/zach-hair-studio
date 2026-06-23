namespace ZachHairStudio.Shared.Features.Bookings;

public class BookingResponseDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string Service { get; set; } = null!;
    public DateTime PreferredDate { get; set; }
    public string? Message { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CustomerName => $"{FirstName} {LastName}";
}
