namespace ZachHairStudio.Shared.Features.Carts;

/// <summary>
/// Cart line stores ProductId + Quantity only (D-05). Unit prices are never
/// persisted here — CartsService enriches from Products.Price at read time.
/// </summary>
public class CartItem
{
    public int Id { get; set; }

    public int CartId { get; set; }

    public Cart Cart { get; set; } = null!;

    public int ProductId { get; set; }

    public int Quantity { get; set; }
}
