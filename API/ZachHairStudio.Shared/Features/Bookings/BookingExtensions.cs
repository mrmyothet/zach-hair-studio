namespace ZachHairStudio.Shared.Features.Bookings;

public static class BookingExtensions
{
    public static BookingResponseDto ToDto(this Booking booking)
        => new BookingResponseDto
        {
            Id = booking.Id,
            FirstName = booking.FirstName,
            LastName = booking.LastName,
            Email = booking.Email,
            Phone = booking.Phone,
            Service = booking.Service,
            PreferredDate = booking.PreferredDate,
            Message = booking.Message,
            Status = booking.Status,
            CreatedAt = booking.CreatedAt,
        };

    public static Booking ToEntity(this BookingCreateDto createDto)
        => new Booking
        {
            FirstName = createDto.FirstName,
            LastName = createDto.LastName,
            Email = createDto.Email,
            Phone = createDto.Phone,
            Service = createDto.Service,
            PreferredDate = createDto.PreferredDate,
            Message = createDto.Message,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
}
