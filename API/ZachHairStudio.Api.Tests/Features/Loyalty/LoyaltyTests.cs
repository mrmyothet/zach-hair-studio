using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Tests.Features.Loyalty;

/// <summary>
/// ACCT-07 / D-13–D-16 — LoyaltyLedger earn on Completed (idempotent per AppointmentId)
/// and server-authoritative checkout redeem (10 pts = $5). Runs over real SQL Server
/// (RESEARCH Pitfall 1). Anonymous JSON so RED compiles before Loyalty types / routes exist.
/// </summary>
public class LoyaltyTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "LoyaltyTests-signing-key-at-least-32-bytes-hmac-sha256!";
    private const string TestPassword = "LoyaltyTest!2026Pw";
    private const string SessionHeaderName = "X-Cart-Session-Id";

    private readonly SqlServerWebApplicationFactory _rawFactory;
    private readonly WebApplicationFactory<Program> _factory;

    public LoyaltyTests(SqlServerWebApplicationFactory factory)
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

    private async Task<string> SeedStaffAndLoginAsync(HttpClient client)
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
                DisplayName = "Loyalty Staff",
                EmailConfirmed = true,
            };
            var create = await userManager.CreateAsync(user, TestPassword);
            Assert.True(create.Succeeded, string.Join(", ", create.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, StaffRoles.Staff);
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private async Task<int> SeedOwnedAppointmentAsync(int clientUserId, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var slotStart = DateTimeOffset.UtcNow.AddDays(3).AddTicks(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF);
        var appointment = new Appointment
        {
            ServiceId = 1,
            StylistId = 1,
            StartsAt = slotStart,
            Status = AppointmentStatus.Confirmed,
            FirstName = "Loyal",
            LastName = "Client",
            Email = email,
            ClientUserId = clientUserId,
            Slots =
            {
                new AppointmentSlot { StylistId = 1, SlotStart = slotStart },
            },
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    private async Task<int> SeedGuestAppointmentAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var slotStart = DateTimeOffset.UtcNow.AddDays(4).AddTicks(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF);
        var appointment = new Appointment
        {
            ServiceId = 1,
            StylistId = 1,
            StartsAt = slotStart,
            Status = AppointmentStatus.Confirmed,
            FirstName = "Guest",
            LastName = "NoAccount",
            Email = email,
            ClientUserId = null,
            Slots =
            {
                new AppointmentSlot { StylistId = 1, SlotStart = slotStart },
            },
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    private static HttpClient Authed(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static int ReadBalance(JsonElement root)
    {
        if (root.TryGetProperty("balance", out var bal)) return bal.GetInt32();
        if (root.TryGetProperty("Balance", out bal)) return bal.GetInt32();
        throw new Xunit.Sdk.XunitException("loyalty response missing Balance");
    }

    private static decimal ReadMoney(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var prop))
            {
                return prop.GetDecimal();
            }
        }

        throw new Xunit.Sdk.XunitException($"missing money field among {string.Join(',', names)}");
    }

    private async Task EnsureProductStockAsync(int productId, decimal price, int stock)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var product = await db.Products.SingleAsync(p => p.Id == productId);
        product.Price = price;
        product.Stock = stock;
        product.IsActive = true;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds earn balance by completing owned appointments (+1 each). Used when
    /// redeem needs a multiple of 10 without depending on LoyaltyLedger type at RED.
    /// </summary>
    private async Task EarnPointsByCompletingAsync(
        HttpClient staffClient, int clientUserId, string email, int points)
    {
        for (var i = 0; i < points; i++)
        {
            var appointmentId = await SeedOwnedAppointmentAsync(clientUserId, email);
            var complete = await staffClient.PatchAsJsonAsync(
                $"/api/schedule/{appointmentId}/status",
                new { NewStatus = "Completed" });
            Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        }
    }

    [Fact]
    public async Task GetLoyalty_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/account/loyalty");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLoyalty_StaffJwt_Returns403()
    {
        var client = _factory.CreateClient();
        var staffToken = await SeedStaffAndLoginAsync(client);
        Authed(client, staffToken);

        var response = await client.GetAsync("/api/account/loyalty");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLoyalty_ClientJwt_ReturnsBalanceMatchingSumDelta()
    {
        var client = _factory.CreateClient();
        var (userId, email, clientToken) = await RegisterClientAsync(client);
        var staffClient = _factory.CreateClient();
        var staffToken = await SeedStaffAndLoginAsync(staffClient);
        Authed(staffClient, staffToken);

        await EarnPointsByCompletingAsync(staffClient, userId, email, points: 2);

        Authed(client, clientToken);
        var response = await client.GetAsync("/api/account/loyalty");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, ReadBalance(json.RootElement));
    }

    [Fact]
    public async Task Complete_OwnedAppointment_EarnsOnePoint_IdempotentPerAppointmentId()
    {
        var client = _factory.CreateClient();
        var (userId, email, clientToken) = await RegisterClientAsync(client);
        var appointmentId = await SeedOwnedAppointmentAsync(userId, email);

        var staffClient = _factory.CreateClient();
        var staffToken = await SeedStaffAndLoginAsync(staffClient);
        Authed(staffClient, staffToken);

        var first = await staffClient.PatchAsJsonAsync(
            $"/api/schedule/{appointmentId}/status",
            new { NewStatus = "Completed" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second Complete is a disallowed transition (terminal) — earn must still stay +1.
        var second = await staffClient.PatchAsJsonAsync(
            $"/api/schedule/{appointmentId}/status",
            new { NewStatus = "Completed" });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        Authed(client, clientToken);
        var loyalty = await client.GetAsync("/api/account/loyalty");
        Assert.Equal(HttpStatusCode.OK, loyalty.StatusCode);
        using var json = JsonDocument.Parse(await loyalty.Content.ReadAsStringAsync());
        Assert.Equal(1, ReadBalance(json.RootElement));
    }

    [Fact]
    public async Task Complete_NullClientUserId_DoesNotEarn()
    {
        var client = _factory.CreateClient();
        var (_, _, clientToken) = await RegisterClientAsync(client);
        var guestId = await SeedGuestAppointmentAsync($"guest-{Guid.NewGuid():N}@example.com");

        var staffClient = _factory.CreateClient();
        var staffToken = await SeedStaffAndLoginAsync(staffClient);
        Authed(staffClient, staffToken);

        var complete = await staffClient.PatchAsJsonAsync(
            $"/api/schedule/{guestId}/status",
            new { NewStatus = "Completed" });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        Authed(client, clientToken);
        var loyalty = await client.GetAsync("/api/account/loyalty");
        Assert.Equal(HttpStatusCode.OK, loyalty.StatusCode);
        using var json = JsonDocument.Parse(await loyalty.Content.ReadAsStringAsync());
        Assert.Equal(0, ReadBalance(json.RootElement));
    }

    [Fact]
    public async Task Checkout_RedeemPoints10_AppliesServerFiveDollarDiscount()
    {
        var client = _factory.CreateClient();
        var (userId, email, clientToken) = await RegisterClientAsync(client);
        var staffClient = _factory.CreateClient();
        var staffToken = await SeedStaffAndLoginAsync(staffClient);
        Authed(staffClient, staffToken);

        await EarnPointsByCompletingAsync(staffClient, userId, email, points: 10);
        await EnsureProductStockAsync(productId: 1, price: 24.00m, stock: 20);

        Authed(client, clientToken);
        client.DefaultRequestHeaders.Remove(SessionHeaderName);
        client.DefaultRequestHeaders.Add(SessionHeaderName, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/orders/checkout", new
        {
            Email = email,
            RedeemPoints = 10,
            Items = new[] { new { ProductId = 1, Quantity = 1 } },
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Unexpected {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var total = ReadMoney(root, "totalAmount", "TotalAmount");
        var discount = ReadMoney(root, "loyaltyDiscount", "LoyaltyDiscount");
        Assert.Equal(5.00m, discount);
        Assert.Equal(19.00m, total);

        var orderId = root.TryGetProperty("orderId", out var oid)
            ? oid.GetInt32()
            : root.GetProperty("OrderId").GetInt32();

        using (var scope = _rawFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == orderId);
            Assert.Equal(19.00m, order.TotalAmount);
            Assert.Equal(userId, order.ClientId);
        }

        var loyalty = await client.GetAsync("/api/account/loyalty");
        using var balJson = JsonDocument.Parse(await loyalty.Content.ReadAsStringAsync());
        Assert.Equal(0, ReadBalance(balJson.RootElement));
    }

    [Fact]
    public async Task Checkout_ClientSuppliedDollarOff_DoesNotChangeTotal_OnlyRedeemPoints()
    {
        var client = _factory.CreateClient();
        var (userId, email, clientToken) = await RegisterClientAsync(client);
        var staffClient = _factory.CreateClient();
        var staffToken = await SeedStaffAndLoginAsync(staffClient);
        Authed(staffClient, staffToken);

        await EarnPointsByCompletingAsync(staffClient, userId, email, points: 10);
        await EnsureProductStockAsync(productId: 1, price: 24.00m, stock: 20);

        Authed(client, clientToken);
        client.DefaultRequestHeaders.Add(SessionHeaderName, Guid.NewGuid().ToString());

        // Forge discount dollars in JSON — server must ignore and apply only RedeemPoints formula.
        var payload = """
            {
              "email": "REPLACE_EMAIL",
              "redeemPoints": 10,
              "discountAmount": 20,
              "loyaltyDiscount": 20,
              "totalAmount": 1,
              "items": [{ "productId": 1, "quantity": 1, "unitPrice": 1, "lineTotal": 1 }]
            }
            """.Replace("REPLACE_EMAIL", email);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/orders/checkout", content);

        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Unexpected {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(19.00m, ReadMoney(json.RootElement, "totalAmount", "TotalAmount"));
        Assert.Equal(5.00m, ReadMoney(json.RootElement, "loyaltyDiscount", "LoyaltyDiscount"));
    }

    [Fact]
    public async Task Checkout_RedeemPointsExceedingBalanceOrNotMultipleOf10_Returns400()
    {
        var client = _factory.CreateClient();
        var (userId, email, clientToken) = await RegisterClientAsync(client);
        var staffClient = _factory.CreateClient();
        var staffToken = await SeedStaffAndLoginAsync(staffClient);
        Authed(staffClient, staffToken);

        await EarnPointsByCompletingAsync(staffClient, userId, email, points: 5);
        await EnsureProductStockAsync(productId: 1, price: 24.00m, stock: 20);

        Authed(client, clientToken);
        client.DefaultRequestHeaders.Add(SessionHeaderName, Guid.NewGuid().ToString());

        var notMultiple = await client.PostAsJsonAsync("/api/orders/checkout", new
        {
            Email = email,
            RedeemPoints = 5,
            Items = new[] { new { ProductId = 1, Quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, notMultiple.StatusCode);

        var overBalance = await client.PostAsJsonAsync("/api/orders/checkout", new
        {
            Email = email,
            RedeemPoints = 10,
            Items = new[] { new { ProductId = 1, Quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, overBalance.StatusCode);

        var loyalty = await client.GetAsync("/api/account/loyalty");
        using var balJson = JsonDocument.Parse(await loyalty.Content.ReadAsStringAsync());
        Assert.Equal(5, ReadBalance(balJson.RootElement));
    }

    [Fact]
    public async Task GuestCheckout_WithoutRedeem_StillWorks()
    {
        await EnsureProductStockAsync(productId: 1, price: 24.00m, stock: 20);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(SessionHeaderName, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/orders/checkout", new
        {
            Email = $"guest-{Guid.NewGuid():N}@example.com",
            Items = new[] { new { ProductId = 1, Quantity = 1 } },
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Unexpected {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
}
