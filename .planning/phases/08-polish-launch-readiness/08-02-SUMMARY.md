---
phase: 08-polish-launch-readiness
plan: 02
subsystem: api
tags: [ef-migrations, launch]

provides:
  - Migrate() only in Development
  - Production pending-migration fail-fast
  - CLAUDE.md production migrate runbook
affects: [deploy]

key-files:
  modified:
    - API/ZachHairStudio.Api/Program.cs
    - .claude/CLAUDE.md

key-decisions:
  - "D-03: Skip Migrate in Production; Dev still migrates"
  - "D-04: Fail fast on pending migrations when relational"
  - "D-05: Runbook in CLAUDE.md"

requirements-completed: [LAUNCH-03]
---

# Plan 08-02 Summary — Production migration path

Startup `Migrate()` runs only in Development. Production checks pending migrations (relational providers) and throws with `dotnet ef database update` guidance; Owner seed still runs in Production. CLAUDE.md documents the production schema path.
