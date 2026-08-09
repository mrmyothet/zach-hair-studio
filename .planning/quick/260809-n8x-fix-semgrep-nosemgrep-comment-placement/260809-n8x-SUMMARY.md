---
phase: quick
plan: 260809-n8x
subsystem: frontend
tags: [ci, semgrep, security, sast]

requires: ["260809-m2q"]
provides:
  - "chat.ts's nosemgrep suppression actually attaches to the flagged new RegExp(...) line, verified via a real local semgrep run"
affects: [ci, security-workflow]

tech-stack:
  added: []
  patterns: ["nosemgrep comments must sit on the line immediately above the flagged expression, not just 'nearby' — verify suppressions with a real scan, not by inspection alone"]

key-files:
  created: []
  modified: [landing-page/lib/chat.ts]

key-decisions:
  - "260809-m2q's suppression comment sat above `return aliases.some((alias) =>`, two lines above the actual `new RegExp(...)` call inside the arrow function body — semgrep's nosemgrep matching is line-adjacent, not block-scoped, so it never attached. Moved the comment inside the arrow function, directly above `new RegExp(...)`."
  - "Verified via a real local semgrep run (installed `pip install semgrep`, ran the exact CI command `semgrep scan --config=auto --error --skip-unknown-extensions .`) both BEFORE (reproduced the live CI failure: 1 blocking finding at chat.ts:108) and AFTER (0 findings, exit 0) the fix — not inferred from comment placement alone, per the lesson from the two prior gitleaks/semgrep investigations this session."
  - "chat.selfcheck.mjs was already correct — its single-line `new RegExp(...).test(...)` call has the nosemgrep comment directly above it, which is why only chat.ts needed this correction."

patterns-established: []

requirements-completed: []

duration: 20min
completed: 2026-08-09
status: complete
---

# Quick Task 260809-n8x: Fix misplaced nosemgrep comment in chat.ts Summary

**Corrected 260809-m2q's `nosemgrep` suppression in `landing-page/lib/chat.ts`, which had not actually taken effect on PR #43's live CI (`sast (semgrep)` still failed against commit `fe26590`) because the comment sat 2 lines above the flagged `new RegExp(...)` call instead of directly above it.**

## Root cause

PR #43's `sast (semgrep)` check-run (workflow run 31308767427, head `fe26590`) still showed `conclusion: "failure"` after 260809-m2q's fix was pushed — despite gitleaks turning green on the same run. Installed semgrep locally (`pip install semgrep`, v1.172.0) and ran the exact CI command against the working tree: reproduced the live failure exactly — `1 finding (1 blocking)` at `landing-page/lib/chat.ts:108`, the `new RegExp(...)` call. `chat.selfcheck.mjs` had 0 findings, confirming the `.mjs` fix was fine.

The bug: 260809-m2q placed the `nosemgrep` comment above `return aliases.some((alias) =>` (chat.ts:106), but the flagged expression is `new RegExp(...)` two lines further down (chat.ts:108), inside the arrow function body. Semgrep's `nosemgrep` suppression only attaches to the line immediately preceding the match — it is not block- or statement-scoped, so a comment "nearby" but not adjacent has no effect. `chat.selfcheck.mjs`'s equivalent call is a single line, so its comment (placed directly above) worked correctly the first time.

## Fix

Moved the rationale + `nosemgrep` comment from above `return aliases.some((alias) =>` to inside the arrow function, directly above `new RegExp(\`\\b${alias}\\b\`).test(normalizedInput)`. No logic change — comment relocation only (3 lines moved, net diff +3/-3).

## Verification

- **Before fix:** local `semgrep scan --config=auto --error --skip-unknown-extensions .` → `Findings: 1 (1 blocking)` at `chat.ts:108`, exit 1 — matches the live CI failure exactly.
- **After fix:** same command → `Findings: 0 (0 blocking)`, exit 0.
- `node lib/chat.selfcheck.mjs` (from `landing-page/`) still passes, exit 0.
- `git diff --stat` confined to `landing-page/lib/chat.ts`, 3 insertions / 3 deletions (comment relocation only).

## Lesson

Two suppression attempts in a row (260809-m2q, this task) were verified only by inspection/plausibility, not by actually running the tool being suppressed. This task broke that pattern — installing semgrep locally and reproducing the exact CI command was what caught the real defect. Going forward: when a suppression comment is added for a CI-only static analysis tool, verify with a real local run of that tool before considering the fix done, the same way 260809-k3d verified gitleaks with a real before/after binary run.

## Self-Check: PASSED

- FOUND: nosemgrep comment now directly above `new RegExp(...)` inside the arrow function in chat.ts
- FOUND: local semgrep run confirms 0 findings, exit 0 (reproduces the exact CI command)
- SELFCHECK_OK: `node lib/chat.selfcheck.mjs` exits 0

## Follow-up (not done in this session)

Push this commit and confirm PR #43's `sast (semgrep)` check-run shows `conclusion: "success"` against the new head commit — this is the third and (pending live confirmation) final iteration needed to close that check.
