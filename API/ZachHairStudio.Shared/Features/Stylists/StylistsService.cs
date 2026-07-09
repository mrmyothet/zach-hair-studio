using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;

namespace ZachHairStudio.Shared.Features.Stylists;

public class StylistsService
{
    private readonly BookingDbContext _dbContext;

    public StylistsService(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<StylistResponseDto>> GetActiveStylistsAsync()
        => await _dbContext.Stylists
            .Where(stylist => stylist.IsActive)
            .OrderBy(stylist => stylist.DisplayOrder)
            .Select(stylist => stylist.ToDto())
            .ToListAsync();
}
