namespace ZachHairStudio.Shared.Features.Orders;

/// <summary>
/// Guest checkout write contract. Money fields are intentionally absent (D-05 / SHOP-03) —
/// server recomputes totals from <c>Products.Price</c>. Optional <see cref="SessionKey"/>
/// mirrors header <c>X-Cart-Session-Id</c> when present; they must match.
/// </summary>
public class CheckoutRequestDto
{
    /// <summary>Optional body mirror of X-Cart-Session-Id. When omitted, controller uses the header.</summary>
    public string? SessionKey { get; set; }

    public List<CheckoutLineItemDto> Items { get; set; } = [];

    public string Email { get; set; } = null!;

    public string? Name { get; set; }
}

public class CheckoutLineItemDto
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }
}
