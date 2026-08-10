namespace ZachHairStudio.Shared.Features.Orders;

public class CheckoutResponseDto
{
    public string CheckoutUrl { get; set; } = null!;

    public int OrderId { get; set; }

    /// <summary>Catalog merchandise subtotal before loyalty (D-15).</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Server-computed loyalty dollars off (never client-supplied).</summary>
    public decimal LoyaltyDiscount { get; set; }

    /// <summary>Amount charged after loyalty (payment session uses this).</summary>
    public decimal TotalAmount { get; set; }

    public int PointsRedeemed { get; set; }
}
