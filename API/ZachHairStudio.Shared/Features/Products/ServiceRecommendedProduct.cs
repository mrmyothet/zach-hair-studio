namespace ZachHairStudio.Shared.Features.Products;

// Explicit join POCO (D-11) — no navigation properties per D-14's one-directional design.
public class ServiceRecommendedProduct
{
    public int ServiceId { get; set; }
    public int ProductId { get; set; }
}
