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

        var ownersInRole = await userManager.GetUsersInRoleAsync(StaffRoles.Owner);
        Assert.Single(ownersInRole);

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
    }
}
