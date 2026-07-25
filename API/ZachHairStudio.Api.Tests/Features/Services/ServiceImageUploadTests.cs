using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Services;

/// <summary>
/// Proves MGMT-01's image-upload endpoint (POST /api/services/{id}/image): an Owner
/// with an allowed image type under the size cap gets ImageUrl set to a served
/// "/uploads/services/..." path; a disallowed content-type or an oversized payload is
/// rejected with 400 before any write; re-uploading replaces ImageUrl with the newest
/// file. The multipart field name ("Image") matches ServiceImageUploadDto's IFormFile
/// property so [FromForm] model binding resolves it.
/// </summary>
public class ServiceImageUploadTests : IClassFixture<SqlServerWebApplicationFactory>
{
    private const string TestSigningKey = "ServiceImageUploadTests-signing-key-32bytes-hmac-sha!!";
    private const string TestPassword = "ServiceImageUploadTest!2026Pw";
    private const int SeededServiceId = 1;
    private const string SeededServiceSlug = "precision-cut";
    private const long MaxAllowedBytes = 5 * 1024 * 1024;

    private readonly WebApplicationFactory<Program> _factory;

    public ServiceImageUploadTests(SqlServerWebApplicationFactory factory)
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

    private async Task<(string Email, string Password)> SeedStaffUserAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";

        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(role));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = $"{role} Tester",
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(user, TestPassword);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, role);

        return (email, TestPassword);
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    private async Task<HttpClient> CreateOwnerClientAsync()
    {
        var (email, password) = await SeedStaffUserAsync(StaffRoles.Owner);
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static MultipartFormDataContent BuildImageContent(byte[] bytes, string contentType, string fileName = "upload.bin")
    {
        var content = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(byteContent, "Image", fileName);
        return content;
    }

    private static async Task<ServiceResponseDto> GetSeededServiceAsync(HttpClient client)
    {
        var response = await client.GetAsync($"/api/services/{SeededServiceSlug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var service = await response.Content.ReadFromJsonAsync<ServiceResponseDto>();
        Assert.NotNull(service);
        return service!;
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public async Task UploadImage_OwnerWithAllowedTypeUnderSizeCap_Returns2xxAndSetsImageUrl(string contentType)
    {
        var client = await CreateOwnerClientAsync();
        var content = BuildImageContent([1, 2, 3, 4, 5, 6, 7, 8], contentType);

        var response = await client.PostAsync($"/api/services/{SeededServiceId}/image", content);

        Assert.True(
            (int)response.StatusCode is >= 200 and < 300,
            $"Expected 2xx, got {(int)response.StatusCode} {response.StatusCode}");

        var reloaded = await GetSeededServiceAsync(client);
        Assert.NotNull(reloaded.ImageUrl);
        Assert.StartsWith("/uploads/services/", reloaded.ImageUrl);
    }

    [Fact]
    public async Task UploadImage_DisallowedContentType_Returns400AndImageUrlUnchanged()
    {
        var client = await CreateOwnerClientAsync();
        var before = await GetSeededServiceAsync(client);
        var content = BuildImageContent([1, 2, 3, 4], "text/plain");

        var response = await client.PostAsync($"/api/services/{SeededServiceId}/image", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var after = await GetSeededServiceAsync(client);
        Assert.Equal(before.ImageUrl, after.ImageUrl);
    }

    [Fact]
    public async Task UploadImage_OversizedFile_Returns400AndImageUrlUnchanged()
    {
        var client = await CreateOwnerClientAsync();
        var before = await GetSeededServiceAsync(client);
        var oversizedBytes = new byte[MaxAllowedBytes + 1];
        var content = BuildImageContent(oversizedBytes, "image/jpeg");

        var response = await client.PostAsync($"/api/services/{SeededServiceId}/image", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var after = await GetSeededServiceAsync(client);
        Assert.Equal(before.ImageUrl, after.ImageUrl);
    }

    [Fact]
    public async Task UploadImage_UploadedTwice_ImageUrlReflectsNewestFile()
    {
        var client = await CreateOwnerClientAsync();

        var firstResponse = await client.PostAsync(
            $"/api/services/{SeededServiceId}/image",
            BuildImageContent([1, 2, 3, 4], "image/jpeg"));
        Assert.True((int)firstResponse.StatusCode is >= 200 and < 300);
        var afterFirst = await GetSeededServiceAsync(client);
        var firstImageUrl = afterFirst.ImageUrl;
        Assert.NotNull(firstImageUrl);

        var secondResponse = await client.PostAsync(
            $"/api/services/{SeededServiceId}/image",
            BuildImageContent([9, 9, 9, 9], "image/png"));
        Assert.True((int)secondResponse.StatusCode is >= 200 and < 300);
        var afterSecond = await GetSeededServiceAsync(client);

        Assert.NotNull(afterSecond.ImageUrl);
        Assert.StartsWith("/uploads/services/", afterSecond.ImageUrl);
        Assert.NotEqual(firstImageUrl, afterSecond.ImageUrl);
    }
}
