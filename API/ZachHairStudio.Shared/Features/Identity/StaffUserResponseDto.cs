namespace ZachHairStudio.Shared.Features.Identity;

/// <summary>
/// Outgoing contract for a created staff user. Never exposes password hashes or
/// any other Identity internals.
/// </summary>
public class StaffUserResponseDto
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Role { get; set; } = null!;
}
