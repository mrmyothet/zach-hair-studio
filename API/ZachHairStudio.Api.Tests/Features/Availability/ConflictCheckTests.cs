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
using ZachHairStudio.Shared.Features.Stylists;

namespace ZachHairStudio.Api.Tests.Features.Availability;

/// <summary>
/// Proves MGMT-03's hard-blocking conflict check over real SQL Server LocalDB: a
/// save (working-hours shrink or new time off) that would leave a Confirmed
/// appointment's slot outside the new hours or inside new time off is refused
/// (409) with an actionable conflict list, and NOTHING persists (D-09, no
/// partial apply). Confirmed appointments are seeded through the real
/// POST /api/appointments path so AppointmentSlot rows exist exactly as
/// production creates them. Each test gets its OWN dedicated stylist row
/// (direct DbContext insert, mirroring WorkingHoursReplaceTests' dedicated-
/// service pattern) so one test's full-week hours replace can never bleed into
/// another test's conflict assertions within the same shared class fixture.
/// RED until Task 2 (conflict scan / Result.ConflictError / controller 409)
/// lands — every assertion below currently observes the pre-Plan-05 behavior
/// (204/201 success, no conflict check at all).
/// </summary>
public class ConflictCheckTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "ConflictCheckTests-signing-key-at-least-32-bytes-long!!!";
    private const string TestPassword = "ConflictCheckTest!2026Pw";
    private const string SeededServiceName = "Test Quick Service";

    private readonly WebApplicationFactory<Program> _factory;

    // Resolved relative to now (via BookingDates), always future/in-horizon,
    // matching every other create-path test's date strategy.
    private static readonly DateOnly TargetDate = BookingDates.NextBookableDate();

    public ConflictCheckTests(SqlServerWebApplicationFactory factory)
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
                DisplayName = "Conflict Tester",
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

    /// <summary>A dedicated Stylist row per test — full isolation from every
    /// other test's working-hours/time-off/appointment state within the same
    /// shared class fixture (mirrors SeedFifteenMinuteServiceAsync's rationale
    /// from WorkingHoursReplaceTests/TimeOffTests).</summary>
    private async Task<int> SeedStylistAsync(string name = "Test Stylist")
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var stylist = new Stylist
        {
            Slug = $"test-stylist-{Guid.NewGuid():N}",
            Name = name,
            IsActive = true,
            DisplayOrder = 999,
        };
        dbContext.Stylists.Add(stylist);
        await dbContext.SaveChangesAsync();
        return stylist.Id;
    }

    /// <summary>Dedicated 15-minute-duration service so cellsNeeded == 1 and
    /// every boundary assertion below is exact.</summary>
    private async Task<int> SeedFifteenMinuteServiceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var service = new Service
        {
            Slug = $"test-quick-service-{Guid.NewGuid():N}",
            Name = SeededServiceName,
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

    private async Task<JsonElement> CreateAppointmentAsync(
        HttpClient client, DateTimeOffset startsAt, int stylistId, int serviceId,
        string firstName = "Jane", string lastName = "Doe")
    {
        var response = await client.PostAsJsonAsync("/api/appointments", new
        {
            ServiceId = serviceId,
            StylistId = stylistId,
            StartsAt = startsAt,
            FirstName = firstName,
            LastName = lastName,
            Email = "jane.doe@example.com",
            Phone = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private static object Segment(DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end) => new
    {
        DayOfWeek = dayOfWeek,
        StartTime = start,
        EndTime = end,
    };

    private async Task<HttpResponseMessage> PutWorkingHoursAsync(
        HttpClient client, int stylistId, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end)
    {
        var payload = new { Segments = new[] { Segment(dayOfWeek, start, end) } };
        return await client.PutAsJsonAsync($"/api/availability/{stylistId}/working-hours", payload);
    }

    private async Task<HttpResponseMessage> PostTimeOffAsync(
        HttpClient client, int stylistId, DateTimeOffset startsAt, DateTimeOffset endsAt, string? reason = null)
    {
        var payload = new { StartsAt = startsAt, EndsAt = endsAt, Reason = reason };
        return await client.PostAsJsonAsync($"/api/availability/{stylistId}/time-off", payload);
    }

    private async Task<JsonElement> GetAvailabilityAsync(HttpClient client, int stylistId)
    {
        var response = await client.GetAsync($"/api/availability/{stylistId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private async Task<HttpStatusCode> PatchStatusAsync(HttpClient client, int appointmentId, string newStatus)
    {
        var response = await client.PatchAsJsonAsync($"/api/schedule/{appointmentId}/status", new { NewStatus = newStatus });
        return response.StatusCode;
    }

    [Fact]
    public async Task Put_ShrinkingHoursExcludesConfirmedAppointment_Returns409WithConflictShape_AndNoPartialApply()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var stylistId = await SeedStylistAsync();
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var establish = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(18, 0));
        Assert.True(establish.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var appointment = await CreateAppointmentAsync(
            client, BookingDates.SlotOn(TargetDate, 14, 0), stylistId, serviceId, "Aria", "Chen");
        var appointmentId = appointment.GetProperty("id").GetInt32();

        var response = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var conflicts = json.RootElement.GetProperty("conflicts");
        Assert.Equal(1, conflicts.GetArrayLength());
        var conflict = conflicts[0];
        Assert.Equal(appointmentId, conflict.GetProperty("appointmentId").GetInt32());
        Assert.Equal("Aria Chen", conflict.GetProperty("clientName").GetString());
        Assert.Equal(SeededServiceName, conflict.GetProperty("serviceName").GetString());
        Assert.False(string.IsNullOrWhiteSpace(conflict.GetProperty("stylistName").GetString()));
        Assert.True(conflict.TryGetProperty("salonLocalTime", out _));

        // No partial apply (D-09): the ORIGINAL 9-18 hours are still in effect.
        var availability = await GetAvailabilityAsync(client, stylistId);
        var hours = availability.GetProperty("workingHours");
        Assert.Equal(1, hours.GetArrayLength());
        Assert.Equal(new TimeOnly(18, 0), TimeOnly.Parse(hours[0].GetProperty("endTime").GetString()!));
    }

    [Fact]
    public async Task Post_TimeOffOverlapsConfirmedAppointment_Returns409_AndNoPartialApply()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var stylistId = await SeedStylistAsync();
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var establish = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(18, 0));
        Assert.True(establish.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var appointment = await CreateAppointmentAsync(
            client, BookingDates.SlotOn(TargetDate, 10, 0), stylistId, serviceId, "Marcus", "Lee");
        var appointmentId = appointment.GetProperty("id").GetInt32();

        var response = await PostTimeOffAsync(
            client, stylistId, BookingDates.SlotOn(TargetDate, 9, 30), BookingDates.SlotOn(TargetDate, 11, 0), "Test overlap");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var conflicts = json.RootElement.GetProperty("conflicts");
        Assert.Equal(1, conflicts.GetArrayLength());
        Assert.Equal(appointmentId, conflicts[0].GetProperty("appointmentId").GetInt32());
        Assert.Equal("Marcus Lee", conflicts[0].GetProperty("clientName").GetString());

        // No partial apply: no time-off row was persisted.
        var availability = await GetAvailabilityAsync(client, stylistId);
        Assert.Equal(0, availability.GetProperty("timeOff").GetArrayLength());
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("NoShow")]
    public async Task Put_AfterCancelOrNoShowReleasesSlot_SameShrinkSucceeds(string terminalStatus)
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var stylistId = await SeedStylistAsync();
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var establish = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(18, 0));
        Assert.True(establish.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var appointment = await CreateAppointmentAsync(client, BookingDates.SlotOn(TargetDate, 14, 0), stylistId, serviceId);
        var appointmentId = appointment.GetProperty("id").GetInt32();

        var blocked = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var statusCode = await PatchStatusAsync(client, appointmentId, terminalStatus);
        Assert.Equal(HttpStatusCode.OK, statusCode);

        var afterRelease = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));
        Assert.True(afterRelease.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Put_CompletedAppointment_NeverAppearsInConflictList_ShrinkSucceeds()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var stylistId = await SeedStylistAsync();
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var establish = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(18, 0));
        Assert.True(establish.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var appointment = await CreateAppointmentAsync(client, BookingDates.SlotOn(TargetDate, 14, 0), stylistId, serviceId);
        var appointmentId = appointment.GetProperty("id").GetInt32();

        var completeStatus = await PatchStatusAsync(client, appointmentId, "Completed");
        Assert.Equal(HttpStatusCode.OK, completeStatus);

        // Completed appointments still retain their AppointmentSlot rows
        // (RESEARCH Pitfall 3) — the conflict scan must join Appointment.Status
        // == Confirmed explicitly and never flag this one.
        var shrink = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));
        Assert.True(shrink.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Put_BoundaryExactlyAtNewClose_IsAllowed()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var stylistId = await SeedStylistAsync();
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var establish = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(18, 0));
        Assert.True(establish.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        // 15-minute appointment at 11:45 ends exactly at 12:00.
        await CreateAppointmentAsync(client, BookingDates.SlotOn(TargetDate, 11, 45), stylistId, serviceId);

        var response = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));

        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Put_BoundaryOneCellPastNewClose_IsBlocked()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var stylistId = await SeedStylistAsync();
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var establish = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(18, 0));
        Assert.True(establish.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        // 15-minute appointment at 12:00 ends at 12:15 — one grid cell past a 12:00 close.
        await CreateAppointmentAsync(client, BookingDates.SlotOn(TargetDate, 12, 0), stylistId, serviceId);

        var response = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Put_NoConfirmedAppointments_SucceedsWithNoConflictPanel()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var stylistId = await SeedStylistAsync();

        var response = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));

        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Put_ConflictingSaveRepeatedTwice_ReturnsSameConflictSet_NeverPartiallyApplies()
    {
        var token = await SeedStaffAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var stylistId = await SeedStylistAsync();
        var serviceId = await SeedFifteenMinuteServiceAsync();

        var establish = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(18, 0));
        Assert.True(establish.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var appointment = await CreateAppointmentAsync(client, BookingDates.SlotOn(TargetDate, 14, 0), stylistId, serviceId);
        var appointmentId = appointment.GetProperty("id").GetInt32();

        var first = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));
        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var firstIds = firstJson.RootElement.GetProperty("conflicts")
            .EnumerateArray().Select(c => c.GetProperty("appointmentId").GetInt32()).ToList();

        var second = await PutWorkingHoursAsync(client, stylistId, TargetDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        var secondIds = secondJson.RootElement.GetProperty("conflicts")
            .EnumerateArray().Select(c => c.GetProperty("appointmentId").GetInt32()).ToList();

        Assert.Equal(new[] { appointmentId }, firstIds);
        Assert.Equal(firstIds, secondIds);

        var availability = await GetAvailabilityAsync(client, stylistId);
        Assert.Equal(new TimeOnly(18, 0), TimeOnly.Parse(availability.GetProperty("workingHours")[0].GetProperty("endTime").GetString()!));
    }
}
