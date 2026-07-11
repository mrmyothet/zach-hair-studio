using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Tests.Features.Identity;

/// <summary>
/// Proves the DASH-05 auth gate end-to-end over real SQL Server LocalDB (Identity's
/// relational semantics must run against real SQL Server, not InMemory — RESEARCH
/// Pitfall 1). Requests are built as anonymous objects (not the Shared DTOs) and
/// responses are read as raw JSON so this file compiles standalone before AuthController/
/// StaffUsersController exist (RED phase); Tasks 2-3 turn these green.
///
/// Users are seeded directly via UserManager/RoleManager in each test — the startup
/// IdentitySeeder is skipped in the Testing environment.
/// </summary>
public class AuthGateTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "AuthGateTests-signing-key-at-least-32-bytes-long-for-hmac-sha256!";
    private const string TestPassword = "AuthGateTest!2026Pw";

    private readonly WebApplicationFactory<Program> _factory;

    public AuthGateTests(SqlServerWebApplicationFactory factory)
    {
        // The real Jwt:SigningKey only exists via dotnet user-secrets in dev — inject a
        // test-only value here so JwtBearer can mint (JwtTokenService) and validate
        // (Program.cs's AddJwtBearer) tokens inside this test host.
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = TestSigningKey,
                    ["Jwt:Issuer"] = "ZachHairStudioTests",
                    ["Jwt:Audience"] = "ZachHairStudioTestsDashboard",
                });
            });
        });
    }

    private async Task<(string Email, string Password)> SeedStaffUserAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";

        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(role));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = $"{role} Tester",
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(user, TestPassword);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, role);

        return (email, TestPassword);
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Login_ValidStaffCredentials_Returns200WithTokenExpiryDisplayNameAndRole()
    {
        var (email, password) = await SeedStaffUserAsync(StaffRoles.Staff);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("token").GetString()));
        Assert.True(root.TryGetProperty("expiresAt", out _));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("displayName").GetString()));
        Assert.Equal(StaffRoles.Staff, root.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_BothReturnIdentical401()
    {
        var (email, _) = await SeedStaffUserAsync(StaffRoles.Staff);
        var client = _factory.CreateClient();

        var unknownEmailResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { Email = $"nobody-{Guid.NewGuid():N}@example.com", Password = "WhateverPw!2026" });
        var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TotallyWrongPw!2026" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);

        // No user enumeration (T-03-06): the two 401 bodies must be byte-identical.
        var unknownBody = await unknownEmailResponse.Content.ReadAsStringAsync();
        var wrongPasswordBody = await wrongPasswordResponse.Content.ReadAsStringAsync();
        Assert.Equal(unknownBody, wrongPasswordBody);
    }

    [Fact]
    public async Task CreateStaffUser_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/staff-users", new
        {
            Email = $"nobody-{Guid.NewGuid():N}@example.com",
            DisplayName = "Nobody",
            Password = "SomePassword!2026",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaffUser_StaffRoleToken_Returns403()
    {
        var (email, password) = await SeedStaffUserAsync(StaffRoles.Staff);
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/staff-users", new
        {
            Email = $"nobody-{Guid.NewGuid():N}@example.com",
            DisplayName = "Nobody",
            Password = "SomePassword!2026",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaffUser_OwnerRoleToken_Returns2xxAndCreatedUserHasStaffRole()
    {
        var (ownerEmail, ownerPassword) = await SeedStaffUserAsync(StaffRoles.Owner);
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, ownerEmail, ownerPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newStaffEmail = $"newstaff-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/staff-users", new
        {
            Email = newStaffEmail,
            DisplayName = "New Staff",
            Password = "SomePassword!2026",
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"Expected 200/201, got {response.StatusCode}");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var createdUser = await userManager.FindByEmailAsync(newStaffEmail);
        Assert.NotNull(createdUser);
        Assert.True(await userManager.IsInRoleAsync(createdUser!, StaffRoles.Staff));
    }
}
