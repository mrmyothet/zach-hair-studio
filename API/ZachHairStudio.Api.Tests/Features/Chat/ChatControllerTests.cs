using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZachHairStudio.Api.Features.Chat;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Tests.Features.Chat;

public class ChatControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string TestSigningKey = "ChatControllerTests-signing-key-at-least-32-bytes-long!";
    private const string TestPassword = "ChatTest!2026Pw";

    private readonly WebApplicationFactory<Program> _factory;

    public ChatControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = TestSigningKey,
                    ["Jwt:Issuer"] = "ZachHairStudioTests",
                    ["Jwt:Audience"] = "ZachHairStudioTestsDashboard",
                }));
        });
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/chat",
            new { messages = new[] { new { role = "user", content = "List services." } } });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_InvalidHistory_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync(new StubAgent("unused"));

        var response = await client.PostAsJsonAsync(
            "/api/chat",
            new { messages = new[] { new { role = "assistant", content = "Previous answer." } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Authenticated_ReturnsAgentReply()
    {
        var client = await CreateAuthenticatedClientAsync(new StubAgent("Here are today's bookings."));

        var response = await client.PostAsJsonAsync(
            "/api/chat",
            new { messages = new[] { new { role = "user", content = "Who's booked today?" } } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Here are today's bookings.", json.RootElement.GetProperty("reply").GetString());
    }

    [Fact]
    public async Task Post_AgentFailure_ReturnsControlled502()
    {
        var client = await CreateAuthenticatedClientAsync(
            new StubAgent(exception: new ChatAgentException("internal model detail")));

        var response = await client.PostAsJsonAsync(
            "/api/chat",
            new { messages = new[] { new { role = "user", content = "List services." } } });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Please rephrase and try again.", body);
        Assert.DoesNotContain("internal model detail", body);
    }

    [Fact]
    public async Task Post_ProviderTimeout_ReturnsControlled504()
    {
        var client = await CreateAuthenticatedClientAsync(
            new StubAgent(exception: new OperationCanceledException()));

        var response = await client.PostAsJsonAsync(
            "/api/chat",
            new { messages = new[] { new { role = "user", content = "List services." } } });

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Contains("Please try again.", await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(ISalonChatAgent agent)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISalonChatAgent>();
                services.AddSingleton(agent);
            });
        });
        var email = $"chat-{Guid.NewGuid():N}@example.com";

        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (!await roles.RoleExistsAsync(StaffRoles.Staff))
            {
                await roles.CreateAsync(new IdentityRole<int>(StaffRoles.Staff));
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Chat Tester",
                EmailConfirmed = true,
            };
            var created = await users.CreateAsync(user, TestPassword);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
            await users.AddToRoleAsync(user, StaffRoles.Staff);
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            json.RootElement.GetProperty("token").GetString());
        return client;
    }

    private sealed class StubAgent(string reply = "", Exception? exception = null) : ISalonChatAgent
    {
        public Task<string> ReplyAsync(
            IReadOnlyList<ChatHistoryMessage> history,
            CancellationToken cancellationToken) =>
            exception is null ? Task.FromResult(reply) : Task.FromException<string>(exception);
    }
}
