using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Availability;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly SlotService _slotService;

    public AppointmentsController(SlotService slotService)
    {
        _slotService = slotService;
    }

    [HttpGet("slots")]
    public async Task<ActionResult<IReadOnlyList<OpenSlotDto>>> GetSlots(
        [FromQuery] int serviceId,
        [FromQuery] int? stylistId,
        [FromQuery] DateOnly date)
    {
        var slots = await _slotService.GetOpenSlotsAsync(serviceId, stylistId, date);
        return Ok(slots);
    }
}
