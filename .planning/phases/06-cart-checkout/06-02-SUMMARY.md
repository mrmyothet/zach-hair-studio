---
phase: 06-cart-checkout
plan: 02
subsystem: ui
tags: [cart, checkout, nextjs, zod, localStorage, shop-01]

requires:
  - phase: 06-cart-checkout
    provides: Session-keyed guest Cart API under X-Cart-Session-Id with server-enriched UnitPrice/LineTotal
provides:
  - Guest cart session helper (localStorage UUID + X-Cart-Session-Id)
  - Zod cart fetch layer (fetchCart / upsertCartItem / removeCartItem)
  - Product-detail Add to Cart panel with quantity stepper
  - Navbar Cart link with live gold count badge
  - /cart review page (line items, Order Summary, empty/error/loading)
affects: [06-cart-checkout Plan 03 Stripe checkout, Plan 04 suggestion chips]

tech-stack:
  added: []
  patterns:
    - "Guest cart session via localStorage + X-Cart-Session-Id (no cookies)"
    - "Throw-on-error cart client mirroring appointments.ts (CartApiError)"
    - "CustomEvent zhs:cart-updated to refresh Navbar badge without full reload"
    - "Charcoal pulse skeleton as first landing-page loading UX"

key-files:
  created:
    - landing-page/lib/cartSession.ts
    - landing-page/lib/cart.ts
    - landing-page/components/AddToCartPanel.tsx
    - landing-page/components/CartPageClient.tsx
    - landing-page/app/cart/page.tsx
  modified:
    - landing-page/components/icons.tsx
    - landing-page/components/Navbar.tsx
    - landing-page/app/products/[slug]/page.tsx
    - landing-page/lib/data.ts

key-decisions:
  - "Add-to-cart is additive (current qty + selected) clamped to stock, then absolute upsert"
  - "Suggestion chips / Complete Your Routine omitted until Plan 04 (no fake chip data)"
  - "Proceed to Checkout links to /checkout (may 404 until Plan 03/04 — acceptable interim)"

patterns-established:
  - "Cart client never swallows errors to empty arrays; empty items is a valid success"
  - "Write bodies send productId+quantity only — money fields are response-only (D-05 / T-06-04)"
  - "Navbar cart count listens for CART_UPDATED_EVENT after mutations"

requirements-completed: [SHOP-01]

coverage:
  - id: D1
    description: Product detail aside exposes Add to Cart with quantity stepper; success updates navbar cart count
    requirement: SHOP-01
    verification:
      - kind: other
        ref: grep Add to Cart + CartIcon/Navbar + AddToCartPanel wired in products/[slug]
        status: pass
    human_judgment: true
    rationale: Manual browser smoke confirms live badge increment after add
  - id: D2
    description: /cart shows line items with server UnitPrice/LineTotal, steppers, Remove, Order Summary, Proceed to Checkout
    requirement: SHOP-01
    verification:
      - kind: other
        ref: grep Order Summary + Proceed to Checkout + line-clamp-2 in CartPageClient.tsx
        status: pass
    human_judgment: true
    rationale: Visual layout and quantity/remove behavior need browser confirmation
  - id: D3
    description: Empty cart renders Your Cart Is Empty with Browse Products CTA
    requirement: SHOP-01
    verification:
      - kind: other
        ref: grep "Your Cart Is Empty" landing-page/components/CartPageClient.tsx
        status: pass
    human_judgment: false
  - id: D4
    description: Cart load failure shows Couldn't Load Your Cart rose banner with Try Again
    requirement: SHOP-01
    verification:
      - kind: other
        ref: grep "Couldn't Load Your Cart" landing-page/components/CartPageClient.tsx
        status: pass
    human_judgment: false
  - id: D5
    description: Navbar Cart badge shows when count >= 1 and hides at 0; out-of-stock disables Add to Cart
    requirement: SHOP-01
    verification:
      - kind: other
        ref: Navbar CartBadge + AddToCartPanel opacity-40 cursor-not-allowed when stock===0
        status: pass
    human_judgment: true
    rationale: Badge visibility and disabled CTA need interactive confirmation
  - id: D6
    description: Upsert request body is productId+quantity only (no client money fields) — T-06-04
    requirement: SHOP-01
    verification:
      - kind: other
        ref: "! grep -qiE '\\b(price|total)\\b' landing-page/lib/cart.ts" plus X-Cart-Session-Id present
        status: pass
    human_judgment: false

duration: 3min
completed: 2026-08-10
status: complete
---

# Phase 6 Plan 02: Guest Cart UI Summary

**Wired landing-page cart session + Zod client to Plan 01 APIs; shipped Add to Cart, navbar badge, and /cart review per UI-SPEC (SHOP-01 human path).**

## Performance

- **Duration:** 3 min
- **Started:** 2026-08-10T06:21:16Z
- **Completed:** 2026-08-10T06:24:18Z
- **Tasks:** 3
- **Files modified:** 9

## Accomplishments

- Guest cart session UUID in localStorage (`zhs-cart-session`) sent as `X-Cart-Session-Id` on every cart call
- Product detail Add to Cart panel with stepper, Added/CheckIcon success swap, rose failure alert, OOS disabled CTA
- Navbar Cart entry (desktop left of Book Now + mobile menu) with gold count badge refreshed via `zhs:cart-updated`
- `/cart` review with line items, Order Summary, empty/error/loading states and UI-SPEC copy

## Task Commits

Each task was committed atomically:

1. **Task 1: cartSession + lib/cart Zod fetch layer** - `30383d6` (feat)
2. **Task 2: Add to Cart on product detail + Navbar cart badge** - `68d6913` (feat)
3. **Task 3: /cart review page** - `6298fcb` (feat)

**Plan metadata:** `707f3ae` (docs: complete plan)

## Files Created/Modified

- `landing-page/lib/cartSession.ts` — localStorage UUID session helper
- `landing-page/lib/cart.ts` — Zod schemas, CartApiError, fetch/upsert/remove, cart-updated event
- `landing-page/components/AddToCartPanel.tsx` — detail-aside add flow
- `landing-page/components/CartPageClient.tsx` — cart review UI + states
- `landing-page/app/cart/page.tsx` — `/cart` route shell
- `landing-page/components/icons.tsx` — CartIcon, PlusIcon, MinusIcon
- `landing-page/components/Navbar.tsx` — Cart link + live badge
- `landing-page/app/products/[slug]/page.tsx` — mounts AddToCartPanel
- `landing-page/lib/data.ts` — `cartNavLink` export

## Decisions Made

- Add-to-cart is additive (existing line qty + selected) then absolute upsert, clamped to stock
- Omitted Complete Your Routine / suggestion chips — Plan 04 owns SHOP-07; no fake chip data
- Proceed to Checkout targets `/checkout` (acceptable interim 404 until checkout plan ships)

## Deviations from Plan

None - plan executed exactly as written.

Minor copy encoding: used `{"Couldn't …"}` string literals so plan `grep -q` gates match UI-SPEC apostrophes (JSX `&apos;` would fail the automated verify).

## Known Stubs

None that block SHOP-01. `/checkout` destination may 404 until Plan 03/04 — intentional interim per plan key_links.

## Threat Flags

None beyond plan threat model (T-06-04 mitigated by productId+quantity-only writes; T-06-05 localStorage nonce accepted).

## Self-Check: PASSED

- FOUND: landing-page/lib/cart.ts
- FOUND: landing-page/lib/cartSession.ts
- FOUND: landing-page/components/AddToCartPanel.tsx
- FOUND: landing-page/components/CartPageClient.tsx
- FOUND: landing-page/app/cart/page.tsx
- FOUND: 30383d6
- FOUND: 68d6913
- FOUND: 6298fcb
