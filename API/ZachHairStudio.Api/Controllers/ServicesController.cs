using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly ServicesService _servicesService;
    private readonly IValidator<ServiceCreateDto> _createValidator;
    private readonly IValidator<ServiceUpdateDto> _updateValidator;

    public ServicesController(
        ServicesService servicesService,
        IValidator<ServiceCreateDto> createValidator,
        IValidator<ServiceUpdateDto> updateValidator)
    {
        _servicesService = servicesService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceResponseDto>>> GetServices()
    {
        var services = await _servicesService.GetActiveServicesAsync();
        return Ok(services);
    }

    [HttpGet("{slug}", Name = nameof(GetService))]
    public async Task<ActionResult<ServiceResponseDto>> GetService(string slug)
    {
        var result = await _servicesService.GetBySlugAsync(slug);
        return result.IsSuccess ? Ok(result.Data) : NotFound();
    }

    [HttpPost]
    public async Task<ActionResult<ServiceResponseDto>> CreateService([FromBody] ServiceCreateDto request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            AddToModelState(validation);
            return ValidationProblem(ModelState);
        }

        var result = await _servicesService.CreateAsync(request);
        if (result.IsValidationError())
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetService), new { slug = result.Data.Slug }, result.Data);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateService(int id, [FromBody] ServiceUpdateDto request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            AddToModelState(validation);
            return ValidationProblem(ModelState);
        }

        var result = await _servicesService.UpdateAsync(id, request);
        if (result.IsNotFound())
        {
            return NotFound();
        }

        if (result.IsValidationError())
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return ValidationProblem(ModelState);
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
}
