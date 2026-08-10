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
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Tests.Features.Account;

/// <summary>
/// ACCT-04 / D-09–D-12 — ownership-gated client cancel + transactional reschedule
/// over real SQL Server (RESEARCH Pitfall 1 — no InMemory). Anonymous JSON so RED
/// compiles before CancelForClient / RescheduleForClient endpoints exist.
/// </summary>
public class ClientRescheduleTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "ClientRescheduleTests-signing-key-at-least-32-bytes-hmac!";
    private const string TestPassword = "ClientRescheduleTest!2026Pw";

    private readonly SqlServerWebApplicationFactory _rawFactory;
    private readonly WebApplicationFactory<Program> _factory;

    private static DateTimeOffset Slot(int hour, int minute = 0)
        => BookingDates.NextBookableSlot(hour, minute);

    public ClientRescheduleTests(SqlServerWebApplicationFactory factory)
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

    private async Task EnsureRolesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        foreach (var role in new[] { StaffRoles.Client, StaffRoles.Staff, StaffRoles.Owner })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }
    }

    private async Task<(int UserId, string Email, string Token)> RegisterClientAsync(
        HttpClient client, string? email = null)
    {
        await EnsureRolesAsync();
        email ??= $"client-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = TestPassword,
            ConfirmPassword = TestPassword,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("token").GetString()!;

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        return (user!.Id, email, token);
    }

    private async Task<(string Email, string Token)> SeedStaffAndLoginAsync(HttpClient client)
    {
        await EnsureRolesAsync();
        var email = $"staff-{Guid.NewGuid():N}@example.com";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Staff Reschedule Tester",
                EmailConfirmed = true,
            };
            var create = await userManager.CreateAsync(user, TestPassword);
            Assert.True(create.Succeeded, string.Join(", ", create.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, StaffRoles.Staff);
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return (email, json.RootElement.GetProperty("token").GetString()!);
    }

    /// <summary>
    /// Seeds an owned Confirmed appointment with a unique (StylistId, SlotStart) cell.
    /// </summary>
    private async Task<int> SeedOwnedAppointmentAsync(
        int clientUserId,
        string email,
        DateTimeOffset startsAt,
        int stylistId = 1,
        AppointmentStatus status = AppointmentStatus.Confirmed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var appointment = new Appointment
        {
            ServiceId = 1,
            StylistId = stylistId,
            StartsAt = startsAt,
            Status = status,
            FirstName = "Owned",
            LastName = "Client",
            Email = email,
            ClientUserId = clientUserId,
            Slots =
            {
                new AppointmentSlot { StylistId = stylistId, SlotStart = startsAt },
            },
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    private async Task AssertAppointmentStatusAsync(int appointmentId, AppointmentStatus expected)
    {
        using var scope = _rawFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var appointment = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == appointmentId);
        Assert.Equal(expected, appointment.Status);
    }

    private async Task AssertSlotCountAsync(int appointmentId, int expected)
    {
        using var scope = _rawFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var count = await db.AppointmentSlots.CountAsync(s => s.AppointmentId == appointmentId);
        Assert.Equal(expected, count);
    }

    [Fact]
    public async Task Cancel_OwnerConfirmed_Returns200_CancelsAndReleasesSlots()
    {
        var client = _factory.CreateClient();
        var (userId, email, token) = await RegisterClientAsync(client);
        var appointmentId = await SeedOwnedAppointmentAsync(userId, email, Slot(10), stylistId: 1);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsync($"/api/account/bookings/{appointmentId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAppointmentStatusAsync(appointmentId, AppointmentStatus.Cancelled);
        await AssertSlotCountAsync(appointmentId, 0);
    }

    [Fact]
    public async Task Reschedule_OwnerConfirmed_BooksNewThenCancelsOld()
    {
        var client = _factory.CreateClient();
        var (userId, email, token) = await RegisterClientAsync(client);
        var oldStartsAt = Slot(10);
        var newStartsAt = Slot(14);
        var appointmentId = await SeedOwnedAppointmentAsync(userId, email, oldStartsAt, stylistId: 1);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync(
            $"/api/account/bookings/{appointmentId}/reschedule",
            new { StartsAt = newStartsAt, StylistId = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var newId = json.RootElement.TryGetProperty("id", out var idProp)
            ? idProp.GetInt32()
            : json.RootElement.GetProperty("Id").GetInt32();
        Assert.NotEqual(appointmentId, newId);

        var newStatus = json.RootElement.TryGetProperty("status", out var statusProp)
            ? statusProp.GetString()
            : json.RootElement.GetProperty("Status").GetString();
        Assert.Equal("Confirmed", newStatus);

        await AssertAppointmentStatusAsync(appointmentId, AppointmentStatus.Cancelled);
        await AssertSlotCountAsync(appointmentId, 0);

        using var scope = _rawFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var created = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == newId);
        Assert.Equal(AppointmentStatus.Confirmed, created.Status);
        Assert.Equal(userId, created.ClientUserId);
        Assert.Equal(newStartsAt, created.StartsAt);
        var newSlots = await db.AppointmentSlots.CountAsync(s => s.AppointmentId == newId);
        Assert.True(newSlots > 0);
    }

    [Fact]
    public async Task Cancel_NonOwner_Returns404_LeavesTargetUnchanged()
    {
        var client = _factory.CreateClient();
        var (ownerId, ownerEmail, _) = await RegisterClientAsync(client);
        var appointmentId = await SeedOwnedAppointmentAsync(ownerId, ownerEmail, Slot(11), stylistId: 2);

        var (_, _, otherToken) = await RegisterClientAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        var response = await client.PostAsync($"/api/account/bookings/{appointmentId}/cancel", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        // Require ownership-gate ProblemDetails (not bare unmatched-route 404).
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Appointment not found", body, StringComparison.OrdinalIgnoreCase);

        await AssertAppointmentStatusAsync(appointmentId, AppointmentStatus.Confirmed);
        await AssertSlotCountAsync(appointmentId, 1);
    }

    [Fact]
    public async Task Reschedule_NonOwner_Returns404_LeavesTargetUnchanged()
    {
        var client = _factory.CreateClient();
        var (ownerId, ownerEmail, _) = await RegisterClientAsync(client);
        var appointmentId = await SeedOwnedAppointmentAsync(ownerId, ownerEmail, Slot(12), stylistId: 3);

        var (_, _, otherToken) = await RegisterClientAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        var response = await client.PostAsJsonAsync(
            $"/api/account/bookings/{appointmentId}/reschedule",
            new { StartsAt = Slot(15), StylistId = 3 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Appointment not found", body, StringComparison.OrdinalIgnoreCase);

        await AssertAppointmentStatusAsync(appointmentId, AppointmentStatus.Confirmed);
        await AssertSlotCountAsync(appointmentId, 1);
    }

    [Fact]
    public async Task Cancel_StaffJwt_Returns403()
    {
        var client = _factory.CreateClient();
        var (userId, email, _) = await RegisterClientAsync(client);
        var appointmentId = await SeedOwnedAppointmentAsync(userId, email, Slot(9), stylistId: 4);

        var (_, staffToken) = await SeedStaffAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var response = await client.PostAsync($"/api/account/bookings/{appointmentId}/cancel", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertAppointmentStatusAsync(appointmentId, AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Reschedule_StaffJwt_Returns403()
    {
        var client = _factory.CreateClient();
        var (userId, email, _) = await RegisterClientAsync(client);
        var appointmentId = await SeedOwnedAppointmentAsync(userId, email, Slot(13), stylistId: 2);

        var (_, staffToken) = await SeedStaffAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var response = await client.PostAsJsonAsync(
            $"/api/account/bookings/{appointmentId}/reschedule",
            new { StartsAt = Slot(16), StylistId = 2 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertAppointmentStatusAsync(appointmentId, AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Cancel_PastStartsAt_Returns400()
    {
        var client = _factory.CreateClient();
        var (userId, email, token) = await RegisterClientAsync(client);

        // Unique past cell — ticks jitter avoids unique-index collisions across shared fixture DB.
        var pastStart = DateTimeOffset.UtcNow.AddHours(-3).AddTicks(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF);
        var appointmentId = await SeedOwnedAppointmentAsync(userId, email, pastStart, stylistId: 1);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsync($"/api/account/bookings/{appointmentId}/cancel", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertAppointmentStatusAsync(appointmentId, AppointmentStatus.Confirmed);
        await AssertSlotCountAsync(appointmentId, 1);
    }

    [Fact]
    public async Task Reschedule_PastStartsAt_Returns400()
    {
        var client = _factory.CreateClient();
        var (userId, email, token) = await RegisterClientAsync(client);

        var pastStart = DateTimeOffset.UtcNow.AddHours(-2).AddTicks(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF);
        var appointmentId = await SeedOwnedAppointmentAsync(userId, email, pastStart, stylistId: 2);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync(
            $"/api/account/bookings/{appointmentId}/reschedule",
            new { StartsAt = Slot(15), StylistId = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertAppointmentStatusAsync(appointmentId, AppointmentStatus.Confirmed);
        await AssertSlotCountAsync(appointmentId, 1);
    }

    [Fact]
    public async Task Reschedule_TargetSlotTaken_Returns409_KeepsOriginalConfirmed()
    {
        var client = _factory.CreateClient();
        var (userId, email, token) = await RegisterClientAsync(client);
        var oldStartsAt = Slot(10);
        var takenStartsAt = Slot(14);
        var appointmentId = await SeedOwnedAppointmentAsync(userId, email, oldStartsAt, stylistId: 1);

        // Occupying booking on the reschedule target cell (concurrent other booker).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            db.Appointments.Add(new Appointment
            {
                ServiceId = 1,
                StylistId = 1,
                StartsAt = takenStartsAt,
                Status = AppointmentStatus.Confirmed,
                FirstName = "Other",
                LastName = "Booker",
                Email = $"other-{Guid.NewGuid():N}@example.com",
                Slots =
                {
                    new AppointmentSlot { StylistId = 1, SlotStart = takenStartsAt },
                },
            });
            await db.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync(
            $"/api/account/bookings/{appointmentId}/reschedule",
            new { StartsAt = takenStartsAt, StylistId = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertAppointmentStatusAsync(appointmentId, AppointmentStatus.Confirmed);
        await AssertSlotCountAsync(appointmentId, 1);
    }
}
