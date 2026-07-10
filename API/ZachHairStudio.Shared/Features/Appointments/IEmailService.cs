using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>
/// Best-effort confirmation-email sink. Implementations MUST NOT throw or roll back
/// the appointment on failure (D-11) — the booking has already committed by the time
/// this is called. The concrete implementation is ResendEmailService.
/// </summary>
public interface IEmailService
{
    Task SendConfirmationAsync(Appointment appointment, ServiceResponseDto service, string stylistName);
}
