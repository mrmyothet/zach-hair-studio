using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Api.Tests.TestSupport;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Availability;

/// <summary>
/// Proves MGMT-02's working-hours replace path over real SQL Server LocalDB: any
/// authenticated staff (not just Owner — D-13) can PUT a stylist's whole week, and
/// GET /api/appointments/slots — the SAME open-slot read path the public booking
/// flow uses via SlotService — reflects the change immediately (D-08 same-model
/// proof, not merely a 200/204 status code). RED until AvailabilityService /
/// AvailabilityController (Tasks 2-3) exist; request bodies use anonymous objects
/// (not the not-yet-existing WorkingHoursReplaceDto) so this file compiles
/// standalone, mirroring AuthGateTests' RED-phase precedent.
/// </summary>
public class WorkingHoursReplaceTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "WorkingHoursReplaceTests-signing-key-at-least-32-bytes-long!";
    private const string TestPassword = "AvailabilityTest!2026Pw";
    private const int TargetStylistId = 1;

    private readonly WebApplicationFactory<Program> _factory;

    // Resolved relative to now (via BookingDates), always future/in-horizon, matching
    // every other create-path test's date strategy.
    private static readonly DateOnly TargetDate = BookingDates.NextBookableDate();

    public WorkingHoursReplaceTests(SqlServerWebApplicationFactory factory)
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
                DisplayName = "Availability Tester",
                EmailConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(user, TestPassword);
            Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            // Deliberately Staff, not Owner — proves D-13's "any authenticated staff",
            // not an Owner-only gate.
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

    /// <summary>
    /// Seeds a dedicated 15-minute-duration service so cellsNeeded == 1 and every
    /// assertion below can reason about exact 15-minute grid points, independent of
    /// the seeded catalog's real durations (45/90/120min etc).
    /// </summary>
    private async Task<int> SeedFifteenMinuteServiceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var service = new Service
        {
            Slug = $"test-quick-service-{Guid.NewGuid():N}",
            Name = "Test Quick Service",
            ShortDescription = "Test-only.",
            LongDescription = "Test-only.",
            Category = "Test",
            DurationMinutes = 15,
            Price = 1m,
            IsActive = true,
            DisplayOrder = 999,
        };
        dbContext.Services.Add(service);
        await dbContext.SaveChangesAsync();
        return service.Id;
    }

    private static object Segment(DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end) => new
    {
        DayOfWeek = dayOfWeek,
        StartTime = start,
        EndTime = end,
    };

    private async Task<List<OpenSlotDto>> GetSlotsAsync(HttpClient client, int serviceId, DateOnly date, int stylistId)
    {
        var response = await client.GetAsync(
            $"/api/appointments/slots?serviceId={serviceId}&stylistId={stylistId}&date={date:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<List<OpenSlotDto>>() ?? new List<OpenSlotDto>();
    }

    [Fact]
    public async Task Put_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/availability/{TargetStylistId}/working-hours",
            new { Segments = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_AuthenticatedStaff_ReplacesWeek_SlotServiceReflectsNarrowedWindow()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var payload = new { Segments = new[] { Segment(TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0)) } };

        var response = await client.PutAsJsonAsync($"/api/availability/{TargetStylistId}/working-hours", payload);

        // Any authenticated staff (Staff role here, not Owner) succeeds — D-13, no per-stylist gate.
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var slots = await GetSlotsAsync(client, serviceId, TargetDate, TargetStylistId);

        Assert.NotEmpty(slots);
        Assert.All(slots, slot =>
        {
            var localTime = TimeOnly.FromDateTime(slot.StartsAt.DateTime);
            Assert.True(localTime >= new TimeOnly(9, 0) && localTime < new TimeOnly(12, 0));
        });
    }

    [Fact]
    public async Task Put_TouchingSegments_YieldContiguousSlotsAcrossBoundary()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var payload = new
        {
            Segments = new[]
            {
                Segment(TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0)),
                Segment(TargetDate.DayOfWeek, new TimeOnly(12, 0), new TimeOnly(13, 0)),
            },
        };

        var putResponse = await client.PutAsJsonAsync($"/api/availability/{TargetStylistId}/working-hours", payload);
        Assert.True(putResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var slots = await GetSlotsAsync(client, serviceId, TargetDate, TargetStylistId);
        var starts = slots.Select(slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime)).OrderBy(t => t).ToList();

        // The 15-min grid point exactly at the boundary (11:45 -> 12:00) is present on
        // both sides with no artificial gap introduced by the two-row split (adjacency).
        Assert.Contains(new TimeOnly(11, 45), starts);
        Assert.Contains(new TimeOnly(12, 0), starts);
        Assert.Equal(new TimeOnly(9, 0), starts.First());
        Assert.Equal(new TimeOnly(12, 45), starts.Last());
    }

    [Fact]
    public async Task Put_GapBetweenSegments_YieldsNoSlotsInsideGap()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var payload = new
        {
            Segments = new[]
            {
                Segment(TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0)),
                Segment(TargetDate.DayOfWeek, new TimeOnly(13, 0), new TimeOnly(14, 0)),
            },
        };

        var putResponse = await client.PutAsJsonAsync($"/api/availability/{TargetStylistId}/working-hours", payload);
        Assert.True(putResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var slots = await GetSlotsAsync(client, serviceId, TargetDate, TargetStylistId);
        var starts = slots.Select(slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime)).ToList();

        Assert.DoesNotContain(starts, time => time >= new TimeOnly(12, 0) && time < new TimeOnly(13, 0));
        Assert.Contains(new TimeOnly(11, 45), starts);
        Assert.Contains(new TimeOnly(13, 0), starts);
    }

    [Fact]
    public async Task Put_EmptySegments_PersistsNoRows_SlotServiceReturnsNoSlotsForStylist()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var payload = new { Segments = Array.Empty<object>() };

        var putResponse = await client.PutAsJsonAsync($"/api/availability/{TargetStylistId}/working-hours", payload);
        Assert.True(putResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var slots = await GetSlotsAsync(client, serviceId, TargetDate, TargetStylistId);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task Put_SamePayloadTwice_YieldsIdenticalPersistedRows_NoDuplicates()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var payload = new { Segments = new[] { Segment(TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(11, 0)) } };

        var firstResponse = await client.PutAsJsonAsync($"/api/availability/{TargetStylistId}/working-hours", payload);
        Assert.True(firstResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
        var firstSlots = await GetSlotsAsync(client, serviceId, TargetDate, TargetStylistId);

        var secondResponse = await client.PutAsJsonAsync($"/api/availability/{TargetStylistId}/working-hours", payload);
        Assert.True(secondResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
        var secondSlots = await GetSlotsAsync(client, serviceId, TargetDate, TargetStylistId);

        Assert.Equal(
            firstSlots.Select(slot => slot.StartsAt),
            secondSlots.Select(slot => slot.StartsAt));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var rowCount = await dbContext.StylistWorkingHours
            .CountAsync(hours => hours.StylistId == TargetStylistId && hours.DayOfWeek == TargetDate.DayOfWeek);

        Assert.Equal(1, rowCount);
    }
}
