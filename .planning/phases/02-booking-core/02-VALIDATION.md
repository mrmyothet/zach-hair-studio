---
phase: 2
slug: booking-core
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-09
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `02-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 (established in Phase 1) |
| **Config file** | `API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` |
| **Quick run command** | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName!~SqlServer"` |
| **Full suite command** | `dotnet test API/ZachHairStudio.slnx` |
| **Estimated runtime** | quick ~20s · full ~90s (LocalDB migrate/drop per fixture) — **confirm in Wave 0** |

**Critical infrastructure gap.** The existing `CustomWebApplicationFactory` uses EF Core's
InMemory provider, which enforces **no unique indexes, alternate keys, or foreign keys**. It
therefore *cannot* prove BOOK-04/SC4 (DB-level double-booking guarantee) or BOOK-05/SC5
(`datetimeoffset` round-trip across a DST boundary). A second, real-SQL-Server LocalDB fixture
is a hard Wave 0 prerequisite — not an optimization.

---

## Sampling Rate

- **After every task commit:** `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName!~SqlServer"`
- **After every plan wave:** `dotnet test API/ZachHairStudio.slnx` (includes the real-SQL-Server concurrency + DST tests)
- **Before `/gsd-verify-work`:** Full suite must be green, including `SqlServerWebApplicationFactory`-backed tests
- **Max feedback latency:** 20 seconds (quick command)

---

## Per-Task Verification Map

Task IDs are assigned by `/gsd-plan-phase` once PLAN.md files exist. This table binds each
phase requirement to its proving test; the planner MUST map every task it creates onto one of
these rows (or add a row).

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | 0 | — | — | Real LocalDB fixture exists and runs migrations | infra | `dotnet test --filter FullyQualifiedName~SqlServer` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1+ | BOOK-01 | — | Open slots reflect working hours − time off − booked cells | unit + integration | `dotnet test --filter FullyQualifiedName~SlotServiceTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1+ | BOOK-02 | — | End-to-end booking: service → slot → confirm | integration (InMemory OK) | `dotnet test --filter FullyQualifiedName~AppointmentsControllerTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1+ | BOOK-03 | — | Confirmation email send is attempted; failure never rolls back the appointment (D-11) | integration | `dotnet test --filter FullyQualifiedName~EmailServiceTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1+ | BOOK-04 | T-2-01 | Two concurrent bookings for one stylist/slot → exactly one 201, one 409 | integration, **real LocalDB** | `dotnet test --filter FullyQualifiedName~ConcurrencyTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1+ | BOOK-05 | — | `DateTimeOffset` correct across the salon zone's 2026 spring-forward (Mar 8) and fall-back (Nov 1) | unit + integration, **real LocalDB** | `dotnet test --filter FullyQualifiedName~DstBoundaryTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | 1+ | BOOK-06 | — | "Any stylist" union + deterministic in-transaction assignment | unit + integration | `dotnet test --filter FullyQualifiedName~AnyStylistAssignmentTests` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `SqlServerWebApplicationFactory.cs` — real LocalDB-backed fixture. Must `UseEnvironment("Testing")`,
      use a per-run unique database name, call `Database.Migrate()` (not `EnsureCreated()`) so the actual
      generated unique index is exercised, and `Database.EnsureDeleted()` on dispose.
- [ ] `Microsoft.EntityFrameworkCore.SqlServer` package reference added to `ZachHairStudio.Api.Tests.csproj`
- [ ] Generate the EF migration and **inspect the emitted SQL** to confirm whether the unique index raises
      SQL Server error **2601** (`CREATE UNIQUE INDEX`) or **2627** (named constraint). RESEARCH.md finds
      2601 is correct and that CONTEXT.md D-03's stated 2627 is wrong. Catch both defensively; assert on
      the observed one. *(Open Question 1)*
- [ ] `RESEND_API_KEY` configured via `dotnet user-secrets` (D-13). Per D-12 this is **required to run the
      test suite** — no fake sender fallback. Blocks every test touching the create-appointment → email path.
      *(Open Question 2 — needs a `checkpoint:human-verify` task before the email slice can run.)*
- [ ] `Salon:IanaTimeZoneId` added to `appsettings.json` / `appsettings.Development.json`

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| A real confirmation email arrives in an inbox | BOOK-03 | D-12 mandates real Resend sends; automated tests assert the send was *attempted*, not that Resend delivered. Asserting on delivery would make CI depend on a third party's queue. | Book an appointment on `/book` with a real address; confirm receipt and that service, stylist, salon-local time + zone label, duration, and price are all present. |
| Resend sending domain is verified | BOOK-03 | External account state; cannot be asserted from the repo. | Confirm the sending domain shows "Verified" in the Resend dashboard before the email slice executes. |
| `/book` progressive-reveal UX and 409 "slot taken" recovery | BOOK-02, BOOK-04 | Visual/interaction quality. Pending `UI-SPEC.md` from `/gsd-ui-phase 2`. | Drive the flow in a browser; have a second client claim the slot mid-flow and confirm the recovery path reads clearly. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 20s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
