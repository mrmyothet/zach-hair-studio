---
phase: 04-staff-management-services-availability
plan: 07
subsystem: ui
tags: [react, nextjs, dashboard, availability, pointer-events, gap-closure]

# Dependency graph
requires:
  - phase: 04-staff-management-services-availability
    provides: "WeekStripEditor paint-drag gesture and render-phase setState fix (plans 04-04, 04-06)"
provides:
  - "Edge-resize drag mode on WeekStripEditor so staff can shrink an existing working-hours segment without deleting and repainting it"
affects: [04-UAT, 04-secure-phase]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Second independent ref-mirrored pointer-drag mode (resizeRef) alongside the existing paint-gesture ref (previewRangeRef), each with its own useEffect keyed on its own state, so pointerup can call emitChange from the handler's own body and never from inside a set*() updater callback (dashboard/.eslintrc.json no-restricted-syntax guard)."

key-files:
  created: []
  modified:
    - dashboard/components/WeekStripEditor.tsx

key-decisions:
  - "Resize commits as a direct per-segment array replace via emitChange, deliberately bypassing mergeSegments so a shrink can never be silently re-expanded by the additive union logic."
  - "Resize clamp bounds are computed against the dragged segment's own opposite edge (minimum one SNAP_MINUTES width) and the immediate adjacent segment in the same day's array (or the track's 0/TOTAL_MINUTES bounds at the ends) -- never against mergeSegments' union math."

patterns-established:
  - "Multiple independent drag gestures on the same pointer surface: each gesture owns its own state + ref + useEffect, and per-gesture onPointerDown handlers call stopPropagation so the track's own paint-drag onPointerDown never double-fires."

requirements-completed: [MGMT-02]

coverage:
  - id: D1
    description: "Staff can grab either edge of an existing working-hours segment and drag it inward to shrink the segment, discoverable via a hover-revealed handle with an ew-resize cursor"
    requirement: "MGMT-02"
    verification:
      - kind: other
        ref: "structural node check: handleResizeUp calls emitChange, never mergeSegments (04-07-PLAN.md Task 1 <verify> automated command)"
        status: pass
    human_judgment: true
    rationale: "Live drag-and-drop pointer interaction and its visual shrink/clamp behavior can only be confirmed by a human exercising the UI (04-07-PLAN.md <verification> manual UAT retest steps 1-5, including the end-to-end UAT Test 13 re-run) -- no dashboard test runner exists in this repo."
  - id: D2
    description: "Existing paint-new-segment, gap-as-break, and x-button remove gestures remain unchanged"
    verification:
      - kind: other
        ref: "git diff shows handleMove/handleUp/mergeSegments/removeSegment bodies byte-unchanged; only WeekStripEditor.tsx touched"
        status: pass
    human_judgment: false

# Metrics
duration: 20min
completed: 2026-07-27
status: complete
---

# Phase 4 Plan 07: WeekStripEditor Edge-Resize (Gap G-04-6) Summary

**Added a ref-committed edge-resize drag mode to WeekStripEditor.tsx -- hover-revealed handle strips with an ew-resize cursor let staff shrink an existing working-hours segment's start/end boundary via direct per-segment array replacement, bypassing the additive-only `mergeSegments` entirely.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-27T05:45:43Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Staff can now shrink an already-painted segment's edge inward, closing the previously-diagnosed gap G-04-6 (the only prior way to reduce a segment's size was delete-via-x-button + repaint from scratch)
- The resize commit path (`handleResizeUp`) writes directly into the target segment's `start`/`end` field via a per-segment array map and calls `emitChange` from its own function body -- never through `mergeSegments`, so a shrink cannot be silently re-expanded by the union logic that also drives brand-new paints
- A discoverable resize-handle affordance (`data-segment-resize`, `cursor-ew-resize`, hover-revealed alongside the existing x remove button) makes the gesture findable without prior knowledge
- The existing paint-drag, gap-as-break, and x-button remove gestures are untouched -- `handleMove`, `handleUp`, `mergeSegments`, and `removeSegment` are byte-identical to before this plan

## Task Commits

Each task was committed atomically:

1. **Task 1: Add edge-resize drag mode with a discoverable handle affordance to WeekStripEditor** - `8c24a84` (feat)

**Plan metadata:** (this commit)

## Files Created/Modified
- `dashboard/components/WeekStripEditor.tsx` - Added `ResizeEdge`/`ResizeTarget` types, `resizeTarget`/`resizePreview` state, a `resizeRef` mirror, `startResize()`, a second pointermove/pointerup `useEffect` (`handleResizeMove` clamping against the segment's own opposite edge and adjacent segments, `handleResizeUp` committing via `emitChange` and never `mergeSegments`), hover-revealed start/end resize-handle divs in the segment-render block, and an updated help-text sentence describing the new gesture

## Decisions Made
- Resize commits as a direct per-segment array replace via `emitChange`, deliberately bypassing `mergeSegments` so a shrink can never be silently re-expanded by the additive union logic (matches the plan's `key_links` and the debug session's recommended fix shape).
- Resize clamp bounds are computed against the dragged segment's own opposite edge (minimum one `SNAP_MINUTES` width) and the immediate adjacent segment in the same day's array (or the track's `0`/`TOTAL_MINUTES` bounds at the ends) -- never against `mergeSegments`' union math.
- Followed the 04-06-established ref-mirror pattern (`resizeRef` mirroring `previewRangeRef`) so `handleResizeUp` reads the live value without a state updater's `prev` argument, keeping `emitChange` out of any `set*()` callback and satisfying the standing `dashboard/.eslintrc.json` no-restricted-syntax guard.

## Deviations from Plan

None - plan executed exactly as written. The action block was followed literally (7 numbered steps: types, state/ref, `startResize`, second `useEffect` with `handleResizeMove`/`handleResizeUp`, effective-start/end computation in the render block, resize-handle JSX, help-text update).

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Known Stubs
None.

## Threat Flags
None - this plan's threat model (T-04-17/18/19) already anticipated the new surface (bypassing `mergeSegments`, the resize commit path, and the clamp math); no new unanticipated surface was introduced.

## Next Phase Readiness
- Gap G-04-6 is closed; the manual UAT retest steps in `04-07-PLAN.md`'s `<verification>` section (hover/drag/release behavior and the end-to-end UAT Test 13 re-run) are the remaining human-judgment step before this gap can be marked fully verified in `04-UAT.md`.
- `npm run lint` and `npm run build` are clean from `dashboard/`, including the standing 04-06 state-updater-purity ESLint guard.
- No blockers for Phase 4 close-out beyond the standard end-of-phase manual UAT pass.

---
*Phase: 04-staff-management-services-availability*
*Completed: 2026-07-27*
