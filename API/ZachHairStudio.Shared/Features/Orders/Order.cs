using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Orders;

/// <summary>
/// Immutable checkout snapshot (D-03). Guest checkout leaves
/// <see cref="ClientId"/> null (D-06 / SHOP-06). Status starts Pending;
/// Fulfilled only via webhook-driven <c>MarkFulfilledAsync</c> (SHOP-05).
/// </summary>
public class Order
{
    public int Id { get; set; }

    /// <summary>Null for guest checkout (SHOP-06). Phase 7 may attach accounts later.</summary>
    public int? ClientId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal TotalAmount { get; set; }

    [EmailAddress, StringLength(150)]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? CustomerName { get; set; }

    [StringLength(200)]
    public string? StripeSessionId { get; set; }

    [StringLength(500)]
    public string? StripeSessionUrl { get; set; }

    public DateTimeOffset PlacedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<OrderItem> Items { get; set; } = [];
}
