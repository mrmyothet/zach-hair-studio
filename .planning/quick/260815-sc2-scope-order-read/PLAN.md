---
quick_id: 260815-sc2
slug: scope-order-read
created: 2026-08-15
requirements: [ACCT-06, SHOP-06]
---

# Quick: scope GET /api/orders/{id} — require session id as second factor

Closes blocker 2 of `.planning/v1.0-MILESTONE-AUDIT.md`.

## Problem

`OrdersController.cs:165` `GetById` has no `[Authorize]` and no ownership
check; `OrdersService.GetByIdAsync` filters on id alone and returns `Email`,
`CustomerName`, and line items. Any visitor can walk `?orderId=1..N` and
harvest every customer's PII.

## Why a session-id second factor (not [Authorize])

`GET /api/account/orders/{id}` (AccountController.cs:184) already serves
authenticated users with proper ownership scoping. This endpoint exists solely
for the **guest** success page, where there is no identity to authorize
against. The unguessable Stripe session id is the capability the guest already
holds — it arrives in the same redirect as `orderId`. Requiring it turns an
enumerable id into a two-part key without adding guest accounts.

## Tasks

1. `OrdersService.GetByIdAsync` — take a required `sessionId` and match it
   against `Order.StripeSessionId` in the same query. Mismatch → NotFound
   (not Forbid: never confirm an order id exists to someone lacking the key).
2. `OrdersController.GetById` — accept `?session=` and pass it through;
   missing/blank → 404, same shape, no oracle.
3. `landing-page/lib/cart.ts` `fetchOrderById` — take and forward the session id.
4. `success/page.tsx` — pass `session_id` from the URL through.
5. Tests: correct session → 200; wrong session → 404; missing session → 404.

## Constraints

- Anonymous access is preserved by design — the session id *is* the credential.
- Do not change `/api/account/orders/{id}`; it is already correct.
- Comparison must be ordinal and constant-shaped (no early-exit on prefix).
