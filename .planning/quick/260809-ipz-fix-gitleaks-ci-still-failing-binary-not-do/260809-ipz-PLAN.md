---
phase: quick
plan: 260809-ipz
type: execute
wave: 1
depends_on: []
files_modified: [.github/workflows/security.yml]
autonomous: true
requirements: []

must_haves:
  truths:
    - "gitleaks secrets-scan step runs as a direct binary install (curl+tar), not Docker, eliminating the container UID/volume-mount ownership failure class"
  artifacts:
    - ".github/workflows/security.yml"
  key_links:
    - "Install gitleaks step downloads+extracts the pinned v8.30.1 linux_x64 release tarball; gitleaks (secrets scan) step invokes the resulting ./gitleaks binary directly with --source ."
---

<objective>
Replace the Docker-based `gitleaks (secrets scan)` step in `.github/workflows/security.yml` with a direct-binary install + run, because the earlier Docker entrypoint fix (commit becd367, adding `git config --global --add safe.directory /repo`) is confirmed ineffective: `ghcr.io/gitleaks/gitleaks:v8.30.1`'s own Dockerfile already runs `git config --global --add safe.directory '*'` at build time, so "dubious ownership" was never the real cause. PR #43 head commit 907293f still shows both gitleaks check runs (job ids 93206376180, 93207390631) as `conclusion="failure"` despite that fix. Real job logs are unobtainable in this environment (`/actions/jobs/{id}/logs` API returns 403; the HTML job page requires sign-in) — a local `gitleaks detect --source . --redact --verbose` against the full working tree at 907293f found "281 commits scanned... no leaks found", ruling out a real secret. Switching to a direct binary install matches what was verified working locally and removes the entire Docker-container-layer class of issues (UID mismatch, volume-mount ownership, entrypoint override quirks) without needing real CI logs to diagnose further.

Note for STATE.md (not for the workflow file): `continue-on-error: true` is job-level — it makes the overall *workflow run* conclusion "success" while individual *check-run* conclusions still show "failure" in the PR checks list. This is expected GitHub Actions behavior (PR `mergeable_state` was "unstable", not "blocked" — confirming this job never actually blocked the merge). It explains why the check looked "still failing" even though the job was advisory-only by design. This binary fix additionally makes the individual check itself pass, not just remain non-blocking.

Purpose: Stop the gitleaks check run from showing red on every PR, without re-litigating a root cause (dubious ownership) that's already disproven, and without further attempts to debug via inaccessible CI logs.
Output: Updated `.github/workflows/security.yml` with a binary-based `Install gitleaks` + `gitleaks (secrets scan)` step pair replacing the Docker `docker run ... --entrypoint sh ...` step.
</objective>

<execution_context>
@.claude/gsd-core/workflows/execute-plan.md
@.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Switch gitleaks CI step from Docker to direct binary</name>
  <files>.github/workflows/security.yml</files>
  <action>
  In the `secrets` job, replace the single `gitleaks (secrets scan)` step (currently the `docker run --rm -v "$PWD:/repo" --entrypoint sh ghcr.io/gitleaks/gitleaks:v8.30.1 -c 'git config ... && gitleaks detect ...'` step plus its "Run the gitleaks Docker image directly, NOT gitleaks-action@v2" comment block, the inline dubious-ownership comment lines, and the trailing "Pinned tag = reproducible" comment) with two steps:

  1. A new `name: Install gitleaks` step whose `run:` block downloads the pinned `v8.30.1` `gitleaks_8.30.1_linux_x64.tar.gz` release asset via `curl -sSfL -o gitleaks.tar.gz` from `https://github.com/gitleaks/gitleaks/releases/download/v8.30.1/gitleaks_8.30.1_linux_x64.tar.gz`, then extracts just the `gitleaks` binary via `tar -xzf gitleaks.tar.gz gitleaks`.
  2. The existing `name: gitleaks (secrets scan)` step, now running `./gitleaks detect --source . --redact --verbose` directly (no Docker), with a short comment above the `run:` line covering: (a) why binary-not-Docker — it avoids the container UID/volume-mount git-ownership failure class that the becd367 entrypoint fix already tried and failed to resolve via Docker, because gitleaks:v8.30.1's own Dockerfile already sets `safe.directory '*'` at build time so that was never the actual cause; (b) gitleaks-action@v2's paid-license requirement for organization accounts applies only to that wrapper Action, not the OSS `gitleaks` binary itself — so running the binary directly here remains license-free, same as the removed Docker invocation was.

  Keep `continue-on-error: true` on the `secrets` job unchanged, keep the `v8.30.1` version pin (now expressed in the release-asset URL instead of a Docker image tag), and keep the `actions/checkout@...` step and `fetch-depth: 0` above it untouched. Do not modify the `sast` (semgrep) job, `permissions`, `concurrency`, or workflow `on:` triggers.
  </action>
  <verify>
    <automated>cd C:\Hnin_Wuttyi\Learning_Project\VibeCodeTours\zach-hair-studio && test $(git diff --name-only | wc -l) -eq 1 && git diff --name-only | grep -qx '.github/workflows/security.yml' && echo SCOPE_OK; git diff .github/workflows/security.yml | grep -qE '^[+-].*(sast|semgrep)' && echo "SAST_TOUCHED_FAIL" || echo SAST_UNTOUCHED_OK; git diff .github/workflows/security.yml | grep -qE '^\+.*docker run' && echo "DOCKER_STILL_PRESENT_FAIL" || echo DOCKER_REMOVED_OK; python3 -c "import yaml; yaml.safe_load(open('.github/workflows/security.yml'))" 2>/dev/null && echo YAML_OK || echo "YAML_CHECK_SKIPPED (python3/pyyaml unavailable — verify indentation by eye instead)"</automated>
  </verify>
  <done>
  `git diff .github/workflows/security.yml` shows changes confined to the `secrets` job's gitleaks step(s) (no changes to the `sast` job or any other file). The old `docker run ... ghcr.io/gitleaks/gitleaks:v8.30.1 ...` invocation is gone, replaced by an `Install gitleaks` step (curl+tar against the pinned `v8.30.1` `gitleaks_8.30.1_linux_x64.tar.gz` release asset) followed by a `gitleaks (secrets scan)` step running `./gitleaks detect --source . --redact --verbose`. `continue-on-error: true` remains on the `secrets` job. File parses as valid YAML (via python3+pyyaml if available, else visual indentation check noted in verify output). A full CI dry-run isn't possible outside GitHub Actions — this is confirmed by diff scope + YAML structure only; live CI verification is a follow-up for the user to do after merging PR #43 (or a fresh push) and watching the `secrets (gitleaks)` check run turn green.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| CI runner -> gitleaks release binary | `curl` fetches a GitHub Releases tarball over HTTPS from the upstream `gitleaks/gitleaks` repo at a pinned tag; the extracted binary then reads the full working tree read-only |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-quick-260809-ipz-01 | Tampering | `curl` download of gitleaks release tarball | low | accept | URL is pinned to an exact tag (`v8.30.1`) and exact asset filename on `github.com/gitleaks/gitleaks/releases/download/...` (GitHub-hosted, HTTPS, same trust level as the previously-used pinned `ghcr.io/gitleaks/gitleaks:v8.30.1` Docker tag); no checksum verification is added here (`ponytail:` acceptable for an advisory-only, continue-on-error CI step — add a SHA256 checksum pin if this job is ever promoted to blocking/required) |
| T-quick-260809-ipz-02 | Tampering | extracted `./gitleaks` binary executed on runner | low | accept | Binary only reads repo files (`detect --source .`) to scan for secret patterns; job stays `continue-on-error: true` (advisory), same blast radius as the Docker invocation it replaces |

</threat_model>

<verification>
`git diff .github/workflows/security.yml` shows only the `secrets` job's gitleaks step(s) changed (Docker invocation removed, binary install+run added). YAML remains syntactically valid (python3+pyyaml check, or visual indentation confirmation if unavailable in this environment). `sast` job, `permissions`, `concurrency`, and `on:` triggers are untouched.
</verification>

<success_criteria>
The `secrets (gitleaks)` CI job runs gitleaks as a direct binary (curl-downloaded, pinned to v8.30.1) instead of via Docker, eliminating the container UID/volume-mount ownership failure class entirely, with no other file or job touched. Live confirmation that the check run itself now passes on GitHub Actions is a follow-up for the user after pushing/merging.
</success_criteria>

<output>
Create `.planning/quick/260809-ipz-fix-gitleaks-ci-still-failing-binary-not-do/260809-ipz-SUMMARY.md` when done. The SUMMARY should additionally note the STATE.md clarification from the objective: `continue-on-error: true` is job-level (workflow run succeeds) but check-run conclusions still show per-job pass/fail in the PR checks list — expected GHA behavior, not a bug in this fix.
</output>
