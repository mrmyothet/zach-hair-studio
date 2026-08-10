---
phase: quick
plan: 260809-ipz
subsystem: infra
tags: [ci, github-actions, gitleaks, docker]

requires: []
provides:
  - "gitleaks secrets-scan step runs as a direct binary (curl+tar of pinned v8.30.1 release), not Docker"
affects: [ci, security-workflow]

tech-stack:
  added: []
  patterns: ["curl -sSfL + tar -xzf a pinned GitHub release tarball, run the extracted binary directly"]

key-files:
  created: []
  modified: [.github/workflows/security.yml]

key-decisions:
  - "Dropped Docker entirely for gitleaks instead of further container-config fixes: the becd367 dubious-ownership fix was confirmed ineffective (gitleaks:v8.30.1's own Dockerfile already sets safe.directory '*' at build time — that was never the real cause), and real CI logs are unobtainable in this environment (jobs/{id}/logs returns 403, HTML job page requires sign-in), so root-causing the actual Docker failure further wasn't productive. Binary install matches the locally-verified working invocation exactly."
  - "continue-on-error: true is job-level: it makes the overall workflow-run conclusion 'success' while individual check-run conclusions still show 'failure' in the PR checks list (confirmed via API: run 31298518481 conclusion=success, but its secrets(gitleaks) check-run 93207390631 conclusion=failure). PR #43's mergeable_state was 'unstable', not 'blocked' — the job never actually blocked merging, only looked red. This binary fix additionally makes the individual check pass, not just remain non-blocking."

patterns-established: []

requirements-completed: []

duration: 15min
completed: 2026-08-09
status: complete
---

# Quick Task 260809-ipz: Fix gitleaks CI still failing (binary, not Docker) Summary

**Replaced the Docker-based `gitleaks (secrets scan)` step with a direct binary install+run (curl+tar of the pinned v8.30.1 release), after confirming the earlier Docker entrypoint/safe.directory fix (becd367) never addressed the real failure cause.**

## Performance

- **Duration:** ~15 min
- **Tasks:** 1/1 completed
- **Files modified:** 1

## Accomplishments

- `.github/workflows/security.yml`'s `secrets` job now installs gitleaks v8.30.1 as a plain binary (`Install gitleaks` step: `curl -sSfL` the linux_x64 release tarball, `tar -xzf`) and runs it directly (`./gitleaks detect --source . --redact --verbose`) — no `docker run`, no volume mount, no entrypoint override.
- Removes the entire container-layer failure class (UID mismatch, volume-mount ownership, entrypoint quirks) that the becd367 fix attempted to patch without success.
- Verified locally: `gitleaks detect --source . --redact --verbose` (no `--config` override, matching the workflow exactly) against the full working tree at commit 907293f reports "281 commits scanned... no leaks found" — confirms this was never a real secret finding.
- Verified YAML validity via `js-yaml` (borrowed from `dashboard/node_modules`): file parses cleanly, `secrets` job has exactly 3 steps (`checkout`, `Install gitleaks`, `gitleaks (secrets scan)`), `sast` job untouched at 4 steps.
- `continue-on-error: true`, the `v8.30.1` pin, and the `sast (semgrep)` job are all unchanged.

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check: PASSED

- FOUND: .github/workflows/security.yml (modified, diff confined to the `secrets` job's gitleaks step pair)
- FOUND: no `docker run` invocation remains in the file
- SAST_UNTOUCHED_OK: no changes to the `sast` job
- YAML_VALID_OK: parsed successfully with js-yaml, structure confirmed

## Follow-up (not done in this session)

Live CI verification requires pushing this commit and re-triggering a PR run — out of scope for a local quick task. After merging, confirm the `secrets (gitleaks)` check-run itself shows `conclusion: "success"` on the next PR run (not just an overall workflow-run success).
