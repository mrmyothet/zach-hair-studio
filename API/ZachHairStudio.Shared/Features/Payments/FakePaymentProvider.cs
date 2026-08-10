namespace ZachHairStudio.Shared.Features.Payments;

/// <summary>
/// Deterministic stand-in for CI/dev until Plan 05 registers StripePaymentProvider.
/// No network I/O; SessionId/Url derived from order id.
/// </summary>
public class FakePaymentProvider : IPaymentProvider
{
    public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessionId = $"fake-{request.OrderId}";
        var url = $"https://example.test/checkout/{request.OrderId}";
        return Task.FromResult(new CheckoutSessionResult(sessionId, url));
    }
}
