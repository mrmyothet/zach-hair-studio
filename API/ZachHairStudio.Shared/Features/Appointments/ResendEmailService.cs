using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>
/// Sends the confirmation email via a single Resend REST call (D-10, no SDK). Every
/// method is best-effort: the whole body is wrapped in try/catch that only logs and
/// NEVER rethrows, so a Resend outage can never roll back a committed booking (D-11).
/// All client-supplied values (name, email) are HtmlEncode-d before interpolation into
/// the HTML body to prevent email/HTML injection (T-02-08). The bearer token is applied
/// to the HttpClient in Program.cs from RESEND_API_KEY — never logged or handled here.
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(HttpClient httpClient, ResendOptions options, ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task SendConfirmationAsync(Appointment appointment, ServiceResponseDto service, string stylistName)
    {
        try
        {
            // HTML-encode every client-supplied field before HTML interpolation (T-02-08).
            var firstName = WebUtility.HtmlEncode(appointment.FirstName);
            var lastName = WebUtility.HtmlEncode(appointment.LastName);
            var email = WebUtility.HtmlEncode(appointment.Email);
            var serviceName = WebUtility.HtmlEncode(service.Name);
            var stylist = WebUtility.HtmlEncode(stylistName);

            // Server-generated, salon-local wall-clock rendered from the stored offset (D-16).
            var when = appointment.StartsAt.ToString("ddd d MMM yyyy, h:mm tt");

            var html =
                $"<p>Hi {firstName} {lastName},</p>" +
                $"<p>Your <strong>{serviceName}</strong> appointment with {stylist} is confirmed for " +
                $"<strong>{when}</strong>.</p>" +
                $"<p>A confirmation has been sent to {email}.</p>" +
                "<p>See you at Zach Hair Studio!</p>";

            var payload = new
            {
                from = _options.FromEmail,
                to = appointment.Email,
                subject = "Your appointment is confirmed",
                html,
            };

            using var response = await _httpClient.PostAsJsonAsync("emails", payload);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Resend confirmation email was rejected for appointment {AppointmentId}: {StatusCode}",
                    appointment.Id, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Never rethrow — D-11: a Resend failure must never cost a client their slot.
            _logger.LogError(ex, "Resend confirmation email threw for appointment {AppointmentId}", appointment.Id);
        }
    }
}
