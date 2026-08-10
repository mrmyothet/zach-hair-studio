using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Orders;

namespace ZachHairStudio.Api.Tests.Features.Account;

/// <summary>
/// ACCT-03 / ACCT-06 / D-04 / D-08 — ownership-gated order history + claim-by-email
/// over real SQL Server (RESEARCH Pitfall 1 — no InMemory). Anonymous JSON + raw reads
/// so RED compiles before AccountController / Claim DTOs exist.
/// </summary>
public class AccountOrdersTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "AccountOrdersTests-signing-key-at-least-32-bytes-hmac!!";
    private const string TestPassword = "AccountOrdersTest!2026Pw";

    private readonly WebApplicationFactory<Program> _factory;

    public AccountOrdersTests(SqlServerWebApplicationFactory factory)
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
        email ??= $"order-client-{Guid.NewGuid():N}@example.com";

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
        var email = $"order-staff-{Guid.NewGuid():N}@example.com";

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

    private async Task<int> SeedGuestOrderAsync(
        string email,
        DateTimeOffset placedAt,
        string? customerName = "Guest Shopper",
        decimal total = 40m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var order = new Order
        {
            ClientId = null,
            Status = OrderStatus.Pending,
            TotalAmount = total,
            Email = email,
            CustomerName = customerName,
            StripeSessionId = $"fake-guest-{Guid.NewGuid():N}",
            PlacedAtUtc = placedAt,
            Items =
            [
                new OrderItem
                {
                    ProductId = 1,
                    ProductName = "Serum",
                    UnitPrice = total,
                    Quantity = 1,
                    LineTotal = total,
                },
            ],
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private static async Task PostClaimAsync(HttpClient client, bool confirm)
    {
        var response = await client.PostAsJsonAsync("/api/account/claim", new { Confirm = confirm });
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected claim success, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task GetOrders_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/account/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_StaffJwt_Returns403()
    {
        var client = _factory.CreateClient();
        var (_, token) = await SeedStaffAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/account/orders");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClaimPreview_IncludesGuestOrdersMatchingEmail()
    {
        var client = _factory.CreateClient();
        var email = $"order-preview-{Guid.NewGuid():N}@example.com";
        var orderId = await SeedGuestOrderAsync(email, DateTimeOffset.UtcNow.AddHours(-2));
        await SeedGuestOrderAsync($"other-{Guid.NewGuid():N}@example.com", DateTimeOffset.UtcNow.AddHours(-1));

        var (_, _, token) = await RegisterClientAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/account/claim-preview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.True(root.TryGetProperty("orders", out var orders)
            || root.TryGetProperty("Orders", out orders),
            "claim-preview must expose orders array");
        Assert.Equal(1, orders.GetArrayLength());

        var id = orders[0].TryGetProperty("id", out var idProp)
            ? idProp.GetInt32()
            : orders[0].GetProperty("Id").GetInt32();
        Assert.Equal(orderId, id);
    }

    [Fact]
    public async Task ClaimConfirmTrue_AttachesGuestOrders_ListShowsOnlyOwnedDateDesc()
    {
        var client = _factory.CreateClient();
        var emailA = $"order-a-{Guid.NewGuid():N}@example.com";
        var emailB = $"order-b-{Guid.NewGuid():N}@example.com";

        var older = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        var olderId = await SeedGuestOrderAsync(emailA, older, "Alice", 25m);
        var newerId = await SeedGuestOrderAsync(emailA, newer, "Alice", 55m);
        var bId = await SeedGuestOrderAsync(emailB, DateTimeOffset.UtcNow.AddDays(-1), "Bob", 30m);

        var (_, _, tokenA) = await RegisterClientAsync(client, emailA);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        await PostClaimAsync(client, confirm: true);

        var listResponse = await client.GetAsync("/api/account/orders");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var list = listJson.RootElement;
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        Assert.Equal(2, list.GetArrayLength());

        var firstId = list[0].TryGetProperty("id", out var id0) ? id0.GetInt32() : list[0].GetProperty("Id").GetInt32();
        var secondId = list[1].TryGetProperty("id", out var id1) ? id1.GetInt32() : list[1].GetProperty("Id").GetInt32();
        Assert.Equal(newerId, firstId);
        Assert.Equal(olderId, secondId);
        Assert.DoesNotContain(bId, new[] { firstId, secondId });
    }

    [Fact]
    public async Task ClaimConfirmFalse_LeavesClientIdNull_OrdersListEmpty()
    {
        var client = _factory.CreateClient();
        var email = $"order-skip-{Guid.NewGuid():N}@example.com";
        await SeedGuestOrderAsync(email, DateTimeOffset.UtcNow.AddHours(-3));

        var (_, _, token) = await RegisterClientAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await PostClaimAsync(client, confirm: false);

        var listResponse = await client.GetAsync("/api/account/orders");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, listJson.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task GetOrder_CrossClient_Returns404WithoutLeakingPii()
    {
        var client = _factory.CreateClient();
        var emailA = $"order-idor-a-{Guid.NewGuid():N}@example.com";
        var emailB = $"order-idor-b-{Guid.NewGuid():N}@example.com";

        var bOrderId = await SeedGuestOrderAsync(emailB, DateTimeOffset.UtcNow.AddHours(-5), "Secret Buyer", 99m);

        var (_, _, tokenB) = await RegisterClientAsync(client, emailB);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        await PostClaimAsync(client, confirm: true);

        client.DefaultRequestHeaders.Authorization = null;
        var (_, _, tokenA) = await RegisterClientAsync(client, emailA);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var response = await client.GetAsync($"/api/account/orders/{bOrderId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(emailB, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret Buyer", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Claim_DoesNotAttachDifferentEmailGuestOrders()
    {
        var client = _factory.CreateClient();
        var emailA = $"order-exact-a-{Guid.NewGuid():N}@example.com";
        var emailOther = $"order-exact-other-{Guid.NewGuid():N}@example.com";

        await SeedGuestOrderAsync(emailA, DateTimeOffset.UtcNow.AddHours(-4));
        var otherId = await SeedGuestOrderAsync(emailOther, DateTimeOffset.UtcNow.AddHours(-3));

        var (_, _, token) = await RegisterClientAsync(client, emailA);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await PostClaimAsync(client, confirm: true);

        var listResponse = await client.GetAsync("/api/account/orders");
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var ids = listJson.RootElement.EnumerateArray()
            .Select(o => o.TryGetProperty("id", out var id) ? id.GetInt32() : o.GetProperty("Id").GetInt32())
            .ToList();
        Assert.DoesNotContain(otherId, ids);
        Assert.Single(ids);
    }
}
