using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Orders;

namespace ZachHairStudio.Shared.Features.Account;

/// <summary>
/// Ownership-gated client history + claim-by-email (ACCT-02/03/06, D-04, D-08).
/// Owner scope always comes from <paramref name="userId"/> — never from client input.
/// </summary>
public class AccountService
{
    private readonly BookingDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountService(BookingDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<Result<IReadOnlyList<AppointmentResponseDto>>> ListBookingsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var appointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ClientUserId == userId)
            .OrderByDescending(a => a.StartsAt)
            .ToListAsync(cancellationToken);

        return await MapAppointmentsAsync(appointments, cancellationToken);
    }

    public async Task<Result<AppointmentResponseDto>> GetBookingAsync(
        int userId,
        int appointmentId,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _dbContext.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == appointmentId && a.ClientUserId == userId,
                cancellationToken);

        if (appointment is null)
        {
            // Miss and cross-client both map to the same 404 (RESEARCH A1) — no existence leak.
            return Result<AppointmentResponseDto>.NotFoundError("Appointment not found.");
        }

        var mapped = await MapAppointmentsAsync([appointment], cancellationToken);
        if (!mapped.IsSuccess)
        {
            return Result<AppointmentResponseDto>.SystemError(mapped.Message);
        }

        return Result<AppointmentResponseDto>.Success(mapped.Data![0]);
    }

    public async Task<Result<IReadOnlyList<OrderResponseDto>>> ListOrdersAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.ClientId == userId)
            .OrderByDescending(o => o.PlacedAtUtc)
            .ToListAsync(cancellationToken);

        var dtos = orders.Select(o => o.ToResponseDto()).ToList();
        return Result<IReadOnlyList<OrderResponseDto>>.Success(dtos);
    }

    public async Task<Result<OrderResponseDto>> GetOrderAsync(
        int userId,
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(
                o => o.Id == orderId && o.ClientId == userId,
                cancellationToken);

        if (order is null)
        {
            return Result<OrderResponseDto>.NotFoundError("Order not found.");
        }

        return Result<OrderResponseDto>.Success(order.ToResponseDto());
    }

    public async Task<Result<ClaimPreviewDto>> ClaimPreviewAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return Result<ClaimPreviewDto>.NotFoundError("User not found.");
        }

        var email = user.Email.Trim();

        var guestAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ClientUserId == null && a.Email != null && a.Email.ToLower() == email.ToLower())
            .OrderByDescending(a => a.StartsAt)
            .ToListAsync(cancellationToken);

        var guestOrders = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.ClientId == null && o.Email != null && o.Email.ToLower() == email.ToLower())
            .OrderByDescending(o => o.PlacedAtUtc)
            .ToListAsync(cancellationToken);

        var serviceIds = guestAppointments.Select(a => a.ServiceId).Distinct().ToList();
        var services = await _dbContext.Services
            .AsNoTracking()
            .Where(s => serviceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var preview = new ClaimPreviewDto
        {
            Appointments = guestAppointments.Select(a => new ClaimAppointmentSummaryDto
            {
                Id = a.Id,
                StartsAt = a.StartsAt,
                ServiceName = services.TryGetValue(a.ServiceId, out var service) ? service.Name : "Service",
                Status = a.Status.ToString(),
            }).ToList(),
            Orders = guestOrders.Select(o => new ClaimOrderSummaryDto
            {
                Id = o.Id,
                PlacedAtUtc = o.PlacedAtUtc,
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                ItemCount = o.Items.Count,
            }).ToList(),
        };

        return Result<ClaimPreviewDto>.Success(preview);
    }

    public async Task<Result<object?>> ClaimAsync(
        int userId,
        bool confirm,
        CancellationToken cancellationToken = default)
    {
        if (!confirm)
        {
            // Explicit skip — leave FKs null (D-04).
            return Result<object?>.Success(null);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return Result<object?>.NotFoundError("User not found.");
        }

        var email = user.Email.Trim();
        var emailLower = email.ToLowerInvariant();

        var appointments = await _dbContext.Appointments
            .Where(a => a.ClientUserId == null && a.Email != null && a.Email.ToLower() == emailLower)
            .ToListAsync(cancellationToken);

        foreach (var appointment in appointments)
        {
            appointment.ClientUserId = userId;
        }

        var orders = await _dbContext.Orders
            .Where(o => o.ClientId == null && o.Email != null && o.Email.ToLower() == emailLower)
            .ToListAsync(cancellationToken);

        foreach (var order in orders)
        {
            order.ClientId = userId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<object?>.Success(null);
    }

    private async Task<Result<IReadOnlyList<AppointmentResponseDto>>> MapAppointmentsAsync(
        IReadOnlyList<Appointment> appointments,
        CancellationToken cancellationToken)
    {
        if (appointments.Count == 0)
        {
            return Result<IReadOnlyList<AppointmentResponseDto>>.Success(Array.Empty<AppointmentResponseDto>());
        }

        var serviceIds = appointments.Select(a => a.ServiceId).Distinct().ToList();
        var stylistIds = appointments.Select(a => a.StylistId).Distinct().ToList();

        var services = await _dbContext.Services
            .AsNoTracking()
            .Where(s => serviceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var stylists = await _dbContext.Stylists
            .AsNoTracking()
            .Where(s => stylistIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var dtos = new List<AppointmentResponseDto>(appointments.Count);
        foreach (var appointment in appointments)
        {
            if (!services.TryGetValue(appointment.ServiceId, out var service)
                || !stylists.TryGetValue(appointment.StylistId, out var stylist))
            {
                return Result<IReadOnlyList<AppointmentResponseDto>>.SystemError(
                    $"Appointment {appointment.Id} references a missing service or stylist.");
            }

            dtos.Add(appointment.ToDto(service, stylist));
        }

        return Result<IReadOnlyList<AppointmentResponseDto>>.Success(dtos);
    }
}
