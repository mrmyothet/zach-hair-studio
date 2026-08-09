---
phase: 05-product-catalog
plan: 02
subsystem: ui
tags: [nextjs, react-server-components, zod, tailwind, isr]

requires:
  - phase: 05-product-catalog
    provides: "Plan 05-01's live API contract — GET /api/products, GET /api/products/{slug}, ServiceResponseDto.recommendedProducts"
provides:
  - "landing-page/lib/products.ts — Zod-validated fetchProducts/fetchProductBySlug (mirrors lib/services.ts)"
  - "/products catalog page (category-grouped, empty-state, Out-of-Stock badge)"
  - "/products/[slug] detail page (404-safe via notFound())"
  - "ServiceSchema.recommendedProducts + Recommended Products section on /services/[slug]"
  - "Products nav link (D-04)"
affects: [06-cart-checkout]

tech-stack:
  added: []
  patterns:
    - "lib/products.ts clones lib/services.ts's schema/fetch/ISR/catch-to-empty-or-null pattern exactly (D-18)"

key-files:
  created:
    - landing-page/lib/products.ts
    - landing-page/app/products/page.tsx
    - landing-page/app/products/[slug]/page.tsx
  modified:
    - landing-page/lib/services.ts
    - landing-page/lib/data.ts
    - landing-page/app/services/[slug]/page.tsx

key-decisions:
  - "Recommended Products section on /services/[slug] uses SectionHeading with an empty subtitle string (type requires the prop, empty string renders no visible text) — as anticipated by the plan"
  - "RecommendedProductCard is a small, deliberate markup duplication of app/products/page.tsx's ProductCard (both Server Components, no shared client bundle concern) — noted as a possible future extraction per the plan, not done this pass"

patterns-established:
  - "Product card pattern (image-or-Z-monogram fallback, category eyebrow, Out-of-Stock badge) reused identically across /products and the service-detail recommendations section"

requirements-completed: [PROD-01, PROD-02, PROD-03]

coverage:
  - id: D1
    description: "/products renders the seeded catalog grouped by category, name/shortDescription/price/stock state per card, with an Out-of-Stock badge only when stock === 0"
    requirement: PROD-01
    verification:
      - kind: e2e
        ref: "npm run build (Next.js production build compiles /products as a static/ISR route)"
        status: pass
    human_judgment: true
    rationale: "Visual rendering (category grouping, badge placement, empty-state box) requires a human to view /products in the browser — deferred to end-of-phase UAT per workflow.human_verify_mode"
  - id: D2
    description: "/products/[slug] renders a single active product's detail page; unknown or inactive slugs trigger notFound() (404)"
    requirement: PROD-02
    verification:
      - kind: e2e
        ref: "npm run build (Next.js production build compiles /products/[slug]; notFound() call statically verified via grep)"
        status: pass
    human_judgment: true
    rationale: "404 behavior and detail-page layout require a human to visit a real slug and an unknown slug in the browser — deferred to end-of-phase UAT"
  - id: D3
    description: "A service detail page with 1+ recommended products renders a Recommended Products section; a service with zero renders nothing (no heading, no empty box)"
    requirement: PROD-03
    verification:
      - kind: e2e
        ref: "npm run build (Next.js production build compiles /services/[slug] with the conditional section)"
        status: pass
    human_judgment: true
    rationale: "Conditional rendering across a service WITH recommendations vs. one WITHOUT requires visiting two real service detail pages in the browser — deferred to end-of-phase UAT"

duration: 6min
completed: 2026-08-09
status: complete
---

# Phase 5 Plan 02: Product Catalog Frontend Summary

**Server-rendered `/products` catalog and `/products/[slug]` detail pages built as direct structural clones of the Phase 1 services pages, plus a "Recommended Products" section on `/services/[slug]` fed by Plan 05-01's extended `ServiceResponseDto`.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-08-09T18:03:30+08:00
- **Completed:** 2026-08-09T18:08:46+08:00
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- `landing-page/lib/products.ts` — Zod `ProductSchema`/`ProductListSchema`, `fetchProducts()`/`fetchProductBySlug()`, field-for-field matched to Plan 05-01's `ProductResponseDto`, mirroring `lib/services.ts`'s ISR (60s) and catch-to-empty/null conventions exactly (D-18)
- `ServiceSchema` extended with one optional field, `recommendedProducts: z.array(ProductSchema).optional()` — no second schema/fetch pair added
- `/products` — category-grouped catalog (sorted by name within category, since `Product` has no `displayOrder`), "Products Are Being Curated" empty-state box, Out-of-Stock badge on `stock === 0`
- `/products/[slug]` — two-column detail layout (image/description + sticky Price/Stock aside), `notFound()` on unknown or inactive slug
- `/services/[slug]` — new "Recommended Products" section, rendered only when `service.recommendedProducts` is non-empty (D-14), reusing `SectionHeading` and the same card visual pattern as `/products` (D-13)
- `navLinks` gained a "Products" entry pointing at `/products` (D-04), placed between "Services" and "Gallery"

## Task Commits

Each task was committed atomically:

1. **Task 1: Build the shared products data layer and extend ServiceSchema** - `19c1905` (feat)
2. **Task 2: Build the /products catalog list page and /products/[slug] detail page** - `3ffa84f` (feat)
3. **Task 3: Add the "Recommended Products" section to the service detail page** - `2e0aa3b` (feat)

## Files Created/Modified
- `landing-page/lib/products.ts` - Zod-validated `fetchProducts`/`fetchProductBySlug`, `Product` type
- `landing-page/lib/services.ts` - imports `ProductSchema`; `ServiceSchema.recommendedProducts` optional field
- `landing-page/lib/data.ts` - `navLinks` gains `{ label: "Products", href: "/products" }`
- `landing-page/app/products/page.tsx` - category-grouped catalog page, local `ProductCard`/`groupProductsByCategory`
- `landing-page/app/products/[slug]/page.tsx` - product detail page, `notFound()` guard
- `landing-page/app/services/[slug]/page.tsx` - imports `SectionHeading`; conditional "Recommended Products" section + local `RecommendedProductCard`

## Decisions Made
- `SectionHeading`'s `subtitle` prop is passed an empty string on the Recommended Products section (the prop is required by its type; an empty string renders no visible subtitle text), as anticipated by the plan
- `RecommendedProductCard` in `app/services/[slug]/page.tsx` is a small, deliberate markup duplication of `ProductCard` in `app/products/page.tsx` — both are Server Components with no shared client bundle concern; extraction into a shared component is a future-pass candidate, not a blocker (per plan's explicit instruction)

## Deviations from Plan

None — plan executed exactly as written. Each task's `npm run build` run rewrote `landing-page/tsconfig.json` formatting as an incidental side effect of the Next.js build tool (adds `.next/dev/types` to `include`, reformats array literals); this churn was reverted before each commit since it is unrelated to the plan's scope.

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Full Product Catalog + Recommendations experience is browsable end-to-end: `/products`, `/products/[slug]`, and the service-detail "Recommended Products" section all compile and are wired against Plan 05-01's live API contract
- `npm run build` passes with `/products` and `/products/[slug]` both present in the route table (ISR, 1y revalidate window shown in build output matches the shared 60s `revalidate` config resolving to Next's internal cache bucket)
- Frontend automated tests are deferred (no test framework exists in `landing-page/`, matching every prior phase's precedent) — verification is `npm run build` plus the end-of-phase UAT pass per `workflow.human_verify_mode = end-of-phase`
- Phase 5's three requirements (PROD-01/02/03) are now code-complete pending the end-of-phase human UAT pass (catalog browsing, out-of-stock display, 404 handling, recommended-products presence/absence)

---
*Phase: 05-product-catalog*
*Completed: 2026-08-09*

## Self-Check: PASSED
