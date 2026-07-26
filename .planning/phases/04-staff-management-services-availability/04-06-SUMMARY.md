---
phase: 04-staff-management-services-availability
plan: 06
subsystem: ui
tags: [react, eslint, dashboard, availability, gap-closure]

# Dependency graph
requires:
  - phase: 04-staff-management-services-availability
    provides: WeekStripEditor drag-paint hours editor and TimeOffCalendar (Plans 04/05)
provides:
  - Fix for G-04-5 - WeekStripEditor no longer updates AvailabilityPage state from inside a render-phase state updater
  - Standing dashboard-wide ESLint guard (no-restricted-syntax) against this bug class
affects: [04-staff-management-services-availability, dashboard-availability-uat]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Ref-mirrored preview state: previewRangeRef is written on pointerdown/pointermove alongside the previewRange state setter, so a pointerup handler can read the live drag range without going through a state updater's prev argument."
    - "ESLint no-restricted-syntax guard for state-updater purity: catches onChange*/emit* calls nested inside set*() functional updaters, dashboard's only automated regression gate given it has no test runner."

key-files:
  created: []
  modified:
    - dashboard/.eslintrc.json
    - dashboard/components/WeekStripEditor.tsx

key-decisions:
  - "Guard added as a core no-restricted-syntax rule (no new eslint plugin, no npm install) since the bug's shape is expressible in a single esquery selector."
  - "handleUp keeps its own dragDay-null guard (mirroring handleMove's existing pattern) since TS closures don't retain the outer effect's narrowing of dragDay."

patterns-established:
  - "Ref-mirrored preview state for pointer-drag commit-on-up interactions in dashboard/ components."

requirements-completed: [MGMT-02]

coverage:
  - id: D1
    description: "ESLint no-restricted-syntax rule added to dashboard/.eslintrc.json, RED-confirmed against the pre-fix WeekStripEditor.tsx (fires exactly once, at the emitChange call inside the previewRange updater)"
    requirement: "MGMT-02"
    verification:
      - kind: other
        ref: "cd dashboard && npm run lint (RED, pre-fix commit db389e5) -> reported 'State updater must be pure' at WeekStripEditor.tsx:150"
        status: pass
    human_judgment: false
  - id: D2
    description: "WeekStripEditor commits the painted drag range from handleUp's own function body via a previewRangeRef mirror, eliminating the render-phase parent setState; lint guard now GREEN and build passes"
    requirement: "MGMT-02"
    verification:
      - kind: other
        ref: "cd dashboard && node -e <structural handleUp body check> -> PASS: handleUp commits from its own body"
        status: pass
      - kind: other
        ref: "cd dashboard && npm run lint -> No ESLint warnings or errors"
        status: pass
      - kind: other
        ref: "cd dashboard && npm run build -> Compiled successfully, all 9 routes generated"
        status: pass
  - id: D3
    description: "Manual browser retest: paint hours (test 10) then Add Time Off (test 11) in one page session produces no React render-phase console error; drag-paint behaviors (snapping, gap-as-break, live preview band) unchanged"
    verification: []
    human_judgment: true
    rationale: "This is a runtime browser console/visual check across a stateful drag interaction sequence - automated lint/build/structural checks prove the code no longer contains the offending pattern, but confirming the actual console is silent during the live pointer-drag + time-off UAT flow requires a human running the dashboard and watching devtools."

duration: 20min
completed: 2026-07-26
status: complete
---

# Phase 4 Plan 6: Fix WeekStripEditor render-phase setState (G-04-5) Summary

**Painting a working-hours block now commits the drag range from a ref read inside the pointerup handler's own body, closing the React "Cannot update a component (AvailabilityPage) while rendering a different component (WeekStripEditor)" error, with a new ESLint no-restricted-syntax rule standing guard against the bug class.**

## Performance

- **Duration:** 20 min
- **Started:** 2026-07-26T22:38:00+07:00
- **Completed:** 2026-07-26T23:16:00+07:00
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Added a core-ESLint `no-restricted-syntax` rule to `dashboard/.eslintrc.json` that detects `onChange*`/`emit*` calls nested inside `set*()` functional updaters — confirmed RED against the pre-fix code (exactly one match, `WeekStripEditor.tsx:150`).
- Reworked `WeekStripEditor`'s drag lifecycle so the painted range is read from a `previewRangeRef` (mirrored on every `pointerdown`/`pointermove`) instead of the `setPreviewRange` updater's `prev` argument, letting `handleUp` call `emitChange` from its own function body — a legitimate event-handler context, not React's render phase.
- Confirmed GREEN: the same lint guard that fired in Task 1 is now silent, and `npm run build` passes (Next's build type-checks the new ref).
- Preserved every existing drag behavior: 15-minute snapping, sub-snap drags discarded, gap-as-break (D-06) on a second non-contiguous drag, and the live dashed preview band still follows the pointer (still driven by the unchanged `previewRange` state, not the ref).

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the state-updater purity guard to dashboard ESLint (RED)** - `db389e5` (test)
2. **Task 2: Commit the drag range through a ref so pointerup never updates the parent mid-render (GREEN)** - `d635e9f` (fix)

**Plan metadata:** (this commit, following SUMMARY.md write)

_Note: Task 1 is a `test`-type commit per the plan's RED/GREEN framing even though phase TDD_MODE is off — it's the plan's own internal red-to-green proof structure (its own guard/rule, not a test-file gate), not the MVP+TDD RED-commit mechanism._

## Files Created/Modified
- `dashboard/.eslintrc.json` - Added `no-restricted-syntax` rule detecting `set*()` updaters that call `onChange*`/`emit*` inside their body; extends array left untouched, no new plugin/dependency.
- `dashboard/components/WeekStripEditor.tsx` - Added `previewRangeRef` (written in `onPointerDown` and `handleMove`); `handleUp` now reads the ref directly and calls `emitChange` from its own body instead of inside the `setPreviewRange` updater; kept an explicit `if (!dragDay) return;` guard in `handleUp` mirroring `handleMove`'s existing pattern (TS closures don't retain the effect's outer narrowing of `dragDay`).

## Decisions Made
- Implemented the guard as a core `no-restricted-syntax` rule rather than pulling in an ESLint plugin — the bug's shape (a call to `on[A-Z]*`/`emit*` inside an arrow function argument to `set[A-Z]*`) is fully expressible with one esquery selector, keeping the "no new npm package" plan constraint intact.
- Kept the `if (!dragDay) return;` guard at the top of `handleUp` (not removed even though the effect's `if (!dragDay) return;` at the top already gates whether the listeners are attached) — TypeScript does not retain that outer narrowing across the nested function declaration, and `emitChange` requires a non-null `DayOfWeekName`. This mirrors `handleMove`'s existing identical guard and was necessary for the code to typecheck under `npm run build`, not a deviation from intent.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched the plan's action steps verbatim (ref declaration, pointerdown/pointermove ref writes, handleUp rewrite reading the ref and calling `emitChange` directly, no touches to `dashboard/app/availability/page.tsx`, no new npm packages).

## Issues Encountered
None. The RED-to-GREEN transition worked exactly as predicted by the plan and the prior diagnosis in `.planning/debug/G-04-5-weekstrip-render-setstate.md` — no re-diagnosis was needed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Gap G-04-5 is closed at the code/lint/build level (Task 2's structural check, `npm run lint`, and `npm run build` all pass).
- **Remaining before Phase 4 can be marked fully verified:** the manual browser UAT retest described in the plan's `<verification>` section (tests 10, 11, and 18 in one page session with the console open) has not been executed as part of this automated run — flagged as coverage item D3 above (`human_judgment: true`). A human (or a future UAT pass) should open `/availability` on the dashboard, paint hours, add time off, save, and confirm no React console error and no visual regression before closing out Phase 4's UAT tracking.
- With this plan complete, Phase 4 (staff-management-services-availability) has all 6 plans executed (04-01 through 04-06).

---
*Phase: 04-staff-management-services-availability*
*Completed: 2026-07-26*
