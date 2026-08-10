using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Carts;

/// <summary>
/// Ephemeral guest cart keyed by client session (D-02). No ApplicationUser /
/// ClientId FK — guest checkout is independent of accounts (Phase 7).
/// </summary>
public class Cart
{
    public int Id { get; set; }

    [Required, StringLength(64)]
    public string SessionKey { get; set; } = null!;

    public List<CartItem> Items { get; set; } = [];
}
