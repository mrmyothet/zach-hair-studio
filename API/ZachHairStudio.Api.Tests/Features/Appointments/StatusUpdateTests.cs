using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Tests.Features.Appointments;

/// <summary>
/// Proves DASH-03/DASH-04 (and D-10/D-11/D-12): status transitions are constrained and
/// server-enforced, Cancel/No-show free the AppointmentSlot rows through the single
/// reusable slot-release path, no-show is independently queryable from cancelled, and
/// the status-audit line populates. Runs over real SQL Server LocalDB so the actual
/// AppointmentSlot unique index / relational filtering is exercised (RESEARCH Pitfall 1/4).
/// </summary>
public class StatusUpdateTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "StatusUpdateTests-signing-key-at-least-32-bytes-long-for-hmac!";
    private const string TestPassword = "StatusUpdateTest!2026Pw";

    private readonly SqlServerWebApplicationFactory _rawFactory;
    private readonly WebApplicationFactory<Program> _factory;

    // 2026-07-15 is a Wednesday covered by the seeded Tue-Sat working hours; resolved
    // through the configured salon zone rather than a hardcoded offset (Pitfall 5).
    private static readonly SalonTimeZone SalonTz = SalonTimeZone.FromOptions(new SalonOptions());

    private static DateTimeOffset Slot(int hour, int minute = 0)
        => SalonTz.ToSalonInstant(new DateTime(2026, 7, 15, hour, minute, 0))!.Value;

    public StatusUpdateTests(SqlServerWebApplicationFactory factory)
    {
        _rawFactory = factory;
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
                DisplayName = "Status Tester",
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

    private async Task<int> CreateAppointmentAsync(HttpClient client, DateTimeOffset startsAt, int stylistId)
    {
        var response = await client.PostAsJsonAsync("/api/appointments", BookingRequest(startsAt, stylistId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task PatchStatus_ConfirmedToCompleted_Returns200WithAuditFields()
    {
        var token = await SeedStaffAndLoginAsync();
        var anonClient = _factory.CreateClient();
        var staffClient = CreateAuthenticatedClient(token);
        var appointmentId = await CreateAppointmentAsync(anonClient, Slot(10), stylistId: 1);

        var response = await staffClient.PatchAsJsonAsync($"/api/schedule/{appointmentId}/status", new { NewStatus = "Completed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("Completed", root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("statusChangedAt").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("statusChangedBy").GetString()));
    }

    [Theory]
    [InlineData("Cancelled", 2)]
    [InlineData("NoShow", 3)]
    public async Task PatchStatus_ConfirmedToCancelledOrNoShow_Returns200AndRemovesSlots(string newStatus, int stylistId)
    {
        var token = await SeedStaffAndLoginAsync();
        var anonClient = _factory.CreateClient();
        var staffClient = CreateAuthenticatedClient(token);
        var appointmentId = await CreateAppointmentAsync(anonClient, Slot(10), stylistId);

        var response = await staffClient.PatchAsJsonAsync($"/api/schedule/{appointmentId}/status", new { NewStatus = newStatus });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(newStatus, json.RootElement.GetProperty("status").GetString());

        using var scope = _rawFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var remainingSlots = await db.AppointmentSlots.CountAsync(s => s.AppointmentId == appointmentId);
        Assert.Equal(0, remainingSlots);
    }

    [Fact]
    public async Task PatchStatus_AlreadyTerminal_Returns400AndLeavesStatusUnchanged()
    {
        var token = await SeedStaffAndLoginAsync();
        var anonClient = _factory.CreateClient();
        var staffClient = CreateAuthenticatedClient(token);
        var appointmentId = await CreateAppointmentAsync(anonClient, Slot(10), stylistId: 4);

        var completeResponse = await staffClient.PatchAsJsonAsync($"/api/schedule/{appointmentId}/status", new { NewStatus = "Completed" });
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var response = await staffClient.PatchAsJsonAsync($"/api/schedule/{appointmentId}/status", new { NewStatus = "Cancelled" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var detailResponse = await staffClient.GetAsync($"/api/schedule/{appointmentId}");
        using var json = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        Assert.Equal("Completed", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetRange_FilterByNoShow_ReturnsOnlyNoShow_NeverCancelled()
    {
        var token = await SeedStaffAndLoginAsync();
        var anonClient = _factory.CreateClient();
        var staffClient = CreateAuthenticatedClient(token);

        var noShowId = await CreateAppointmentAsync(anonClient, Slot(11), stylistId: 1);
        var cancelledId = await CreateAppointmentAsync(anonClient, Slot(11), stylistId: 2);

        var noShowPatch = await staffClient.PatchAsJsonAsync($"/api/schedule/{noShowId}/status", new { NewStatus = "NoShow" });
        Assert.Equal(HttpStatusCode.OK, noShowPatch.StatusCode);
        var cancelledPatch = await staffClient.PatchAsJsonAsync($"/api/schedule/{cancelledId}/status", new { NewStatus = "Cancelled" });
        Assert.Equal(HttpStatusCode.OK, cancelledPatch.StatusCode);

        var noShowResponse = await staffClient.GetAsync("/api/schedule?from=2026-07-15&to=2026-07-15&status=NoShow");
        using var noShowJson = JsonDocument.Parse(await noShowResponse.Content.ReadAsStringAsync());
        var noShowIds = noShowJson.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(noShowId, noShowIds);
        Assert.DoesNotContain(cancelledId, noShowIds);
        Assert.All(noShowJson.RootElement.EnumerateArray(), e => Assert.Equal("NoShow", e.GetProperty("status").GetString()));

        var cancelledResponse = await staffClient.GetAsync("/api/schedule?from=2026-07-15&to=2026-07-15&status=Cancelled");
        using var cancelledJson = JsonDocument.Parse(await cancelledResponse.Content.ReadAsStringAsync());
        var cancelledIds = cancelledJson.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(cancelledId, cancelledIds);
        Assert.DoesNotContain(noShowId, cancelledIds);
    }
}
