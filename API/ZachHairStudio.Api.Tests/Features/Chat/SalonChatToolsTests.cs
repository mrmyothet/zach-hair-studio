using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ZachHairStudio.Api.Features.Chat;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;

namespace ZachHairStudio.Api.Tests.Features.Chat;

public class SalonChatToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SalonChatToolsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CatalogTools_ReturnActiveCatalogEntries()
    {
        using var scope = _factory.Services.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<SalonChatTools>();

        using var services = JsonDocument.Parse(await tools.ExecuteAsync(
            SalonChatTools.ListServicesName,
            BinaryData.FromString("{}"),
            CancellationToken.None));
        using var stylists = JsonDocument.Parse(await tools.ExecuteAsync(
            SalonChatTools.ListStylistsName,
            BinaryData.FromString("{}"),
            CancellationToken.None));

        Assert.Contains(
            services.RootElement.GetProperty("services").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "Precision Cut");
        Assert.Contains(
            stylists.RootElement.GetProperty("stylists").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "Zin Min");
    }

    [Fact]
    public async Task ListBookings_ExcludesContactDetails()
    {
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.Appointments.Add(new Appointment
        {
            ServiceId = 1,
            StylistId = 1,
            StartsAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(6.5)),
            FirstName = "Jane",
            LastName = "Doe",
            Email = "private@example.com",
            Phone = "+95 123456",
        });
        await db.SaveChangesAsync();

        var tools = scope.ServiceProvider.GetRequiredService<SalonChatTools>();
        var result = await tools.ExecuteAsync(
            SalonChatTools.ListBookingsName,
            BinaryData.FromString($$"""{ "from": "{{date:yyyy-MM-dd}}" }"""),
            CancellationToken.None);

        Assert.Contains("Jane Doe", result);
        Assert.DoesNotContain("private@example.com", result);
        Assert.DoesNotContain("+95 123456", result);
        Assert.DoesNotContain("email", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsStructuredErrorsForMalformedArguments()
    {
        using var scope = _factory.Services.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<SalonChatTools>();

        var malformed = await tools.ExecuteAsync(
            SalonChatTools.GetAppointmentSlotsName,
            BinaryData.FromString("not-json"),
            CancellationToken.None);
        var invalidDate = await tools.ExecuteAsync(
            SalonChatTools.GetAppointmentSlotsName,
            BinaryData.FromString("""{ "serviceId": 1, "date": "tomorrow" }"""),
            CancellationToken.None);
        var unknownProperty = await tools.ExecuteAsync(
            SalonChatTools.ListServicesName,
            BinaryData.FromString("""{ "unexpected": true }"""),
            CancellationToken.None);

        Assert.Equal("Tool arguments were not valid JSON.", Error(malformed));
        Assert.Contains("Expected yyyy-MM-dd", Error(invalidDate));
        Assert.Equal("Tool arguments contained an unknown property.", Error(unknownProperty));
    }

    [Fact]
    public async Task GetAppointmentSlots_ReturnsCanonicalSlotPayload()
    {
        using var scope = _factory.Services.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<SalonChatTools>();
        var date = NextDay(DayOfWeek.Tuesday);

        var result = await tools.ExecuteAsync(
            SalonChatTools.GetAppointmentSlotsName,
            BinaryData.FromString($$"""{ "serviceId": 1, "date": "{{date:yyyy-MM-dd}}", "stylistId": 1 }"""),
            CancellationToken.None);

        using var json = JsonDocument.Parse(result);
        Assert.Equal(1, json.RootElement.GetProperty("serviceId").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("stylistId").GetInt32());
        Assert.True(json.RootElement.GetProperty("count").GetInt32() > 0);
        Assert.All(
            json.RootElement.GetProperty("slots").EnumerateArray(),
            slot => Assert.Equal(1, slot.GetProperty("stylistId").GetInt32()));
    }

    private static string Error(string result)
    {
        using var json = JsonDocument.Parse(result);
        return json.RootElement.GetProperty("error").GetString()!;
    }

    private static DateOnly NextDay(DayOfWeek dayOfWeek)
    {
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        while (date.DayOfWeek != dayOfWeek)
        {
            date = date.AddDays(1);
        }
        return date;
    }
}
