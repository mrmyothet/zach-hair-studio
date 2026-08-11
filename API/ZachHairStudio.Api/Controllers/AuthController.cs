using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly JwtTokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<RegisterRequestDto> registerValidator,
        JwtTokenService tokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
        _tokenService = tokenService;
        _logger = logger;
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

        _logger.LogInformation(
            "Auth login succeeded for user {UserId} email {EmailHint}",
            user.Id,
            TruncateEmail(user.Email));

        return Ok(new LoginResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            DisplayName = user.DisplayName,
            Role = roles.FirstOrDefault() ?? string.Empty,
        });
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? request.Email.Split('@')[0]
            : request.DisplayName.Trim();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = displayName,
            EmailConfirmed = true,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, StaffRoles.Client);
        if (!roleResult.Succeeded)
        {
            // Roll back the just-created user — leaving a role-less account behind
            // would make retries fail on duplicate email while login stays broken.
            await _userManager.DeleteAsync(user);
            return Problem(
                title: "Failed to assign the Client role.",
                detail: string.Join(" ", roleResult.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.CreateToken(user, roles);

        _logger.LogInformation(
            "Auth register succeeded for user {UserId} email {EmailHint}",
            user.Id,
            TruncateEmail(user.Email));

        return Ok(new LoginResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            DisplayName = user.DisplayName,
            Role = roles.FirstOrDefault() ?? string.Empty,
        });
    }

    /// <summary>LAUNCH-04 / D-07 — never log full email; keep a short hint only.</summary>
    private static string TruncateEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return "(none)";
        }

        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return email[..Math.Min(3, email.Length)] + "***";
        }

        var local = email[..Math.Min(2, at)];
        return local + "***@" + email[(at + 1)..];
    }
}
