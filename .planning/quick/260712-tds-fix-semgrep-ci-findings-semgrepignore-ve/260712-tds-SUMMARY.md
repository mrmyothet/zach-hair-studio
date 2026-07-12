---
quick_id: 260712-tds
status: complete
date: 2026-07-12
commit: 40e3207
---

# Summary: Fix semgrep CI findings

All 277 blocking findings from the CI semgrep scan (`temp/20260712_semgrep_02.log`) resolved; local re-run of the exact CI command (`semgrep scan --config=auto --error --skip-unknown-extensions .`) now reports **0 findings**, exit 0.

## What changed

- **`.semgrepignore` (new):** excludes vendored/third-party code from SAST — `.claude/` GSD tooling (~274 of the 277 findings, including the `landing-page/.claude/` copy), `API/index.html` (static prototype; `cdn.tailwindcss.com` serves dynamically generated JS so an SRI `integrity` hash cannot apply), and `API/ZachHairStudio.Admin/wwwroot/lib/` (ASP.NET template's jquery/jquery-validation bundles — 37 findings locally, not yet in the commit CI scanned). Starts with `:include .gitignore` since a custom `.semgrepignore` replaces semgrep's defaults.
- **`.github/workflows/gitleaks.yml`:** pinned mutable action tags to commit SHAs — `actions/checkout@11bd719...` (v4.2.2, same pin `security.yml` uses) and `gitleaks/gitleaks-action@ff98106...` (v2.3.9, lightweight tag resolved via `git ls-remote`).

## Deviation from plan

Plan listed 2 tasks; a third exclusion (`API/ZachHairStudio.Admin/wwwroot/lib/`) was added after the local verification run surfaced 37 findings in template-scaffolded client libs that the CI run's commit didn't contain.

## Not done (deliberate)

The findings inside `.claude/` vendored tooling were excluded, not fixed — that code is third-party (OpenGSD) and fixes belong upstream.
