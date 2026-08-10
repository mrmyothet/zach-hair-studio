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
/// Gap closure (ACCT-02 / ACCT-04 / D-08) — Client JWT on public POST /api/appointments
/// must set Appointment.ClientUserId so the row appears in account history and cancel works.
/// Guest and Staff Bearer must leave ClientUserId null. SqlServer only (RESEARCH Pitfall 1).
/// </summary>
public class ClientOwnedBookingCreateTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "ClientOwnedBookingCreateTests-signing-key-32b-hmac!";
    private const string TestPassword = "ClientOwnedCreateTest!2026Pw";

    private readonly SqlServerWebApplicationFactory _rawFactory;
    private readonly WebApplicationFactory<Program> _factory;

    private static DateTimeOffset Slot(int hour, int minute = 0)
        => BookingDates.NextBookableSlot(hour, minute);

    public ClientOwnedBookingCreateTests(SqlServerWebApplicationFactory factory)
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

    private async Task AssertDbClientUserIdAsync(int appointmentId, int? expected)
    {
        using var scope = _rawFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var appointment = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == appointmentId);
        Assert.Equal(expected, appointment.ClientUserId);
    }

    [Fact]
    public async Task ClientJwt_PostAppointments_OwnsRow_ListAndCancelSucceed()
    {
        var client = _factory.CreateClient();
        var (userId, email, token) = await RegisterClientAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var startsAt = Slot(9);
        var create = await client.PostAsJsonAsync("/api/appointments", BookingRequest(startsAt, email, stylistId: 1));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var createJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var appointmentId = ReadId(createJson.RootElement);
        Assert.Equal(userId, ReadClientUserId(createJson.RootElement));
        await AssertDbClientUserIdAsync(appointmentId, userId);

        var list = await client.GetAsync("/api/account/bookings");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var ids = listJson.RootElement.EnumerateArray().Select(ReadId).ToHashSet();
        Assert.Contains(appointmentId, ids);

        var cancel = await client.PostAsync($"/api/account/bookings/{appointmentId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        using var scope = _rawFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var appointment = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == appointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public async Task Anonymous_PostAppointments_ClientUserIdRemainsNull_GuestPath()
    {
        var client = _factory.CreateClient();
        var email = $"guest-{Guid.NewGuid():N}@example.com";

        var create = await client.PostAsJsonAsync(
            "/api/appointments",
            BookingRequest(Slot(10), email, stylistId: 2));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var createJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var appointmentId = ReadId(createJson.RootElement);
        Assert.Null(ReadClientUserId(createJson.RootElement));
        await AssertDbClientUserIdAsync(appointmentId, expected: null);
    }

    [Fact]
    public async Task StaffJwt_PostAppointments_DoesNotAttachOwnership()
    {
        var client = _factory.CreateClient();
        var (staffEmail, token) = await SeedStaffAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await client.PostAsJsonAsync(
            "/api/appointments",
            BookingRequest(Slot(11), staffEmail, stylistId: 3));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var createJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var appointmentId = ReadId(createJson.RootElement);
        Assert.Null(ReadClientUserId(createJson.RootElement));
        await AssertDbClientUserIdAsync(appointmentId, expected: null);
    }
}
