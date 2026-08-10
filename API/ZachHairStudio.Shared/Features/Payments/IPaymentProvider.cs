namespace ZachHairStudio.Shared.Features.Payments;

public record CheckoutSessionRequest(
    int OrderId,
    decimal TotalAmount,
    string? CustomerEmail,
    IReadOnlyList<CheckoutLine> Lines);

public record CheckoutLine(string ProductName, decimal UnitPrice, int Quantity);

public record CheckoutSessionResult(string SessionId, string Url);

/// <summary>
/// Payment-provider seam (D-01). Fake in this plan; StripePaymentProvider arrives in Plan 05.
/// </summary>
public interface IPaymentProvider
{
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request,
        CancellationToken cancellationToken = default);
}
