using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Controllers;

/// <summary>
/// Any-authenticated-staff availability write endpoints (MGMT-02, D-13). Class-
/// level [Authorize] with NO Owner-role restriction and NO per-stylist ownership
/// check — mirrors ScheduleController's any-staff gate, not ServicesController's
/// Owner-only gate. Writes go through AvailabilityService directly into the
/// same StylistWorkingHours/StylistTimeOff tables SlotService reads (D-08); no
/// conflict check yet (arrives in Plan 05 / MGMT-03).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AvailabilityController : ControllerBase
{
    private readonly AvailabilityService _availabilityService;
    private readonly IValidator<WorkingHoursReplaceDto> _workingHoursValidator;
    private readonly IValidator<TimeOffCreateDto> _timeOffValidator;

    public AvailabilityController(
        AvailabilityService availabilityService,
        IValidator<WorkingHoursReplaceDto> workingHoursValidator,
        IValidator<TimeOffCreateDto> timeOffValidator)
    {
        _availabilityService = availabilityService;
        _workingHoursValidator = workingHoursValidator;
        _timeOffValidator = timeOffValidator;
    }

    [HttpGet("{stylistId}")]
    [ProducesResponseType(typeof(AvailabilityResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailability(int stylistId)
    {
        var result = await _availabilityService.GetAvailabilityAsync(stylistId);

        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Stylist not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        return Ok(result.Data);
    }

    [HttpPut("{stylistId}/working-hours")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReplaceWorkingHours(int stylistId, [FromBody] WorkingHoursReplaceDto request)
    {
        var validation = await _workingHoursValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            AddToModelState(validation);
            return ValidationProblem(ModelState);
        }

        var result = await _availabilityService.ReplaceWorkingHoursAsync(stylistId, request);

        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Stylist not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        if (result.IsValidationError())
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return ValidationProblem(ModelState);
        }

        if (result.IsConflict())
        {
            return ConflictProblem(result.Message, result.Conflicts);
        }

        if (result.IsSystemError())
        {
            return InconsistentDataProblem(result.Message);
        }

        return NoContent();
    }

    [HttpPost("{stylistId}/time-off")]
    [ProducesResponseType(typeof(StylistTimeOff), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddTimeOff(int stylistId, [FromBody] TimeOffCreateDto request)
    {
        var validation = await _timeOffValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            AddToModelState(validation);
            return ValidationProblem(ModelState);
        }

        var result = await _availabilityService.AddTimeOffAsync(stylistId, request);

        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Stylist not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        if (result.IsValidationError())
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return ValidationProblem(ModelState);
        }

        if (result.IsConflict())
        {
            return ConflictProblem(result.Message, result.Conflicts);
        }

        if (result.IsSystemError())
        {
            return InconsistentDataProblem(result.Message);
        }

        return Created($"/api/availability/{stylistId}/time-off/{result.Data.Id}", result.Data);
    }

    [HttpDelete("{stylistId}/time-off/{timeOffId}")]
    public async Task<IActionResult> RemoveTimeOff(int stylistId, int timeOffId)
    {
        var result = await _availabilityService.RemoveTimeOffAsync(stylistId, timeOffId);

        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Time off not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        if (result.IsSystemError())
        {
            return InconsistentDataProblem(result.Message);
        }

        return NoContent();
    }

    private void AddToModelState(ValidationResult validation)
    {
        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
    }

    /// <summary>Controlled 500 for a stylist id that resolves in one query but not
    /// another — mirrors ScheduleController's InconsistentDataProblem helper.</summary>
    private ObjectResult InconsistentDataProblem(string detail) =>
        Problem(
            title: "Availability data is inconsistent.",
            detail: detail,
            statusCode: StatusCodes.Status500InternalServerError);

    /// <summary>
    /// The hard-block conflict response (MGMT-03, D-09): ProblemDetails plus a
    /// "conflicts" extension carrying the D-11-scoped conflict list, extending
    /// AppointmentsController's Conflict(...) 409 pattern.
    /// </summary>
    private ObjectResult ConflictProblem(string detail, IReadOnlyList<AvailabilityConflictDto>? conflicts)
    {
        var problem = new ProblemDetails
        {
            Title = "Can't Save — Conflicting Appointments",
            Detail = detail,
            Status = StatusCodes.Status409Conflict,
        };
        problem.Extensions["conflicts"] = conflicts ?? Array.Empty<AvailabilityConflictDto>();
        return Conflict(problem);
    }
}
