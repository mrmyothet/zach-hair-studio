namespace ZachHairStudio.Shared.Features.Identity;

/// <summary>
/// Incoming contract for POST /api/auth/login (D-03). Staff exchange email+password
/// for a JWT; there is no self-registration path (D-04).
/// </summary>
public class LoginRequestDto
{
    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;
}
