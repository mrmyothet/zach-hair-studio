namespace ZachHairStudio.Shared.Features.Carts;

/// <summary>
/// Write contract for add/update cart line. ProductId + Quantity only — no
/// client-trusted Price/Total (D-05 / SHOP-03 prelude, T-06-01).
/// </summary>
public class CartItemUpsertDto
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }
}
