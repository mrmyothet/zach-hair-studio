using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Account;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Orders;

namespace ZachHairStudio.Api.Controllers;

/// <summary>
/// Client-role account history + claim + self-service cancel/reschedule
/// (ACCT-02/03/04/06, D-04, D-08–D-12). Owner scope is resolved solely from
/// <see cref="ClaimTypes.NameIdentifier"/>.
/// </summary>
[ApiController]
[Route("api/account")]
[Authorize(Roles = StaffRoles.Client)]
public class AccountController : ControllerBase
{
    private readonly AccountService _accountService;
    private readonly AppointmentsService _appointmentsService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<ClaimRequestDto> _claimValidator;
    private readonly IValidator<ClientRescheduleRequestDto> _rescheduleValidator;

    public AccountController(
        AccountService accountService,
        AppointmentsService appointmentsService,
        UserManager<ApplicationUser> userManager,
        IValidator<ClaimRequestDto> claimValidator,
        IValidator<ClientRescheduleRequestDto> rescheduleValidator)
    {
        _accountService = accountService;
        _appointmentsService = appointmentsService;
        _userManager = userManager;
        _claimValidator = claimValidator;
        _rescheduleValidator = rescheduleValidator;
    }

    [HttpGet("bookings")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponseDto>>> ListBookings(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var error))
        {
            return error!;
        }

        var result = await _accountService.ListBookingsAsync(userId, cancellationToken);
        if (result.IsSystemError())
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Couldn't load bookings",
                Detail = result.Message,
                Status = StatusCodes.Status500InternalServerError,
            });
        }

        return Ok(result.Data);
    }

    [HttpGet("bookings/{id:int}")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponseDto>> GetBooking(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var error))
        {
            return error!;
        }

        var result = await _accountService.GetBookingAsync(userId, id, cancellationToken);
        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Appointment not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        if (result.IsSystemError())
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Couldn't load booking",
                Detail = result.Message,
                Status = StatusCodes.Status500InternalServerError,
            });
        }

        return Ok(result.Data);
    }

    [HttpPost("bookings/{id:int}/cancel")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponseDto>> CancelBooking(int id)
    {
        if (!TryGetUserId(out var userId, out var error))
        {
            return error!;
        }

        var actor = await ResolveActorDisplayNameAsync(userId);
        var result = await _appointmentsService.CancelForClientAsync(id, userId, actor);
        return MapAppointmentMutation(result, notFoundTitle: "Appointment not found");
    }

    [HttpPost("bookings/{id:int}/reschedule")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponseDto>> RescheduleBooking(
        int id,
        [FromBody] ClientRescheduleRequestDto request)
    {
        if (!TryGetUserId(out var userId, out var error))
        {
            return error!;
        }

        var validation = await _rescheduleValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var failure in validation.Errors)
            {
                ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var actor = await ResolveActorDisplayNameAsync(userId);
        var result = await _appointmentsService.RescheduleForClientAsync(id, userId, request, actor);
        return MapAppointmentMutation(result, notFoundTitle: "Appointment not found");
    }

    [HttpGet("orders")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderResponseDto>>> ListOrders(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var error))
        {
            return error!;
        }

        var result = await _accountService.ListOrdersAsync(userId, cancellationToken);
        return Ok(result.Data);
    }

    [HttpGet("orders/{id:int}")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var error))
        {
            return error!;
        }

        var result = await _accountService.GetOrderAsync(userId, id, cancellationToken);
        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Order not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        return Ok(result.Data);
    }

    [HttpGet("claim-preview")]
    [ProducesResponseType(typeof(ClaimPreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimPreviewDto>> ClaimPreview(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var error))
        {
            return error!;
        }

        var result = await _accountService.ClaimPreviewAsync(userId, cancellationToken);
        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "User not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        return Ok(result.Data);
    }

    [HttpPost("claim")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Claim(
        [FromBody] ClaimRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var error))
        {
            return error!;
        }

        var validation = await _claimValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var failure in validation.Errors)
            {
                ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var result = await _accountService.ClaimAsync(userId, request.Confirm, cancellationToken);
        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "User not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        return NoContent();
    }

    private ActionResult<AppointmentResponseDto> MapAppointmentMutation(
        Shared.Result<AppointmentResponseDto> result,
        string notFoundTitle)
    {
        if (result.IsValidationError())
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return ValidationProblem(ModelState);
        }

        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = notFoundTitle,
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        if (result.IsDuplicateRecord())
        {
            return Conflict(new ProblemDetails
            {
                Title = "Slot taken",
                Detail = result.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }

        if (result.IsSystemError())
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Couldn't update booking",
                Detail = result.Message,
                Status = StatusCodes.Status500InternalServerError,
            });
        }

        return Ok(result.Data);
    }

    private async Task<string> ResolveActorDisplayNameAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is not null && !string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName;
        }

        return "Client";
    }

    private bool TryGetUserId(out int userId, out ActionResult? error)
    {
        userId = 0;
        error = null;

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw, out userId))
        {
            error = Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Missing or invalid user identity.",
                Status = StatusCodes.Status401Unauthorized,
            });
            return false;
        }

        return true;
    }
}
