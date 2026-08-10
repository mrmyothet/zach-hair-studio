namespace ZachHairStudio.Shared.Features.Carts;

/// <summary>
/// Server-enriched cart line. UnitPrice/LineTotal come from Products.Price at
/// read time — never from client input (D-05).
/// </summary>
public class CartItemResponseDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string ProductSlug { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }

    public int Stock { get; set; }
}
