---
quick_id: 260815-sc2
slug: scope-order-read
status: complete
completed: 2026-08-15
requirements: [ACCT-06, SHOP-06]
---

# Summary: scope GET /api/orders/{id} — session id as second factor

Closes blocker 2 of `.planning/v1.0-MILESTONE-AUDIT.md`.

## Changes

- `OrdersService.GetByIdAsync` — now takes `string? sessionId` and matches it
  against `Order.StripeSessionId` using `CryptographicOperations.FixedTimeEquals`
  (no early-exit on a shared prefix). Blank/missing/mismatched → `NotFoundError`.
- `OrdersController.GetById` — accepts `[FromQuery(Name = "session")]` and passes
  it through. Still anonymous, by design.
- `landing-page/lib/cart.ts` `fetchOrderById(orderId, sessionId)` — sends
  `?session=`, returns null on a blank session without making the request.
- `landing-page/app/checkout/success/page.tsx` — `notFound()` when `session_id`
  is absent; forwards it to the fetch.
- `API/ZachHairStudio.Api.Tests/Features/Orders/OrderReadScopingTests.cs` — 4 tests.

## Design note: why not `[Authorize]`

`GET /api/account/orders/{id}` (`AccountController.cs:184`) already serves
authenticated clients with owner scoping. This endpoint exists only for the
**guest** success page, where there is no identity to authorize against. The
unguessable payment-session id is the capability the guest already holds — it
arrives in the same provider redirect as `orderId`. Requiring it converts an
enumerable integer into a two-part key without inventing guest accounts.

Mismatch returns **404, not 403**: a distinguishable response would confirm that
an order id is real and hand an attacker a working enumeration oracle.

## Verification

- `OrderReadScopingTests`: **4 passed** — correct session → 200 (and the body
  carries the email, proving the read still works); wrong session → 404;
  missing → 404; whitespace-only → 404.
- Orders + Payments + Carts filter: 16 failed / 34 passed (50). Stashed-HEAD
  baseline on the identical filter: 16 failed / 30 passed (46). **Same 16
  failures before and after** — all `PlatformNotSupportedException: LocalDB is
  not supported on this platform`, environmental to this Linux container. The
  delta is exactly my 4 new passing tests.
- One test bug found and fixed mid-run: I first deserialized into
  `OrderResponseDto`, but the API writes `OrderStatus` as a string. The endpoint
  had returned 200 correctly; the assertion was wrong. Now reads via
  `JsonDocument`.
- Frontend typecheck **not run** — `landing-page/node_modules` is absent in this
  container. Verified by inspection: `fetchOrderById` has one caller, updated.

## Caveat for local testing

`FakePaymentProvider` returns `https://example.test/checkout/{orderId}` — it
never redirects to `/checkout/success` at all. So this flow cannot be exercised
end-to-end in dev without real Stripe test keys or a hand-built URL
(`/checkout/success?orderId={id}&session_id=fake-{id}`). This is the same blind
spot that let the original defect pass UAT.
