namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>
/// Non-secret Resend settings bound from the "Resend" appsettings section. The API
/// key is NOT here — it lives in user-secrets/env under RESEND_API_KEY (D-13) and is
/// applied to the HttpClient Authorization header in Program.cs, never in a tracked file.
/// </summary>
public class ResendOptions
{
    /// <summary>Verified sending-domain from-address (D-10), e.g. bookings@media.zachhairstudio.com.</summary>
    public string FromEmail { get; set; } = "bookings@media.zachhairstudio.com";
}
