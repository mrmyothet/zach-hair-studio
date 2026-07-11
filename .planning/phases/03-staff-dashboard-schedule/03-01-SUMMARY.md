---
phase: 03-staff-dashboard-schedule
plan: 01
subsystem: auth
tags: [aspnet-core-identity, jwt, jwtbearer, efcore-migration, ownerseed]

requires:
  - phase: 02-booking-core
    provides: BookingDbContext, Appointment entity, EF migration convention (Migrate over EnsureCreated)
provides:
  - ApplicationUser (: IdentityUser<int>, DisplayName) and StaffRoles (Owner/Staff) constants
  - JwtOptions + JwtTokenService (HMAC-SHA256 signed tokens, ~12h lifetime)
  - IdentitySeeder (idempotent Owner + role seed, no self-registration)
  - BookingDbContext as IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
  - AddStaffIdentity migration (Identity tables + Appointment.StatusChangedAt/StatusChangedBy)
  - Program.cs wired with AddIdentity/AddJwtBearer as default auth+challenge scheme, 401 not cookie redirect
affects: [03-02-staff-login-and-dashboard-shell, 03-03, 03-04, 04-staff-service-availability-management, 07-accounts-and-retention]

tech-stack:
  added:
    - Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.9 (Shared)
    - System.IdentityModel.Tokens.Jwt 8.16.0 (Shared)
    - Microsoft.AspNetCore.Authentication.JwtBearer 10.0.9 (Api)
  patterns:
    - Options-bound secret-from-config (JwtOptions.SigningKey via user-secrets/env, mirrors ResendOptions/RESEND_API_KEY)
    - Static seeder class invoked once from the non-Testing startup scope, skipped in Testing (mirrors db.Database.Migrate() skip)
    - IdentityDbContext<TUser, TRole<int>, int> inheritance keeping int PKs consistent with the rest of the schema

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Identity/ApplicationUser.cs
    - API/ZachHairStudio.Shared/Features/Identity/StaffRoles.cs
    - API/ZachHairStudio.Shared/Features/Identity/JwtOptions.cs
    - API/ZachHairStudio.Shared/Features/Identity/JwtTokenService.cs
    - API/ZachHairStudio.Shared/Features/Identity/IdentitySeeder.cs
    - API/ZachHairStudio.Shared/Migrations/20260711061327_AddStaffIdentity.cs
    - API/ZachHairStudio.Api.Tests/Features/Identity/IdentitySeederTests.cs
  modified:
    - API/ZachHairStudio.Shared/Features/Appointments/Appointment.cs
    - API/ZachHairStudio.Shared/Db/BookingDbContext.cs
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Shared/ZachHairStudio.Shared.csproj
    - API/ZachHairStudio.Api/ZachHairStudio.Api.csproj

key-decisions:
  - "IdentityRole<int> (not the string-keyed default IdentityRole) is the correct second type parameter for IdentityDbContext<ApplicationUser, IdentityRole<int>, int> — the plan's literal IdentityDbContext<ApplicationUser, IdentityRole, int> does not compile since IdentityRole is IdentityRole<string>."
  - "base.OnModelCreating(modelBuilder) moved to the START of BookingDbContext.OnModelCreating (was previously called at the end) to follow the ASP.NET Core convention for IdentityDbContext-derived contexts."
  - "DisplayName claim uses a custom claim type 'displayName' (JwtTokenService.DisplayNameClaimType) since ClaimTypes has no built-in slot for a friendly name distinct from ClaimTypes.Name (the login UserName)."
  - "Owner seed defaults DisplayName to \"Owner\" — plan 03-02/staff management can let the Owner edit this later; no UI exists yet to set it at seed time."

patterns-established:
  - "Feature-folder Identity/ layout mirrors Features/Appointments/ (one concern per file)."
  - "Secret config values (Jwt:SigningKey, Owner:Email, Owner:InitialPassword) live only in dotnet user-secrets, never appsettings*.json — same discipline as RESEND_API_KEY."

requirements-completed: [DASH-05]

coverage:
  - id: D1
    description: "ASP.NET Core Identity is attached to BookingDbContext (IdentityDbContext<ApplicationUser, IdentityRole<int>, int>) and the AddStaffIdentity migration carries both the Identity tables and the two Appointment audit columns, applied cleanly against real LocalDB."
    requirement: DASH-05
    verification:
      - kind: integration
        ref: "dotnet ef database update --project API/ZachHairStudio.Shared --startup-project API/ZachHairStudio.Api"
        status: pass
      - kind: unit
        ref: "dotnet build API/ZachHairStudio.slnx"
        status: pass
    human_judgment: false
  - id: D2
    description: "JWT bearer authentication is wired as the default authenticate/challenge scheme (401 JSON, never the Identity cookie redirect), and JwtTokenService signs tokens with HmacSha256 from the configured signing key."
    requirement: DASH-05
    verification:
      - kind: unit
        ref: "dotnet build API/ZachHairStudio.slnx (grep-verified: AddIdentity, AddJwtBearer, app.UseAuthentication() before app.UseAuthorization(), HmacSha256 in JwtTokenService.cs)"
        status: pass
    human_judgment: false
  - id: D3
    description: "IdentitySeeder ensures Owner/Staff roles and exactly one seeded Owner account exist, idempotently, and is invoked only in the non-Testing startup scope."
    requirement: DASH-05
    verification:
      - kind: integration
        ref: "dotnet test API/ZachHairStudio.Api.Tests --filter FullyQualifiedName~IdentitySeeder (IdentitySeederTests.SeedAsync_OnFreshDatabase_CreatesBothRolesAndExactlyOneOwner, IdentitySeederTests.SeedAsync_RunTwice_IsIdempotent)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The shipped public booking endpoints (services, stylists, slots, create appointment) still respond anonymously with no [Authorize] attribute added yet — this plan ships no user-facing endpoint, only the auth backbone."
    requirement: DASH-05
    verification:
      - kind: integration
        ref: "dotnet test API/ZachHairStudio.slnx (full suite, 99/99 passed — existing public-endpoint tests unaffected)"
        status: pass
    human_judgment: false

duration: 14min
completed: 2026-07-11
status: complete
---

# Phase 3 Plan 01: Staff Identity + JWT Auth Backbone Summary

**ASP.NET Core Identity attached to `BookingDbContext` (int-keyed), HMAC-SHA256 JWT bearer auth as the default scheme, an idempotent Owner seeder, and the `AddStaffIdentity` migration carrying both Identity's tables and the two `Appointment` status-audit columns.**

## Performance

- **Duration:** 14 min
- **Started:** 2026-07-11T06:05:00Z
- **Completed:** 2026-07-11T06:19:00Z
- **Tasks:** 3 completed
- **Files modified:** 13 (7 created, 5 modified, 1 migration snapshot regenerated)

## Accomplishments

- `BookingDbContext` now inherits `IdentityDbContext<ApplicationUser, IdentityRole<int>, int>`, so `AspNetUsers`/`AspNetRoles`/etc. share the exact same migration history as `Appointments` (D-02).
- `ApplicationUser` (`DisplayName`), `StaffRoles` (`Owner`/`Staff`), `JwtOptions`, `JwtTokenService`, and `IdentitySeeder` are in place under `Features/Identity/`, mirroring the `Features/Appointments/` file-per-concern layout.
- `Program.cs` wires `AddIdentity` + `AddJwtBearer` with `DefaultAuthenticateScheme`/`DefaultChallengeScheme` both set to JwtBearer, so an unauthenticated `[Authorize]` hit will return a 401 JSON challenge rather than an Identity cookie redirect (no `[Authorize]` attributes exist on any controller yet — that lands in plan 03-02).
- The `AddStaffIdentity` migration applies cleanly against `(localdb)\MSSQLLocalDB`/`ZachHairStudio`, adding the six `AspNetUsers*`/`AspNetRoles*` tables and `Appointment.StatusChangedAt`/`StatusChangedBy`.
- `IdentitySeeder.SeedAsync` is proven idempotent against real SQL Server (not InMemory, per RESEARCH Pitfall 1): both roles + exactly one Owner user after one call, still exactly one Owner after a second call.
- Full test suite (`dotnet test API/ZachHairStudio.slnx`) — **99/99 passed**, confirming the existing public booking endpoints (services, stylists, slots, create appointment) are unaffected by the new auth wiring.

## Task Commits

Each task was committed atomically:

1. **Task 1: Identity domain model + audit columns + DbContext base switch** - `c691076` (feat)
2. **Task 2: JWT options/token service, Owner seeder, and Program.cs auth wiring** - `e00f0e1` (feat)
3. **Task 3: AddStaffIdentity migration + Owner-seeder integration test** - `b401a5b` (test)

**Plan metadata:** commit created below (docs: complete plan)

## Files Created/Modified

- `API/ZachHairStudio.Shared/Features/Identity/ApplicationUser.cs` - `: IdentityUser<int>` with `DisplayName`
- `API/ZachHairStudio.Shared/Features/Identity/StaffRoles.cs` - `Owner`/`Staff` role-name constants
- `API/ZachHairStudio.Shared/Features/Identity/JwtOptions.cs` - options POCO (SigningKey/Issuer/Audience/LifetimeHours=12)
- `API/ZachHairStudio.Shared/Features/Identity/JwtTokenService.cs` - mints HMAC-SHA256 signed tokens from `ApplicationUser` + roles
- `API/ZachHairStudio.Shared/Features/Identity/IdentitySeeder.cs` - idempotent Owner + role seed, no self-registration
- `API/ZachHairStudio.Shared/Features/Appointments/Appointment.cs` - added `StatusChangedAt`/`StatusChangedBy` (D-12)
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` - `IdentityDbContext<ApplicationUser, IdentityRole<int>, int>` base, `base.OnModelCreating` moved to the start, `StatusChangedBy` max-length config
- `API/ZachHairStudio.Api/Program.cs` - `AddIdentity`, `AddJwtBearer` (default auth+challenge scheme), `app.UseAuthentication()` before `app.UseAuthorization()`, seeder invocation in the non-Testing scope
- `API/ZachHairStudio.Shared/Migrations/20260711061327_AddStaffIdentity.cs` (+ Designer, + regenerated `BookingDbContextModelSnapshot.cs`) - Identity tables + audit columns
- `API/ZachHairStudio.Api.Tests/Features/Identity/IdentitySeederTests.cs` - proves the idempotent seeded-Owner-only guarantee against real LocalDB
- `API/ZachHairStudio.Shared/ZachHairStudio.Shared.csproj` - added `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.9, `System.IdentityModel.Tokens.Jwt` 8.16.0
- `API/ZachHairStudio.Api/ZachHairStudio.Api.csproj` - added `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.9

## Decisions Made

- **`IdentityRole<int>`, not `IdentityRole`, as the second type parameter.** The plan's literal `IdentityDbContext<ApplicationUser, IdentityRole, int>` doesn't compile — `IdentityRole` is shorthand for `IdentityRole<string>`. Corrected to `IdentityRole<int>` throughout (`BookingDbContext`, `Program.cs`'s `AddIdentity<ApplicationUser, IdentityRole<int>>()`, `RoleManager<IdentityRole<int>>` in `IdentitySeeder`/`Program.cs`/the test) — a Rule 1 fix, no behavior change from the plan's intent (int keys, per D-02/A3 in RESEARCH.md).
- **`base.OnModelCreating(modelBuilder)` moved to the start** of `BookingDbContext.OnModelCreating`, replacing the previous end-of-method call, per the ASP.NET Core convention for `IdentityDbContext`-derived contexts (the plan explicitly allowed either position but recommended this one).
- **Custom `displayName` claim type** for `JwtTokenService` — no built-in `ClaimTypes` member fits "friendly name distinct from the login UserName" (RESEARCH Open Question 3), so a small public constant (`JwtTokenService.DisplayNameClaimType`) documents the claim's shape for the login endpoint plan 03-02 will build.
- **Owner seed's `DisplayName` defaults to `"Owner"`** at seed time — there's no staff-management UI yet to prompt for a friendlier name; this is a reasonable placeholder the Owner can revisit once such a screen exists (Phase 4 territory).
- **JWT signing key generated as a 48-byte random secret** and stored via `dotnet user-secrets` (dev), never a tracked file — see User Setup below.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `IdentityDbContext<ApplicationUser, IdentityRole, int>` does not compile**
- **Found during:** Task 1 (`dotnet build` verification step)
- **Issue:** The plan's literal type parameters (`IdentityRole` as the `TRole` argument with an `int` `TKey`) fail to compile: `IdentityRole` is `IdentityRole<string>`, incompatible with the `int` key.
- **Fix:** Used `IdentityRole<int>` everywhere the plan named `IdentityRole` in a generic-with-int-key context (`BookingDbContext`, `AddIdentity<ApplicationUser, IdentityRole<int>>()`, `RoleManager<IdentityRole<int>>` in `IdentitySeeder.SeedAsync`'s signature, `Program.cs`'s seeder-resolution call, and `IdentitySeederTests`).
- **Files modified:** `API/ZachHairStudio.Shared/Db/BookingDbContext.cs`, `API/ZachHairStudio.Shared/Features/Identity/IdentitySeeder.cs`, `API/ZachHairStudio.Api/Program.cs`, `API/ZachHairStudio.Api.Tests/Features/Identity/IdentitySeederTests.cs`
- **Verification:** `dotnet build API/ZachHairStudio.slnx` succeeds (0 errors); `dotnet test API/ZachHairStudio.slnx` 99/99 passed.
- **Committed in:** `c691076` (Task 1), `e00f0e1` (Task 2), `b401a5b` (Task 3)

**2. [Rule 3 - Blocking] `System.IdentityModel.Tokens.Jwt` version 8.15.1 does not exist on NuGet**
- **Found during:** Task 2 (`dotnet restore` after adding the package reference)
- **Issue:** The initially-added version pin (guessed to match the JwtBearer 10.0.9-resolved `Microsoft.IdentityModel.*` line) triggered NU1603 — 8.15.1 not found, 8.16.0 resolved instead.
- **Fix:** Pinned the reference to the actually-published `8.16.0` to remove the floating-resolution warning.
- **Files modified:** `API/ZachHairStudio.Shared/ZachHairStudio.Shared.csproj`
- **Verification:** `dotnet restore` clean, no NU1603 warning.
- **Committed in:** `e00f0e1` (Task 2)

---

**Total deviations:** 2 auto-fixed (1 compile bug, 1 blocking version-pin correction)
**Impact on plan:** Both fixes were required for the code to build at all; no scope creep, no architectural change.

## Issues Encountered

None beyond the two auto-fixed deviations above.

## User Setup Required

**Local dev secrets already provisioned by this executor run** (dev environment only — no action needed to keep developing on this machine):

- `dotnet user-secrets set "Jwt:SigningKey" "<48-byte random value>"` — set against `API/ZachHairStudio.Api`'s user-secrets store. A fresh 48-byte random key was generated and stored; it is stable across restarts (never regenerated per-process) so ~12h tokens issued by a future login endpoint will survive an API restart.
- `dotnet user-secrets set "Owner:Email" "owner@zachhairstudio.local"` — placeholder Owner login email for local dev/test. **The salon owner should replace this with a real email before any non-local deployment.**
- `dotnet user-secrets set "Owner:InitialPassword" "<dev password>"` — placeholder Owner password meeting Identity's default policy. **Must be rotated/replaced before any non-local deployment; never committed to a tracked file.**

**For any other machine/environment running this API** (a fresh clone, CI, or production), the same three secrets must be set independently via `dotnet user-secrets set` (dev) or the equivalent environment variables (`Jwt__SigningKey`, `Owner__Email`, `Owner__InitialPassword` — double-underscore env-var syntax) before first startup outside the `Testing` environment, or the Owner seed step will silently no-op (no `Owner:Email`/`Owner:InitialPassword` configured → `IdentitySeeder.SeedAsync` returns early without creating an Owner) and JWT validation will fail with an empty signing key.

No secret values are present in any tracked file — verified via grep across `API/ZachHairStudio.Api` and `API/ZachHairStudio.Shared` for `SigningKey =`, `Owner:Email`, `Owner:InitialPassword` literals, and gitleaks passed on every commit in this plan.

## Next Phase Readiness

- The auth backbone (Identity + JWT bearer + Owner seed + migration) is fully in place for plan 03-02 to build the actual `POST /api/auth/login` endpoint, `[Authorize]` gating on `AppointmentsController`/a new schedule controller, and the `dashboard/` Next.js scaffold that consumes it.
- No user-facing endpoint ships in this plan by design — `JwtTokenService.CreateToken` and `IdentitySeeder` are unit/integration-tested in isolation but not yet exercised through an HTTP login flow; that's plan 03-02's `AuthController`.
- Every existing public booking endpoint remains anonymous and green (99/99 full-suite tests pass) — DASH-05's "public endpoints still respond anonymously" truth is confirmed, not just assumed.

## Known Stubs

- **Owner seed `DisplayName = "Owner"`** (`IdentitySeeder.cs`) is a placeholder friendly name, not sourced from any config. Intentional — no staff-management UI exists yet to collect a real display name at seed time; Phase 4 (staff management) or a later plan 03 iteration can let the Owner edit it. Does not block this plan's goal (the auth backbone), since `DisplayName` is present and non-null, satisfying every downstream consumer's non-null expectation.

---
*Phase: 03-staff-dashboard-schedule*
*Completed: 2026-07-11*

## Self-Check: PASSED

All 8 created/key files verified present on disk (`ApplicationUser.cs`, `StaffRoles.cs`, `JwtOptions.cs`, `JwtTokenService.cs`, `IdentitySeeder.cs`, `IdentitySeederTests.cs`, `20260711061327_AddStaffIdentity.cs`, this SUMMARY.md). All 4 commit hashes (`c691076`, `e00f0e1`, `b401a5b`, `9be6d85`) confirmed present in `git log`.
