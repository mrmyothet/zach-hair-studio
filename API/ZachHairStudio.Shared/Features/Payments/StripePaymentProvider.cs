using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace ZachHairStudio.Shared.Features.Payments;

/// <summary>
/// Real Stripe Checkout Session create (SHOP-02, D-01). Secrets from
/// <see cref="StripeOptions"/> (user-secrets/env only — never tracked files).
/// </summary>
public class StripePaymentProvider : IPaymentProvider
{
    private readonly StripeOptions _options;
    private readonly SessionService _sessionService;

    public StripePaymentProvider(IOptions<StripeOptions> options, IStripeClient stripeClient)
    {
        _options = options.Value;
        _sessionService = new SessionService(stripeClient);
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var successUrl = _options.SuccessUrl.Contains("{CHECKOUT_SESSION_ID}", StringComparison.Ordinal)
            ? _options.SuccessUrl
            : $"{_options.SuccessUrl.TrimEnd('/')}?session_id={{CHECKOUT_SESSION_ID}}";

        var createOptions = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = _options.CancelUrl,
            ClientReferenceId = request.OrderId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["order_id"] = request.OrderId.ToString(),
            },
            CustomerEmail = request.CustomerEmail,
            LineItems = request.Lines
                .Select(line => new SessionLineItemOptions
                {
                    Quantity = line.Quantity,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = (long)(line.UnitPrice * 100m),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = line.ProductName,
                        },
                    },
                })
                .ToList(),
        };

        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"order-{request.OrderId}",
        };

        var session = await _sessionService.CreateAsync(
            createOptions,
            requestOptions,
            cancellationToken);

        return new CheckoutSessionResult(session.Id, session.Url);
    }
}
