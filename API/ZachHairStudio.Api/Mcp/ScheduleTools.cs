using System.ComponentModel;
using ModelContextProtocol.Server;
using ZachHairStudio.Api.Features.Chat;

namespace ZachHairStudio.Api.Mcp;

// Plain (non-static) class: WithTools<T>() takes a generic type argument, and C#
// forbids static classes there. Members below are still static, matching the SDK's
// reference pattern for [McpServerToolType] tool classes.
[McpServerToolType]
public class ScheduleTools
{
    [McpServerTool(Name = "get_appointment_slots", ReadOnly = true)]
    [Description("Lists open appointment start times for a service on a given date. " +
        "Omitting the stylist argument returns the union of all active stylists' openings.")]
    public static Task<string> GetAppointmentSlots(
        SalonChatTools tools,
        [Description("The service catalog id to check availability for.")] int serviceId,
        [Description("The date to check, formatted yyyy-MM-dd, interpreted in salon local time.")] string date,
        [Description("Optional stylist id. Omit to return the any-stylist union view.")] int? stylistId = null) =>
        tools.GetAppointmentSlotsAsync(serviceId, date, stylistId);
}
