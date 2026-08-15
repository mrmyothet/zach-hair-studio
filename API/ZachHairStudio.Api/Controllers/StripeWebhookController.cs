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

            if (result.IsNotFound())
            {
                // Transient: the webhook can outrun the checkout transaction's commit,
                // so the order row may not be visible yet. 5xx asks Stripe to redeliver —
                // the one failure mode a retry actually fixes.
                _logger.LogError(
                    "Webhook could not find an order for session {SessionId}; asking Stripe to retry: {Message}",
                    session.Id,
                    result.Message);

                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            if (!result.IsSuccess)
            {
                // Terminal (e.g. Cancelled/Failed): no redelivery can change this state.
                // Ack so Stripe stops retrying, and log at Error — a paid order that
                // cannot be fulfilled needs a human, not another delivery attempt.
                _logger.LogError(
                    "Webhook cannot fulfill session {SessionId} — terminal state, manual review required: {Message}",
                    session.Id,
                    result.Message);
            }
        }

        return Ok();
    }
}
