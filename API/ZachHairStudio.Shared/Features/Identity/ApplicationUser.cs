using Microsoft.AspNetCore.Identity;

namespace ZachHairStudio.Shared.Features.Identity;

/// <summary>
/// The staff Identity user (D-01/D-02). Int-keyed to stay consistent with every other
/// entity's int Id (Service, Stylist, Appointment). DisplayName is the friendly name shown
/// in status-audit lines (e.g. "Aria Chen") — distinct from UserName/Email, which are the
/// login credentials.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public string DisplayName { get; set; } = null!;
}
