---
phase: 04-staff-management-services-availability
reviewed: 2026-07-27T00:00:00Z
depth: standard
files_reviewed: 1
files_reviewed_list:
  - dashboard/components/WeekStripEditor.tsx
findings:
  critical: 0
  warning: 4
  info: 1
  total: 5
status: issues_found
---

# Phase 04: Code Review Report (Gap Closure Re-Review)

**Reviewed:** 2026-07-27
**Depth:** standard
**Files Reviewed:** 1
**Status:** issues_found

## Summary

Targeted re-review of commit `8c24a84` ("feat(04-07): add edge-resize drag mode to
WeekStripEditor", G-04-6), which adds a second pointer-drag gesture (edge-resize)
alongside the pre-existing paint gesture in `WeekStripEditor.tsx`. Full file read,
plus the commit diff, plus the two call-sites the component depends on
(`dashboard/lib/useAvailability.ts` and `dashboard/app/availability/page.tsx`) and the
server-side validator (`API/.../WorkingHoursReplaceDtoValidator.cs`) to confirm which
invariants the client can actually rely on.

Good news first, since the domain brief specifically asked about these: the resize
commit correctly bypasses `mergeSegments` (the exact bug G-04-6 targets), `emitChange`
is called from the event-handler body rather than from inside a `set*()` updater (no
`no-restricted-syntax` violation — confirmed clean via `npm run lint`), the
`byDay`-goes-stale-mid-gesture risk does not materialize because nothing in this
component or its parent (`AvailabilityPage`) calls `onChange` between pointerdown and
pointerup, and the render `key` (`${seg.start}-${seg.end}`) intentionally uses the
*committed* values rather than the live preview, so it stays stable for the whole drag
(no remount jank).

The problems are all in the corners: no guard against a no-op resize commit (an
inconsistency vs. the sibling paint gesture, which has exactly this guard), an
un-defended degenerate clamp branch that produces a wrong answer instead of a safe one
if its input invariant is ever violated, a concrete overlapping-hit-target bug between
two touching segments' handles, and a resize interaction with no keyboard path and no
non-hover discovery affordance, despite carrying `aria-label`s that suggest it was
built with accessibility in mind.

## Critical Issues

None found.

## Warnings

### WR-01: `handleResizeUp` commits even when nothing changed (no-op click emits a change)

**File:** `dashboard/components/WeekStripEditor.tsx:203-216`
**Issue:** The pre-existing paint gesture guards its commit: `handleUp` (line 170)
only calls `emitChange` `if (end - start >= SNAP_MINUTES)` — i.e. it will not commit a
degenerate/no-op paint. `handleResizeUp` has no equivalent guard. If the user merely
clicks a resize handle (pointerdown immediately followed by pointerup, with no
intervening `pointermove`), `resizeRef.current` still holds the initial value set by
`startResize` (line 149-151), so `updated` is computed with `value === initial` — i.e.
byte-for-byte the same segment — yet `emitChange` is still called unconditionally
(line 213). That produces a brand-new array reference through `onChange`, which in
`AvailabilityPage.handleHoursChange` (dashboard/app/availability/page.tsx:67-71) resets
`saveSuccess` to `false` and clears `conflicts` even though the user made no actual
edit — e.g. a "Availability saved." confirmation banner would silently disappear from
an accidental hover-click on a resize handle right after a successful save.
**Fix:**
```tsx
function handleResizeUp() {
  if (!resizeRef.current) {
    setResizeTarget(null);
    return;
  }
  const { target, value } = resizeRef.current;
  const seg = byDay[target.day][target.index];
  const unchanged = target.edge === "start" ? value === seg.start : value === seg.end;
  if (!unchanged) {
    const updated = byDay[target.day].map((s, i) => {
      if (i !== target.index) return s;
      return target.edge === "start" ? { ...s, start: value } : { ...s, end: value };
    });
    emitChange(target.day, updated);
  }
  setResizeTarget(null);
  resizeRef.current = null;
}
```

### WR-02: Degenerate clamp bound in `handleResizeMove` silently produces an out-of-range value instead of failing safe

**File:** `dashboard/components/WeekStripEditor.tsx:57-59, 195-198`
**Issue:** `clamp(n, min, max)` is `Math.min(max, Math.max(min, n))`, which silently
assumes `min <= max`. In `handleResizeMove`, the start-edge branch is
`clamp(raw, prev ? prev.end : 0, seg.end - SNAP_MINUTES)`. If `seg.end - SNAP_MINUTES <
prev.end` (the segment being resized is shorter than `SNAP_MINUTES` away from its
predecessor's end), `clamp` degrades to *always* returning `max` (`seg.end -
SNAP_MINUTES`) regardless of `raw` — and that returned value is, by the very condition
that made it degenerate, **less than `prev.end`**, i.e. the function returns a start
time that overlaps the previous segment, which is exactly the invariant the clamp
exists to prevent.

Under the invariants this component itself maintains (every segment committed through
paint or resize is snapped to the 15-minute grid, has length >= `SNAP_MINUTES`, and is
non-overlapping with its neighbours), this branch is not reachable — `seg.end -
prev.end >= seg.end - seg.start >= SNAP_MINUTES` always holds. But that invariant is
enforced by *this component's own gestures*, not by the clamp function itself, and not
by every path that can populate `value`: `WorkingHoursReplaceDtoValidator` (server-side,
`API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDtoValidator.cs`)
only rejects overlaps and enforces grid alignment on the PUT path — it does not enforce
a minimum 15-minute *gap* between adjacent segments, and it is never run against rows
inserted by seed data, migrations, or any future write path other than this one PUT
endpoint. If a stylist ever has two working-hours segments closer together than 15
minutes (from seed data, a data-migration, or a future client), the very first
start-edge resize on the later segment will silently commit an overlapping segment via
`emitChange` — which bypasses `mergeSegments` by design — with no error, no clamp
warning, and no server-side re-validation until the next Save.
**Fix:** Make the clamp (or its call sites) defensive instead of relying on an
external invariant:
```ts
function clamp(n: number, min: number, max: number): number {
  if (min > max) return min; // degenerate range: prefer the safe/no-op bound
  return Math.min(max, Math.max(min, n));
}
```
or, more locally, skip the commit entirely when the computed bound is degenerate:
```ts
const startMax = seg.end - SNAP_MINUTES;
const startMin = prev ? prev.end : 0;
if (startMin > startMax) return; // segment too small to resize against neighbour
```

### WR-03: Resize handles at a touching segment boundary occupy the same pixel region — ambiguous/unreachable hit target

**File:** `dashboard/components/WeekStripEditor.tsx:307, 317`
**Issue:** The start handle is `absolute inset-y-0 -left-1 w-2` (an 8px-wide band
straddling the segment's left edge, from -4px to +4px relative to that edge) and the
end handle is `absolute inset-y-0 -right-1 w-2` (the same 8px band straddling the
segment's right edge). When two segments in the same day touch exactly (`prev.end ===
seg.start` — a state resize itself can legitimately produce, since its own clamp
bounds permit dragging all the way to `next.start`/`prev.end`), segment *i*'s end
handle and segment *i+1*'s start handle are rendered at the exact same x-coordinate
band. Later siblings paint on top in DOM order, so segment *i+1* (rendered after
segment *i* in the `segments.map` at line 274) will always win the hit-test at that
shared boundary. This makes it effectively impossible to grab segment *i*'s end handle
again once it touches its neighbour — the user can only ever resize segment *i+1*'s
start from that point on, silently redirecting what looks like the same edge to a
different data mutation than the one the user is dragging.
**Fix:** Either shrink the handle bands so they don't extend past the segment's own
boundary into the shared pixel region (e.g. `inset-y-0 left-0 w-1` for start / `right-0
w-1` for end, kept inside the segment), or detect the touching case and merge/disable
the redundant handle pair, or add a small always-visible gap indicator between touching
segments so grabbing the correct edge is unambiguous.

### WR-04: New resize handles have no keyboard path and are undiscoverable on touch — `aria-label` alone doesn't make them accessible

**File:** `dashboard/components/WeekStripEditor.tsx:299-318`
**Issue:** Both resize handles are plain `<div>`s with only `onPointerDown` and
`aria-label`; they carry no `role`, no `tabIndex`, and no `onKeyDown`. A generic `<div>`
has an implicit ARIA role of `generic`, is not part of the tab order, and cannot
receive focus — so the `aria-label` is inert for keyboard/screen-reader users, who
have no way to discover or operate this control at all (contrast with the pre-existing
remove button at line 319, which is at least a real `<button>` and thus natively
focusable/keyboard-operable). Additionally, both handles are gated behind
`hidden group-hover:block` with no `group-focus-within:block` fallback, so on
touch-primary devices (no persistent `:hover` state) the handles may never become
visible/tappable at all, and the 8px (`w-2`) hit target is well under common touch
target guidance even when visible.
**Fix:** At minimum, add keyboard operability and a non-hover reveal path:
```tsx
<div
  data-segment-resize="start"
  role="slider"
  tabIndex={0}
  aria-label={`Resize ${WEEKDAY_LABEL[day]} segment start`}
  aria-valuenow={seg.start}
  aria-valuemin={prev ? prev.end : 0}
  aria-valuemax={seg.end - SNAP_MINUTES}
  onKeyDown={(e) => {
    if (e.key === "ArrowLeft") { /* nudge start -15, clamp, emitChange */ }
    if (e.key === "ArrowRight") { /* nudge start +15, clamp, emitChange */ }
  }}
  onPointerDown={(e) => { e.stopPropagation(); e.preventDefault(); startResize(day, i, "start"); }}
  className="group-hover:block group-focus-within:block focus:block absolute inset-y-0 -left-1 w-2 cursor-ew-resize"
/>
```

## Info

### IN-01: Duplicated pointer-gesture boilerplate between the paint and resize effects

**File:** `dashboard/components/WeekStripEditor.tsx:154-225`
**Issue:** The two `useEffect` hooks (paint drag, lines 154-183; edge resize, lines
185-225) are structurally identical — guard on a nullable target state, attach
`pointermove`/`pointerup` to `window`, tear both down in cleanup, with an
exhaustive-deps escape hatch on the same dependency shape. This isn't a bug, but the
duplication means any future fix to one gesture's listener-management (e.g. adding
`Escape`-to-cancel, or pointer capture) has to be remembered and re-applied to the
other by hand.
**Fix:** Consider extracting a small `useWindowPointerDrag(active, onMove, onUp)` hook
shared by both gestures, or at least leave a comment cross-referencing the twin effect
so a future editor updates both together.

---

_Reviewed: 2026-07-27_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
