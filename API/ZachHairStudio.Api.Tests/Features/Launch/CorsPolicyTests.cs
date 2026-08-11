using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ZachHairStudio.Api.Tests.Features.Launch;

/// <summary>
/// LAUNCH-02 / D-01 — Production CORS must use Cors:Origins allowlist (no AllowAnyOrigin).
/// Uses CustomWebApplicationFactory (InMemory) so this host does not need LocalDB.
/// </summary>
public class CorsPolicyTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string TestSigningKey = "CorsPolicyTests-signing-key-at-least-32-bytes-hmac!!";

    private readonly CustomWebApplicationFactory _rawFactory;

    public CorsPolicyTests(CustomWebApplicationFactory factory)
    {
        _rawFactory = factory;
    }

    [Fact]
    public async Task TestingEnvironment_AllowsAnyOrigin_ViaPreflight()
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
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/stylists");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Production_WithConfiguredOrigins_AllowsListedOrigin_Only()
    {
        var factory = _rawFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = TestSigningKey,
                    ["Jwt:Issuer"] = "ZachHairStudioTests",
                    ["Jwt:Audience"] = "ZachHairStudioTestsDashboard",
                    ["Cors:Origins"] = "https://landing.example;https://dashboard.example",
                    // Production path skips Migrate but still needs a connection for DI;
                    // CustomWebApplicationFactory replaces DbContext with InMemory.
                    ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\none;Database=CorsPolicyTests;",
                });
            });
        });

        var client = factory.CreateClient();

        using var allowed = new HttpRequestMessage(HttpMethod.Options, "/api/stylists");
        allowed.Headers.Add("Origin", "https://landing.example");
        allowed.Headers.Add("Access-Control-Request-Method", "GET");
        var allowedResponse = await client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.NoContent, allowedResponse.StatusCode);
        Assert.Equal(
            "https://landing.example",
            allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var denied = new HttpRequestMessage(HttpMethod.Options, "/api/stylists");
        denied.Headers.Add("Origin", "https://evil.example");
        denied.Headers.Add("Access-Control-Request-Method", "GET");
        var deniedResponse = await client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.NoContent, deniedResponse.StatusCode);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
