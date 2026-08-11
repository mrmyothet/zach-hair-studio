using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ZachHairStudio.Api.Tests.Features.Launch;

/// <summary>
/// LAUNCH-05 / D-08 — auth endpoints are fixed-window rate limited (10/min per IP).
/// Uses CustomWebApplicationFactory (InMemory) — no LocalDB required.
/// </summary>
public class RateLimitTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string TestSigningKey = "RateLimitTests-signing-key-at-least-32-bytes-hmac!!!";

    private readonly CustomWebApplicationFactory _rawFactory;

    public RateLimitTests(CustomWebApplicationFactory factory)
    {
        _rawFactory = factory;
    }

    [Fact]
    public async Task AuthLogin_ExceedingFixedWindow_Returns429()
    {
        var factory = _rawFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
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

        var client = factory.CreateClient();
        HttpStatusCode? saw429 = null;

        // Policy permit limit is 10/min — burst past it with invalid logins (still count).
        for (var i = 0; i < 15; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { Email = $"burst-{i}@example.com", Password = "NotTheRealPassword!1" });

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                saw429 = response.StatusCode;
                break;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, saw429);
    }
}
