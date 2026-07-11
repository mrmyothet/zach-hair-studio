using FluentValidation;

namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>
/// NewStatus must be a defined enum value that is not Confirmed — Confirmed is the
/// booking-time default, never an inbound target of a status update (D-10).
/// </summary>
public class AppointmentStatusUpdateDtoValidator : AbstractValidator<AppointmentStatusUpdateDto>
{
    public AppointmentStatusUpdateDtoValidator()
    {
        RuleFor(x => x.NewStatus)
            .IsInEnum()
            .Must(NotBeConfirmed)
            .WithMessage("NewStatus must be one of Completed, Cancelled, or NoShow.");
    }

    private static bool NotBeConfirmed(AppointmentStatus status) => status != AppointmentStatus.Confirmed;
}
