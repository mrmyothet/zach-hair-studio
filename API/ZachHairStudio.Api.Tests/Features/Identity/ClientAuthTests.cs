using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Tests.Features.Identity;

/// <summary>
/// ACCT-01 / D-01 / D-03 — client register + login over real SQL Server LocalDB
/// (Identity relational semantics; RESEARCH Pitfall 1 — no InMemory). Anonymous
/// request bodies so RED compiles against the Login contract before Register DTOs
/// land in Task 2. IdentitySeeder is skipped in Testing — ensure Client role exists
/// before Register AddToRoleAsync.
/// </summary>
public class ClientAuthTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "ClientAuthTests-signing-key-at-least-32-bytes-long-hmac!";
    private const string TestPassword = "ClientAuthTest!2026Pw";

    private readonly WebApplicationFactory<Program> _factory;

    public ClientAuthTests(SqlServerWebApplicationFactory factory)
    {
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

    private async Task EnsureClientRoleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        if (!await roleManager.RoleExistsAsync(StaffRoles.Client))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(StaffRoles.Client));
        }
    }

    [Fact]
    public async Task Register_ValidCredentials_Returns200WithClientRoleJwt()
    {
        await EnsureClientRoleAsync();
        var client = _factory.CreateClient();
        var email = $"client-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = TestPassword,
            ConfirmPassword = TestPassword,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("token").GetString()));
        Assert.True(root.TryGetProperty("expiresAt", out _));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("displayName").GetString()));
        Assert.Equal(StaffRoles.Client, root.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_AfterRegister_Returns200WithClientRole()
    {
        await EnsureClientRoleAsync();
        var client = _factory.CreateClient();
        var email = $"client-login-{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = TestPassword,
            ConfirmPassword = TestPassword,
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = TestPassword,
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var json = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        Assert.Equal(StaffRoles.Client, json.RootElement.GetProperty("role").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Register_MismatchedConfirmPassword_Returns400ValidationProblem()
    {
        await EnsureClientRoleAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"mismatch-{Guid.NewGuid():N}@example.com",
            Password = TestPassword,
            ConfirmPassword = "DifferentPassword!2026",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400ValidationProblem()
    {
        await EnsureClientRoleAsync();
        var client = _factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = TestPassword,
            ConfirmPassword = TestPassword,
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = TestPassword,
            ConfirmPassword = TestPassword,
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }
}
