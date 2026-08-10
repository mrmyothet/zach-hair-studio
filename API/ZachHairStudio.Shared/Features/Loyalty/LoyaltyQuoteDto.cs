namespace ZachHairStudio.Shared.Features.Loyalty;

/// <summary>
/// Server-computed checkout money preview (D-15). Never invent dollars client-side.
/// </summary>
public class LoyaltyQuoteDto
{
    public decimal Subtotal { get; set; }

    public decimal LoyaltyDiscount { get; set; }

    public decimal TotalAmount { get; set; }

    public int PointsRedeemed { get; set; }

    public int Balance { get; set; }
}
