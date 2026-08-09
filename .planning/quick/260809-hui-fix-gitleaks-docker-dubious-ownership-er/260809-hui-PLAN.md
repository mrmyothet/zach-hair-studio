---
phase: quick
plan: 260809-hui
type: execute
wave: 1
depends_on: []
files_modified: [.github/workflows/security.yml]
autonomous: true
requirements: []

must_haves:
  truths:
    - "gitleaks Docker step no longer fails with 'detected dubious ownership' because /repo is marked safe inside the container's own gitconfig"
  artifacts:
    - ".github/workflows/security.yml"
  key_links:
    - "docker run --entrypoint sh wraps `git config --global --add safe.directory /repo && gitleaks detect ...` in one -c string"
---

<objective>
Fix the `secrets (gitleaks)` job in `.github/workflows/security.yml` so the gitleaks Docker container marks `/repo` as a safe git directory before running `detect`, eliminating the `fatal: detected dubious ownership in repository at '/repo'` failure.

Purpose: `actions/checkout` only writes `safe.directory` into the runner's gitconfig, not the gitleaks container's own gitconfig (container UID != mounted repo owner) — the container's `git log` call inside `gitleaks detect --source` refuses to run, exiting 1 before scanning anything. This is a container-ownership config bug, not a real secret finding (confirmed locally: gitleaks binary against full history + repo's `.gitleaks.toml` finds no real leaks).
Output: Updated `.github/workflows/security.yml` with an entrypoint override that sets `safe.directory` inside the container before invoking `gitleaks detect`.
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
  <name>Task 1: Fix gitleaks Docker dubious-ownership failure</name>
  <files>.github/workflows/security.yml</files>
  <action>
  In the `secrets` job's `gitleaks (secrets scan)` step (the `run:` block currently invoking `docker run --rm -v "$PWD:/repo" ghcr.io/gitleaks/gitleaks:v8.30.1 detect --source /repo --redact --verbose`), replace only that run block:

  - Add `--entrypoint sh` to the `docker run` invocation, right after the existing `-v "$PWD:/repo"` flag and before the image reference.
  - Change the trailing arguments from `detect --source /repo --redact --verbose` to a `sh -c` string that first runs `git config --global --add safe.directory /repo`, then `gitleaks detect --source /repo --redact --verbose`, joined with `&&` inside a single quoted `-c '...'` argument.
  - Insert a short comment directly above the new run block (alongside the existing pinned-tag/`detect`-subcommand comment) explaining: the gitleaks container's UID differs from the mounted `/repo` directory's owner, so git inside the container refuses to operate ("dubious ownership") unless `/repo` is explicitly marked safe in that container's own gitconfig; `actions/checkout`'s `safe.directory` fix only lands in the runner's gitconfig, not the container's, so it must be set here per-invocation.
  - Keep the existing "Pinned tag = reproducible..." comment as-is.
  - Touch nothing else in the file: do not modify the `sast` (semgrep) job, checkout step, permissions, concurrency, or triggers.
  </action>
  <verify>
    <automated>cd C:\Hnin_Wuttyi\Learning_Project\VibeCodeTours\zach-hair-studio && test $(git diff --name-only | wc -l) -eq 1 && git diff --name-only | grep -qx '.github/workflows/security.yml' && echo SCOPE_OK; git diff .github/workflows/security.yml | grep -qE '^[+-].*(sast|semgrep)' && echo "SAST_TOUCHED_FAIL" || echo SAST_UNTOUCHED_OK; python3 -c "import yaml; yaml.safe_load(open('.github/workflows/security.yml'))" 2>/dev/null && echo YAML_OK || echo "YAML_CHECK_SKIPPED (python3/pyyaml unavailable — verify indentation by eye instead)"</automated>
  </verify>
  <done>
  `git diff .github/workflows/security.yml` shows changes confined to the `secrets` job's gitleaks run block plus its accompanying comment (no changes to the `sast` job or any other file). The new run block reads (in shell semantics): `docker run --rm -v "$PWD:/repo" --entrypoint sh ghcr.io/gitleaks/gitleaks:v8.30.1 -c 'git config --global --add safe.directory /repo && gitleaks detect --source /repo --redact --verbose'`. File parses as valid YAML (via python3+pyyaml if available, else visual indentation check noted in verify output). A full CI dry-run isn't possible outside GitHub Actions — this is confirmed by diff scope + YAML structure only.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| CI runner -> gitleaks container | Mounted repo path (`$PWD:/repo`); container executes arbitrary `git`/`gitleaks` commands against it, read-only intent |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-quick-260809-hui-01 | Tampering | gitleaks Docker entrypoint override | low | accept | Entrypoint override only chains a `git config --global --add safe.directory` call ahead of the existing pinned `gitleaks:v8.30.1` image invocation — no new image, no new external input, `--rm` container is ephemeral and `-v` mount stays read-effectively (gitleaks does not write to the repo) |

</threat_model>

<verification>
`git diff .github/workflows/security.yml` shows only the gitleaks run block and its adjacent comment changed. YAML remains syntactically valid (python3+pyyaml check, or visual indentation confirmation if unavailable in this environment).
</verification>

<success_criteria>
The `secrets (gitleaks)` CI job's Docker invocation marks `/repo` as a safe git directory inside the container before calling `gitleaks detect`, resolving the "dubious ownership" exit-1 failure, with no other file or job touched.
</success_criteria>

<output>
Create `.planning/quick/260809-hui-fix-gitleaks-docker-dubious-ownership-er/260809-hui-SUMMARY.md` when done
</output>
