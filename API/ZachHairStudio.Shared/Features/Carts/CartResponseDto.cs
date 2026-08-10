namespace ZachHairStudio.Shared.Features.Carts;

/// <summary>
/// Guest cart review payload. Subtotal is the sum of server-computed LineTotals.
/// </summary>
public class CartResponseDto
{
    public string SessionKey { get; set; } = null!;

    public IReadOnlyList<CartItemResponseDto> Items { get; set; } = [];

    public decimal Subtotal { get; set; }
}
