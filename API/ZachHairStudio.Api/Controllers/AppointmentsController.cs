using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly SlotService _slotService;
    private readonly AppointmentsService _appointmentsService;
    private readonly IValidator<AppointmentCreateDto> _createValidator;

    public AppointmentsController(
        SlotService slotService,
        AppointmentsService appointmentsService,
        IValidator<AppointmentCreateDto> createValidator)
    {
        _slotService = slotService;
        _appointmentsService = appointmentsService;
        _createValidator = createValidator;
    }

    [HttpGet("slots")]
    public async Task<ActionResult<IReadOnlyList<OpenSlotDto>>> GetSlots(
        [FromQuery] int serviceId,
        [FromQuery] int? stylistId,
        [FromQuery] DateOnly date)
    {
        var slots = await _slotService.GetOpenSlotsAsync(serviceId, stylistId, date);
        return Ok(slots);
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentResponseDto>> CreateAppointment([FromBody] AppointmentCreateDto request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var result = await _appointmentsService.CreateAsync(request);

        if (result.IsValidationError())
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return ValidationProblem(ModelState);
        }

        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Slot unavailable",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        // The 2601/2627 unique-index violation surfaces here as a clean 409 in ALL
        // environments (including Development) — no SqlException stack trace ever
        // leaks in a 500 (T-02-07, V7).
        if (result.IsDuplicateRecord())
        {
            return Conflict(new ProblemDetails
            {
                Title = "Slot taken",
                Detail = result.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }

        return Created($"/api/appointments/{result.Data.Id}", result.Data);
    }
}
