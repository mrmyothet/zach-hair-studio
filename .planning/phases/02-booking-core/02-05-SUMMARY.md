---
phase: 02-booking-core
plan: 05
subsystem: ui
tags: [nextjs, react, typescript, zod, tailwind, booking, timezone, intl]

# Dependency graph
requires:
  - phase: 02-03
    provides: GET /api/appointments/slots (OpenSlotDto[]) and the SlotService open-slot grid
  - phase: 02-04
    provides: POST /api/appointments (AppointmentCreateDto -> 201 AppointmentResponseDto, 409 on slot-taken) and the 60-day booking-horizon validator
provides:
  - "lib/appointments.ts client: fetchOpenSlots (uncached), createAppointment (status-aware), fetchStylists, and Zod response schemas"
  - "AppointmentBookingForm: progressive-reveal service -> stylist -> date/slot -> details flow with salon-local slot times, a self-sufficient confirmation, and 409 recovery"
  - "/book wired to the real appointments API preserving the ?service deep link"
  - "Homepage Contact form routed into /book; retired /api/bookings client fully removed from the frontend"
affects: [booking, phase-06-verify, dashboard, notifications]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Explicit-timezone rendering: appointment DateTimeOffsets formatted with Intl.DateTimeFormat timeZone=America/New_York, never the browser zone (D-16)"
    - "Typed API error (AppointmentApiError.status) so callers branch 409 vs 400 vs network instead of swallowing to a default"
    - "Fresh-always fetch (cache: no-store) for slot availability; empty-day ([]) distinguished from load-failure (throw)"

key-files:
  created:
    - landing-page/lib/appointments.ts
    - landing-page/components/AppointmentBookingForm.tsx
  modified:
    - landing-page/components/icons.tsx
    - landing-page/app/book/page.tsx
    - landing-page/components/Contact.tsx
  deleted:
    - landing-page/components/BookingRequestForm.tsx
    - landing-page/lib/api.ts

key-decisions:
  - "Render slot/confirmation times with an explicit IANA timeZone (America/New_York) via Intl, which is DST-safe and matches the server's DateTimeOffset offset, over hand-parsing the ISO offset"
  - "Fetch stylists server-side in the /book RSC and pass as a prop (no client waterfall / chip loading state); fetchStylists degrades to [] so the Any-Available default always works"
  - "Homepage Contact quick form now navigates to /book?service={slug} on submit instead of POSTing; button relabeled 'Continue to Booking' — owner-reviewable"
  - "Date input bounded today .. today+60d to match AppointmentCreateDtoValidator's horizon (no same-day/lead cutoff)"

patterns-established:
  - "AppointmentApiError typed error exposing HTTP status for branchable failure handling"
  - "Progressive-reveal multi-step form where changing an earlier step resets later selections while preserving controlled contact state across a 409"

requirements-completed: [BOOK-02, BOOK-03, BOOK-06]

coverage:
  - id: D1
    description: "lib/appointments.ts: fetchOpenSlots (uncached), createAppointment (409/400/network distinct via AppointmentApiError), fetchStylists, Zod schemas mirroring the DTOs"
    requirement: BOOK-02
    verification:
      - kind: other
        ref: "npm --prefix landing-page run build (tsc typecheck + compile) — pass; npx tsc --noEmit — pass"
        status: pass
    human_judgment: false
  - id: D2
    description: "Progressive-reveal /book flow: service -> stylist (Any default) -> date/slot grid -> details -> Confirm; real slots in salon-local time with the zone caption"
    requirement: BOOK-06
    verification:
      - kind: other
        ref: "npm --prefix landing-page run build — pass (/book compiles as a dynamic route)"
        status: pass
      - kind: manual_procedural
        ref: "Plan 06 human-verify: walk the flow against a live API; confirm slot times read as Eastern regardless of the viewer's machine timezone"
        status: unknown
    human_judgment: true
    rationale: "Slot-grid freshness, salon-local time correctness, and the full interactive flow can only be proven against a live backend (API not running in this worktree)"
  - id: D3
    description: "Self-sufficient on-screen confirmation with service, concrete stylist, salon-local date/time+zone, duration, price, and the email-not-guaranteed caption (D-11)"
    requirement: BOOK-03
    verification:
      - kind: manual_procedural
        ref: "Plan 06 human-verify: submit a booking and confirm every detail renders with the concrete assigned stylist and Eastern time"
        status: unknown
    human_judgment: true
    rationale: "Requires a real 201 AppointmentResponse from the live API to render; deferred to Plan 06"
  - id: D4
    description: "409 recovery: keeps contact details, marks the taken slot unavailable, re-fetches the grid, focuses the date step, shows the destructive banner"
    requirement: BOOK-03
    verification:
      - kind: manual_procedural
        ref: "Plan 06 human-verify: force a 409 (concurrent booking) and confirm details survive and the grid refreshes"
        status: unknown
    human_judgment: true
    rationale: "The 409 race path needs two concurrent bookings against a live API to exercise; cannot be reproduced statically"
  - id: D5
    description: "Frontend fully retired from /api/bookings: BookingRequestForm.tsx and lib/api.ts deleted; Contact routes into /book; zero createBooking/BookingRequest/BookingResponse references"
    requirement: BOOK-02
    verification:
      - kind: other
        ref: "grep -rn 'createBooking|BookingRequest|BookingResponse' landing-page (src) — zero matches; ls of both files — absent"
        status: pass
    human_judgment: false

# Metrics
duration: 10min
completed: 2026-07-10
status: complete
---

# Phase 2 Plan 05: Public /book Booking Flow Summary

**Progressive-reveal /book flow (service → stylist → date/slot → details) backed by a new status-aware appointments client, rendering real open slots in salon-local Eastern time with a self-sufficient confirmation and 409 recovery — plus wholesale retirement of the legacy free-text booking path from the frontend.**

## Performance

- **Duration:** ~10 min (excludes one-time `npm ci` in the fresh worktree)
- **Started:** 2026-07-10T04:50:20Z
- **Completed:** 2026-07-10T05:00:55Z
- **Tasks:** 3
- **Files modified:** 7 (2 created, 3 modified, 2 deleted)

## Accomplishments
- `lib/appointments.ts` bridges the client to `GET /api/appointments/slots` and `POST /api/appointments`, with a typed `AppointmentApiError` that keeps 409 (slot-taken), 400 (validation), and network failures distinct — and `fetchOpenSlots` is always fresh (`cache: no-store`, never Next `revalidate`).
- `AppointmentBookingForm` implements the four-step progressive reveal per the UI-SPEC: service select, stylist chips (Any Available Stylist default), a fresh slot grid with loading/empty-day/load-failure states, and guest contact fields — gated so each step reveals only when the prior is resolved and later steps reset when an earlier answer changes.
- Every appointment time is rendered with `Intl.DateTimeFormat({ timeZone: "America/New_York" })` plus the shared caption "All times shown in salon local time (Eastern)" — no browser-timezone formatting anywhere (D-16).
- Self-sufficient confirmation panel (service, concrete stylist, Eastern date/time + zone, duration, price, email-not-guaranteed caption) and a 409 recovery path that preserves the client's contact details, marks the taken slot unavailable, re-fetches the grid, and returns focus to the date step.
- Retired the legacy path (D-14): deleted `BookingRequestForm.tsx` and `lib/api.ts`, repointed the homepage `Contact` form to navigate into `/book?service={slug}`, and confirmed zero remaining `createBooking`/`BookingRequest`/`BookingResponse` references.

## Task Commits

Each task was committed atomically:

1. **Task 1: appointments API client + icons** - `64db4c7` (feat)
2. **Task 2: progressive-reveal /book flow + 409 recovery; delete BookingRequestForm** - `9bee1f3` (feat)
3. **Task 3: repoint homepage Contact into /book; remove dead createBooking client** - `08167f7` (feat)

_No TDD tasks in this plan (all `type="auto"`)._

## Files Created/Modified
- `landing-page/lib/appointments.ts` (created) - fetchOpenSlots / createAppointment / fetchStylists, AppointmentApiError, and Zod schemas mirroring OpenSlotDto / AppointmentResponseDto / StylistResponseDto.
- `landing-page/components/AppointmentBookingForm.tsx` (created) - progressive-reveal booking UI, salon-local slot rendering, confirmation panel, 409 recovery.
- `landing-page/components/icons.tsx` (modified) - added CheckIcon, CalendarIcon, ClockIcon, AlertIcon (existing exports intact).
- `landing-page/app/book/page.tsx` (modified) - swaps to AppointmentBookingForm, fetches stylists in the RSC Promise.all, updates the heading/subtitle, preserves the ?service deep link.
- `landing-page/components/Contact.tsx` (modified) - routes to /book?service={slug} on submit; removed the POST/createBooking path and dead submit states.
- `landing-page/components/BookingRequestForm.tsx` (deleted) - retired (D-14).
- `landing-page/lib/api.ts` (deleted) - retired createBooking/BookingRequest/BookingResponse; no importers remained.

## Decisions Made
- **Explicit IANA timezone over ISO-offset parsing:** formatting with `timeZone: "America/New_York"` is DST-safe and produces the same wall-clock the server's DateTimeOffset carries, while structurally guaranteeing the browser zone is never used. Simpler and safer than hand-parsing the offset.
- **Stylists fetched server-side and passed as a prop:** avoids a client fetch waterfall and a chip loading state; `fetchStylists` degrades to `[]` on failure since the "Any Available Stylist" default works without the list.
- **CalendarIcon added but not yet placed:** created per the plan/UI-SPEC icon list; ClockIcon/CheckIcon/AlertIcon are in active use, CalendarIcon is available for future date-step polish.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Restored frontend dependencies in the fresh worktree**
- **Found during:** Task 1 verification
- **Issue:** The worktree had no `landing-page/node_modules`, so `next`/`tsc` could not run.
- **Fix:** `npm ci` against the existing tracked `package-lock.json` (no new packages, no lockfile change).
- **Files modified:** none tracked (node_modules is gitignored).
- **Verification:** build and tsc subsequently ran.
- **Committed in:** n/a (no tracked change).

**2. [Rule 3 - Blocking] Used tsc typecheck as the Task 1 static gate instead of `next lint`**
- **Found during:** Task 1 verification
- **Issue:** `npm run lint` (`next lint`) prompts interactively to configure ESLint — the project has never had an ESLint config (none tracked, none in the main checkout; CLAUDE.md notes "No .eslintrc configured"), so it cannot run non-interactively. Configuring ESLint would mean adding unrequested new packages/config (out of scope, and package installs are excluded from auto-fix).
- **Fix:** Verified Task 1 with `npx tsc --noEmit` (exit 0); the stronger `next build` type+compile gate covers Tasks 2 and 3.
- **Files modified:** none.
- **Verification:** `npx tsc --noEmit` passed; `npm --prefix landing-page run build` passed.
- **Committed in:** n/a.

---

**Total deviations:** 2 (both Rule 3 - blocking environment/tooling; neither changed shipped code).
**Impact on plan:** None on scope. The lint verification step is unrunnable due to a pre-existing project gap; the type-check + build provide equivalent-or-stronger static assurance.

## Issues Encountered
- **Two initial bugs in AppointmentBookingForm caught before commit:** a bogus `import { useCallbackRef }` (not a real React export) and a `reloadKey` `useState` referenced in the slot-fetch effect's dependency array before its declaration (temporal dead zone). Both fixed by removing the import and hoisting the `reloadKey` declaration above the effect; `next build` then passed.

## Owner-Reviewable Flags
- **Homepage Contact repoint:** the UI-SPEC scoped only `/book`, not the homepage "Book Your Appointment" quick form. That form now collects name/email/phone but discards them and simply routes to `/book?service={slug}` (only the service preselect is carried), with the button relabeled "Continue to Booking". The exact treatment of this section — keep the quick form, slim it to a service picker + CTA, or remove it — is an owner decision.
- **60-day booking horizon + no same-day/lead cutoff:** the date input is bounded today .. today+60d to mirror `AppointmentCreateDtoValidator`. This is a placeholder business rule (like the seed prices in Phase 1), not a locked decision — confirm with the owner.

## Notes for Plan 06 Human-Verify
- **Slot-time zone correctness (highest priority):** with your machine timezone set to a non-Eastern zone (e.g. America/Los_Angeles), confirm a 10:00 AM Eastern slot still displays as 10:00 AM and the confirmation reads e.g. "10:00 AM EDT" — never the local zone.
- **409 recovery:** force a concurrent booking of the same slot; confirm the contact details remain filled, the destructive banner appears, the taken slot is marked unavailable, the grid re-fetches, and focus returns to the date step (form is NOT reset).
- **Empty-day vs load-failure:** confirm a fully-booked date shows "No Openings This Day" while an API outage shows "Couldn't Load Times" with a working Try Again.
- **Deep link + confirmation:** confirm `?service={slug}` preselects the service, and the confirmation shows a concrete stylist name (never "Any Available Stylist") with duration and price.

## Next Phase Readiness
- The public booking flow is complete and compiles; the frontend has no remaining reference to the retired `/api/bookings` endpoint.
- Blocker for full sign-off: the .NET API is not running in this worktree, so the interactive flow, live slots, confirmation, and 409 race are unverified here — deferred to Plan 06's human-verify.

## Self-Check: PASSED

- FOUND: landing-page/lib/appointments.ts
- FOUND: landing-page/components/AppointmentBookingForm.tsx
- FOUND: .planning/phases/02-booking-core/02-05-SUMMARY.md
- FOUND commits: 64db4c7, 9bee1f3, 08167f7

---
*Phase: 02-booking-core*
*Completed: 2026-07-10*
