namespace ZachHairStudio.Shared.Features.Orders;

public static class OrderExtensions
{
    public static CheckoutResponseDto ToCheckoutResponseDto(this Order order, string checkoutUrl)
        => new CheckoutResponseDto
        {
            OrderId = order.Id,
            CheckoutUrl = checkoutUrl,
        };

    public static OrderResponseDto ToResponseDto(this Order order)
        => new OrderResponseDto
        {
            Id = order.Id,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            Email = order.Email,
            CustomerName = order.CustomerName,
            PlacedAtUtc = order.PlacedAtUtc,
            Items = order.Items
                .Select(item => new OrderItemResponseDto
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    LineTotal = item.LineTotal,
                })
                .ToList(),
        };
}

/// <summary>Read model for optional GET order-by-id (success page).</summary>
public class OrderResponseDto
{
    public int Id { get; set; }

    public OrderStatus Status { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Email { get; set; }

    public string? CustomerName { get; set; }

    public DateTimeOffset PlacedAtUtc { get; set; }

    public List<OrderItemResponseDto> Items { get; set; } = [];
}

public class OrderItemResponseDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }
}
