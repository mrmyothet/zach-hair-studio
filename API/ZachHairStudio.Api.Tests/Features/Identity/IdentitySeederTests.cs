using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Tests.Features.Identity;

// Real SQL Server LocalDB (not InMemory) — Identity's relational semantics must run
// against real SQL Server (RESEARCH Pitfall 1).
public class IdentitySeederTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private readonly SqlServerWebApplicationFactory _factory;

    public IdentitySeederTests(SqlServerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static IConfiguration BuildOwnerConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Owner:Email"] = "owner@seeder-test.local",
                ["Owner:InitialPassword"] = "SeederTest!2026Pw",
            })
            .Build();

    [Fact]
    public async Task SeedAsync_OnFreshDatabase_CreatesBothRolesAndExactlyOneOwner()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var config = BuildOwnerConfig();

        await IdentitySeeder.SeedAsync(roleManager, userManager, config);

        Assert.True(await roleManager.RoleExistsAsync(StaffRoles.Owner));
        Assert.True(await roleManager.RoleExistsAsync(StaffRoles.Staff));
        Assert.True(await roleManager.RoleExistsAsync(StaffRoles.Client));

        var ownersInRole = await userManager.GetUsersInRoleAsync(StaffRoles.Owner);
        Assert.Single(ownersInRole);

        var clientsInRole = await userManager.GetUsersInRoleAsync(StaffRoles.Client);
        Assert.Empty(clientsInRole);

        var owner = ownersInRole[0];
        Assert.Equal("owner@seeder-test.local", owner.Email);
        Assert.False(string.IsNullOrWhiteSpace(owner.DisplayName));
    }

    [Fact]
    public async Task SeedAsync_RunTwice_IsIdempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var config = BuildOwnerConfig();

        await IdentitySeeder.SeedAsync(roleManager, userManager, config);
        await IdentitySeeder.SeedAsync(roleManager, userManager, config);

        var ownersInRole = await userManager.GetUsersInRoleAsync(StaffRoles.Owner);
        Assert.Single(ownersInRole);

        var allRoles = roleManager.Roles.ToList();
        Assert.Single(allRoles, r => r.Name == StaffRoles.Owner);
        Assert.Single(allRoles, r => r.Name == StaffRoles.Staff);
        Assert.Single(allRoles, r => r.Name == StaffRoles.Client);
    }

    [Fact]
    public async Task SeedAsync_ExistingOwnerMissingRole_RepairsMembership()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var config = BuildOwnerConfig();

        // Ensure roles exist, then create the Owner user WITHOUT a role — the failure
        // mode observed when AddToRoleAsync was skipped / failed on first seed.
        foreach (var role in new[] { StaffRoles.Owner, StaffRoles.Staff })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }

        const string email = "owner-repair@seeder-test.local";
        var orphan = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Orphan Owner",
            EmailConfirmed = true,
        };
        Assert.True((await userManager.CreateAsync(orphan, "SeederTest!2026Pw")).Succeeded);
        Assert.False(await userManager.IsInRoleAsync(orphan, StaffRoles.Owner));

        var repairConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Owner:Email"] = email,
                ["Owner:InitialPassword"] = "unused-because-user-exists",
            })
            .Build();

        await IdentitySeeder.SeedAsync(roleManager, userManager, repairConfig);

        var repaired = await userManager.FindByEmailAsync(email);
        Assert.NotNull(repaired);
        Assert.True(await userManager.IsInRoleAsync(repaired!, StaffRoles.Owner));

        // This class shares ONE InMemory database (IClassFixture), so the second Owner
        // created above would otherwise leak into the sibling tests that assert exactly
        // one Owner exists — an order-dependent failure. Remove it before finishing.
        Assert.True((await userManager.DeleteAsync(repaired!)).Succeeded);
    }
}
