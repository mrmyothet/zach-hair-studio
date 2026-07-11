---
phase: 3
slug: staff-dashboard-schedule
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-11
updated: 2026-07-11
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Populated from `03-RESEARCH.md` Validation Architecture. Wave 0 API test files
> were created by plans 03-01..03-03; frontend plans 03-04/03-05 use build/lint
> + human visual verify (no Jest/Playwright suite this phase).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 (`ZachHairStudio.Api.Tests`) |
| **Config file** | `API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` |
| **Quick run command** | `dotnet test API/ZachHairStudio.Api.Tests --filter FullyQualifiedName~Identity\|FullyQualifiedName~Schedule\|FullyQualifiedName~StatusUpdate\|FullyQualifiedName~Auth` |
| **Full suite command** | `dotnet test API/ZachHairStudio.slnx` |
| **Frontend smoke** | `cd dashboard && npm run lint && npm run build` |
| **Estimated runtime** | ~30–90s API filter; ~2–5 min full suite; ~30–60s dashboard build |

---

## Sampling Rate

- **After every API task commit:** Run the relevant `dotnet test --filter` for that plan's namespace
- **After every plan wave:** `dotnet test API/ZachHairStudio.slnx`
- **After dashboard tasks (03-04/03-05):** `npm run lint` + `npm run build` in `dashboard/`
- **Before `/gsd-verify-work`:** Full suite green + dashboard build green + human schedule walkthrough
- **Max feedback latency:** ~90s for filtered API tests

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 03-01-* | 01 | 1 | DASH-05 | T-03-* | JWT/Identity wired; public booking stays anonymous | integration | `dotnet test --filter FullyQualifiedName~Identity` | ✅ | ✅ (executed) |
| 03-02-* | 02 | 2 | DASH-05 | T-03-05 | Login 401 no enumeration; staff endpoints 401/403 | integration | `dotnet test --filter FullyQualifiedName~AuthGateTests` | ✅ | ✅ (executed) |
| 03-03-* | 03 | 2 | DASH-01..04 | T-03-08..10 | Schedule gated; transitions enforced; NoShow≠Cancelled | integration | `dotnet test --filter FullyQualifiedName~ScheduleControllerTests\|FullyQualifiedName~StatusUpdateTests` | ✅ | ✅ (executed) |
| 03-04-* | 04 | 3 | DASH-05 | — | Login + client-side guard; bearer attach | build + manual | `cd dashboard && npm run lint && npm run build` | ⬜ | ⬜ pending |
| 03-05-* | 05 | 4 | DASH-01..04 | T-03-* UI | Day/week UI + status actions + polling | build + manual | `cd dashboard && npm run lint && npm run build` | ⬜ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase API requirements (xUnit + Mvc.Testing already present). Wave 0 test files were delivered by plans 03-01..03-03:

- [x] `API/ZachHairStudio.Api.Tests/Features/Identity/AuthGateTests.cs` — DASH-05
- [x] `API/ZachHairStudio.Api.Tests/Features/Identity/IdentitySeederTests.cs` — seeded Owner
- [x] `API/ZachHairStudio.Api.Tests/Features/Appointments/ScheduleControllerTests.cs` — DASH-01/02
- [x] `API/ZachHairStudio.Api.Tests/Features/Appointments/StatusUpdateTests.cs` — DASH-03/04
- [x] Framework install: none — already present

Frontend (03-04/03-05): no new test framework this phase — lint/build + human visual verify per plan checkpoints.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Staff login → /schedule redirect; wrong password inline error | DASH-05 | Browser UX | Plan 03-04 human-verify checkpoint |
| Day/week grid, detail panel, Complete/Cancel/No-show + confirm dialogs, 60s refresh caption | DASH-01..04 | Visual/layout + interaction | Plan 03-05 human-verify checkpoint |
| Cancelled vs No-show never merged in UI treatments | DASH-04 | Visual copy/badge | Toggle "Show cancelled & no-shows"; confirm distinct labels |

---

## Validation Sign-Off

- [x] All API tasks have automated verify; frontend tasks have build + manual checkpoint
- [x] Sampling continuity: no 3 consecutive tasks without verify
- [x] Wave 0 API gaps closed by executed plans 01–03
- [x] No watch-mode flags
- [x] Feedback latency < 90s for filtered API tests
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-07-11 (filled from RESEARCH Validation Architecture during plan-phase gap closure)
