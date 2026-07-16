---
quick_id: 260712-tds
description: Fix semgrep CI findings — semgrepignore vendored tooling, pin gitleaks workflow action SHAs
date: 2026-07-12
---

# Quick Task 260712-tds: Fix semgrep CI findings

Source: `temp/20260712_semgrep_02.log` — 277 blocking findings from the `sast (semgrep)` job in `.github/workflows/security.yml`.

Triage:
- ~274 findings are in vendored GSD tooling (`.claude/**`, `landing-page/.claude/**`) — not project code, not fixable here.
- 1 finding: `.github/workflows/gitleaks.yml` uses mutable action tags (`actions/checkout@v4`, `gitleaks/gitleaks-action@v2`) — real supply-chain hardening gap, fix by pinning to commit SHAs.
- 1 finding: `API/index.html` (static prototype, unreferenced) loads `https://cdn.tailwindcss.com` without SRI — the CDN script is dynamically generated so SRI is not applicable; exclude from scan.

## Tasks

1. Add `.semgrepignore` at repo root: `:include .gitignore`, `.claude/` (covers root + landing-page copies), `API/index.html`.
2. Pin `gitleaks.yml` action refs: `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`, `gitleaks/gitleaks-action@ff98106e4c7b2bc287b24eaf42907196329070c7 # v2.3.9` (SHAs resolved via `git ls-remote`; gitleaks tag is lightweight → SHA is the commit).

Verify: `semgrep scan --config=auto --error --skip-unknown-extensions .` (if available locally) or next CI run reports 0 findings.
