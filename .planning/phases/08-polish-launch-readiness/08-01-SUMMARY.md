---
phase: 08-polish-launch-readiness
plan: 01
subsystem: api
tags: [cors, admin-retirement, launch]

provides:
  - Production CORS allowlist via Cors:Origins
  - ZachHairStudio.Admin removed from solution and disk
affects: [deploy config]

key-files:
  created:
    - API/ZachHairStudio.Api/CorsOrigins.cs
    - API/ZachHairStudio.Api.Tests/Features/Launch/CorsPolicyTests.cs
  modified:
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Api/appsettings.json
    - API/ZachHairStudio.Api/appsettings.Development.json
    - API/ZachHairStudio.slnx
    - .claude/CLAUDE.md

key-decisions:
  - "D-01: Production WithOrigins from Cors:Origins; Dev/Testing AllowAnyOrigin"
  - "D-02: Delete Admin MVC; dashboard/ is staff UI"

requirements-completed: [LAUNCH-02]
---

# Plan 08-01 Summary — CORS + Admin retirement

Production CORS uses `Cors:Origins` (semicolon-separated). Development/Testing stay permissive. `ZachHairStudio.Admin` removed from `ZachHairStudio.slnx` and deleted. `CorsPolicyTests` cover Testing `*` and Production allowlist preflight.
