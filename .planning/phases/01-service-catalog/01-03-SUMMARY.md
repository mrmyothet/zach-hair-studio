---
phase: 01-service-catalog
plan: 03
subsystem: ui
tags: [nextjs, react-server-components, zod, service-catalog, booking-route]

requires:
  - phase: 01-service-catalog
    provides: Services API endpoints, ServiceResponseDto contract, seeded service slugs
provides:
  - Zod-validated service fetch layer for the landing page
  - Shared service duration formatter
  - Public /services catalog page grouped by category
  - Public /services/[slug] detail page
  - Dedicated /book route with service preselection from query string
affects: [service-catalog, homepage-services, booking-core]

tech-stack:
  added:
    - zod 4.4.3
  patterns:
    - React Server Component service fetching with ISR
    - Zod response parsing at the frontend API boundary
    - Dedicated booking request route seeded from service slug

key-files:
  created:
    - landing-page/lib/services.ts
    - landing-page/lib/formatDuration.ts
    - landing-page/app/services/page.tsx
    - landing-page/app/services/[slug]/page.tsx
    - landing-page/app/book/page.tsx
    - landing-page/components/BookingRequestForm.tsx
  modified:
    - landing-page/package.json
    - landing-page/package-lock.json

key-decisions:
  - "Service API responses are parsed through Zod before rendering."
  - "Catalog pages use Server Components and a 60-second revalidate window."
  - "User changed the booking CTA target from homepage contact anchor to a dedicated /book route."

patterns-established:
  - "Frontend catalog data comes from `landing-page/lib/services.ts`, not direct component fetch duplication."
  - "Service detail CTAs pass `service={slug}` into a dedicated booking route."
  - "Duration display is centralized in `formatDuration(minutes)`."

requirements-completed: [CAT-01, CAT-02, PLAT-02]
coverage:
  - id: D1
    description: "/services renders the service catalog grouped by category with name, teaser, duration, and price."
    requirement: CAT-01
    verification:
      - kind: other
        ref: "npx tsc --noEmit && npm run build"
        status: pass
      - kind: manual_procedural
        ref: "Human checkpoint: /services grouped catalog verified"
        status: pass
    human_judgment: false
  - id: D2
    description: "/services/[slug] renders a single service detail page and unknown slugs render 404."
    requirement: CAT-02
    verification:
      - kind: other
        ref: "npx tsc --noEmit && npm run build"
        status: pass
      - kind: manual_procedural
        ref: "Human checkpoint: service detail and unknown-slug 404 verified"
        status: pass
    human_judgment: false
  - id: D3
    description: "Frontend service data is validated with Zod schemas matching ServiceResponseDto."
    requirement: PLAT-02
    verification:
      - kind: other
        ref: "npx tsc --noEmit && npm run build"
        status: pass
    human_judgment: false
  - id: D4
    description: "Book This Service opens /book?service={slug} with the selected service prefilled."
    requirement: CAT-02
    verification:
      - kind: manual_procedural
        ref: "Human checkpoint: dedicated booking route flow approved"
        status: pass
    human_judgment: false

duration: 51min
completed: 2026-07-08
status: complete
---

# Phase 1 Plan 03 Summary

**API-backed service catalog pages with Zod validation and a dedicated booking request route**

## Performance

- **Duration:** 51 min, including checkpoint-driven CTA route adjustment
- **Started:** 2026-07-08T04:43:00Z
- **Completed:** 2026-07-08T05:34:00Z
- **Tasks:** 4
- **Files modified:** 8

## Accomplishments

- Added `landing-page/lib/services.ts` with Zod schemas, typed service fetchers, and 60-second ISR fetch behavior.
- Added `landing-page/lib/formatDuration.ts` and used it across catalog/detail display.
- Built `/services` as a Server Component catalog page grouped by category and ordered by display order.
- Built `/services/[slug]` as a Server Component detail page with `notFound()` handling for missing services.
- Added `/book?service={slug}` with its own API-backed booking request form after the user requested a dedicated route instead of the homepage contact anchor.

## Task Commits

Each implementation task was committed atomically:

1. **Task 1: Shared services data layer + duration formatter** - `6953062` (feat)
2. **Task 2: /services catalog list page** - `60a5b5f` (feat)
3. **Task 3: /services/[slug] detail page + booking CTA** - `86ed534` (feat)
4. **Checkpoint fix: dedicated booking route instead of homepage contact anchor** - `45355a8` (fix)

## Files Created/Modified

- `landing-page/lib/services.ts` - Zod schemas and API-backed service fetchers.
- `landing-page/lib/formatDuration.ts` - Shared duration formatter.
- `landing-page/app/services/page.tsx` - Public catalog list page.
- `landing-page/app/services/[slug]/page.tsx` - Public service detail page.
- `landing-page/app/book/page.tsx` - Dedicated booking request route.
- `landing-page/components/BookingRequestForm.tsx` - Client booking form with API service preselection.
- `landing-page/package.json` - Zod dependency.
- `landing-page/package-lock.json` - Dependency lockfile update.

## Decisions Made

- Chose a 60-second revalidate window for service catalog fetches.
- Avoided `generateStaticParams` for service details so staff-editable services in Phase 4 are not baked at build time.
- Changed the original Plan 03 CTA target from `/#contact?service={slug}` to `/book?service={slug}` at user request, creating a route-specific form rather than using the existing homepage contact form.

## Deviations from Plan

### Auto-fixed Issues

**1. User-directed CTA route change**
- **Found during:** Task 4 human verification
- **Issue:** The original plan routed Book CTAs to the existing homepage contact form. The user requested a new route instead.
- **Fix:** Added `/book` and `BookingRequestForm`, then changed detail CTA href to `/book?service={slug}`.
- **Files modified:** `landing-page/app/services/[slug]/page.tsx`, `landing-page/app/book/page.tsx`, `landing-page/components/BookingRequestForm.tsx`
- **Verification:** `npx tsc --noEmit`, `npm run build`, lints, and human checkpoint approval.
- **Committed in:** `45355a8`

---

**Total deviations:** 1 user-directed route change
**Impact on plan:** Improves booking flow separation and avoids coupling service detail CTAs to the homepage contact anchor. Plan 04 should account for the new `/book` route when retiring static contact dropdown data.

## Issues Encountered

- The first human verification failed because the CTA still used the original homepage contact target. The route was changed and reverified.

## User Setup Required

None.

## Next Phase Readiness

Plan 04 can move the homepage Services subset and remaining Contact dropdown off static `lib/data.ts`. It should preserve the dedicated `/book` route as the service-detail booking destination and update any remaining static booking-service options consistently.

## Self-Check: PASSED

- `npx tsc --noEmit` passed in `landing-page/`.
- `npm run build` passed in `landing-page/`.
- Human checkpoint approved the updated `/book?service={slug}` flow and unknown-slug 404.

---
*Phase: 01-service-catalog*
*Completed: 2026-07-08*
