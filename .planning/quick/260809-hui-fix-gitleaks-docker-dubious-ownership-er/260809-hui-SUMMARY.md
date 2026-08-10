---
phase: quick
plan: 260809-hui
subsystem: infra
tags: [ci, github-actions, gitleaks, docker]

requires: []
provides:
  - "gitleaks Docker step marks /repo safe inside the container's own gitconfig before running detect"
affects: [ci, security-workflow]

tech-stack:
  added: []
  patterns: ["docker run --entrypoint sh wrapping a multi-command -c string"]

key-files:
  created: []
  modified: [.github/workflows/security.yml]

key-decisions:
  - "Set safe.directory inside the gitleaks container's own gitconfig per-invocation, since actions/checkout's fix only reaches the runner's gitconfig, not the container's (container UID != mounted repo owner)."

patterns-established: []

requirements-completed: []

duration: 5min
completed: 2026-08-09
status: complete
---

# Quick Task 260809-hui: Fix gitleaks Docker dubious-ownership error Summary

**Wrapped the gitleaks Docker invocation in `--entrypoint sh -c '... && gitleaks detect ...'` so the container marks `/repo` as a safe git directory in its own gitconfig before scanning.**

## Performance

- **Duration:** ~5 min
- **Tasks:** 1/1 completed
- **Files modified:** 1

## Accomplishments

- `secrets (gitleaks)` job in `.github/workflows/security.yml` no longer fails with `fatal: detected dubious ownership in repository at '/repo'`. The container's UID differs from the mounted `/repo` owner, and `actions/checkout`'s `safe.directory` fix only lands in the runner's gitconfig, not the container's — so `git config --global --add safe.directory /repo` now runs inside the container itself, immediately before `gitleaks detect`.

## Deviations from Plan

None - plan executed exactly as written.

## Self-Check: PASSED

- FOUND: .github/workflows/security.yml (modified, diff confined to the gitleaks run block + comment)
- FOUND: becd367 (commit exists in git log)
- SAST_UNTOUCHED_OK: no changes to the `sast` job
