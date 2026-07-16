using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Api.Tests.TestSupport;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Tests.Features.Appointments;

/// <summary>
/// Proves DASH-01/DASH-02/DASH-05 for the staff schedule read endpoints, over real SQL
/// Server LocalDB (relational date-range filtering — RESEARCH Pitfall 1/4). Appointments
/// are created through the existing POST /api/appointments path so AppointmentSlot rows
/// exist exactly as they would in production; a staff bearer token is minted the same way
/// AuthGateTests does (test-only Jwt:SigningKey injected via WithWebHostBuilder).
/// </summary>
public class ScheduleControllerTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "ScheduleControllerTests-signing-key-at-least-32-bytes-long!";
    private const string TestPassword = "ScheduleTest!2026Pw";

    private readonly WebApplicationFactory<Program> _factory;

    // Base working day resolved relative to UtcNow (always future/in-horizon/seeded), via
    // BookingDates. dayOffset places appointments on distinct seeded working days within
    // the same week (0 = base day, 1 = base day + 1, both inside the seeded Tue-Sat window).
    private static readonly DateOnly BaseDate = BookingDates.NextBookableDate();

    private static DateTimeOffset Slot(int dayOffset, int hour, int minute = 0)
        => BookingDates.SlotOn(BookingDates.NextBookableDate(dayOffset), hour, minute);

    public ScheduleControllerTests(SqlServerWebApplicationFactory factory)
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

    private async Task<string> SeedStaffAndLoginAsync()
    {
        var email = $"staff-{Guid.NewGuid():N}@example.com";

        using (var scope = _factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            if (!await roleManager.RoleExistsAsync(StaffRoles.Staff))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(StaffRoles.Staff));
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Schedule Tester",
                EmailConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(user, TestPassword);
            Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, StaffRoles.Staff);
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object BookingRequest(DateTimeOffset startsAt, int stylistId) => new
    {
        ServiceId = 1,
        StylistId = stylistId,
        StartsAt = startsAt,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        Phone = (string?)null,
    };

    private async Task<JsonElement> CreateAppointmentAsync(HttpClient client, DateTimeOffset startsAt, int stylistId)
    {
        var response = await client.PostAsJsonAsync("/api/appointments", BookingRequest(startsAt, stylistId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/schedule?from={BaseDate:yyyy-MM-dd}&to={BaseDate:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Patch_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync("/api/schedule/1/status", new { NewStatus = "Completed" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRange_ReturnsAppointmentsWithinWindow_ExcludesOutsideWindow()
    {
        var token = await SeedStaffAndLoginAsync();
        var anonClient = _factory.CreateClient();
        var staffClient = CreateAuthenticatedClient(token);

        var inWindow = await CreateAppointmentAsync(anonClient, Slot(0, 10), stylistId: 1);
        var outsideWindow = await CreateAppointmentAsync(anonClient, Slot(1, 10), stylistId: 2);

        var response = await staffClient.GetAsync($"/api/schedule?from={BaseDate:yyyy-MM-dd}&to={BaseDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = json.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();

        Assert.Contains(inWindow.GetProperty("id").GetInt32(), ids);
        Assert.DoesNotContain(outsideWindow.GetProperty("id").GetInt32(), ids);
    }

    [Fact]
    public async Task GetById_ReturnsFullDetailWithNullAuditFieldsBeforeAnyStatusChange()
    {
        var token = await SeedStaffAndLoginAsync();
        var anonClient = _factory.CreateClient();
        var staffClient = CreateAuthenticatedClient(token);
        var appointment = await CreateAppointmentAsync(anonClient, Slot(0, 14), stylistId: 3);

        var response = await staffClient.GetAsync($"/api/schedule/{appointment.GetProperty("id").GetInt32()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("Confirmed", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("statusChangedAt").ValueKind is JsonValueKind.Null);
        Assert.True(root.GetProperty("statusChangedBy").ValueKind is JsonValueKind.Null);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var token = await SeedStaffAndLoginAsync();
        var staffClient = CreateAuthenticatedClient(token);

        var response = await staffClient.GetAsync("/api/schedule/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
