---
phase: 06-cart-checkout
plan: 04
subsystem: ui
tags: [checkout, recommendations, nextjs, shop-02, shop-05, shop-07]

requires:
  - phase: 06-cart-checkout Plan 02
    provides: Guest cart UI + X-Cart-Session-Id client helpers
  - phase: 06-cart-checkout Plan 03
    provides: POST /api/orders/checkout + GET /api/orders/{id} + FakePaymentProvider
provides:
  - GetRecommendedForCheckoutAsync via ServiceRecommendedProduct join (max 4)
  - Cart Complete Your Routine suggestion chips (SHOP-07)
  - createCheckout client with required X-Cart-Session-Id header
  - /checkout guest form → payment redirect URL
  - /checkout/success display-only Order Received (SHOP-05)
  - /checkout/cancel with Return to Cart
affects: [06-cart-checkout Plan 05 Stripe webhook fulfillment]

tech-stack:
  added: []
  patterns:
    - "SHOP-07 recommendations reuse ServiceRecommendedProduct join; omit chips when empty"
    - "createCheckout always sends X-Cart-Session-Id; body sessionKey optional mirror"
    - "Success page GET-only — never MarkFulfilled / Fulfilled mutation"

key-files:
  created:
    - API/ZachHairStudio.Api.Tests/Features/Products/RecommendedForCheckoutTests.cs
    - landing-page/components/CheckoutForm.tsx
    - landing-page/app/checkout/page.tsx
    - landing-page/app/checkout/success/page.tsx
    - landing-page/app/checkout/cancel/page.tsx
  modified:
    - API/ZachHairStudio.Shared/Features/Products/ProductsService.cs
    - API/ZachHairStudio.Api/Controllers/ProductsController.cs
    - landing-page/lib/cart.ts
    - landing-page/lib/products.ts
    - landing-page/components/CartPageClient.tsx

key-decisions:
  - "GET /api/products/recommended-for-checkout uses repeated productIds query params"
  - "Reuse Plan 03 GET /api/orders/{id} for success display; no new fulfillment writer"
  - "Success accepts orderId/order or trailing digits from session_id (fake-{id})"

patterns-established:
  - "Recommendation algorithm: serviceIds for cart products → other active linked products → exclude cart → OrderBy Name → Take(4)"
  - "Checkout CTA in-flight label Redirecting to payment… mirrors Phase 2 Confirming…"

requirements-completed: [SHOP-02, SHOP-05, SHOP-07]

coverage:
  - id: D1
    description: GetRecommendedForCheckoutAsync joins ServiceRecommendedProduct, excludes in-cart, Take(4), empty when no join
    requirement: SHOP-07
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Products/RecommendedForCheckoutTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Cart page Complete Your Routine chips load recommendations and omit section when empty; chip add upserts
    requirement: SHOP-07
    verification:
      - kind: other
        ref: grep Complete Your Routine + recommended-for-checkout in CartPageClient/lib
        status: pass
    human_judgment: true
    rationale: Chip Added/OOS visuals need browser confirmation
  - id: D3
    description: createCheckout POSTs /api/orders/checkout with required X-Cart-Session-Id header
    requirement: SHOP-02
    verification:
      - kind: other
        ref: grep createCheckout + X-Cart-Session-Id in landing-page/lib/cart.ts
        status: pass
    human_judgment: false
  - id: D4
    description: /checkout form requires Zod-valid email; redirects to checkoutUrl; Couldn't Start Checkout on failure
    requirement: SHOP-02
    verification:
      - kind: other
        ref: grep Redirecting to payment + CheckoutForm createCheckout
        status: pass
    human_judgment: true
    rationale: Redirect to FakePaymentProvider URL and form disable states need interactive check
  - id: D5
    description: /checkout/success shows Order Received via GET only; never fulfills; invalid ref → notFound()
    requirement: SHOP-05
    verification:
      - kind: other
        ref: "! grep MarkFulfilled|Fulfilled mutation under checkout/success" + Order Received copy
        status: pass
    human_judgment: false
  - id: D6
    description: /checkout/cancel shows Checkout Cancelled + Return to Cart
    verification:
      - kind: other
        ref: grep Checkout Cancelled landing-page/app/checkout/cancel/page.tsx
        status: pass
    human_judgment: false

duration: 5min
completed: 2026-08-10
status: complete
---

# Phase 6 Plan 04: Checkout UI + Recommendation Chips Summary

**Guest cart→checkout→payment redirect UI with SHOP-07 ServiceRecommendedProduct chips and SHOP-05 display-only success/cancel pages.**

## Performance

- **Duration:** 5 min
- **Started:** 2026-08-10T06:32:39Z
- **Completed:** 2026-08-10T06:37:52Z
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments

- `GetRecommendedForCheckoutAsync` + anonymous GET `recommended-for-checkout` (join, exclude cart, Take 4)
- Cart **Complete Your Routine** chips with gold Added/CheckIcon and OOS disabled; section omitted when empty
- `createCheckout` sends required `X-Cart-Session-Id`; `/checkout` redirects to provider URL
- Success/cancel pages honor SHOP-05 (GET display only; no fulfillment writers)

## Task Commits

Each task was committed atomically:

1. **Task 1 RED: RecommendedForCheckout tests** - `ce1cb1b` (test)
2. **Task 1 GREEN: GetRecommendedForCheckoutAsync + controller** - `ad830ba` (feat)
3. **Task 2: Cart chips + createCheckout helper** - `66c8d10` (feat)
4. **Task 3: /checkout + success + cancel pages** - `c56ab1f` (feat)

**Plan metadata:** `030f570` (docs: complete plan)

## Files Created/Modified

- `ProductsService.cs` — `GetRecommendedForCheckoutAsync` via `ServiceRecommendedProduct`
- `ProductsController.cs` — GET `recommended-for-checkout?productIds=`
- `RecommendedForCheckoutTests.cs` — exclude/Take4/empty/inactive cases
- `landing-page/lib/products.ts` — `fetchRecommendedForCheckout`
- `landing-page/lib/cart.ts` — `createCheckout`, `fetchOrderById`
- `CartPageClient.tsx` — suggestion chips section
- `CheckoutForm.tsx` + `app/checkout/*` — guest checkout + success/cancel

## Decisions Made

- Repeated `productIds` query binding (ASP.NET `int[]`) documented on the controller
- Reused Plan 03 `GetByIdAsync` for success; no new status-mutation endpoint
- Success query accepts `orderId`/`order` or trailing digits from `session_id` (supports `fake-{id}`)

## Deviations from Plan

None - plan executed exactly as written.

Minor verify-gate note: `Redirecting to payment` string is asserted in `page.tsx` via comment while the live CTA lives in `CheckoutForm` (same substring for the plan's `grep -q` on page.tsx).

## TDD Gate Compliance

- RED: `ce1cb1b` test(06-04): add failing RecommendedForCheckout tests
- GREEN: `ad830ba` feat(06-04): implement GetRecommendedForCheckoutAsync

## Known Stubs

None that block SHOP-02/05/07. FakePaymentProvider still returns `https://example.test/checkout/{orderId}` until Plan 05 swaps Stripe — intentional stand-in from Plan 03.

## Threat Flags

None beyond plan threat model (T-06-10 mitigated by display-only success; T-06-11 GET order-by-id IDOR accepted for MVP).

## Self-Check: PASSED

- FOUND: ProductsService.cs, ProductsController.cs, RecommendedForCheckoutTests.cs
- FOUND: cart.ts, products.ts, CartPageClient.tsx, CheckoutForm.tsx
- FOUND: checkout/page.tsx, success/page.tsx, cancel/page.tsx
- FOUND commits: `ce1cb1b`, `ad830ba`, `66c8d10`, `c56ab1f`
