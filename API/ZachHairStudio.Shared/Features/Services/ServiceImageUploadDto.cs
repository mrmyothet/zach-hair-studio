using Microsoft.AspNetCore.Http;

namespace ZachHairStudio.Shared.Features.Services;

public class ServiceImageUploadDto
{
    public IFormFile Image { get; set; } = null!;
}
