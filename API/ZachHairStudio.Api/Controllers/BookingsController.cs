using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Bookings;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public BookingsController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingResponseDto>>> GetBookings()
    {
        var bookings = await _dbContext.Bookings
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => b.ToDto())
            .ToListAsync();

        return Ok(bookings);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingResponseDto>> GetBooking(int id)
    {
        var booking = await _dbContext.Bookings.FindAsync(id);
        if (booking is null)
        {
            return NotFound();
        }

        return Ok(booking.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<BookingResponseDto>> CreateBooking([FromBody] BookingCreateDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var booking = request.ToEntity();
        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking.ToDto());
    }

    [HttpPost("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromQuery] BookingStatus status)
    {
        var booking = await _dbContext.Bookings.FindAsync(id);
        if (booking is null)
        {
            return NotFound();
        }

        booking.Status = status;
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }
}
