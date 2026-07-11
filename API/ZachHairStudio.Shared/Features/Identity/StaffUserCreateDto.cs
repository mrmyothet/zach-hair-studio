namespace ZachHairStudio.Shared.Features.Identity;

/// <summary>
/// Incoming contract for POST /api/staff-users (D-04, Owner-only). Every account
/// created through this endpoint is assigned the Staff role — the Owner account
/// itself is seeded at startup, never created through this endpoint.
/// </summary>
public class StaffUserCreateDto
{
    public string Email { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Password { get; set; } = null!;
}
