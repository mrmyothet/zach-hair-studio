---
phase: 260809-adm
plan: 01
subsystem: dashboard
tags: [ui, chat, dashboard]

# Dependency graph
requires:
  - phase: n/a
    provides: AdminChatWidget component + adminChat.ts STARTER_PROMPTS constant
provides:
  - persistent starter-prompt quick-reply row in AdminChatWidget
affects: [dashboard-admin-chat]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Quick-reply/shortcut rows in chat UIs render in their own persistent slot, not nested inside an empty-state conditional."

key-files:
  created: []
  modified:
    - dashboard/components/AdminChatWidget.tsx

key-decisions:
  - "Sticky row above the input form (not repeated after every assistant reply) — user picked this placement over the alternative when asked."

patterns-established: []

requirements-completed: [QUICK-260809-adm]

coverage:
  - id: D1
    description: "Starter-prompt buttons stay visible after the first message is sent"
    requirement: "QUICK-260809-adm"
    verification:
      - kind: other
        ref: "npx tsc --noEmit -p . (dashboard/) — no errors"
        status: pass
      - kind: other
        ref: "Manual visual check via npm run dev -- -p 3001 (staff/user to confirm)"
        status: deferred
    human_judgment: true

# Metrics
duration: 15min
completed: 2026-08-09
status: complete
---

# Quick Task 260809-adm: Keep AdminChat Starter Prompts Visible Summary

**Moved the AdminChatWidget's STARTER_PROMPTS quick-reply row out of the `messages.length === 0` empty-state block into a persistent row above the input form, so common-question shortcuts stay usable after the first message instead of disappearing forever.**

## Performance

- **Duration:** 15 min
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Root-caused the reported bug: `STARTER_PROMPTS.map(...)` was rendered only inside `{messages.length === 0 && (...)}` in `dashboard/components/AdminChatWidget.tsx`, so the buttons vanished permanently after the first send.
- Split that block: the placeholder text stays conditional on an empty conversation; the button row now renders unconditionally in its own `<div>` between the scrolling message list and the input form.
- Added `disabled={isTyping}` to the starter-prompt buttons (new — wasn't present before), matching the send button's existing disabled styling, so a shortcut can't be clicked mid-reply.
- Verified with `npx tsc --noEmit -p .` from `dashboard/` — clean, no errors.

## Task Commits

1. **Task 1: Make the starter-prompt row persistent** - `589470e` (fix)

_No plan metadata commit — handled separately by the orchestrator._

## Files Created/Modified
- `dashboard/components/AdminChatWidget.tsx` - Starter-prompt button row moved out of the empty-state conditional into a persistent row; buttons now disable while `isTyping`.

## Decisions Made
- Placement: sticky row above the input form, not repeated after every assistant reply — asked the user directly (AskUserQuestion), they picked the sticky-row option as recommended.

## Deviations from Plan
None.

## Issues Encountered
None.

## User Setup Required
None. Recommended follow-up: visually confirm in a running dashboard (`npm run dev -- -p 3001`) that the row renders correctly before/after sending a message — this was verified via type-check only, not a live browser.

## Next Phase Readiness
- No blockers. This is a self-contained UI fix.

---
*Phase: 260809-adm*
*Completed: 2026-08-09*

## Self-Check: PASSED

Modified file found on disk; task commit hash (589470e) found in git log; tsc --noEmit reports 0 errors.
