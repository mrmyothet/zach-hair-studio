---
quick_id: 260815-3xf
slug: stripe-success-orderid
created: 2026-08-15
requirements: [SHOP-02]
---

# Quick: put orderId in SuccessUrl; drop the regex

Closes blocker 1 from `.planning/v1.0-MILESTONE-AUDIT.md`.

## Problem

`StripePaymentProvider` builds `SuccessUrl` carrying only
`?session_id={CHECKOUT_SESSION_ID}`. The success page recovers the order id
with `session.match(/(\d+)$/)` — a heuristic written for `FakePaymentProvider`'s
`fake-{orderId}` ids. A real `cs_test_*` id ends in a digit only ~30% of the
time and those digits are unrelated to the order, so a paid guest gets a 404
or, worse, another customer's order.

## Tasks

1. `StripePaymentProvider.cs` — append `orderId={request.OrderId}` to the
   success URL alongside `session_id`, preserving any operator-supplied
   `{CHECKOUT_SESSION_ID}` placeholder and existing query string.
2. `success/page.tsx` — delete the trailing-digit regex branch from
   `parseOrderId`; accept only an explicit numeric `orderId`/`order` param.
3. Self-check asserting a realistic `cs_test_` id (ending in a letter) no
   longer resolves via the session id, and that the built URL carries orderId.

## Out of scope

Scoping `GET /api/orders/{id}` (audit blocker 2, ACCT-06) — separate task.
Without it, `orderId` in the URL is still an enumerable handle to PII.
