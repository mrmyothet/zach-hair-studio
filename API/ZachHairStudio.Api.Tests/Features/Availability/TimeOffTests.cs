using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Api.Tests.TestSupport;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Availability;

/// <summary>
/// Proves MGMT-02's time-off write path over real SQL Server LocalDB: any
/// authenticated staff can POST/DELETE a stylist's one-off time off (D-07,
/// D-13), and GET /api/appointments/slots — the SAME open-slot read path the
/// public booking flow uses via SlotService — blocks/unblocks accordingly
/// (D-08 same-model proof). RED until AvailabilityService/AvailabilityController
/// (Tasks 2-3) exist; request bodies use anonymous objects (not the not-yet-
/// existing TimeOffCreateDto) so this file compiles standalone.
/// </summary>
public class TimeOffTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "TimeOffTests-signing-key-at-least-32-bytes-long!!!!!";
    private const string TestPassword = "AvailabilityTest!2026Pw";
    private const int TargetStylistId = 2;

    private readonly WebApplicationFactory<Program> _factory;

    private static readonly DateOnly TargetDate = BookingDates.NextBookableDate();

    public TimeOffTests(SqlServerWebApplicationFactory factory)
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

    /// <summary>Dedicated 15-minute-duration service so cellsNeeded == 1 and slot math
    /// is exact for the block/unblock assertions below.</summary>
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

    private async Task<List<OpenSlotDto>> GetSlotsAsync(HttpClient client, int serviceId, DateOnly date, int stylistId)
    {
        var response = await client.GetAsync(
            $"/api/appointments/slots?serviceId={serviceId}&stylistId={stylistId}&date={date:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<List<OpenSlotDto>>() ?? new List<OpenSlotDto>();
    }

    private async Task ReplaceWorkingHoursAsync(HttpClient client, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end)
    {
        var payload = new
        {
            Segments = new[]
            {
                new { DayOfWeek = dayOfWeek, StartTime = start, EndTime = end },
            },
        };
        var response = await client.PutAsJsonAsync($"/api/availability/{TargetStylistId}/working-hours", payload);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/availability/{TargetStylistId}/time-off",
            new
            {
                StartsAt = BookingDates.SlotOn(TargetDate, 9, 0),
                EndsAt = BookingDates.SlotOn(TargetDate, 10, 0),
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_TimeOff_BlocksOverlappingSlots_Delete_RestoresThem()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var serviceId = await SeedFifteenMinuteServiceAsync();

        await ReplaceWorkingHoursAsync(client, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));

        var beforeSlots = await GetSlotsAsync(client, serviceId, TargetDate, TargetStylistId);
        Assert.Contains(beforeSlots, slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime) == new TimeOnly(10, 0));

        var timeOffPayload = new
        {
            StartsAt = BookingDates.SlotOn(TargetDate, 10, 0),
            EndsAt = BookingDates.SlotOn(TargetDate, 11, 0),
            Reason = "Test block",
        };

        var postResponse = await client.PostAsJsonAsync($"/api/availability/{TargetStylistId}/time-off", timeOffPayload);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        using var postJson = JsonDocument.Parse(await postResponse.Content.ReadAsStringAsync());
        var timeOffId = postJson.RootElement.GetProperty("id").GetInt32();

        var duringSlots = await GetSlotsAsync(client, serviceId, TargetDate, TargetStylistId);
        Assert.DoesNotContain(duringSlots, slot =>
        {
            var localTime = TimeOnly.FromDateTime(slot.StartsAt.DateTime);
            return localTime >= new TimeOnly(10, 0) && localTime < new TimeOnly(11, 0);
        });
        Assert.Contains(duringSlots, slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime) == new TimeOnly(9, 0));

        var deleteResponse = await client.DeleteAsync($"/api/availability/{TargetStylistId}/time-off/{timeOffId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterSlots = await GetSlotsAsync(client, serviceId, TargetDate, TargetStylistId);
        Assert.Contains(afterSlots, slot => TimeOnly.FromDateTime(slot.StartsAt.DateTime) == new TimeOnly(10, 0));
    }

    [Fact]
    public async Task Delete_UnknownTimeOff_Returns404()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync($"/api/availability/{TargetStylistId}/time-off/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
