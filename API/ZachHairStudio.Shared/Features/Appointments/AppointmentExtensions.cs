using ZachHairStudio.Shared.Features.Services;
using ZachHairStudio.Shared.Features.Stylists;

namespace ZachHairStudio.Shared.Features.Appointments;

public static class AppointmentExtensions
{
    public static AppointmentResponseDto ToDto(this Appointment appointment, Service service, Stylist stylist)
        => new AppointmentResponseDto
        {
            Id = appointment.Id,
            ServiceId = appointment.ServiceId,
            ServiceName = service.Name,
            StylistId = stylist.Id,
            StylistName = stylist.Name,
            StartsAt = appointment.StartsAt,
            DurationMinutes = service.DurationMinutes,
            Price = service.Price,
            Status = appointment.Status.ToString(),
            FirstName = appointment.FirstName,
            LastName = appointment.LastName,
            Email = appointment.Email,
            Phone = appointment.Phone,
            StatusChangedAt = appointment.StatusChangedAt,
            StatusChangedBy = appointment.StatusChangedBy,
        };
}
