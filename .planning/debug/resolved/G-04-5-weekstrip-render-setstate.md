---
status: resolved
trigger: "G-04-5-weekstrip-render-setstate: React throws \"Cannot update a component (`AvailabilityPage`) while rendering a different component (`WeekStripEditor`)\" during Phase 4 UAT test 11 (Add Time Off and Save) on the dashboard's Availability page."
created: 2026-07-26T00:00:00Z
updated: 2026-07-26T23:16:00Z
resolved_by: "04-06-PLAN.md (d635e9f) — previewRangeRef added, handleUp commits emitChange from its own body instead of inside the setPreviewRange updater"
---

## Current Focus
<!-- OVERWRITE on each update - reflects NOW -->

reasoning_checkpoint:
  hypothesis: "handleUp() in WeekStripEditor.tsx calls emitChange() (which calls the onChange prop, i.e. AvailabilityPage's setLocalHours) from INSIDE the functional updater passed to setPreviewRange(). React invokes useState functional updaters while processing that hook's queue during the component's render phase, so the onChange->setLocalHours call executes literally inside renderWithHooks(WeekStripEditorFiber) — updating a different fiber's (AvailabilityPage) state while WeekStripEditor is the currentlyRenderingFiber. This is exactly what produces React's 'Cannot update a component (AvailabilityPage) while rendering a different component (WeekStripEditor)' warning."
  confirming_evidence:
    - "Read WeekStripEditor.tsx lines 144-156: `setPreviewRange((prev) => { if (dragDay) { ...; if (end - start >= SNAP_MINUTES) { emitChange(dragDay, mergeSegments([...])); } } return prev; });` — emitChange() is called as a side effect inside the updater function body, not the outer handleUp scope."
    - "emitChange() (line 115-129) unconditionally calls `onChange(next)` (line 128) — onChange is the WeekStripEditor `Props.onChange`, wired in page.tsx line 156 to handleHoursChange, which calls `setLocalHours` (page.tsx line 68) — a state setter owned by AvailabilityPage, a different component/fiber than WeekStripEditor."
    - "handleMove (the sibling updater in the same effect, lines 139-142) is pure — it only computes `{ ...prev, b: ... }` and has no side effect — confirming the bug is specifically the emitChange call embedded in handleUp's updater, not a general pattern used elsewhere in the file."
    - "TimeOffCalendar.handleDayClick calls onChange directly in an onClick handler body (not inside a setState updater) — a safe, ordinary pattern — ruling out TimeOffCalendar as the render-phase violator, consistent with the warning naming WeekStripEditor (not TimeOffCalendar) as the rendering component."
  falsification_test: "If emitChange were called in handleUp's outer function body (outside the setPreviewRange updater) instead of inside the updater callback, the warning would not occur — that would confirm this exact code path is the cause. Reproducing by dragging a paint gesture on the WeekStripEditor (Test 10's action) should raise the identical console warning even without ever touching the time-off calendar, since the violation is entirely internal to WeekStripEditor and independent of TimeOffCalendar."
  fix_rationale: "Not applying a fix (goal: find_root_cause_only). Root cause is architectural: a `useState` functional updater must be a pure function of previous state — it must never call an external setState/onChange as a side effect. The fix direction is to read `previewRange` via a ref (updated on every pointermove) instead of via the `prev` argument, so `handleUp` can call `emitChange`/`onChange` directly in its own body (a legitimate event-handler context) rather than inside the updater passed to setPreviewRange."
  blind_spots: "Did not run the app/reproduce in a live browser session to directly observe the console warning firing (static code reading only, per goal: find_root_cause_only — no runtime verification performed). Did not confirm whether Test 10 (paint weekly hours) also produces this same warning in practice, though the reproduction note explicitly says the week-strip editor being mounted alongside the time-off calendar is what's relevant — the mechanism found is triggered by WeekStripEditor's own drag-paint interaction (handleUp), not by anything in TimeOffCalendar itself."

hypothesis: CONFIRMED — see reasoning_checkpoint above
test: static code read of WeekStripEditor.tsx, TimeOffCalendar.tsx, useAvailability.ts, page.tsx
expecting: n/a — root cause confirmed
next_action: n/a — diagnosis complete, goal is find_root_cause_only, returning ROOT CAUSE FOUND to caller

## Symptoms
<!-- Written during gathering, then IMMUTABLE -->

expected: Click Add Time Off, then click a start day and an end day in the month calendar to paint a range. The range renders as a dashed muted band and appears in the list below the grid. Click Save Changes and see the success confirmation. No React warnings/errors in the console.
actual: While using the Availability page's time-off flow, React logs: "Cannot update a component (`AvailabilityPage`) while rendering a different component (`WeekStripEditor`). To locate the bad setState() call inside `WeekStripEditor`, follow the stack trace as described in https://react.dev/link/setstate-in-render"
errors: React dev-mode warning/error exactly as quoted above (setState-in-render class of bug — a component is calling a state setter belonging to a different, currently-rendering component, either directly during render or via a ref/callback invoked synchronously during render).
reproduction: Test 11 in .planning/phases/04-staff-management-services-availability/04-UAT.md — dashboard Availability page (dashboard/app/availability/page.tsx), interacting with the time-off calendar while the week-strip editor is also mounted on the same page.
started: Discovered during UAT of Phase 4 (staff-management-services-availability), specifically the Availability feature (04-04-PLAN.md / 04-05-PLAN.md work — week-strip hours editor + time-off calendar + conflict-check panel all live on dashboard/app/availability/page.tsx).

## Eliminated
<!-- APPEND only - prevents re-investigating -->

## Evidence
<!-- APPEND only - facts discovered -->

- timestamp: 2026-07-26T00:00:00Z
  checked: dashboard/app/availability/page.tsx (full read)
  found: AvailabilityPage owns localHours/localTimeOff state and passes handleHoursChange/handleTimeOffChange as onChange callbacks to WeekStripEditor and TimeOffCalendar respectively (both via useCallback, stable refs). No obvious setState-in-render at this level — need to check if WeekStripEditor calls onChange() (i.e. handleHoursChange, which sets AvailabilityPage's localHours state) synchronously during its own render rather than in an event handler/effect.
  implication: The warning names AvailabilityPage as the "different component" being updated while WeekStripEditor renders — strongly suggests WeekStripEditor is invoking the onChange prop (handleHoursChange, which lives in/updates AvailabilityPage) directly in its render body or via a ref callback that fires synchronously during render, not inside an event handler or effect.

- timestamp: 2026-07-26T00:01:00Z
  checked: dashboard/components/WeekStripEditor.tsx (full read)
  found: >
    handleUp() (lines 144-156, inside the useEffect at lines 136-165) calls
    setPreviewRange(prev => { ...; if (end - start >= SNAP_MINUTES) { emitChange(dragDay,
    mergeSegments([...])); } return prev; }). emitChange (lines 115-129) unconditionally calls
    onChange(next) at line 128. handleMove (the sibling updater, lines 139-142) by contrast is pure —
    it only returns `{ ...prev, b: ... }` with no side effect. ref callback at line 185-187 only
    assigns trackRefs.current[day], no state setter involved. groupByDay(value) at line 105 (render
    body) is a pure function, no side effect.
  implication: >
    The functional updater passed to setPreviewRange is not pure — it performs a side effect
    (emitChange -> onChange) as part of computing the next state. Because React invokes useState
    functional updaters while processing the hook's queue during that component's render phase, this
    executes onChange (-> AvailabilityPage's setLocalHours) literally inside WeekStripEditor's render
    phase. This is the exact mechanism that produces "Cannot update a component (AvailabilityPage)
    while rendering a different component (WeekStripEditor)".

- timestamp: 2026-07-26T00:02:00Z
  checked: dashboard/components/TimeOffCalendar.tsx (full read) and dashboard/lib/useAvailability.ts (full read)
  found: >
    TimeOffCalendar.handleDayClick (lines 113-131) calls onChange([...value, range]) directly in the
    onClick handler body (not inside any setState updater) — a safe, ordinary event-handler pattern.
    removeRange and updateReason likewise call onChange directly from event handlers. useAvailability.ts
    (SWR wrapper) and saveAvailability contain no setState calls at all (pure data-fetching/mutation
    functions) — no render-phase violation candidates found in either file.
  implication: >
    Rules out TimeOffCalendar and useAvailability.ts as the source of the render-phase violation,
    consistent with the warning explicitly naming WeekStripEditor (not TimeOffCalendar) as the
    rendering component. Confirms the violation is isolated to WeekStripEditor.tsx's handleUp/
    setPreviewRange interaction, independent of the time-off flow itself — the time-off calendar
    triggering an AvailabilityPage re-render (which also re-renders sibling WeekStripEditor) is
    coincidental to when the warning surfaces, not its cause.

## Resolution
<!-- OVERWRITE as understanding evolves -->

root_cause: >
  dashboard/components/WeekStripEditor.tsx, handleUp() (inside the useEffect at lines 136-165, function
  body at lines 144-156): the functional updater passed to `setPreviewRange(prev => { ... })` calls
  `emitChange(dragDay, mergeSegments([...byDay[dragDay], { start, end }]))` as a side effect when the
  drag distance exceeds SNAP_MINUTES. `emitChange` (lines 115-129) calls `onChange(next)` at line 128,
  which is the `Props.onChange` wired in dashboard/app/availability/page.tsx line 156 to
  `handleHoursChange` (page.tsx lines 67-71), which calls `setLocalHours` — a state setter owned by
  AvailabilityPage, not WeekStripEditor. React invokes `useState` functional updaters while processing
  that hook's pending-update queue during the owning component's render phase (renderWithHooks). Because
  the updater's side effect reaches into AvailabilityPage's setter, the call executes while
  WeekStripEditor is the `currentlyRenderingFiber`, tripping React's render-phase-update guard and
  producing "Cannot update a component (AvailabilityPage) while rendering a different component
  (WeekStripEditor)". The developer used the functional-updater form specifically to avoid a stale
  closure on `previewRange` (handleMove/handleUp are recreated only when `dragDay` changes, so a plain
  closure read of `previewRange` would be stale by the time pointerup fires) — but mixed that legitimate
  "read latest state" need with an illegitimate side effect (calling the parent's onChange) inside the
  updater body.
fix: ""
verification: ""
files_changed: []
