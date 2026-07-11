using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Controllers;

/// <summary>
/// Staff-only schedule endpoints (DASH-01..04). Class-level [Authorize] is the DASH-05
/// gate — every action requires a valid staff JWT; the public AppointmentsController
/// stays anonymous and untouched.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScheduleController : ControllerBase
{
    private readonly AppointmentsService _appointmentsService;
    private readonly IValidator<AppointmentStatusUpdateDto> _statusValidator;

    public ScheduleController(
        AppointmentsService appointmentsService,
        IValidator<AppointmentStatusUpdateDto> statusValidator)
    {
        _appointmentsService = appointmentsService;
        _statusValidator = statusValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponseDto>>> GetRange(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] AppointmentStatus? status)
    {
        var result = await _appointmentsService.ListByDateRangeAsync(from, to, status);

        if (result.IsValidationError())
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Data);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AppointmentResponseDto>> GetById(int id)
    {
        var result = await _appointmentsService.GetByIdAsync(id);

        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Appointment not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        return Ok(result.Data);
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult<AppointmentResponseDto>> UpdateStatus(int id, [FromBody] AppointmentStatusUpdateDto request)
    {
        var validation = await _statusValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        // The acting staff name comes from the authenticated principal's claims, never
        // the request body (it is not a client-suppliable field).
        var displayName = User.FindFirst(JwtTokenService.DisplayNameClaimType)?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "Unknown";

        var result = await _appointmentsService.UpdateStatusAsync(id, request.NewStatus, displayName);

        if (result.IsValidationError())
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return ValidationProblem(ModelState);
        }

        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Appointment not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        return Ok(result.Data);
    }
}
