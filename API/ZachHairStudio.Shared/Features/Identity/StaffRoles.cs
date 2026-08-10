namespace ZachHairStudio.Shared.Features.Identity;

/// <summary>
/// Role-name constants for the shared Identity store (Phase 3 staff + Phase 7 Client).
/// Clients self-register into Client; Owner/Staff remain seed/Owner-created only.
/// </summary>
public static class StaffRoles
{
    public const string Owner = "Owner";
    public const string Staff = "Staff";
    public const string Client = "Client";
}
