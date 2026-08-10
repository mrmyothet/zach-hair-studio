using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Loyalty;
using ZachHairStudio.Shared.Features.Orders;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    /// <summary>Same constant as <see cref="CartsController.SessionHeaderName"/> (Plan 04 mirror lock).</summary>
    public const string SessionHeaderName = CartsController.SessionHeaderName;

    private readonly OrdersService _ordersService;
    private readonly IValidator<CheckoutRequestDto> _checkoutValidator;

    public OrdersController(
        OrdersService ordersService,
        IValidator<CheckoutRequestDto> checkoutValidator)
    {
        _ordersService = ordersService;
        _checkoutValidator = checkoutValidator;
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponseDto>> Checkout(
        [FromBody] CheckoutRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetSessionKey(out var sessionKey, out var error))
        {
            return error!;
        }

        if (!string.IsNullOrWhiteSpace(request.SessionKey)
            && !string.Equals(request.SessionKey.Trim(), sessionKey, StringComparison.Ordinal))
        {
            ModelState.AddModelError(
                nameof(request.SessionKey),
                $"SessionKey must match header '{SessionHeaderName}'.");
            return ValidationProblem(ModelState);
        }

        // Normalize: body SessionKey is optional mirror; header is authoritative.
        request.SessionKey = sessionKey;

        var validation = await _checkoutValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var failure in validation.Errors)
            {
                ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        TryGetClientUserId(out var clientUserId);

        var result = await _ordersService.CreateCheckoutAsync(request, clientUserId, cancellationToken);

        if (result.IsValidationError())
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return ValidationProblem(ModelState);
        }

        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Product not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        if (result.IsConflict())
        {
            return Conflict(new ProblemDetails
            {
                Title = "Insufficient stock",
                Detail = result.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }

        if (result.IsSystemError() || result.IsError || result.IsDataError())
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Checkout failed",
                Detail = result.Message,
                Status = StatusCodes.Status500InternalServerError,
            });
        }

        return Created($"/api/orders/{result.Data.OrderId}", result.Data);
    }

    /// <summary>
    /// Apply Points preview — catalog recompute + server loyalty dollars, no stock/Stripe (D-15).
    /// </summary>
    [HttpPost("checkout/quote")]
    [ProducesResponseType(typeof(LoyaltyQuoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoyaltyQuoteDto>> QuoteCheckout(
        [FromBody] CheckoutRequestDto request,
        CancellationToken cancellationToken)
    {
        // Quote does not require cart session (preview-only); SessionKey optional.
        var validation = await _checkoutValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var failure in validation.Errors)
            {
                ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        TryGetClientUserId(out var clientUserId);

        var result = await _ordersService.QuoteCheckoutAsync(request, clientUserId, cancellationToken);

        if (result.IsValidationError())
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return ValidationProblem(ModelState);
        }

        if (result.IsNotFound())
        {
            return NotFound(new ProblemDetails
            {
                Title = "Product not found",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }

        return Ok(result.Data);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _ordersService.GetByIdAsync(id, cancellationToken);
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

    /// <summary>
    /// Resolve optional Client JWT NameIdentifier. Staff/anonymous → null (guest path).
    /// Never trust body owner ids (T-07-20 / D-08).
    /// </summary>
    private bool TryGetClientUserId(out int? clientUserId)
    {
        clientUserId = null;

        if (User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (!User.IsInRole(StaffRoles.Client))
        {
            return false;
        }

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw, out var userId))
        {
            return false;
        }

        clientUserId = userId;
        return true;
    }

    private bool TryGetSessionKey(out string sessionKey, out ActionResult? error)
    {
        sessionKey = string.Empty;
        error = null;

        if (!Request.Headers.TryGetValue(SessionHeaderName, out var values)
            || string.IsNullOrWhiteSpace(values.FirstOrDefault()))
        {
            error = BadRequest(new ProblemDetails
            {
                Title = "Missing cart session",
                Detail = $"Header '{SessionHeaderName}' is required.",
                Status = StatusCodes.Status400BadRequest,
            });
            return false;
        }

        sessionKey = values.First()!.Trim();
        if (sessionKey.Length > 64)
        {
            error = BadRequest(new ProblemDetails
            {
                Title = "Invalid cart session",
                Detail = $"Header '{SessionHeaderName}' must be at most 64 characters.",
                Status = StatusCodes.Status400BadRequest,
            });
            return false;
        }

        return true;
    }
}
