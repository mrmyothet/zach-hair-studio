---
quick_id: 260815-wh4
slug: webhook-retry-signal
created: 2026-08-15
requirements: [SHOP-05, LAUNCH-04]
---

# Quick: webhook returns non-2xx when fulfillment fails

Closes the non-critical finding in `.planning/v1.0-MILESTONE-AUDIT.md`.

## Problem

`StripeWebhookController.cs:78-84` logs a failed `MarkFulfilledAsync` at
`LogWarning` and still returns `200 Ok`. Stripe reads 2xx as "handled" and
never retries, so a paid-but-unfulfilled order is silently dropped.

## Not every failure should retry

`MarkFulfilledAsync` returns three distinct outcomes. Blanket non-2xx would
make Stripe retry a permanently-terminal case for days:

| Outcome | Meaning | Retry? |
|---|---|---|
| Success | fulfilled, or already Fulfilled (idempotent no-op) | no — 200 |
| NotFound | order row not visible yet (replica lag / race with commit) | **yes — 503** |
| ValidationError | terminal state e.g. Cancelled/Failed; retrying cannot help | no — 200, log Error |

NotFound is the genuinely transient one: the webhook can outrun the checkout
transaction's commit. That is exactly what a retry fixes.

A terminal-status order must NOT retry — Stripe would hammer the endpoint for
its full backoff window over a state no redelivery can change. It needs a loud
log and a human, not a retry. `LogError` is the alarm; the 200 stops the noise.

## Tasks

1. `StripeWebhookController` — branch on the result: NotFound → 503 with
   `LogError`; ValidationError/other → 200 with `LogError`; success → 200.
2. Tests: NotFound-order event → 503; terminal-status order → 200; existing
   paid/idempotent-redelivery tests must stay 200.

## Out of scope

A dead-letter table or alerting hookup for the terminal case. `LogError` is the
seam; wiring it to a channel is an ops decision, not this task's.
