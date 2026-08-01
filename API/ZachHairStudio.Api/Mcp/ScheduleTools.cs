using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ModelContextProtocol.Server;
using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Mcp;

// Plain (non-static) class: WithTools<T>() takes a generic type argument, and C#
// forbids static classes there. Members below are still static, matching the SDK's
// reference pattern for [McpServerToolType] tool classes.
[McpServerToolType]
public class ScheduleTools
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [McpServerTool(Name = "get_appointment_slots", ReadOnly = true)]
    [Description("Lists open appointment start times for a service on a given date. " +
        "Omitting the stylist argument returns the union of all active stylists' openings.")]
    public static async Task<string> GetAppointmentSlots(
        SlotService slotService,
        [Description("The service catalog id to check availability for.")] int serviceId,
        [Description("The date to check, formatted yyyy-MM-dd, interpreted in salon local time.")] string date,
        [Description("Optional stylist id. Omit to return the any-stylist union view.")] int? stylistId = null)
    {
        if (!DateOnly.TryParseExact(
                date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return JsonSerializer.Serialize(
                new { error = $"Invalid date '{date}'. Expected format: yyyy-MM-dd." }, SerializerOptions);
        }

        var slots = await slotService.GetOpenSlotsAsync(serviceId, stylistId, parsedDate);

        return JsonSerializer.Serialize(
            new
            {
                date = parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                serviceId,
                stylistId,
                count = slots.Count,
                slots,
            },
            SerializerOptions);
    }
}
