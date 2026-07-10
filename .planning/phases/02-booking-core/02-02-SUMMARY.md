---
phase: 02-booking-core
plan: 02
subsystem: testing
tags: [ef-core, sql-server, localdb, webapplicationfactory, xunit, user-secrets, resend]

# Dependency graph
requires:
  - phase: 02-01
    provides: AddBookingCore migration (AppointmentSlot unfiltered unique index, datetimeoffset(0) column)
provides:
  - Real-SQL-Server LocalDB test fixture (SqlServerWebApplicationFactory) that runs the actual AddBookingCore migration
  - Smoke tests proving 4 seeded stylists, AppointmentSlot DateTimeOffset round-trip, and RESEND_API_KEY resolution in Testing
  - RESEND_API_KEY configured in dotnet user-secrets (real Resend sends enabled in Development and Testing per D-12)
  - Program.cs loads user secrets unconditionally so the key resolves under dotnet test (env "Testing")
affects: [02-04, 02-06, booking, appointments, email, concurrency, dst]

# Tech tracking
tech-stack:
  added: [Microsoft.EntityFrameworkCore.SqlServer 10.0.9 (test project)]
  patterns:
    - "Dual test fixtures: fast InMemory (CustomWebApplicationFactory) for logic tests, real LocalDB (SqlServerWebApplicationFactory) for DB-constraint/semantics tests"
    - "Per-run unique LocalDB database migrated with Database.Migrate() and dropped via EnsureDeletedAsync() in DisposeAsync"

key-files:
  created:
    - API/ZachHairStudio.Api.Tests/SqlServerWebApplicationFactory.cs
    - API/ZachHairStudio.Api.Tests/Features/Infrastructure/SqlServerFixtureSmokeTests.cs
  modified:
    - API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj
    - .claude/CLAUDE.md
    - API/ZachHairStudio.Api/Program.cs

key-decisions:
  - "Load user secrets unconditionally in Program.cs (AddUserSecrets<Program> + re-apply AddEnvironmentVariables) so RESEND_API_KEY resolves in the Testing environment, not only Development (D-12 requires real sends in Testing)"
  - "Override DisposeAsync (not sync Dispose) to drop the throwaway LocalDB before the host's service provider is torn down"
  - "Kept InMemory fixture alongside the new SqlServer fixture — fast path for non-constraint tests"

patterns-established:
  - "SqlServer fixture class/test names contain 'SqlServer' so `dotnet test --filter FullyQualifiedName~SqlServer` selects (or the quick-run excludes) the slow LocalDB tests"
  - "Config-resolution invariants (secret present in Testing) asserted non-empty only — never asserting on, logging, or printing the value"

requirements-completed: [BOOK-03, BOOK-04, BOOK-05]

coverage:
  - id: D1
    description: "Real-SQL-Server LocalDB fixture migrates the actual AddBookingCore schema (not InMemory) so SC4/SC5 become provable"
    requirement: BOOK-04
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Infrastructure/SqlServerFixtureSmokeTests.cs#MigratedDatabase_ExposesFourSeededStylists"
        status: pass
    human_judgment: false
  - id: D2
    description: "AppointmentSlot DateTimeOffset round-trips through SQL Server datetimeoffset with its offset intact (SC5 prerequisite)"
    requirement: BOOK-05
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Infrastructure/SqlServerFixtureSmokeTests.cs#AppointmentSlot_RoundTripsDateTimeOffset"
        status: pass
    human_judgment: false
  - id: D3
    description: "RESEND_API_KEY resolves in the Testing environment (Program.cs unconditional AddUserSecrets), so Plan 04's create-appointment→email path can run under dotnet test"
    requirement: BOOK-03
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Infrastructure/SqlServerFixtureSmokeTests.cs#ResendApiKey_ResolvesInTestingEnvironment"
        status: pass
    human_judgment: false
  - id: D4
    description: "Resend sending domain is Verified (needed for actual email delivery in Plan 06 manual check)"
    verification: []
    human_judgment: true
    rationale: "Only the presence of RESEND_API_KEY in user-secrets was confirmed at execution time; the sending domain's Verified status in the Resend dashboard was NOT confirmed and requires human inspection."

# Metrics
duration: ~12min
completed: 2026-07-10
status: complete
---

# Phase 2 Plan 02: Real-SQL-Server Test Fixture + Resend Key Summary

**Real-SQL-Server LocalDB fixture (SqlServerWebApplicationFactory) that migrates the actual AddBookingCore schema, plus an unconditional user-secrets load so RESEND_API_KEY resolves under dotnet test — unblocking SC4/SC5 and the real email path for Plan 04.**

## Performance

- **Duration:** ~12 min
- **Completed:** 2026-07-10
- **Tasks:** 2 (Task 1 human checkpoint pre-satisfied; Task 2 auto)
- **Files modified:** 5 (2 created, 3 modified)

## Accomplishments
- `SqlServerWebApplicationFactory` stands up a per-run unique LocalDB database, applies the real `AddBookingCore` migration via `Database.Migrate()` (so the unfiltered unique index and `datetimeoffset(0)` column are exercised), and drops the database on `DisposeAsync` — no orphaned databases.
- `SqlServerFixtureSmokeTests` proves the migrated DB exposes 4 seeded stylists, round-trips an `AppointmentSlot` `DateTimeOffset` (value + offset), and confirms `RESEND_API_KEY` resolves in the Testing environment.
- Approved deviation: `Program.cs` now loads user secrets unconditionally, closing the gap where the key resolved under `dotnet run` (Development) but never under `dotnet test` (env "Testing").
- CLAUDE.md dev-simplicity note records that D-12 makes `RESEND_API_KEY` required to run the API and tests.

## Task Commits

1. **Deviation (RESEND_API_KEY resolves in Testing)** - `b204daa` (fix)
2. **Task 2: SqlServer fixture, smoke tests, package ref, CLAUDE.md note** - `39e8d97` (test)

_Task 1 (checkpoint:human-verify) was satisfied before dispatch: the orchestrator confirmed RESEND_API_KEY is present in `dotnet user-secrets` for API/ZachHairStudio.Api. No auto-work or commit for Task 1._

## Files Created/Modified
- `API/ZachHairStudio.Api.Tests/SqlServerWebApplicationFactory.cs` - Real LocalDB WebApplicationFactory: UseSqlServer, Database.Migrate(), EnsureDeletedAsync on dispose, per-run unique DB name; no UseInMemoryDatabase.
- `API/ZachHairStudio.Api.Tests/Features/Infrastructure/SqlServerFixtureSmokeTests.cs` - Three smoke tests (stylists, datetimeoffset round-trip, RESEND_API_KEY resolution).
- `API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` - Added Microsoft.EntityFrameworkCore.SqlServer 10.0.9; kept InMemory.
- `.claude/CLAUDE.md` - Dev-simplicity note updated for D-12 (RESEND_API_KEY required).
- `API/ZachHairStudio.Api/Program.cs` - Unconditional AddUserSecrets<Program>(optional: true) + re-applied AddEnvironmentVariables().

## Decisions Made
- **Unconditional user-secrets load (deviation).** The default ASP.NET Core host registers the user-secrets provider only in Development, but the test fixtures use `UseEnvironment("Testing")`. Without this change, `RESEND_API_KEY` would resolve under `dotnet run` yet never under `dotnet test`, defeating D-12 (real Resend sends in Development AND Testing). Added `builder.Configuration.AddUserSecrets<Program>(optional: true)` right after the builder is created, then re-applied `AddEnvironmentVariables()` so env vars keep precedence over a possibly-stale secrets.json. `optional: true` keeps CI (no secrets.json) booting.
- **DisposeAsync over sync Dispose.** The initial sync `Dispose(bool)` implementation threw `ObjectDisposedException` because WebApplicationFactory tears down the host's service provider before the sync override runs. Overriding `DisposeAsync` and dropping the DB before `base.DisposeAsync()` resolves it cleanly.

## Deviations from Plan

### Auto-fixed / Approved Issues

**1. [Rule 3 - Blocking / user-approved] RESEND_API_KEY did not resolve in the Testing environment**
- **Found during:** Task 2 (implementing the Testing-environment fixture)
- **Issue:** `Program.cs` had no `AddUserSecrets` call. The default host registers user secrets only in Development, but both `CustomWebApplicationFactory` and the new `SqlServerWebApplicationFactory` call `UseEnvironment("Testing")` (to skip the startup auto-migrate branch). So the key resolved under `dotnet run` but never under `dotnet test`, defeating D-12 and blocking Plan 04's create-appointment→email tests.
- **Fix:** Added `builder.Configuration.AddUserSecrets<Program>(optional: true)` immediately after builder creation, then re-applied `builder.Configuration.AddEnvironmentVariables()` to preserve env-var-over-secrets precedence. `AddUserSecrets<Program>()` resolves the UserSecretsId from the ZachHairStudio.Api assembly (the only project carrying `<UserSecretsId>`).
- **Files modified:** API/ZachHairStudio.Api/Program.cs (adds this file to the plan's files_modified)
- **Verification:** New test `ResendApiKey_ResolvesInTestingEnvironment` resolves IConfiguration from the host and asserts the key is non-empty (value never asserted/logged/printed). Passes.
- **Committed in:** `b204daa`

**2. [Rule 1 - Bug] Fixture disposal threw ObjectDisposedException**
- **Found during:** Task 2 (first SqlServer test run)
- **Issue:** Sync `Dispose(bool)` called `Services.CreateScope()` after WebApplicationFactory had already disposed the host's service provider → `ObjectDisposedException` at class cleanup (tests passed but cleanup failed).
- **Fix:** Replaced with `public override async ValueTask DisposeAsync()` that drops the DB (EnsureDeletedAsync) before `base.DisposeAsync()`.
- **Files modified:** API/ZachHairStudio.Api.Tests/SqlServerWebApplicationFactory.cs
- **Verification:** `dotnet test --filter FullyQualifiedName~SqlServer` now passes 3/3 with clean disposal, exit 0.
- **Committed in:** `39e8d97`

---

**Total deviations:** 2 (1 user-approved blocking config fix, 1 disposal bug)
**Impact on plan:** Both were necessary for correctness. The AddUserSecrets fix is the sole change to Program.cs and is required to satisfy D-12 in Testing; the disposal fix keeps LocalDB clean. No scope creep.

## Verification Results
- `dotnet build API/ZachHairStudio.Api.Tests` — **Build succeeded, 0 errors, 7 warnings.** All 7 are the pre-existing CS8601 warnings in `API/ZachHairStudio.Shared/Result.cs` (known, out of scope — not touched by this plan).
- `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~SqlServer"` — **Passed 3/3, exit 0.**
- `dotnet test API/ZachHairStudio.Api.Tests` (full suite) — **Passed 67/67, 0 failed.** No real email is sent by this plan's tests (the create-appointment→email path does not exist yet — that is Plan 04).
- `git grep RESEND_API_KEY` across tracked files — only documentation (CLAUDE.md), a code comment (Program.cs), and .planning references. **No value present.** gitleaks pre-commit passed on both commits (no `--no-verify`).

## Issues Encountered
- Fixture disposal ObjectDisposedException — resolved by moving cleanup into DisposeAsync (see Deviation 2).

## User Setup Required
Task 1 (checkpoint:human-verify) captured the external Resend setup. `RESEND_API_KEY` is confirmed present in `dotnet user-secrets` for `API/ZachHairStudio.Api`.

**Unconfirmed:** the Resend **sending domain Verified status** was NOT confirmed at execution time — only that the key exists. If the sending domain is not yet Verified in the Resend dashboard, actual email delivery will fail, which blocks the manual email-delivery check in Plan 06. Confirm `Resend Dashboard -> Domains` shows the salon sending domain as "Verified" before Plan 06's manual verification.

## Next Phase Readiness
- Plan 04 can now prove SC4 (DB-level double-booking via the unfiltered unique index) and SC5 (DST-correct datetimeoffset) against the `SqlServerWebApplicationFactory`, and can exercise the real create-appointment→email path (key resolves in Testing).
- **Blocker/risk for Plan 06:** sending-domain Verified status unconfirmed (see User Setup Required).

---
*Phase: 02-booking-core*
*Completed: 2026-07-10*

## Self-Check: PASSED
- FOUND: API/ZachHairStudio.Api.Tests/SqlServerWebApplicationFactory.cs
- FOUND: API/ZachHairStudio.Api.Tests/Features/Infrastructure/SqlServerFixtureSmokeTests.cs
- FOUND: .planning/phases/02-booking-core/02-02-SUMMARY.md
- FOUND commit: b204daa
- FOUND commit: 39e8d97
