using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Tests.Features.Account;

/// <summary>
/// ACCT-02 / ACCT-06 / D-04 / D-08 — ownership-gated booking history + claim-by-email
/// over real SQL Server (RESEARCH Pitfall 1 — no InMemory). Anonymous JSON + raw reads
/// so RED compiles before AccountController / Claim DTOs exist.
/// </summary>
public class AccountBookingsTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "AccountBookingsTests-signing-key-at-least-32-bytes-hmac!";
    private const string TestPassword = "AccountBookingsTest!2026Pw";

    private readonly WebApplicationFactory<Program> _factory;

    public AccountBookingsTests(SqlServerWebApplicationFactory factory)
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
                DisplayName = "Staff Tester",
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

    private async Task<int> SeedGuestAppointmentAsync(
        string email,
        DateTimeOffset startsAt,
        string firstName = "Guest",
        string lastName = "Booker")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        // Unique slot per seed to avoid (StylistId, SlotStart) unique-index collisions
        // when tests share the SqlServerWebApplicationFactory database.
        var slotStart = startsAt.AddTicks(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF);
        var appointment = new Appointment
        {
            ServiceId = 1,
            StylistId = 1,
            StartsAt = slotStart,
            Status = AppointmentStatus.Confirmed,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Slots =
            {
                new AppointmentSlot { StylistId = 1, SlotStart = slotStart },
            },
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    private static async Task PostClaimAsync(HttpClient client, bool confirm)
    {
        var response = await client.PostAsJsonAsync("/api/account/claim", new { Confirm = confirm });
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected claim success, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task GetBookings_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/account/bookings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBookings_StaffJwt_Returns403()
    {
        var client = _factory.CreateClient();
        var (_, token) = await SeedStaffAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/account/bookings");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClaimPreview_AfterRegister_ReturnsGuestAppointmentsMatchingEmail()
    {
        var client = _factory.CreateClient();
        var email = $"claim-preview-{Guid.NewGuid():N}@example.com";
        var older = DateTimeOffset.UtcNow.AddDays(3);
        var newer = DateTimeOffset.UtcNow.AddDays(10);
        var guestId = await SeedGuestAppointmentAsync(email, older);
        await SeedGuestAppointmentAsync(email, newer);
        // Different email must never appear in claim preview (Pitfall 5).
        await SeedGuestAppointmentAsync($"other-{Guid.NewGuid():N}@example.com", DateTimeOffset.UtcNow.AddDays(5));

        var (_, _, token) = await RegisterClientAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/account/claim-preview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.True(root.TryGetProperty("appointments", out var appointments)
            || root.TryGetProperty("Appointments", out appointments),
            "claim-preview must expose appointments array");
        Assert.Equal(2, appointments.GetArrayLength());

        var ids = appointments.EnumerateArray()
            .Select(a => a.TryGetProperty("id", out var id) ? id.GetInt32()
                : a.GetProperty("Id").GetInt32())
            .ToHashSet();
        Assert.Contains(guestId, ids);
    }

    [Fact]
    public async Task ClaimConfirmTrue_AttachesGuestBookings_ListShowsOnlyOwnedDateDesc()
    {
        var client = _factory.CreateClient();
        var emailA = $"client-a-{Guid.NewGuid():N}@example.com";
        var emailB = $"client-b-{Guid.NewGuid():N}@example.com";

        var older = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 9, 15, 14, 0, 0, TimeSpan.Zero);
        var olderId = await SeedGuestAppointmentAsync(emailA, older, "Alice", "Older");
        var newerId = await SeedGuestAppointmentAsync(emailA, newer, "Alice", "Newer");
        var bId = await SeedGuestAppointmentAsync(emailB, DateTimeOffset.UtcNow.AddDays(7), "Bob", "Other");

        var (_, _, tokenA) = await RegisterClientAsync(client, emailA);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        await PostClaimAsync(client, confirm: true);

        var listResponse = await client.GetAsync("/api/account/bookings");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var list = listJson.RootElement;
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        Assert.Equal(2, list.GetArrayLength());

        var firstId = list[0].TryGetProperty("id", out var id0) ? id0.GetInt32() : list[0].GetProperty("Id").GetInt32();
        var secondId = list[1].TryGetProperty("id", out var id1) ? id1.GetInt32() : list[1].GetProperty("Id").GetInt32();
        Assert.Equal(newerId, firstId);
        Assert.Equal(olderId, secondId);

        // B's guest row must not appear for A.
        var listedIds = new[] { firstId, secondId };
        Assert.DoesNotContain(bId, listedIds);
    }

    [Fact]
    public async Task ClaimConfirmFalse_LeavesFkNull_ListEmpty()
    {
        var client = _factory.CreateClient();
        var email = $"skip-claim-{Guid.NewGuid():N}@example.com";
        await SeedGuestAppointmentAsync(email, DateTimeOffset.UtcNow.AddDays(4));

        var (_, _, token) = await RegisterClientAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await PostClaimAsync(client, confirm: false);

        var listResponse = await client.GetAsync("/api/account/bookings");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, listJson.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task GetBooking_CrossClient_Returns404WithoutLeakingPii()
    {
        var client = _factory.CreateClient();
        var emailA = $"idor-a-{Guid.NewGuid():N}@example.com";
        var emailB = $"idor-b-{Guid.NewGuid():N}@example.com";

        var bAppointmentId = await SeedGuestAppointmentAsync(
            emailB, DateTimeOffset.UtcNow.AddDays(8), "Secret", "Victim");

        var (_, _, tokenB) = await RegisterClientAsync(client, emailB);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        await PostClaimAsync(client, confirm: true);

        client.DefaultRequestHeaders.Authorization = null;
        var (_, _, tokenA) = await RegisterClientAsync(client, emailA);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var response = await client.GetAsync($"/api/account/bookings/{bAppointmentId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(emailB, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Victim", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Claim_DoesNotAttachDifferentEmailGuestRows()
    {
        var client = _factory.CreateClient();
        var emailA = $"exact-a-{Guid.NewGuid():N}@example.com";
        var emailOther = $"exact-other-{Guid.NewGuid():N}@example.com";

        await SeedGuestAppointmentAsync(emailA, DateTimeOffset.UtcNow.AddDays(2));
        var otherId = await SeedGuestAppointmentAsync(emailOther, DateTimeOffset.UtcNow.AddDays(3));

        var (_, _, token) = await RegisterClientAsync(client, emailA);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await PostClaimAsync(client, confirm: true);

        var listResponse = await client.GetAsync("/api/account/bookings");
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var ids = listJson.RootElement.EnumerateArray()
            .Select(a => a.TryGetProperty("id", out var id) ? id.GetInt32() : a.GetProperty("Id").GetInt32())
            .ToList();
        Assert.DoesNotContain(otherId, ids);
        Assert.Single(ids);
    }
}
