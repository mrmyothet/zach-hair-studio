---
quick_id: 260815-3xf
slug: stripe-success-orderid
status: complete
completed: 2026-08-15
requirements: [SHOP-02]
---

# Summary: put orderId in SuccessUrl; drop the regex

Closes blocker 1 of `.planning/v1.0-MILESTONE-AUDIT.md`.

## Changes

- `API/ZachHairStudio.Shared/Features/Payments/StripePaymentProvider.cs` —
  extracted `BuildSuccessUrl(configuredUrl, orderId)`, which appends
  `orderId={id}` to the success URL while preserving an operator-supplied
  `{CHECKOUT_SESSION_ID}` placeholder and picking the right `?`/`&` separator.
  Made `public static` for testability (the project has no `InternalsVisibleTo`
  convention, so adding one would have been the larger change).
- `landing-page/app/checkout/success/page.tsx` — `parseOrderId` now accepts only
  an explicit numeric `orderId`/`order`. The trailing-digit `session_id` branch
  is gone. `session_id` stays in the `Props` type because Stripe still sends it.
- `API/ZachHairStudio.Api.Tests/Features/Payments/StripeSuccessUrlTests.cs` — 3
  tests: orderId appended, operator placeholder not duplicated, and a realistic
  `cs_test_*` id (ending in a letter) carries no usable order id.

## Verification

- `StripeSuccessUrlTests` + `StripeWebhookTests`: **7 passed, 0 failed**.
- Full suite: 200 failed / 176 passed. Those 200 failures are **pre-existing** —
  a HEAD baseline with all changes stashed (`git stash -u`) gives the identical
  200 failures (173 passed / 373 total). Cause is environmental:
  `System.PlatformNotSupportedException: LocalDB is not supported on this
  platform` from `SqlServerWebApplicationFactory.CreateHost`. Every SQL
  Server-backed test fails on this Linux container regardless of this change.
- Frontend typecheck **not run** — `landing-page/node_modules` is not installed
  in this container. The edit was verified by inspection: the call site passes an
  object with an extra `session_id` property to a narrower parameter type, which
  TypeScript permits for a non-literal argument.

## Follow-up (not done here)

`GET /api/orders/{id}` is still anonymous and unscoped (`OrdersController.cs:161`,
audit blocker 2 / ACCT-06). Putting `orderId` in the URL makes the handle
explicit but does not protect it — the order read must be scoped before this
flow is safe in production.
