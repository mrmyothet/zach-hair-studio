---
phase: 260809-gpw
plan: 01
subsystem: dashboard
tags: [security, sast, semgrep, ci]

# Dependency graph
requires:
  - phase: n/a
    provides: dashboard AdminChat routing logic (adminChat.ts + adminChat.test.mjs)
provides:
  - clean semgrep sast scan on adminChat.ts / adminChat.test.mjs
affects: [ci-security-sast]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Prefer word-split + Array.includes over per-candidate new RegExp() for fixed-vocabulary matching — avoids semgrep detect-non-literal-regexp and is simpler besides"

key-files:
  created: []
  modified:
    - dashboard/lib/adminChat.ts
    - dashboard/lib/adminChat.test.mjs

key-decisions:
  - "Fixed at the root (removed the RegExp construction) rather than suppressing the semgrep rule, since the same simplification also reads cleaner than the per-item regex."

patterns-established: []

requirements-completed: [QUICK-260809-gpw]

coverage:
  - id: D1
    description: "semgrep detect-non-literal-regexp finding cleared on both adminChat.ts and its mirrored adminChat.test.mjs"
    requirement: "QUICK-260809-gpw"
    verification:
      - kind: other
        ref: "semgrep scan --config=auto --error --skip-unknown-extensions dashboard/lib/adminChat.ts dashboard/lib/adminChat.test.mjs (0 findings, was 2 blocking)"
        status: pass
      - kind: other
        ref: "node dashboard/lib/adminChat.test.mjs (all assertions passed)"
        status: pass
    human_judgment: false

# Metrics
duration: 10min
completed: 2026-08-09
status: complete
---

# Quick Task 260809-gpw: Fix Semgrep SAST Finding in AdminChat Summary

**Replaced a per-weekday `new RegExp()` construction with a word-split membership check in `resolveDate`, clearing both semgrep `detect-non-literal-regexp` findings that failed PR #39's `security / sast (semgrep)` CI check.**

## Performance

- **Duration:** 10 min
- **Tasks:** 1
- **Files modified:** 2

## Accomplishments
- Reproduced the CI failure locally: `semgrep scan --config=auto --error --skip-unknown-extensions .` found 2 blocking findings, both `javascript.lang.security.audit.detect-non-literal-regexp`, at `dashboard/lib/adminChat.ts:90` and its hand-mirrored `dashboard/lib/adminChat.test.mjs:35`.
- Replaced `WEEKDAYS.findIndex((day) => new RegExp(`\\b${day}\\b`).test(t))` with `t.split(/\W+/)` + `words.includes(day)` in both files.
- Verified: `node dashboard/lib/adminChat.test.mjs` still passes ("all assertions passed"); re-running semgrep against the two files now reports 0 findings.

## Task Commits

1. **Task 1: Replace per-weekday RegExp construction with a word-split membership check** - `ba50770` (fix)

_No plan metadata commit — handled separately by the orchestrator._

## Files Created/Modified
- `dashboard/lib/adminChat.ts` - `resolveDate`'s named-weekday match now uses a word-split check instead of constructing a `RegExp` per `WEEKDAYS` candidate
- `dashboard/lib/adminChat.test.mjs` - Same change applied to the hand-maintained mirror, per the file's own header comment requiring the two stay in lockstep

## Decisions Made
- Fixed the underlying construction rather than adding a semgrep `# nosemgrep` suppression — `day` is always a fixed `WEEKDAYS` const value today so the finding isn't currently exploitable, but the word-split form is also just simpler code, so there was no reason to keep the regex.

## Deviations from Plan
None.

## Issues Encountered
None. The Vercel "Authorization required to deploy" check on the same PR is a separate, unrelated issue — it requires a repo/org admin to authorize the Vercel GitHub App and isn't fixable from this code.

## User Setup Required
None.

## Next Phase Readiness
- Push this commit and re-run the PR's `security` workflow; `sast (semgrep)` should go green.
- The `Vercel` check still needs a maintainer to grant deploy authorization in the Vercel dashboard/GitHub App settings — out of scope for this task.

---
*Phase: 260809-gpw*
*Completed: 2026-08-09*

## Self-Check: PASSED

Both modified files found on disk; task commit hash (ba50770) found in git log; semgrep re-run confirms 0 findings; adminChat.test.mjs self-check passes.
