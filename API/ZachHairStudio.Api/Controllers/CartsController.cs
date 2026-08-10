using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Carts;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/carts")]
public class CartsController : ControllerBase
{
    public const string SessionHeaderName = "X-Cart-Session-Id";

    private readonly CartsService _cartsService;
    private readonly IValidator<CartItemUpsertDto> _upsertValidator;

    public CartsController(
        CartsService cartsService,
        IValidator<CartItemUpsertDto> upsertValidator)
    {
        _cartsService = cartsService;
        _upsertValidator = upsertValidator;
    }

    [HttpGet]
    public async Task<ActionResult<CartResponseDto>> GetCart()
    {
        if (!TryGetSessionKey(out var sessionKey, out var error))
        {
            return error!;
        }

        var result = await _cartsService.GetCartAsync(sessionKey);
        return Ok(result.Data);
    }

    [HttpPut("items")]
    [HttpPost("items")]
    public async Task<ActionResult<CartResponseDto>> UpsertItem([FromBody] CartItemUpsertDto request)
    {
        if (!TryGetSessionKey(out var sessionKey, out var error))
        {
            return error!;
        }

        var validation = await _upsertValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var failure in validation.Errors)
            {
                ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var result = await _cartsService.UpsertItemAsync(sessionKey, request);

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
                Title = "Cart conflict",
                Detail = result.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }

        return Ok(result.Data);
    }

    [HttpDelete("items/{productId:int}")]
    public async Task<ActionResult<CartResponseDto>> RemoveItem(int productId)
    {
        if (!TryGetSessionKey(out var sessionKey, out var error))
        {
            return error!;
        }

        var result = await _cartsService.RemoveItemAsync(sessionKey, productId);
        return Ok(result.Data);
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
