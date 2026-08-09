---
phase: quick
plan: 260809-m2q
subsystem: frontend
tags: [ci, semgrep, security, sast]

requires: []
provides:
  - "chat.ts and chat.selfcheck.mjs document why alias-driven RegExp() is not a ReDoS risk and suppress the specific semgrep finding"
affects: [ci, security-workflow]

tech-stack:
  added: []
  patterns: ["targeted nosemgrep suppression with a one-line rationale comment, mirroring the .gitleaks.toml allowlist approach for the same investigation"]

key-files:
  created: []
  modified: [landing-page/lib/chat.ts, landing-page/lib/chat.selfcheck.mjs]

key-decisions:
  - "Confirmed false positive, not fixed via rewrite: alias in both files comes only from the hardcoded CATEGORY_ALIASES map (4 static keys, short lowercase word lists), never from normalizedInput (the actual user-controlled chat message). There is no tainted input reaching new RegExp() — semgrep's detect-non-literal-regexp rule flags any non-literal constructor argument regardless of origin."
  - "Suppressed with a standard // nosemgrep: <rule-id> comment plus a one-line rationale directly above each flagged call, in both chat.ts and its self-check mirror chat.selfcheck.mjs (kept in sync per that file's own header comment) — no logic change in either file."
  - "Same investigation session as quick task 260809-k3d (gitleaks .codex manifest allowlist fix) — both are real (but false-positive) advisory security-scan findings on PR #43, closed via targeted, documented suppressions rather than code rewrites, consistent with security.yml's own comment: 'Expect some false positives — triaging them is the lesson.'"

patterns-established: []

requirements-completed: []

duration: 15min
completed: 2026-08-09
status: complete
---

# Quick Task 260809-m2q: Suppress semgrep ReDoS false positive Summary

**Added a targeted `nosemgrep` suppression with rationale above the `new RegExp(...)` call in `landing-page/lib/chat.ts` and its self-check mirror `chat.selfcheck.mjs`, closing PR #43's 2 blocking `detect-non-literal-regexp` findings — a real but false-positive advisory `sast (semgrep)` result, not a genuine ReDoS risk.**

## Performance

- **Duration:** ~15 min
- **Tasks:** 1/1 completed
- **Files modified:** 2

## Accomplishments

- Confirmed via code inspection that `alias` in `findMatchingService` (both `chat.ts:104` and `chat.selfcheck.mjs:34`) is drawn exclusively from `CATEGORY_ALIASES[category] ?? []` — a module-scoped hardcoded object with 4 keys, each a short static array of lowercase words ("cut", "haircut", "hair cut", "dye", "colour", "style", "treatment"). It never touches `normalizedInput` (the actual user-controlled chat message), so there's no ReDoS attack surface.
- Added a two-line comment (rationale + `// nosemgrep: javascript.lang.security.audit.detect-non-literal-regexp.detect-non-literal-regexp`) directly above the flagged line in each file — no logic changed, diff is comment-only (+3/+3 lines).
- Verified `node lib/chat.selfcheck.mjs` (run from `landing-page/`) still passes with all assertions after the edit.
- Both findings from the pasted CI output (`chat.ts:104-106`, `chat.selfcheck.mjs:34`) are addressed identically, keeping the two files in sync as the self-check file's own header comment requires.

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check: PASSED

- FOUND: `nosemgrep` suppression + rationale comment above the `new RegExp(...)` call in `chat.ts` and `chat.selfcheck.mjs`
- FOUND: `git diff --stat` shows only these two files, 3 insertions each, no deletions
- SELFCHECK_OK: `node lib/chat.selfcheck.mjs` exits 0 ("chat.selfcheck: all assertions passed")

## Follow-up (not done in this session)

Live CI verification requires pushing this commit and re-checking PR #43's `sast (semgrep)` step — confirm both `detect-non-literal-regexp` findings no longer appear in the scan output. This is the second of two advisory security-scan false positives closed in this session; the first was gitleaks (quick task 260809-k3d).
