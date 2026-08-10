using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using ZachHairStudio.Shared.Features.Orders;
using ZachHairStudio.Shared.Features.Payments;

namespace ZachHairStudio.Api.Controllers;

/// <summary>
/// Stripe webhook ingress (SHOP-05). Verifies <c>Stripe-Signature</c> on the raw body
/// via <see cref="EventUtility.ConstructEvent"/> — never model-binds the event JSON.
/// Fulfillment is delegated to Plan 03 <see cref="OrdersService.MarkFulfilledAsync"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/stripe/webhook")]
public class StripeWebhookController : ControllerBase
{
    private readonly OrdersService _ordersService;
    private readonly StripeOptions _options;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        OrdersService ordersService,
        IOptions<StripeOptions> options,
        ILogger<StripeWebhookController> logger)
    {
        _ordersService = ordersService;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        string json;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        var signature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest();
        }

        Event stripeEvent;
        try
        {
            // throwOnApiVersionMismatch: false — account API versions can lag the SDK pin;
            // we only read Session id / client_reference_id / payment_status.
            stripeEvent = EventUtility.ConstructEvent(
                json,
                signature,
                _options.WebhookSecret,
                tolerance: 300,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            return BadRequest();
        }

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted
            && stripeEvent.Data.Object is Session session
            && string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            var result = await _ordersService.MarkFulfilledAsync(
                session.ClientReferenceId,
                session.Id,
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Webhook MarkFulfilledAsync for session {SessionId} did not succeed: {Message}",
                    session.Id,
                    result.Message);
            }
        }

        return Ok();
    }
}
