using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Identity;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/staff-users")]
[Authorize(Roles = StaffRoles.Owner)]
public class StaffUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<StaffUserCreateDto> _createValidator;

    public StaffUsersController(
        UserManager<ApplicationUser> userManager,
        IValidator<StaffUserCreateDto> createValidator)
    {
        _userManager = userManager;
        _createValidator = createValidator;
    }

    [HttpPost]
    public async Task<ActionResult<StaffUserResponseDto>> Create([FromBody] StaffUserCreateDto request)
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

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
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

        await _userManager.AddToRoleAsync(user, StaffRoles.Staff);

        var response = new StaffUserResponseDto
        {
            Id = user.Id,
            Email = user.Email!,
            DisplayName = user.DisplayName,
            Role = StaffRoles.Staff,
        };

        return Created($"/api/staff-users/{user.Id}", response);
    }
}
