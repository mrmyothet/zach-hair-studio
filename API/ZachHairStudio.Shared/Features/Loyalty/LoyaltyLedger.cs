using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Loyalty;

/// <summary>
/// Append-only loyalty ledger row (D-14). Balance = SUM(Delta) for ClientUserId.
/// Earn rows link AppointmentId; Redeem rows link OrderId.
/// </summary>
public class LoyaltyLedger
{
    public int Id { get; set; }

    public int ClientUserId { get; set; }

    /// <summary>Positive for Earn, negative for Redeem.</summary>
    public int Delta { get; set; }

    [Required, StringLength(40)]
    public string Reason { get; set; } = null!;

    public int? AppointmentId { get; set; }

    public int? OrderId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
