using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Services;

/// <summary>
/// Proves MGMT-01's Owner-only gate on ServicesController's write actions while the
/// public GET actions stay anonymous (Pitfall 5 regression guard: a class-level
/// [Authorize] would incorrectly 401 the landing page's catalog reads). Mirrors
/// AuthGateTests' test-only JWT signing key + Owner/Staff seeding pattern over real
/// SQL Server (Identity's relational semantics require it, not InMemory).
/// </summary>
public class ServicesControllerAuthTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "ServicesControllerAuthTests-signing-key-32bytes-hmac!!";
    private const string TestPassword = "ServicesAuthTest!2026Pw";
    private const int SeededServiceId = 1;
    private const string SeededServiceSlug = "precision-cut";

    private readonly WebApplicationFactory<Program> _factory;

    public ServicesControllerAuthTests(SqlServerWebApplicationFactory factory)
    {
        // The real Jwt:SigningKey only exists via dotnet user-secrets in dev — inject a
        // test-only value here so JwtBearer can mint and validate tokens inside this
        // test host, exactly as AuthGateTests does.
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

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string role)
    {
        var (email, password) = await SeedStaffUserAsync(role);
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static ServiceCreateDto NewServiceDto()
        => new ServiceCreateDto
        {
            Slug = $"auth-test-{Guid.NewGuid():N}",
            Name = "Auth Test Service",
            ShortDescription = "A service created by an auth test.",
            LongDescription = "A service created by an auth test to prove the Owner-only gate holds.",
            Category = "Cuts",
            DurationMinutes = 45,
            Price = 35,
            DisplayOrder = 99,
        };

    private static ServiceUpdateDto UpdateDtoFor(ServiceResponseDto service, bool isActive = true)
        => new ServiceUpdateDto
        {
            Slug = service.Slug,
            Name = service.Name,
            ShortDescription = service.ShortDescription,
            LongDescription = service.LongDescription,
            Category = service.Category,
            DurationMinutes = service.DurationMinutes,
            Price = service.Price,
            ImageUrl = service.ImageUrl,
            IsActive = isActive,
            DisplayOrder = service.DisplayOrder,
        };

    /// <summary>
    /// Creates a service via an Owner client, then retires it (PUT IsActive=false),
    /// returning its slug so each includeInactive test asserts on its own uniquely-slugged
    /// row without cross-test interference on the shared SqlServerWebApplicationFactory fixture.
    /// </summary>
    private static async Task<string> CreateRetiredServiceAsync(HttpClient ownerClient)
    {
        var createResponse = await ownerClient.PostAsJsonAsync("/api/services", NewServiceDto());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceResponseDto>();

        var updateResponse = await ownerClient.PutAsJsonAsync(
            $"/api/services/{created!.Id}",
            UpdateDtoFor(created, isActive: false));
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        return created.Slug;
    }

    [Fact]
    public async Task GetServices_Anonymous_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/services");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetService_Anonymous_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/services/{SeededServiceSlug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateService_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/services", NewServiceDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateService_StaffRoleToken_Returns403()
    {
        var client = await CreateAuthenticatedClientAsync(StaffRoles.Staff);

        var response = await client.PostAsJsonAsync("/api/services", NewServiceDto());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateService_OwnerRoleToken_Returns201()
    {
        var client = await CreateAuthenticatedClientAsync(StaffRoles.Owner);

        var response = await client.PostAsJsonAsync("/api/services", NewServiceDto());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UpdateService_OwnerRoleToken_Returns204()
    {
        var client = await CreateAuthenticatedClientAsync(StaffRoles.Owner);
        var current = await (await client.GetAsync($"/api/services/{SeededServiceSlug}"))
            .Content.ReadFromJsonAsync<ServiceResponseDto>();

        var response = await client.PutAsJsonAsync($"/api/services/{SeededServiceId}", UpdateDtoFor(current!));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateService_StaffRoleToken_Returns403()
    {
        var anonymous = _factory.CreateClient();
        var current = await (await anonymous.GetAsync($"/api/services/{SeededServiceSlug}"))
            .Content.ReadFromJsonAsync<ServiceResponseDto>();

        var client = await CreateAuthenticatedClientAsync(StaffRoles.Staff);

        var response = await client.PutAsJsonAsync($"/api/services/{SeededServiceId}", UpdateDtoFor(current!));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // DD-1: the flag is honored only for an authenticated Owner and is otherwise
    // silently ignored (not 403'd) elsewhere — GetServices stays anonymous.
    [Fact]
    public async Task GetServices_OwnerWithIncludeInactive_ReturnsRetiredService()
    {
        var owner = await CreateAuthenticatedClientAsync(StaffRoles.Owner);
        var retiredSlug = await CreateRetiredServiceAsync(owner);

        var response = await owner.GetAsync("/api/services?includeInactive=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var services = await response.Content.ReadFromJsonAsync<List<ServiceResponseDto>>();
        Assert.Contains(services!, service => service.Slug == retiredSlug);
        var retiredService = services!.Single(service => service.Slug == retiredSlug);
        Assert.False(retiredService.IsActive);
    }

    [Fact]
    public async Task GetServices_AnonymousWithIncludeInactive_OmitsRetiredService()
    {
        var owner = await CreateAuthenticatedClientAsync(StaffRoles.Owner);
        var retiredSlug = await CreateRetiredServiceAsync(owner);

        var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync("/api/services?includeInactive=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var services = await response.Content.ReadFromJsonAsync<List<ServiceResponseDto>>();
        Assert.DoesNotContain(services!, service => service.Slug == retiredSlug);
    }

    [Fact]
    public async Task GetServices_StaffRoleWithIncludeInactive_OmitsRetiredService()
    {
        var owner = await CreateAuthenticatedClientAsync(StaffRoles.Owner);
        var retiredSlug = await CreateRetiredServiceAsync(owner);

        var staff = await CreateAuthenticatedClientAsync(StaffRoles.Staff);
        var response = await staff.GetAsync("/api/services?includeInactive=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var services = await response.Content.ReadFromJsonAsync<List<ServiceResponseDto>>();
        Assert.DoesNotContain(services!, service => service.Slug == retiredSlug);
    }

    [Fact]
    public async Task GetServices_OwnerWithoutIncludeInactive_OmitsRetiredService()
    {
        var owner = await CreateAuthenticatedClientAsync(StaffRoles.Owner);
        var retiredSlug = await CreateRetiredServiceAsync(owner);

        var response = await owner.GetAsync("/api/services");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var services = await response.Content.ReadFromJsonAsync<List<ServiceResponseDto>>();
        Assert.DoesNotContain(services!, service => service.Slug == retiredSlug);
    }
}
