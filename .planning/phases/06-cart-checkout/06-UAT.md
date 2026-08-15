---
status: testing
phase: 06-cart-checkout
source: 06-VERIFICATION.md
started: 2026-08-15T04:00:00Z
updated: 2026-08-15T04:00:00Z
supersedes: prior 2026-08-14 session (30/30) — invalidated, see note
---

## Note on the superseded session

The previous UAT recorded 30/30 pass on 2026-08-14. It is **not** valid evidence for this
phase and was replaced rather than resumed:

- It predates commits `5cebf63`, `674806e`, `c1530ad`, which changed the checkout return
  path, the guest order read, and webhook retry behavior.
- Its test 8 (Stripe end-to-end) was a confirmed false positive. Dev/Testing binds
  `FakePaymentProvider`, whose `fake-{orderId}` session id satisfied a since-deleted regex.
  That provider redirects to `https://example.test/checkout/{orderId}` and never reaches
  `/checkout/success` at all.
- Its test 30 (stock concurrency on SQL Server) was marked pass but cannot execute on this
  Linux host — `SqlServerWebApplicationFactory` throws
  `PlatformNotSupportedException: LocalDB is not supported on this platform`.

Only the items below remain. Everything else in Phase 6 is verified by code inspection plus
41 passing SQLite-backed tests (see `06-VERIFICATION.md`).

## Current Test

number: 1
name: Last-unit concurrency against real SQL Server
expected: |
  On a host with a reachable SQL Server, set TEST_SQLSERVER_CONNECTION (or
  ConnectionStrings__DefaultConnection) and run:
    dotnet test API/ZachHairStudio.Api.Tests/ --filter FullyQualifiedName~StockConcurrencyTests
  TwoParallelCheckoutsForLastUnit_ExactlyOneSuccessAndOne409 passes: exactly one 2xx,
  exactly one 409, and Products.Stock ends at 0 — never negative.
awaiting: user response

## Tests

### 1. Last-unit concurrency against real SQL Server
expected: With TEST_SQLSERVER_CONNECTION pointed at a live SQL Server, StockConcurrencyTests passes — exactly one 2xx, one 409, final Stock == 0 and never negative.
result: [pending]
blocks: SC3 / SHOP-04
why_human: Depends on real SQL Server row-locking under two genuinely parallel transactions. SQLite cannot substitute — it serializes writes, so a green run there would prove nothing. No code change needed; the factory already honors TEST_SQLSERVER_CONNECTION.

### 2. Real Stripe test-mode end-to-end
expected: With Stripe:SecretKey (sk_test_) and Stripe:WebhookSecret in user-secrets, API + landing-page running, and `stripe listen --forward-to localhost:5236/api/stripe/webhook` active — add a product, check out, pay with 4242 4242 4242 4242. Stripe shows a real hosted Checkout page with correct line items; the browser returns to /checkout/success?session_id=cs_...&orderId=N showing "Order Received" with the right order; the DB order flips to Fulfilled exactly once.
result: [pending]
blocks: SHOP-02 runtime
why_human: StripePaymentProvider.CreateCheckoutSessionAsync is never executed by any test — Testing and all test factories bind FakePaymentProvider. There is zero automated evidence that Stripe accepts the SessionCreateOptions payload or that Session.Url/Session.Id come back usable.

### 3. Webhook-only fulfillment (negative check)
expected: With `stripe listen` STOPPED, complete a test-mode payment and land on /checkout/success. The page renders "Order Received" but the DB order stays Pending. Starting `stripe listen` and replaying the event then flips it to Fulfilled.
result: [pending]
blocks: SC4 runtime confirmation
why_human: Static analysis already proves MarkFulfilledAsync is the only Fulfilled writer and the success page is GET-only. Proving the redirect alone cannot fulfill requires observing the real redirect with the webhook suppressed.

### 4. Guest checkout visual/UX walkthrough
expected: Product detail Add to Cart → navbar badge → /cart line items, quantity steppers, Remove, "Complete Your Routine" chips → Proceed to Checkout → /checkout email form → redirect. Each step matches 06-UI-SPEC styling and empty/error/loading states; the chips section is omitted entirely when there are no recommendations.
result: [pending]
why_human: Visual appearance and responsive layout cannot be verified by grep. landing-page has no node_modules on this host, so neither tsc nor a build could run.

### 5. Cancel path from the Stripe hosted page
expected: Begin checkout, then abandon on Stripe's hosted page. The browser returns to the configured CancelUrl (http://localhost:3000/cart), the cart still holds its items, and the order is not Fulfilled.
result: [pending]
why_human: Requires the real Stripe hosted page's cancel affordance.

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps

[none yet]
