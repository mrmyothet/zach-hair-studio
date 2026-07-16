---
phase: 03-staff-dashboard-schedule
plan: 05
subsystem: ui
tags: [nextjs, swr, schedule, day-grid, week-view, appointment-status, dashboard, asia-yangon]

requires:
  - phase: 03-staff-dashboard-schedule
    provides: GET/PATCH /api/schedule + status transitions (03-03); dashboard login/guard + openapi-fetch client (03-04); POST /api/staff-users Owner gate (03-02)
provides:
  - "Salon-local (Asia/Yangon) schedule time utils + 20px/15min block geometry"
  - "useSchedule SWR hook (60s poll + revalidateOnFocus + mutate + lastUpdatedAt)"
  - "scheduleStatus PATCH caller with 401 → handleUnauthorized"
  - "DayGrid stylist-column time-grid + WeekChips Monday-start week view"
  - "Appointment detail panel + ConfirmDialog for Cancel/No-show"
  - "Protected /schedule page with toolbar navigation and status actions"
  - "Owner-only /staff/new add-staff screen"
affects: [04-staff-service-availability-management, verify-work]

tech-stack:
  added: []
  patterns:
    - "Hand-rolled CSS Grid day view (no FullCalendar / react-big-calendar); block top/height from scheduleTime helpers"
    - "SWR refreshInterval 60_000 + revalidateOnFocus for schedule freshness (D-14); no realtime push"
    - "Complete applies immediately; Cancel/No-show require ConfirmDialog before PATCH (D-11)"
    - "Cancelled vs No-show kept visually distinct; terminal statuses hidden until toggle (D-08, DASH-04)"
    - "Client Owner role-gate on /staff/new is UX only; server [Authorize(Roles=Owner)] is authority (T-03-15)"

key-files:
  created:
    - dashboard/lib/scheduleTime.ts
    - dashboard/lib/useSchedule.ts
    - dashboard/lib/scheduleStatus.ts
    - dashboard/components/icons.tsx
    - dashboard/components/AppointmentBlock.tsx
    - dashboard/components/DayGrid.tsx
    - dashboard/components/WeekChips.tsx
    - dashboard/components/ConfirmDialog.tsx
    - dashboard/components/AppointmentDetailPanel.tsx
    - dashboard/components/ScheduleToolbar.tsx
    - dashboard/app/staff/new/page.tsx
  modified:
    - dashboard/app/schedule/page.tsx

key-decisions:
  - "No calendar library — day grid positions blocks with inline top/height from Asia/Yangon scheduleTime helpers (RESEARCH A2)."
  - "Schedule fetches all statuses; D-08 hide/reveal of Cancelled/NoShow is client-side via includeCancelled."
  - "Post-checkpoint: treat HTTP 201 Created as success on add-staff POST (openapi-fetch success path)."
  - "Post-checkpoint: repair Owner role seed so staff login JWT carries the Owner role claim."

patterns-established:
  - "dashboard/lib/scheduleTime.ts is the single salon-zone math source; components never hardcode offsets."
  - "Status mutations call updateAppointmentStatus then useSchedule mutate() — no full page reload."
  - "ConfirmDialog owned by schedule page; blocks/detail panel only request confirm for Cancel/NoShow."

requirements-completed: [DASH-01, DASH-02, DASH-03, DASH-04]

coverage:
  - id: D1
    description: "Day and week schedule views render salon-local appointments (Myanmar Time), with duration-sized day blocks and Monday-start week chips."
    requirement: DASH-01
    verification:
      - kind: other
        ref: "dashboard/components/DayGrid.tsx + WeekChips.tsx + scheduleTime.ts (code inspection; prior npm run build/lint on Task 3)"
        status: pass
      - kind: manual_procedural
        ref: "Human schedule walkthrough approval, 2026-07-16 (Task 4, 03-05-PLAN.md)"
        status: pass
    human_judgment: true
    rationale: "Visual day-grid geometry, week drill-through, and Myanmar Time caption require human confirmation against live data."
  - id: D2
    description: "Appointment detail panel shows full fields plus status-audit line when present; Complete/Cancel/No-show available from block and panel."
    requirement: DASH-02
    verification:
      - kind: other
        ref: "dashboard/components/AppointmentDetailPanel.tsx + AppointmentBlock.tsx"
        status: pass
      - kind: manual_procedural
        ref: "Human schedule walkthrough approval, 2026-07-16 (Task 4, 03-05-PLAN.md)"
        status: pass
    human_judgment: true
    rationale: "Detail panel content and dual-surface status controls need live UI walkthrough."
  - id: D3
    description: "Complete applies immediately; Cancel and No-show require confirmation before PATCH /api/schedule/{id}/status; grid refreshes via SWR mutate."
    requirement: DASH-03
    verification:
      - kind: other
        ref: "dashboard/app/schedule/page.tsx + scheduleStatus.ts + ConfirmDialog.tsx"
        status: pass
      - kind: manual_procedural
        ref: "Human schedule walkthrough approval, 2026-07-16 (Task 4, 03-05-PLAN.md)"
        status: pass
    human_judgment: true
    rationale: "Confirm-dialog UX and slot-release after Cancel/No-show need human verification against the API."
  - id: D4
    description: "Cancelled and no-show appointments are hidden by default and revealed distinctly via 'Show cancelled & no-shows' toggle."
    requirement: DASH-04
    verification:
      - kind: other
        ref: "AppointmentBlock/WeekChips Cancelled vs No-show treatments + ScheduleToolbar includeCancelled"
        status: pass
      - kind: manual_procedural
        ref: "Human schedule walkthrough approval, 2026-07-16 (Task 4, 03-05-PLAN.md)"
        status: pass
    human_judgment: true
    rationale: "Default-hidden terminal rows and distinct Cancelled vs No-show visuals require human judgment."

duration: close-out
completed: 2026-07-16
status: complete
---

# Phase 3 Plan 05: Schedule UI Summary

**Staff schedule UI with Asia/Yangon day-grid and Monday-start week view, detail panel, Complete/Cancel/No-show actions, 60s SWR polling, and Owner add-staff — verified by human walkthrough.**

## Performance

- **Duration:** close-out after human-verify (implementation earlier in wave)
- **Started:** 2026-07-16T10:53:00Z
- **Completed:** 2026-07-16T10:55:00Z
- **Tasks:** 4 (3 auto + 1 human-verify checkpoint)
- **Files modified:** 12 schedule/UI artifacts (plus 2 post-checkpoint fix commits)

## Accomplishments

- Salon-book day view (stylist columns × time axis) and Monday-start week chips with today-landing toolbar navigation (prev/next/Today/date picker/Day–Week).
- Appointment detail slide-over with status-audit line; Complete immediate; Cancel/No-show behind ConfirmDialog; terminal statuses toggle-revealed and visually distinct.
- SWR schedule hook polls every 60s, revalidates on focus, and exposes manual refresh + “Updated Xm ago”.
- Owner-only `/staff/new` creates Staff accounts via `POST /api/staff-users`; Staff-role users redirected away.

## Task Commits

Each task was committed atomically:

1. **Task 1: Schedule data layer — salon-time utils, SWR hook, status caller, icons** - `704c769` (feat)
2. **Task 2: Schedule view + status components** - `5cdfdb0` (feat)
3. **Task 3: Toolbar + protected `/schedule` + Owner add-staff** - `1fdcb34` (feat)
4. **Task 4: Human schedule walkthrough** - no code commit (checkpoint approval 2026-07-16)

**Post-checkpoint fixes (before SUMMARY close-out):**

- `97b87b5` fix(03): repair Owner role seed so staff login accepts JWT
- `e4ee348` fix(03-05): treat 201 Created as success on add-staff

**Plan metadata:** commit created below (docs: complete plan)

## Files Created/Modified

- `dashboard/lib/scheduleTime.ts` - Asia/Yangon formatters, Monday week windows, 20px/15min block geometry
- `dashboard/lib/useSchedule.ts` - SWR schedule fetch (60s + focus + mutate + lastUpdatedAt)
- `dashboard/lib/scheduleStatus.ts` - PATCH status caller with 401 → handleUnauthorized
- `dashboard/components/icons.tsx` - Hand-rolled SVG icon set for toolbar/actions
- `dashboard/components/AppointmentBlock.tsx` - Duration-sized day block + quick actions
- `dashboard/components/DayGrid.tsx` - Stylist-column CSS Grid time view
- `dashboard/components/WeekChips.tsx` - 7 Monday-start day columns of chips
- `dashboard/components/ConfirmDialog.tsx` - Destructive confirm for Cancel/No-show
- `dashboard/components/AppointmentDetailPanel.tsx` - Detail slide-over + status controls
- `dashboard/components/ScheduleToolbar.tsx` - Navigation, Day/Week, cancelled toggle, refresh
- `dashboard/app/schedule/page.tsx` - Protected schedule assembly (replaces 03-04 stub)
- `dashboard/app/staff/new/page.tsx` - Owner-only add-staff form

## Decisions Made

- **No calendar library** — hand-rolled CSS Grid with `scheduleTime` block geometry (plan prohibition / RESEARCH A2).
- **Client-side terminal filter** — fetch all statuses; `includeCancelled` toggles Cancelled/NoShow visibility (D-08).
- **201 as add-staff success** — openapi-fetch path treats Created as success so Owner create-staff UX works (`e4ee348`).
- **Owner role seed repair** — ensure seeded Owner JWT includes role claim for login (`97b87b5`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Repair Owner role seed for staff login JWT**
- **Found during:** Task 4 (human-verify walkthrough)
- **Issue:** Seeded Owner login produced a JWT that staff login/auth gate did not accept as Owner (role seed mismatch).
- **Fix:** Corrected Owner role seed so login accepts JWT with Owner claim.
- **Files modified:** API seed/identity-related files (see commit)
- **Verification:** Human re-verified Owner login during walkthrough
- **Committed in:** `97b87b5`

**2. [Rule 1 - Bug] Treat 201 Created as success on add-staff**
- **Found during:** Task 4 (Owner add-staff step)
- **Issue:** `POST /api/staff-users` returns 201 Created; client treated non-200 as failure so success UX never fired.
- **Fix:** Accept 201 as success on the add-staff response path.
- **Files modified:** `dashboard/app/staff/new/page.tsx` (and/or related client handling — see commit)
- **Verification:** Human approved walkthrough including create-staff success
- **Committed in:** `e4ee348`

---

**Total deviations:** 2 auto-fixed (2 Rule 1 bugs, both post-checkpoint during human verify)
**Impact on plan:** Required for walkthrough success criteria; no scope creep.

## Issues Encountered

- Owner role seed and 201 Created handling blocked the human-verify steps until fixed in `97b87b5` and `e4ee348`; walkthrough then approved 2026-07-16.

## User Setup Required

None beyond existing Phase 03 secrets and dashboard env:

- Reuses `Jwt:SigningKey` / `Owner:Email` / `Owner:InitialPassword` user-secrets from 03-01.
- Dashboard `NEXT_PUBLIC_API_URL` optional (defaults to `http://localhost:5236`).
- Run API `:5236` + dashboard `:3001` via the `dev` skill for local verification.

## Next Phase Readiness

- DASH-01..04 frontend delivery complete; Phase 03 schedule UI is ready for phase verification / next roadmap phase (staff service & availability management).
- No calendar library and no cookie/NextAuth session introduced.

## Known Stubs

None — `/schedule` stub from 03-04 is replaced by the full schedule UI.

---
*Phase: 03-staff-dashboard-schedule*
*Completed: 2026-07-16*

## Self-Check: PASSED

Key files present on disk (scheduleTime, useSchedule, scheduleStatus, icons, AppointmentBlock, DayGrid, WeekChips, ConfirmDialog, AppointmentDetailPanel, ScheduleToolbar, schedule/page, staff/new). Task commits `704c769`, `5cdfdb0`, `1fdcb34` and post-checkpoint fixes `97b87b5`, `e4ee348` confirmed in `git log`. Human-verify Task 4 approved 2026-07-16.
