using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly JwtTokenService _tokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IValidator<LoginRequestDto> loginValidator,
        JwtTokenService tokenService)
    {
        _userManager = userManager;
        _loginValidator = loginValidator;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var validation = await _loginValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(request.Email);

        // Identical response for "no such user" and "wrong password" (T-03-06) — never
        // reveal whether an email is registered.
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Invalid email or password.",
                Status = StatusCodes.Status401Unauthorized,
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.CreateToken(user, roles);

        return Ok(new LoginResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            DisplayName = user.DisplayName,
            Role = roles.FirstOrDefault() ?? string.Empty,
        });
    }
}
