using ZachHairStudio.Shared.Features.Payments;

namespace ZachHairStudio.Api.Tests.Features.Payments;

/// <summary>
/// SHOP-02 regression: the success page resolves the order from an explicit
/// orderId param. Guards the defect where the order id was recovered by matching
/// trailing digits of the Stripe session id — which works for FakePaymentProvider's
/// "fake-{id}" but not for a real random "cs_test_..." id.
/// </summary>
public class StripeSuccessUrlTests
{
    [Fact]
    public void BuildSuccessUrl_AppendsOrderIdAndKeepsSessionPlaceholder()
    {
        var url = StripePaymentProvider.BuildSuccessUrl("http://localhost:3000/checkout/success", 42);

        Assert.Contains("orderId=42", url, StringComparison.Ordinal);
        Assert.Contains("{CHECKOUT_SESSION_ID}", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSuccessUrl_OperatorSuppliedPlaceholder_IsNotDuplicated()
    {
        var configured = "https://salon.example/checkout/success?session_id={CHECKOUT_SESSION_ID}";

        var url = StripePaymentProvider.BuildSuccessUrl(configured, 7);

        Assert.Equal(
            "https://salon.example/checkout/success?session_id={CHECKOUT_SESSION_ID}&orderId=7",
            url);
    }

    /// <summary>
    /// A realistic Stripe id ends in a letter, so the old trailing-digit heuristic
    /// yielded no match and the paid customer saw a 404. Nothing may depend on it.
    /// </summary>
    [Fact]
    public void RealisticStripeSessionId_CarriesNoUsableOrderId()
    {
        const string sessionId = "cs_test_a1BcD2eFgH3iJkL4mNoP5qRsT6uVwX7yZaBcDeFgHiJkLmNoPqRsTuVwXy";

        Assert.False(char.IsDigit(sessionId[^1]));

        // The order id is only ever recoverable from the explicit param.
        var url = StripePaymentProvider.BuildSuccessUrl("http://localhost:3000/checkout/success", 1234);
        Assert.Contains("orderId=1234", url, StringComparison.Ordinal);
    }
}
