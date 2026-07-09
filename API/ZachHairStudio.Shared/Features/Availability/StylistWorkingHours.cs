namespace ZachHairStudio.Shared.Features.Availability;

public class StylistWorkingHours
{
    public int Id { get; set; }

    public int StylistId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}
