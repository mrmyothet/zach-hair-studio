using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Stylists;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StylistsController : ControllerBase
{
    private readonly StylistsService _stylistsService;

    public StylistsController(StylistsService stylistsService)
    {
        _stylistsService = stylistsService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StylistResponseDto>>> GetStylists()
    {
        var stylists = await _stylistsService.GetActiveStylistsAsync();
        return Ok(stylists);
    }
}
