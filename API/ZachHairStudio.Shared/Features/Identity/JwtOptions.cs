namespace ZachHairStudio.Shared.Features.Identity;

/// <summary>
/// Non-secret-adjacent JWT settings bound from the "Jwt" appsettings section. SigningKey
/// is sourced from user-secrets/env only (D-13-style, never a tracked file) — see
/// Program.cs. LifetimeHours defaults to 12, matching D-03's ~12h workday token.
/// </summary>
public class JwtOptions
{
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ZachHairStudio";

    public string Audience { get; set; } = "ZachHairStudioDashboard";

    public int LifetimeHours { get; set; } = 12;
}
