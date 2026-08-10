namespace ZachHairStudio.Shared.Features.Identity;

/// <summary>
/// Incoming contract for POST /api/auth/register (ACCT-01, D-03). Clients self-register
/// with email+password; DisplayName is optional and defaults to the email local-part.
/// </summary>
public class RegisterRequestDto
{
    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string ConfirmPassword { get; set; } = null!;

    public string? DisplayName { get; set; }
}
