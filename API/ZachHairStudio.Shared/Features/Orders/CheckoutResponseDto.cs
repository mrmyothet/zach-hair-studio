namespace ZachHairStudio.Shared.Features.Orders;

public class CheckoutResponseDto
{
    public string CheckoutUrl { get; set; } = null!;

    public int OrderId { get; set; }
}
