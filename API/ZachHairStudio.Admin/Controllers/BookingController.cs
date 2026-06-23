using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Bookings;

namespace ZachHairStudio.Admin.Controllers;

public class BookingController : Controller
{
    private readonly BookingDbContext _dbContext;

    public BookingController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(BookingStatus? status)
    {
        var query = _dbContext.Bookings.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var bookings = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
        return View(bookings);
    }

    public async Task<IActionResult> Details(int id)
    {
        var booking = await _dbContext.Bookings.FindAsync(id);
        if (booking is null)
        {
            return NotFound();
        }

        return View(booking);
    }

    [HttpPost]
    public async Task<IActionResult> Confirm(int id)
    {
        var booking = await _dbContext.Bookings.FindAsync(id);
        if (booking is null)
        {
            return NotFound();
        }

        booking.Status = BookingStatus.Confirmed;
        await _dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var booking = await _dbContext.Bookings.FindAsync(id);
        if (booking is null)
        {
            return NotFound();
        }

        booking.Status = BookingStatus.Cancelled;
        await _dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
