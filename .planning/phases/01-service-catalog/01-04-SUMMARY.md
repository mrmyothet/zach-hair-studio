---
phase: 01-service-catalog
plan: 04
subsystem: ui
tags: [nextjs, react-server-components, service-catalog, single-source-of-truth]

requires:
  - phase: 01-service-catalog
    provides: Services API endpoints, seeded service catalog, ServiceResponseDto contract
  - phase: 01-service-catalog
    provides: fetchServices data layer, formatDuration helper (Plan 03)
provides:
  - API-backed homepage Services subset linking into the full catalog
  - API-backed Contact service dropdown with slug-based preselection
  - Retirement of the static service catalog from lib/data.ts
affects: [booking-core, homepage-services, staff-management]

tech-stack:
  added: []
  patterns:
    - Server Component fetches services once and passes them to client components as props
    - Query-param preselection guarded against unknown slugs

key-files:
  created: []
  modified:
    - landing-page/app/page.tsx
    - landing-page/components/Services.tsx
    - landing-page/components/Contact.tsx
    - landing-page/lib/data.ts

key-decisions:
  - "Homepage shows the first 6 services by displayOrder (HOMEPAGE_SERVICE_COUNT)."
  - "Contact receives services as a prop from the server page; client components never call fetchServices directly."
  - "Preselect resolves through a slug->Service Map and falls back to the empty option for unknown slugs."
  - "Booking API contract preserved: createBooking still receives a human-readable service string, not a slug."

patterns-established:
  - "Every service surface (homepage, /services, /services/[slug], Contact) reads from the single fetchServices data layer."
  - "Service option labels are produced by one formatServiceOption(service) helper."
  - "lib/data.ts holds only presentational site content (nav, gallery, team, reviews, branches, email) — never catalog data."

requirements-completed: [CAT-01, CAT-03]
coverage:
  - id: D1
    description: "Homepage Services section renders an API-backed subset of services and links to the full /services catalog."
    requirement: CAT-01
    verification:
      - kind: other
        ref: "cd landing-page && npm run build"
        status: pass
      - kind: other
        ref: "grep -q fetchServices app/page.tsx; ! grep -q '@/lib/data' components/Services.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "Contact form service dropdown is populated from the API catalog, with option value = service slug."
    requirement: CAT-03
    verification:
      - kind: other
        ref: "cd landing-page && ! grep -q serviceOptions components/Contact.tsx && npm run build"
        status: pass
    human_judgment: false
  - id: D3
    description: "Static `services` and `serviceOptions` exports removed from lib/data.ts; no remaining importers."
    requirement: CAT-03
    verification:
      - kind: other
        ref: "! grep -rq serviceOptions landing-page/{app,components,lib}"
        status: pass
      - kind: other
        ref: "cd landing-page && npm run build"
        status: pass
    human_judgment: false
  - id: D4
    description: "Opening the homepage with ?service={slug} pre-selects that service; unknown slugs fall back to the empty option; booking submit still succeeds end-to-end."
    requirement: CAT-01
    verification: []
    human_judgment: true
    rationale: "Task 4 is a blocking human-verify checkpoint requiring the running stack (.NET API + next dev). Visual regression, preselect behavior, and live booking submit cannot be proven by build/grep alone. Not yet performed — reviewer deferred it."

duration: 14min
completed: 2026-07-08
status: complete
---

# Phase 1 Plan 04 Summary

**Single source of truth for the service catalog — homepage subset and Contact dropdown moved onto the API data layer, static catalog deleted from lib/data.ts**

## Performance

- **Duration:** 14 min (12:26:22 → 12:40:29 +07:00)
- **Started:** 2026-07-08T05:26:22Z
- **Completed:** 2026-07-08T05:40:29Z
- **Tasks:** 3 of 4 (Task 4 is a human-verify checkpoint, still outstanding)
- **Files modified:** 4

## Accomplishments

- Converted `app/page.tsx` into an async Server Component that calls `fetchServices()` exactly once and fans the result out to both `Services` and `Contact`.
- Made `Services.tsx` fully props-driven, rendering the first 6 services by `displayOrder`, with each card linking to `/services/{slug}` and a "View Full Service Menu" CTA into `/services`.
- Rebuilt the Contact dropdown on the API catalog, with `?service={slug}` preselection and a graceful empty state.
- Deleted the `services` array, the `serviceOptions` array, and the co-located `Service` type from `lib/data.ts` (55 lines removed), completing D-14.

## Task Commits

Each implementation task was committed atomically:

1. **Task 1: API-backed homepage Services subset** — `dcb09eb` (feat)
2. **Task 2: Contact dropdown from API + `?service=` preselect** — `ee4b797` (feat)
3. **Task 3: Retire static service catalog data** — `0ce0cd6` (feat)

## Files Created/Modified

- `landing-page/app/page.tsx` — Async Server Component; awaits `searchParams` and `fetchServices()` in parallel, sorts by `displayOrder`, slices to `HOMEPAGE_SERVICE_COUNT = 6`.
- `landing-page/components/Services.tsx` — Accepts `services: Service[]` prop; renders empty state when the list is empty; card markup and Tailwind classes preserved.
- `landing-page/components/Contact.tsx` — Accepts `services` and `initialServiceSlug`; builds options from the catalog; disables submit when the catalog is empty.
- `landing-page/lib/data.ts` — `services`, `serviceOptions`, and the old `Service` type removed. Retains `navLinks`, `galleryItems`, `team`, `reviews`, `branches`, `contactEmail`.

## Decisions Made

- **Homepage subset size: 6.** Defined as `HOMEPAGE_SERVICE_COUNT` in `app/page.tsx`, applied after sorting by `displayOrder` with `toSorted()` so the source array is not mutated.
- **How Contact derives the readable service string.** `Contact` memoizes a `Map<slug, Service>` (`servicesBySlug`). On submit, the selected slug is looked up in that map and passed through `formatServiceOption(service)` → `"{name} - {price}"`. If the slug is somehow unknown, the raw slug is sent rather than dropping the field. This preserves the existing `createBooking` contract, which expects a human-readable `service` string — Phase 2 rebuilds booking against real slots.
- **Preselect is validated, not trusted.** `searchParams.get("service") ?? initialServiceSlug` is only applied when `servicesBySlug.has(requestedSlug)`; otherwise it resolves to `""` (the disabled "Select a service..." option). This is the mitigation for threat **T-01-09** (crafted `?service` value).
- **The hardcoded "Full Glam Package" card was removed** rather than retained, since that service is now seeded in the database by Plan 02. `grep -rn "Full Glam"` across `app/`, `components/`, and `lib/` returns nothing.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 — Missing Critical] Preselect passed as a server prop in addition to `useSearchParams`**
- **Found during:** Task 2 (Contact dropdown)
- **Issue:** The plan specified reading `?service=` via `useSearchParams()` alone. On the server render pass that hook yields no value, so the first painted HTML would show the empty option and only correct itself after hydration — a visible flash on the exact flow Plan 03's `/book` CTA depends on.
- **Fix:** `app/page.tsx` also awaits `searchParams` and passes `initialServiceSlug` down. `Contact` resolves `searchParams.get("service") ?? initialServiceSlug ?? ""`, so the server-rendered markup already carries the correct selection, and a `useEffect` keeps it in sync on client-side navigation.
- **Files modified:** `landing-page/app/page.tsx`, `landing-page/components/Contact.tsx`
- **Verification:** `npm run build` passes; `/` is correctly reported as a dynamic (`ƒ`) route because it now reads `searchParams`.
- **Committed in:** `dcb09eb`, `ee4b797`

**2. [Rule 1 — Plan Underspecified] Whole service card is the link, not just a "Book" button**
- **Found during:** Task 1 (homepage subset)
- **Issue:** The plan left the link target to discretion ("prefer linking to the detail page").
- **Fix:** The entire card is a `<Link href={`/services/${service.slug}`}>`, giving a larger hit target and funneling the homepage into the catalog per D-01.
- **Files modified:** `landing-page/components/Services.tsx`
- **Verification:** `npm run build` passes.
- **Committed in:** `dcb09eb`

---

**Total deviations:** 2 auto-fixed (1 missing critical, 1 plan underspecified)
**Impact on plan:** Both improve correctness of the D-04 preselect flow and the D-01 funnel. No scope creep; no change to the booking API contract.

## Issues Encountered

- **The plan was executed but never closed out.** The three task commits landed on 2026-07-08 without a SUMMARY.md, leaving `STATE.md` and `ROADMAP.md` reporting 3/4 plans. Re-running `/gsd-execute-phase` tripped the `safe_resume_gate` (production commits present, SUMMARY absent, no matching `.planning/async-jobs/` manifest). Resolved by the **close out manually** recovery path: commits inspected, all automated verification re-run green, this SUMMARY written from the actual diff. No executor was re-dispatched, so no duplicate work was produced.

## User Setup Required

None for this plan. Note the standing environment caveat from STATE.md: default `MSSQLLocalDB` fails on this machine — local API runs need `ConnectionStrings__DefaultConnection` pointed at `(localdb)\ZachHairStudio2025` / database `ZachHairStudioDev`.

## Outstanding

**Task 4 — human-verify checkpoint (blocking, not yet performed).** Reviewer deferred it. To close:

1. Start the stack via the `dev` skill (.NET API + `next dev`).
2. Visit `http://localhost:3000` — Services section shows the seeded services, each linking into the catalog, plus the full-menu link.
3. Scroll to Contact — dropdown lists API services as `{name} - {price}`.
4. From `/services/{slug}`, click Book — Plan 03 routes this to `/book?service={slug}`; confirm the homepage `/?service={slug}` path also preselects correctly.
5. Submit a test booking — confirm it still succeeds.

## Next Phase Readiness

Phase 1's four plans are code-complete and the catalog now has exactly one database-backed source feeding every service surface. Phase 2 (Booking Core) can build slot logic on `Service.DurationMinutes` without reconciling a second static list.

Blockers before Phase 2 planning:
- Task 4 human verification above.
- Phase 2 is flagged for a focused research pass (DB-level uniqueness/overlap constraints, `DateTimeOffset`/IANA timezone strategy, seeded-availability model shape).
- Phase 2 will replace the free-text `service` string on the booking contract that this plan deliberately preserved.

---
*Phase: 01-service-catalog*
*Completed: 2026-07-08 (closed out 2026-07-09)*
