# Phase 6 — Stripe API Coverage Decision

**Phase:** 06-cart-checkout  
**Provider:** Stripe (Checkout Sessions + Webhooks) via Stripe.net 52.2.0  
**Seam:** `IPaymentProvider` (CONTEXT Payment Provider / D-01)  
**Mode:** Test-mode keys for MVP (Claude's Discretion)  
**Created:** 2026-08-10

> Full coverage by default. Every OPT-OUT is explicit with reason. Capability surface is Checkout Session create + webhook verification/fulfillment — not the entire Stripe product catalog.

## Coverage Matrix

| Capability | Decision | Reason |
|------------|----------|--------|
| Checkout Session create `Mode=payment` — `SessionService.CreateAsync` | **INTEGRATE** | Locked: SHOP-02 guest pay path; CONTEXT Stripe Checkout Session |
| Line items via `price_data` — `SessionLineItemPriceDataOptions` | **INTEGRATE** | SHOP-03 server price authority; avoid syncing Stripe Product/Price objects |
| `ClientReferenceId` + `Metadata["order_id"]` — Session create options | **INTEGRATE** | Correlate webhook → local `Order.Id` for MarkFulfilled |
| `CustomerEmail` prefills — Session create options | **INTEGRATE** | UI-SPEC `/checkout` Email required; prefill Stripe page |
| `SuccessUrl` / `CancelUrl` redirects — Session create options | **INTEGRATE** | Display-only return pages; never fulfill from redirect (SHOP-05) |
| Optional Idempotency-Key `order-{orderId}` — Stripe request options | **INTEGRATE** | Discretion: safe Session create retries after network blips |
| Webhook signature verification — `EventUtility.ConstructEvent` | **INTEGRATE** | SHOP-05; never hand-rolled HMAC |
| `checkout.session.completed` + `payment_status=paid` — Event Types | **INTEGRATE** | Card MVP fulfillment gate (SHOP-05) |
| Idempotent fulfill once — app + filtered unique `Order.StripeSessionId` | **INTEGRATE** | Stripe retries; Pattern 8 |
| Stripe CLI `listen` / Dashboard endpoint — DevOps | **INTEGRATE** | Local UAT only; SHOP-05 human verify; `Stripe:WebhookSecret` from CLI `whsec_` in user-secrets |
| Stripe Elements / `@stripe/stripe-js` card form — Client SDK | **OPT-OUT** | Hosted Checkout collapses PCI; CONTEXT locks Checkout Session; no new frontend Stripe dep |
| Pre-created Stripe Products/Prices — Catalog sync APIs | **OPT-OUT** | Small salon catalog; `price_data` avoids dual catalog (RESEARCH Alternatives) |
| Payment Intents API direct — PaymentIntents | **OPT-OUT** | Checkout Session wraps PI; no custom confirm flow this phase |
| Customer Portal — Billing Portal | **OPT-OUT** | No subscriptions / self-serve billing UI in SHOP-* |
| Subscriptions / recurring — Subscriptions API | **OPT-OUT** | REQUIREMENTS out-of-scope membership billing |
| Stripe Connect / marketplace — Connect | **OPT-OUT** | Single-salon retailer; REQUIREMENTS excludes marketplace |
| Refunds / disputes — Refunds API | **OPT-OUT** | No SHOP requirement; staff ops later if needed |
| `checkout.session.async_payment_succeeded` — Async payment events | **OPT-OUT** | RESEARCH A3: immediate card `paid` only for MVP |
| Tax / Shipping Rates / Address collection extras — Checkout options | **OPT-OUT** | Physical-goods ship policy not in phase scope; keep Session minimal |

## Secrets (never tracked)

| Key | Source | ValidateOnStart |
|-----|--------|-----------------|
| `Stripe:SecretKey` | `dotnet user-secrets` / env (`sk_test_...` MVP) | Yes (mirror Jwt) |
| `Stripe:WebhookSecret` | CLI listen or Dashboard endpoint (`whsec_...`) | Yes |

## Package

| Package | Version | Project | Legitimacy |
|---------|---------|---------|------------|
| Stripe.net | 52.2.0 | `ZachHairStudio.Shared` (alongside `StripePaymentProvider`) | OK — official Stripe SDK (see 06-RESEARCH Package Legitimacy Audit) |

## Opt-out summary

No OPT-OUT removes a SHOP-01..07 requirement. Deferred Stripe surfaces are subscriptions, Connect, refunds, Elements, async payment methods, and tax/shipping — all outside phase boundary or superseded by hosted Checkout + `price_data`.
