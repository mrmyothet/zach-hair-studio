namespace ZachHairStudio.Shared.Features.Identity;

/// <summary>
/// Outgoing contract for a successful login. Token is the signed JWT the dashboard
/// attaches as a Bearer credential; ExpiresAt lets the client know when to re-login.
/// </summary>
public class LoginResponseDto
{
    public string Token { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public string DisplayName { get; set; } = null!;

    public string Role { get; set; } = null!;
}
