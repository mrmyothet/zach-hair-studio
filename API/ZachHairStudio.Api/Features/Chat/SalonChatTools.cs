using System.Globalization;
using System.Text.Json;
using OpenAI.Chat;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Services;
using ZachHairStudio.Shared.Features.Stylists;

namespace ZachHairStudio.Api.Features.Chat;

public sealed class SalonChatTools
{
    public const string ListServicesName = "list_services";
    public const string ListStylistsName = "list_stylists";
    public const string ListBookingsName = "list_bookings";
    public const string GetAppointmentSlotsName = "get_appointment_slots";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ChatTool> Definitions { get; } =
    [
        Function(ListServicesName, "Lists the salon's active services with their exact ids, names, durations, and prices.", EmptySchema),
        Function(ListStylistsName, "Lists active salon stylists with their exact ids and names.", EmptySchema),
        Function(ListBookingsName, "Lists staff-visible bookings for an inclusive salon-local date range.", """
            {
              "type": "object",
              "properties": {
                "from": { "type": "string", "description": "Start date in yyyy-MM-dd format." },
                "to": { "type": "string", "description": "End date in yyyy-MM-dd format. Defaults to from." }
              },
              "required": ["from"],
              "additionalProperties": false
            }
            """),
        Function(GetAppointmentSlotsName, "Lists open appointment starts for an exact service id and date, optionally for one exact stylist id.", """
            {
              "type": "object",
              "properties": {
                "serviceId": { "type": "integer", "minimum": 1, "description": "Exact id returned by list_services." },
                "date": { "type": "string", "description": "Salon-local date in yyyy-MM-dd format." },
                "stylistId": { "type": ["integer", "null"], "minimum": 1, "description": "Optional exact id returned by list_stylists." }
              },
              "required": ["serviceId", "date"],
              "additionalProperties": false
            }
            """),
    ];

    private const string EmptySchema = """
        { "type": "object", "properties": {}, "additionalProperties": false }
        """;

    private readonly ServicesService _services;
    private readonly StylistsService _stylists;
    private readonly AppointmentsService _appointments;
    private readonly SlotService _slots;

    public SalonChatTools(
        ServicesService services,
        StylistsService stylists,
        AppointmentsService appointments,
        SlotService slots)
    {
        _services = services;
        _stylists = stylists;
        _appointments = appointments;
        _slots = slots;
    }

    public async Task<string> ExecuteAsync(string name, BinaryData arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(arguments);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Error("Tool arguments must be a JSON object.");
            }

            string[]? allowedArguments = name switch
            {
                ListServicesName or ListStylistsName => [],
                ListBookingsName => ["from", "to"],
                GetAppointmentSlotsName => ["serviceId", "date", "stylistId"],
                _ => null,
            };
            if (allowedArguments is not null
                && root.EnumerateObject().Any(property => !allowedArguments.Contains(property.Name)))
            {
                return Error("Tool arguments contained an unknown property.");
            }

            return name switch
            {
                ListServicesName => await ListServicesAsync(),
                ListStylistsName => await ListStylistsAsync(),
                ListBookingsName => await ListBookingsAsync(root),
                GetAppointmentSlotsName => await GetAppointmentSlotsAsync(root),
                _ => Error($"Unknown tool '{name}'."),
            };
        }
        catch (JsonException)
        {
            return Error("Tool arguments were not valid JSON.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Error("The salon data operation could not be completed.");
        }
    }

    public async Task<string> GetAppointmentSlotsAsync(int serviceId, string date, int? stylistId = null)
    {
        if (serviceId <= 0)
        {
            return Error("serviceId must be a positive integer returned by list_services.");
        }

        if (stylistId <= 0)
        {
            return Error("stylistId must be a positive integer returned by list_stylists.");
        }

        if (!TryDate(date, out var parsedDate))
        {
            return Error($"Invalid date '{date}'. Expected yyyy-MM-dd.");
        }

        var slots = await _slots.GetOpenSlotsAsync(serviceId, stylistId, parsedDate);
        return JsonSerializer.Serialize(new
        {
            date,
            serviceId,
            stylistId,
            count = slots.Count,
            slots,
        }, JsonOptions);
    }

    private async Task<string> ListServicesAsync()
    {
        var services = await _services.GetServicesAsync();
        return JsonSerializer.Serialize(new
        {
            services = services.Select(service => new
            {
                service.Id,
                service.Name,
                service.Slug,
                service.DurationMinutes,
                service.Price,
                service.ShortDescription,
            }),
        }, JsonOptions);
    }

    private async Task<string> ListStylistsAsync()
    {
        var stylists = await _stylists.GetActiveStylistsAsync();
        return JsonSerializer.Serialize(new
        {
            stylists = stylists.Select(stylist => new
            {
                stylist.Id,
                stylist.Name,
                stylist.Slug,
            }),
        }, JsonOptions);
    }

    private async Task<string> ListBookingsAsync(JsonElement arguments)
    {
        if (!TryRequiredString(arguments, "from", out var fromText) || !TryDate(fromText, out var from))
        {
            return Error("from is required in yyyy-MM-dd format.");
        }

        var toText = arguments.TryGetProperty("to", out var toElement) && toElement.ValueKind == JsonValueKind.String
            ? toElement.GetString()!
            : fromText;
        if (!TryDate(toText, out var to) || to < from || to.DayNumber - from.DayNumber > 31)
        {
            return Error("to must be on or after from, in yyyy-MM-dd format, with a maximum 31-day range.");
        }

        var result = await _appointments.ListByDateRangeAsync(from, to, status: null);
        if (!result.IsSuccess)
        {
            return Error(result.Message);
        }

        return JsonSerializer.Serialize(new
        {
            from = fromText,
            to = toText,
            bookings = result.Data.Select(appointment => new
            {
                appointment.Id,
                appointment.StartsAt,
                clientName = $"{appointment.FirstName} {appointment.LastName}",
                appointment.ServiceName,
                appointment.StylistName,
                appointment.Status,
            }),
        }, JsonOptions);
    }

    private async Task<string> GetAppointmentSlotsAsync(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("serviceId", out var serviceElement)
            || !serviceElement.TryGetInt32(out var serviceId))
        {
            return Error("serviceId is required and must be an integer returned by list_services.");
        }

        if (!TryRequiredString(arguments, "date", out var date))
        {
            return Error("date is required in yyyy-MM-dd format.");
        }

        int? stylistId = null;
        if (arguments.TryGetProperty("stylistId", out var stylistElement)
            && stylistElement.ValueKind != JsonValueKind.Null)
        {
            if (!stylistElement.TryGetInt32(out var parsedStylistId))
            {
                return Error("stylistId must be an integer returned by list_stylists.");
            }
            stylistId = parsedStylistId;
        }

        return await GetAppointmentSlotsAsync(serviceId, date, stylistId);
    }

    private static ChatTool Function(string name, string description, string schema) =>
        ChatTool.CreateFunctionTool(name, description, BinaryData.FromString(schema));

    private static bool TryDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryRequiredString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string Error(string message) =>
        JsonSerializer.Serialize(new { error = message }, JsonOptions);
}
