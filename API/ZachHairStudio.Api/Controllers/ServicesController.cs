using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    // Path.GetRandomFileName()'s own generated "extension" is discarded; the extension
    // actually written is derived from the validated Content-Type, never from the
    // client-supplied FileName (T-04-02 — path-traversal safe, RESEARCH Pattern 3).
    private static readonly Dictionary<string, string> ImageExtensionsByContentType = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    private readonly ServicesService _servicesService;
    private readonly IValidator<ServiceCreateDto> _createValidator;
    private readonly IValidator<ServiceUpdateDto> _updateValidator;
    private readonly IValidator<ServiceImageUploadDto> _imageUploadValidator;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ServicesController(
        ServicesService servicesService,
        IValidator<ServiceCreateDto> createValidator,
        IValidator<ServiceUpdateDto> updateValidator,
        IValidator<ServiceImageUploadDto> imageUploadValidator,
        IWebHostEnvironment webHostEnvironment)
    {
        _servicesService = servicesService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _imageUploadValidator = imageUploadValidator;
        _webHostEnvironment = webHostEnvironment;
    }

    // GetServices stays anonymous (D-01) — the public landing-page catalog depends on it.
    // includeInactive is honored ONLY for an authenticated Owner; for anyone else it is
    // silently ignored rather than rejected with 403, because bolting a new authz error
    // surface onto a deliberately anonymous endpoint would advertise a privileged mode to
    // unauthenticated clients. This is fail-closed by construction: the relaxed filter is
    // reachable only from inside the IsInRole check below, so no misconfigured attribute,
    // missing [Authorize], or expired token can reach the inactive rows (DD-1).
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceResponseDto>>> GetServices([FromQuery] bool includeInactive = false)
    {
        var effectiveIncludeInactive = includeInactive && User.IsInRole(StaffRoles.Owner);
        var services = await _servicesService.GetServicesAsync(effectiveIncludeInactive);
        return Ok(services);
    }

    [HttpGet("{slug}", Name = nameof(GetService))]
    public async Task<ActionResult<ServiceResponseDto>> GetService(string slug)
    {
        var result = await _servicesService.GetBySlugAsync(slug);
        return result.IsSuccess ? Ok(result.Data) : NotFound();
    }

    // Owner-only (D-01). Action-level, not class-level — a class-level [Authorize]
    // would also gate GetServices/GetService above and break the public, anonymous
    // catalog the landing page depends on (CAT-01/CAT-02, RESEARCH Pitfall 5).
    [HttpPost]
    [Authorize(Roles = StaffRoles.Owner)]
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
    [Authorize(Roles = StaffRoles.Owner)]
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

    // Owner-only (D-01/D-03). Server-generated filename + content-type allowlist +
    // size cap are all enforced here before any byte touches disk (T-04-02/T-04-03).
    [HttpPost("{id}/image")]
    [Authorize(Roles = StaffRoles.Owner)]
    public async Task<ActionResult<ServiceResponseDto>> UploadImage(int id, [FromForm] ServiceImageUploadDto request)
    {
        var validation = await _imageUploadValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            AddToModelState(validation);
            return ValidationProblem(ModelState);
        }

        var extension = ImageExtensionsByContentType[request.Image.ContentType];

        // Resolve via IWebHostEnvironment.WebRootPath — never a bare relative string
        // (RESEARCH Pitfall 4: a relative path resolves against the process's working
        // directory, which differs between `dotnet run` and a published process).
        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "services");
        Directory.CreateDirectory(uploadsFolder);

        var storedFileName = Path.GetRandomFileName() + extension;
        var filePath = Path.Combine(uploadsFolder, storedFileName);

        await using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await request.Image.CopyToAsync(fileStream);
        }

        var result = await _servicesService.SetImageAsync(id, $"/uploads/services/{storedFileName}");
        if (result.IsNotFound())
        {
            try
            {
                System.IO.File.Delete(filePath);
            }
            catch
            {
                // best-effort cleanup
            }
            return NotFound();
        }

        return Ok(result.Data);
    }

    private void AddToModelState(ValidationResult validation)
    {
        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
    }
}
