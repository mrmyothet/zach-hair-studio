---
phase: 7
slug: accounts-retention
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-10
---

# Phase 7 — Validation Strategy

> Seeded from RESEARCH.md ## Validation Architecture. Task→plan mapping refreshed for 07-01..07-04 PLAN.md.

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
| 07-01-01 | 07-01 | 1 | ACCT-01 | T-07-01 | Register Client + login JWT with Client role | integration | `dotnet test --filter FullyQualifiedName~ClientAuthTests` | ❌ W0 | ⬜ pending |
| 07-01-02 | 07-01 | 1 | ACCT-05 | T-07-02 | Client role same AspNet schema; seeder asserts Client | integration | `dotnet test --filter FullyQualifiedName~IdentitySeederTests` | ⚠️ extend | ⬜ pending |
| 07-02-01 | 07-02 | 2 | ACCT-02 | T-07-08 | Client lists only own appointments | integration | `dotnet test --filter FullyQualifiedName~AccountBookingsTests` | ❌ W0 | ⬜ pending |
| 07-02-02 | 07-02 | 2 | ACCT-03 | T-07-08 | Client lists only own orders | integration | `dotnet test --filter FullyQualifiedName~AccountOrdersTests` | ❌ W0 | ⬜ pending |
| 07-02-03 | 07-02 | 2 | ACCT-06 | T-07-06 | Cross-client id access rejected (IDOR → 404) | integration | IDOR cases in AccountBookingsTests + AccountOrdersTests | ❌ W0 | ⬜ pending |
| 07-03-01 | 07-03 | 3 | ACCT-04 | T-07-12 | Owner cancel/reschedule; non-owner 404; txn book-new→cancel-old | integration | `dotnet test --filter FullyQualifiedName~ClientRescheduleTests` | ❌ W0 | ⬜ pending |
| 07-04-01 | 07-04 | 4 | ACCT-07 | T-07-20 | Ledger earn + server checkout discount; no client $ trust | integration | `dotnet test --filter FullyQualifiedName~LoyaltyTests` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Features/Identity/ClientAuthTests.cs` — ACCT-01 (Plan 07-01)
- [ ] Extend IdentitySeederTests — Client role seeded — ACCT-05 (Plan 07-01)
- [ ] `Features/Account/AccountBookingsTests.cs` — ACCT-02/06 (Plan 07-02)
- [ ] `Features/Account/AccountOrdersTests.cs` — ACCT-03/06 (Plan 07-02)
- [ ] `Features/Account/ClientRescheduleTests.cs` — ACCT-04 (Plan 07-03)
- [ ] `Features/Loyalty/LoyaltyTests.cs` — ACCT-07 (Plan 07-04)
- [ ] Reuse AuthGateTests UserManager + Jwt inject pattern

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Register → login → Bookings/Orders tabs on landing-page | ACCT-01..03 | No frontend test script | Create account; confirm Navbar Account; history tabs load own data only |
| Cancel/Reschedule from account bookings UI | ACCT-04 | No frontend test script | Confirmed upcoming → Cancel releases slot; Reschedule book-new then cancel-old |
| Loyalty strip + checkout Apply Points | ACCT-07 | No frontend test script | Complete owned appt → balance +1; redeem 10 pts updates server totals; guest checkout omits redeem |

---

## Validation Sign-Off

- [ ] All tasks have automated verify or Wave 0 deps
- [ ] Sampling continuity OK
- [ ] `nyquist_compliant: true` when validated

**Approval:** pending
