using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Api.Controllers;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Services;

public class ServicesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ServicesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // CreateService is Owner-gated as of Phase 4 (T-04-01) — this validator-shape
    // assertion needs an authenticated Owner caller to reach past the [Authorize]
    // filter into model validation. Uses the host's real (dev user-secrets) signing
    // key like other CustomWebApplicationFactory tests that don't need a test override.
    private async Task<HttpClient> CreateOwnerClientAsync()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        const string password = "ServicesControllerTest!2026Pw";

        using (var scope = _factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            if (!await roleManager.RoleExistsAsync(StaffRoles.Owner))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(StaffRoles.Owner));
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Owner Tester",
                EmailConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(user, password);
            Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, StaffRoles.Owner);
        }

        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var json = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GetServices_ReturnsOkWithSeededServicesOrderedByDisplayOrder()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/services");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var services = await response.Content.ReadFromJsonAsync<List<ServiceResponseDto>>();
        Assert.NotNull(services);
        Assert.Equal(
            [
                "precision-cut",
                "color-and-highlights",
                "blowout-and-styling",
                "keratin-treatment",
                "scalp-treatment",
                "full-glam-package"
            ],
            services.Select(service => service.Slug));
    }

    [Fact]
    public async Task GetService_WithUnknownSlug_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/services/unknown-service");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateService_WithEmptyName_ReturnsBadRequestWithErrorsBody()
    {
        var client = await CreateOwnerClientAsync();
        var request = CreateDto(name: string.Empty);

        var response = await client.PostAsJsonAsync("/api/services", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("errors", out var errors));
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
    }

    [Fact]
    public void ServicesController_DoesNotDependOnBookingDbContext()
    {
        var ctorParams = typeof(ServicesController)
            .GetConstructors()
            .SelectMany(ctor => ctor.GetParameters());

        Assert.DoesNotContain(ctorParams, parameter => parameter.ParameterType == typeof(BookingDbContext));
    }

    private static ServiceCreateDto CreateDto(string name = "Precision Cut")
        => new ServiceCreateDto
        {
            Slug = "precision-cut",
            Name = name,
            ShortDescription = "A tailored cut.",
            LongDescription = "A tailored cut designed around your style and routine.",
            Category = "Cuts",
            DurationMinutes = 45,
            Price = 35,
            DisplayOrder = 1,
        };
}
