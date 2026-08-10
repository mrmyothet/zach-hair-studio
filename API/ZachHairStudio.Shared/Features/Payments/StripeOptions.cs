namespace ZachHairStudio.Shared.Features.Payments;

/// <summary>
/// Stripe / checkout redirect settings. Non-secret URLs may live in appsettings;
/// <see cref="SecretKey"/> and <see cref="WebhookSecret"/> come from user-secrets/env
/// (never tracked files) — Plan 05 wires real Stripe.
/// </summary>
public class StripeOptions
{
    public string SuccessUrl { get; set; } = "http://localhost:3000/checkout/success";

    public string CancelUrl { get; set; } = "http://localhost:3000/cart";

    public string SecretKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;
}
