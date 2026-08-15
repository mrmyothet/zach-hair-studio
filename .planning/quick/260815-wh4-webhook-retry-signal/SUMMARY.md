---
quick_id: 260815-wh4
slug: webhook-retry-signal
status: complete
completed: 2026-08-15
requirements: [SHOP-05, LAUNCH-04]
---

# Summary: webhook returns non-2xx when fulfillment fails

Closes the non-critical webhook finding in `.planning/v1.0-MILESTONE-AUDIT.md`.

## Changes

- `API/ZachHairStudio.Api/Controllers/StripeWebhookController.cs` — the failed
  `MarkFulfilledAsync` branch now splits by outcome instead of always 200:
  - `IsNotFound()` → **503** + `LogError` (asks Stripe to redeliver)
  - other failure → **200** + `LogError` (terminal; ack and escalate to a human)
  - success (incl. already-Fulfilled no-op) → 200, unchanged
- `API/ZachHairStudio.Api.Tests/Features/Payments/StripeWebhookRetryTests.cs` —
  2 tests.

## Why not blanket non-2xx

The task as literally worded ("non-2xx when fulfillment fails") would also retry
terminal failures. `MarkFulfilledAsync` returns `ValidationError` when an order
is in a state it cannot be fulfilled from (e.g. `Failed`) — no redelivery can
change that, so Stripe would hammer the endpoint through its entire backoff
window over an unfixable state.

`NotFound` is the genuinely transient case: the webhook can outrun the checkout
transaction's commit, so the order row isn't visible yet. That is precisely what
a retry fixes, and it is the case that was silently dropping paid orders.

Both now log at `LogError` — the visibility half of the finding — but only the
retryable one returns 5xx.

## Verification

- `StripeWebhook*` filter: **6 passed, 0 failed** — 2 new (unknown order → 503;
  terminal `Failed` order → 200 and status unchanged) plus the 4 existing
  (missing signature → 400, bad signature → 400, paid → Fulfilled + 200,
  idempotent redelivery → 200 twice). The pre-existing tests confirm the happy
  and idempotent paths still ack.
- Orders + Payments + Carts filter: 16 failed / 36 passed (52). Same 16
  `PlatformNotSupportedException: LocalDB` failures as the baseline established
  in `260815-sc2` (16 failed / 30 passed at HEAD); delta is my 2 new passes plus
  the 4 from the previous task.

## Out of scope

No dead-letter table or alert routing for the terminal case — `LogError` is the
seam, wiring it to a channel is an ops decision. Add when someone owns the alert.
