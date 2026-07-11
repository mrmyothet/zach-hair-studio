using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace ZachHairStudio.Shared.Features.Identity;

/// <summary>
/// Ensures the Owner/Staff roles exist and exactly one seeded Owner account exists (D-04).
/// Owner credentials come from configuration (user-secrets/env, "Owner:Email" /
/// "Owner:InitialPassword") — never a tracked file. No self-registration path. Idempotent:
/// running SeedAsync repeatedly creates nothing new once the roles and Owner already exist.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(
        RoleManager<IdentityRole<int>> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration config)
    {
        foreach (var role in new[] { StaffRoles.Owner, StaffRoles.Staff })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }

        var ownerEmail = config["Owner:Email"];
        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            return;
        }

        var existingOwner = await userManager.FindByEmailAsync(ownerEmail);
        if (existingOwner is not null)
        {
            return;
        }

        var ownerPassword = config["Owner:InitialPassword"];
        if (string.IsNullOrWhiteSpace(ownerPassword))
        {
            return;
        }

        var owner = new ApplicationUser
        {
            UserName = ownerEmail,
            Email = ownerEmail,
            DisplayName = "Owner",
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(owner, ownerPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(owner, StaffRoles.Owner);
        }
    }
}
