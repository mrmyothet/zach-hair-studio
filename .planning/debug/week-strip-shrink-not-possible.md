---
status: diagnosed
trigger: "G-04-6: Availability lets staff shrink an existing working-hours segment on the week strip, not only add new ones by dragging"
created: 2026-07-27T00:10:00Z
updated: 2026-07-27T00:20:00Z
---

## Current Focus

hypothesis: CONFIRMED — WeekStripEditor.tsx has no edge-resize/shrink interaction at all. Its only two mutation paths are (1) pointerdown-drag-anywhere-on-track which always unions the new range into the day's existing segments via mergeSegments (additive-only, can only grow or leave unchanged, never shrink), and (2) the × remove button which deletes an entire segment outright. There is no partial-shrink / drag-an-edge-inward code path.
test: Traced onPointerDown (track-wide, only excludes `[data-segment-remove]`), handleMove/handleUp (always calls emitChange(dragDay, mergeSegments([...byDay[dragDay], {start,end}]))), and mergeSegments (last.end = Math.max(last.end, seg.end) — union only, mathematically cannot reduce an endpoint). Also checked git history: 2 commits total on this file (ae0c963 04-04 original, d635e9f 04-06 fix) — 04-06 only changed how the commit is triggered (ref vs setState-updater), not the merge/paint semantics. Confirms this is a pre-existing interaction-design gap from 04-04, not a regression from 04-06.
expecting: N/A — root cause confirmed, goal is find_root_cause_only.
next_action: Return ROOT CAUSE FOUND to caller (no fix in this mode).

## Symptoms

expected: On the Availability page's week strip, staff should be able to shrink an already-painted working-hours segment (e.g. drag one edge of an existing gold-dark block inward to make it shorter), in addition to painting brand-new segments by click-and-drag on empty cells.
actual: "I am not able to shrik the existing ones, only can add by dragging" — user can only add new working-hours blocks by click-dragging; no way to shrink/resize an already-saved segment through the UI (no draggable edge handle; dragging over an existing segment doesn't reduce it).
errors: None reported
reproduction: Zach Hair Studio dashboard, /availability page, UAT Test 13 (.planning/phases/04-staff-management-services-availability/04-UAT.md) — pick a stylist with existing saved working hours, try to shrink one of the rendered segments on the week strip.
started: Discovered during UAT test 13, 2026-07-27, phase 04. Pre-existing since the component's introduction in 04-04 (commit ae0c963); NOT introduced by 04-06 (commit d635e9f), which only changed how drag commits are emitted (previewRangeRef-based) and did not touch merge/paint semantics.

## Eliminated

- hypothesis: "04-06's previewRangeRef-based commit-path change (G-04-5 fix) broke or removed a pre-existing resize feature."
  evidence: "git log --follow on WeekStripEditor.tsx shows exactly 2 commits: ae0c963 (04-04, original feature) and d635e9f (04-06, the G-04-5 render-phase-update fix). The 04-06 diff only touches handleMove/handleUp's mechanism for reading/committing the drag range (state updater -> ref) and does not add, remove, or alter any resize/edge-handle logic. mergeSegments and the additive-only emitChange call are byte-identical before and after 04-06."
  timestamp: 2026-07-27T00:15:00Z

- hypothesis: "A resize affordance exists but is visually hidden/undiscoverable (e.g., CSS hides an edge handle)."
  evidence: "Read the full segment-rendering JSX (WeekStripEditor.tsx lines 213-234): each segment div renders exactly one interactive child — the `data-segment-remove` × button (shown on hover via `group-hover:flex`). No other child elements, no edge-specific divs, no `cursor: ew-resize` styling, no additional pointerdown/pointermove handlers scoped to the segment edges exist anywhere in the file."
  timestamp: 2026-07-27T00:16:00Z

## Evidence

- timestamp: 2026-07-27T00:12:00Z
  checked: "WeekStripEditor.tsx onPointerDown handler (lines 189-199) on the day's track div"
  found: "The only guard against starting a fresh paint-drag is `if (target.closest('[data-segment-remove]')) return;` — i.e. clicking the × remove button. A pointerdown anywhere else on the track, INCLUDING on top of an already-rendered segment (the segment div at lines 215-233 does not call stopPropagation, unlike the remove button at line 226 which does), starts a brand-new drag via `setDragDay(day)` + a fresh previewRange at the click position."
  implication: "There is no branch that detects 'pointer landed near an existing segment's edge' and switches into a resize mode. Every pointerdown that isn't on the × button is treated identically: begin painting a new range from scratch."

- timestamp: 2026-07-27T00:13:00Z
  checked: "handleUp() commit logic (lines 148-157) and mergeSegments() (lines 56-68)"
  found: "handleUp always calls `emitChange(dragDay, mergeSegments([...byDay[dragDay], { start, end }]))` — i.e. it takes ALL of the day's existing segments plus the newly dragged range and merges them. mergeSegments sorts by start ascending and for overlapping/touching segments sets `last.end = Math.max(last.end, seg.end)`. Math.max can only produce a value >= the existing end; it can never shrink an existing segment's boundary. Segment `start` is likewise fixed as whichever segment sorts first in ascending order, never adjusted upward by a later overlapping drag."
  implication: "Mathematically, no drag gesture recognized by this component can reduce the size of an existing segment. Painting on top of / partially inside an existing segment either extends it (if the drag range exceeds a current edge) or is a complete no-op (if the drag range falls entirely inside the existing segment, since the union already covers it) — from the user's perspective this looks exactly like 'dragging over an existing segment doesn't reduce it,' matching the reported symptom verbatim."

- timestamp: 2026-07-27T00:14:00Z
  checked: "removeSegment() (lines 132-135) and the × button JSX (lines 223-232)"
  found: "The only way to reduce a segment's footprint via the current UI is to delete it entirely with the × button (which calls `removeSegment`, filtering the whole segment out of that day's array) and then paint a new, smaller replacement from scratch via a fresh drag."
  implication: "This is a workaround, not a shrink affordance — it requires the user to know the segment's exact original times to safely repaint (or accept losing that information), and it is not what UAT Test 13 or the user-facing help text describe ('drag one edge inward'). It also isn't discoverable: the help copy above the strip (lines 170-173) only documents 'drag to paint' and 'drag again to add a break,' with no mention of delete-then-repaint as the shrink path."

- timestamp: 2026-07-27T00:15:30Z
  checked: "git log --oneline --follow -- dashboard/components/WeekStripEditor.tsx"
  found: "Exactly 2 commits: ae0c963 (feat(04-04): WeekStripEditor drag-paint weekly hours — original implementation) and d635e9f (fix(04-06): commit drag range via ref so pointerup never updates parent mid-render (GREEN) — the G-04-5 fix). Diff of d635e9f confirmed scoped entirely to how/when the range commits (setState-updater -> ref-based), not what gets committed or how merging works."
  implication: "This is a pre-existing gap in interaction design present since the feature's original 04-04 implementation, not a regression introduced by the 04-06 G-04-5 fix. Confirms the symptoms.timeline hypothesis stated in the task."

## Resolution

root_cause: "dashboard/components/WeekStripEditor.tsx implements only two ways to mutate a day's working-hours segments: (1) any pointerdown-drag on the track (except on the × remove button) starts a brand-new paint gesture whose committed range is always UNIONED into the existing segment list via mergeSegments() — `last.end = Math.max(last.end, seg.end)` can only grow or preserve an endpoint, never shrink one — and (2) the × button in removeSegment() deletes an entire segment outright. No code path detects a pointerdown near an existing segment's left/right edge and enters a resize/shrink mode; there is no edge-handle UI element, no cursor affordance, and no subtraction/intersection logic anywhere in the file. This is a gap in the original interaction design shipped in 04-04 (commit ae0c963), not a regression from the 04-06 G-04-5 setState-during-render fix (commit d635e9f), which only changed the drag-commit mechanism (state updater -> ref) without touching merge/paint semantics."
fix: ""
verification: ""
files_changed: []
