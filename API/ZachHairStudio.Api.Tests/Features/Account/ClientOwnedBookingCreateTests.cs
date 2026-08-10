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
/// Gap closure (ACCT-02 / ACCT-04 / D-08 / D-12) — Client JWT on public POST
/// /api/appointments must set Appointment.ClientUserId so account list/cancel work.
/// SqlServer only (RESEARCH Pitfall 1 — no InMemory).
/// </summary>
public class ClientOwnedBookingCreateTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "ClientOwnedBookingCreateTests-signing-key-32b-hmac!";
    private const string TestPassword = "ClientOwnedBookCreate!2026Pw";

    private readonly WebApplicationFactory<Program> _factory;

    private static DateTimeOffset Slot(int hour, int minute = 0)
        => BookingDates.NextBookableSlot(hour, minute);

    public ClientOwnedBookingCreateTests(SqlServerWebApplicationFactory factory)
    {
        // Use only the JWT-configured factory for HTTP and DB scopes — accessing the
        // raw fixture Services starts a host without Jwt:SigningKey and fails ValidateOnStart.
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
        email ??= $"owned-create-{Guid.NewGuid():N}@example.com";

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
        var email = $"staff-owned-create-{Guid.NewGuid():N}@example.com";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Staff Owned-Create Tester",
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

    private static object BookingRequest(DateTimeOffset startsAt, string email, int stylistId = 1) => new
    {
        ServiceId = 1,
        StylistId = stylistId,
        StartsAt = startsAt,
        FirstName = "Owned",
        LastName = "Booker",
        Email = email,
        Phone = (string?)null,
    };

    private static int? ReadClientUserId(JsonElement root)
    {
        if (root.TryGetProperty("clientUserId", out var camel) && camel.ValueKind != JsonValueKind.Null)
        {
            return camel.GetInt32();
        }

        if (root.TryGetProperty("ClientUserId", out var pascal) && pascal.ValueKind != JsonValueKind.Null)
        {
            return pascal.GetInt32();
        }

        return null;
    }

    private static int ReadId(JsonElement root)
        => root.TryGetProperty("id", out var id) ? id.GetInt32() : root.GetProperty("Id").GetInt32();

    private async Task<int?> LoadDbClientUserIdAsync(int appointmentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var appointment = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == appointmentId);
        return appointment.ClientUserId;
    }

    [Fact]
    public async Task ClientJwt_PostAppointments_OwnsRow_AppearsInAccountBookings_AndCancelSucceeds()
    {
        var client = _factory.CreateClient();
        var (userId, email, token) = await RegisterClientAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var startsAt = Slot(9, 15);
        var createResponse = await client.PostAsJsonAsync(
            "/api/appointments",
            BookingRequest(startsAt, email, stylistId: 1));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var appointmentId = ReadId(createJson.RootElement);
        Assert.Equal(userId, ReadClientUserId(createJson.RootElement));
        Assert.Equal(userId, await LoadDbClientUserIdAsync(appointmentId));

        var listResponse = await client.GetAsync("/api/account/bookings");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var listedIds = listJson.RootElement.EnumerateArray().Select(ReadId).ToHashSet();
        Assert.Contains(appointmentId, listedIds);

        var cancelResponse = await client.PostAsync($"/api/account/bookings/{appointmentId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var appointment = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == appointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public async Task Anonymous_PostAppointments_ClientUserIdRemainsNull()
    {
        var client = _factory.CreateClient();
        var email = $"guest-create-{Guid.NewGuid():N}@example.com";
        var startsAt = Slot(10, 30);

        var response = await client.PostAsJsonAsync(
            "/api/appointments",
            BookingRequest(startsAt, email, stylistId: 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Null(ReadClientUserId(json.RootElement));
        Assert.Null(await LoadDbClientUserIdAsync(ReadId(json.RootElement)));
    }

    [Fact]
    public async Task StaffJwt_PostAppointments_DoesNotAttachClientUserId()
    {
        var client = _factory.CreateClient();
        var (staffEmail, token) = await SeedStaffAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var startsAt = Slot(11, 45);
        var response = await client.PostAsJsonAsync(
            "/api/appointments",
            BookingRequest(startsAt, staffEmail, stylistId: 3));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Null(ReadClientUserId(json.RootElement));
        Assert.Null(await LoadDbClientUserIdAsync(ReadId(json.RootElement)));
    }
}
