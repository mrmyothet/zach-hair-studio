namespace ZachHairStudio.Shared.Features.Appointments;

public class AppointmentSlot
{
    public int Id { get; set; }

    public int AppointmentId { get; set; }

    public Appointment Appointment { get; set; } = null!;

    public int StylistId { get; set; }

    public DateTimeOffset SlotStart { get; set; }
}
