using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;

namespace ZachHairStudio.Shared.Features.Loyalty;

/// <summary>
/// Append-only LoyaltyLedger operations (ACCT-07, D-13–D-16). Balance = SUM(Delta).
/// Redeem dollars are always server-computed via <see cref="LoyaltyRates"/>.
/// </summary>
public class LoyaltyService
{
    private readonly BookingDbContext _dbContext;

    public LoyaltyService(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> GetBalanceAsync(int clientUserId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoyaltyLedgers
            .AsNoTracking()
            .Where(row => row.ClientUserId == clientUserId)
            .SumAsync(row => (int?)row.Delta, cancellationToken) ?? 0;
    }

    /// <summary>
    /// Idempotent +1 Earn for a completed appointment (D-13, Pitfall 3).
    /// No-op when an Earn row already exists for <paramref name="appointmentId"/>.
    /// </summary>
    public async Task EarnForCompletedAsync(
        int appointmentId,
        int clientUserId,
        CancellationToken cancellationToken = default)
    {
        var alreadyEarned = await _dbContext.LoyaltyLedgers.AnyAsync(
            row => row.AppointmentId == appointmentId && row.Reason == LoyaltyReasons.Earn,
            cancellationToken);

        if (alreadyEarned)
        {
            return;
        }

        _dbContext.LoyaltyLedgers.Add(new LoyaltyLedger
        {
            ClientUserId = clientUserId,
            Delta = LoyaltyRates.PointsPerCompletedAppointment,
            Reason = LoyaltyReasons.Earn,
            AppointmentId = appointmentId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique filtered index won a race — treat as idempotent success.
            _dbContext.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Validates redeem request and returns server dollars capped at merchandise subtotal.
    /// Does not write ledger rows.
    /// </summary>
    public async Task<Result<LoyaltyQuoteDto>> QuoteRedeemAsync(
        int clientUserId,
        int? redeemPoints,
        decimal merchandiseSubtotal,
        CancellationToken cancellationToken = default)
    {
        if (merchandiseSubtotal < 0)
        {
            return Result<LoyaltyQuoteDto>.ValidationError("Subtotal cannot be negative.");
        }

        var balance = await GetBalanceAsync(clientUserId, cancellationToken);
        var points = redeemPoints ?? 0;

        if (points < 0)
        {
            return Result<LoyaltyQuoteDto>.ValidationError("RedeemPoints cannot be negative.");
        }

        if (points % LoyaltyRates.RedeemBlockPoints != 0)
        {
            return Result<LoyaltyQuoteDto>.ValidationError(
                $"RedeemPoints must be a multiple of {LoyaltyRates.RedeemBlockPoints}.");
        }

        if (points > balance)
        {
            return Result<LoyaltyQuoteDto>.ValidationError("Insufficient loyalty balance.");
        }

        var uncapped = LoyaltyRates.DollarsForPoints(points);
        var discount = Math.Min(uncapped, merchandiseSubtotal);

        return Result<LoyaltyQuoteDto>.Success(new LoyaltyQuoteDto
        {
            Subtotal = merchandiseSubtotal,
            LoyaltyDiscount = discount,
            TotalAmount = merchandiseSubtotal - discount,
            PointsRedeemed = points,
            Balance = balance,
        });
    }

    /// <summary>
    /// Appends a negative Redeem ledger row. Must be called inside the caller's
    /// CreateExecutionStrategy transaction; does not SaveChanges itself.
    /// </summary>
    public void AppendRedeem(int clientUserId, int pointsToSpend, int orderId)
    {
        if (pointsToSpend <= 0)
        {
            return;
        }

        _dbContext.LoyaltyLedgers.Add(new LoyaltyLedger
        {
            ClientUserId = clientUserId,
            Delta = -pointsToSpend,
            Reason = LoyaltyReasons.Redeem,
            OrderId = orderId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
    }
}
