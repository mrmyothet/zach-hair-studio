# Backlog

Items found outside the phase that raised them. Not blocking their origin phase.

## BUG-01 — Loyalty redemption is not applied to the Stripe charge (ACCT-07, Phase 7)

**Severity:** high — real money. The customer is overcharged.

**Found:** 2026-08-15, during Phase 6 verification (filed against Phase 7, not Phase 6).

**What happens:** `OrdersService.CreateCheckoutAsync` sets
`order.TotalAmount = subtotal - loyaltyDiscount` (`OrdersService.cs:127`), then builds the
payment request with that discounted total. But `StripePaymentProvider` never reads
`request.TotalAmount` — it constructs `LineItems` purely from
`UnitAmount = line.UnitPrice * 100` per line (`StripePaymentProvider.cs:54-61`).

So `CheckoutSessionRequest.TotalAmount` is a dead field. A logged-in client who redeems
points sees the discount in the DB and in the checkout response, but **Stripe charges the
full undiscounted sum**.

**Why it is not a Phase 6 gap:** SC2 (server-authoritative pricing) still holds — the charged
amount is derived server-side from the catalog, never from client input. The defect is that
the Phase 7 loyalty discount never reaches the provider.

**Why it was not caught:** no automated test exercises `StripePaymentProvider` at all
(Testing binds `FakePaymentProvider`, which ignores both fields), and the loyalty
earn/redeem runtime is itself `behavior_unverified` in `07-VERIFICATION.md`.

**Fix sketch:** apply the discount at the provider boundary — either a Stripe
`Discounts`/coupon on the session, or distribute the reduction across line `UnitAmount`s so
the line items still sum to `order.TotalAmount`. Do not silently pass `TotalAmount` as a
single line item; that would destroy the per-product breakdown on the hosted page.

**Regression test to add with the fix:** assert that the sum of session line-item amounts
equals `order.TotalAmount` when `pointsRedeemed > 0`.
