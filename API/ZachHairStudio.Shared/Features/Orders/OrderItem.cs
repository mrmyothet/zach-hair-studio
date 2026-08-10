using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Orders;

/// <summary>
/// Line snapshot at checkout (D-03). ProductName/UnitPrice/LineTotal are frozen
/// from catalog at order creation — later catalog price edits do not mutate history.
/// </summary>
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }

    [Required, StringLength(150)]
    public string ProductName { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }
}
