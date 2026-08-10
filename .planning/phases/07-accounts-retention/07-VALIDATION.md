---
phase: 7
slug: accounts-retention
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-10
---

# Phase 7 — Validation Strategy

> Seeded from RESEARCH.md ## Validation Architecture. Task IDs refined when PLAN.md exists.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + Microsoft.AspNetCore.Mvc.Testing (existing ZachHairStudio.Api.Tests) |
| **Config file** | API/ZachHairStudio.Api.Tests + SqlServerWebApplicationFactory |
| **Quick run command** | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Account\|FullyQualifiedName~Loyalty\|FullyQualifiedName~AuthGate\|FullyQualifiedName~Client"` |
| **Full suite command** | `dotnet test API/ZachHairStudio.Api.Tests` |
| **Estimated runtime** | ~60-120 seconds |

---

## Sampling Rate

- **Per task commit:** filtered test command for touched area
- **Per wave merge:** `dotnet test API/ZachHairStudio.Api.Tests`
- **Phase gate:** Full suite green before `/gsd-verify-work`
- Landing-page UI: manual/smoke (no frontend test script)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 07-00-01 | TBD | TBD | ACCT-01 | — | Register Client + login JWT with Client role | integration | `dotnet test --filter FullyQualifiedName~ClientAuthTests` | ❌ W0 | ⬜ pending |
| 07-00-02 | TBD | TBD | ACCT-02 | — | Client lists only own appointments | integration | `dotnet test --filter FullyQualifiedName~AccountBookingsTests` | ❌ W0 | ⬜ pending |
| 07-00-03 | TBD | TBD | ACCT-03 | — | Client lists only own orders | integration | `dotnet test --filter FullyQualifiedName~AccountOrdersTests` | ❌ W0 | ⬜ pending |
| 07-00-04 | TBD | TBD | ACCT-04 | — | Owner cancel/reschedule; non-owner 404 | integration | `dotnet test --filter FullyQualifiedName~ClientRescheduleTests` | ❌ W0 | ⬜ pending |
| 07-00-05 | TBD | TBD | ACCT-05 | — | Client role same AspNet schema | integration | IdentitySeeder / role seed assert | ⚠️ extend | ⬜ pending |
| 07-00-06 | TBD | TBD | ACCT-06 | — | Cross-client id access rejected | integration | IDOR cases in Account*Tests | ❌ W0 | ⬜ pending |
| 07-00-07 | TBD | TBD | ACCT-07 | — | Ledger earn + server checkout discount | integration | `dotnet test --filter FullyQualifiedName~LoyaltyTests` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Features/Identity/ClientAuthTests.cs` — ACCT-01/05
- [ ] `Features/Account/AccountBookingsTests.cs` — ACCT-02/06
- [ ] `Features/Account/AccountOrdersTests.cs` — ACCT-03/06
- [ ] `Features/Account/ClientRescheduleTests.cs` — ACCT-04
- [ ] `Features/Loyalty/LoyaltyTests.cs` — ACCT-07
- [ ] Extend IdentitySeederTests — Client role seeded
- [ ] Reuse AuthGateTests UserManager + Jwt inject pattern

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Register → login → Bookings/Orders tabs on landing-page | ACCT-01..03 | No frontend test script | Create account; confirm Navbar Account; history tabs load own data only |

---

## Validation Sign-Off

- [ ] All tasks have automated verify or Wave 0 deps
- [ ] Sampling continuity OK
- [ ] `nyquist_compliant: true` when validated

**Approval:** pending
