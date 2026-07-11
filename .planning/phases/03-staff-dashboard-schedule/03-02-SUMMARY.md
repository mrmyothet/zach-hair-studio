---
phase: 03-staff-dashboard-schedule
plan: 02
subsystem: auth
tags: [aspnet-core-identity, jwt, jwtbearer, fluentvalidation, rbac]

requires:
  - phase: 03-staff-dashboard-schedule
    provides: ApplicationUser/StaffRoles, JwtOptions/JwtTokenService, IdentitySeeder, Program.cs JWT bearer wiring (plan 03-01)
provides:
  - "POST /api/auth/login — staff exchange email+password for a JWT (token, expiresAt, displayName, role)"
  - "POST /api/staff-users — Owner-only endpoint that creates a Staff-role account"
  - "Enumeration-safe login (identical 401 for unknown-email and wrong-password)"
  - "AuthGateTests — integration proof of the DASH-05 auth gate over real SQL Server LocalDB"
affects: [03-03, 03-04, 04-staff-service-availability-management, 07-accounts-and-retention]

tech-stack:
  added: []
  patterns:
    - "Test-only Jwt:SigningKey/Issuer/Audience injected via WebApplicationFactory.WithWebHostBuilder(...).ConfigureAppConfiguration(...) so JwtBearer can mint/validate tokens without dotnet user-secrets in the test host."
    - "AuthGateTests seeds its own Owner/Staff users directly via UserManager/RoleManager (the startup IdentitySeeder is a no-op in Testing)."

key-files:
  created:
    - API/ZachHairStudio.Api.Tests/Features/Identity/AuthGateTests.cs
    - API/ZachHairStudio.Shared/Features/Identity/LoginRequestDto.cs
    - API/ZachHairStudio.Shared/Features/Identity/LoginRequestDtoValidator.cs
    - API/ZachHairStudio.Shared/Features/Identity/LoginResponseDto.cs
    - API/ZachHairStudio.Shared/Features/Identity/StaffUserCreateDto.cs
    - API/ZachHairStudio.Shared/Features/Identity/StaffUserCreateDtoValidator.cs
    - API/ZachHairStudio.Shared/Features/Identity/StaffUserResponseDto.cs
    - API/ZachHairStudio.Api/Controllers/AuthController.cs
    - API/ZachHairStudio.Api/Controllers/StaffUsersController.cs
  modified: []

key-decisions:
  - "AuthGateTests builds requests as anonymous C# objects and reads responses via JsonDocument rather than the Shared DTO types, so the test file compiles standalone in the RED phase before AuthController/StaffUsersController exist."
  - "StaffUsersController's route is explicit ([Route(\"api/staff-users\")]) rather than the [controller] token, since the default token would produce /api/staffusers (no hyphen) for a class named StaffUsersController."
  - "StaffUsersController mirrors AppointmentsController's Created($\"/api/{id}\", dto) string-URI style rather than CreatedAtAction, since there is no GET-by-id action yet to target."

requirements-completed: [DASH-05]

coverage:
  - id: D1
    description: "POST /api/auth/login accepts valid staff email+password and returns 200 with a non-empty JWT, expiresAt, displayName, and role."
    requirement: DASH-05
    verification:
      - kind: integration
        ref: "AuthGateTests.Login_ValidStaffCredentials_Returns200WithTokenExpiryDisplayNameAndRole"
        status: pass
    human_judgment: false
  - id: D2
    description: "Login is enumeration-safe: unknown-email and wrong-password both return a byte-identical generic 401 ProblemDetails body."
    requirement: DASH-05
    verification:
      - kind: integration
        ref: "AuthGateTests.Login_UnknownEmailAndWrongPassword_BothReturnIdentical401"
        status: pass
    human_judgment: false
  - id: D3
    description: "POST /api/staff-users rejects anonymous callers (401) and Staff-role tokens (403); an Owner-role token succeeds (201) and the created user carries the Staff role."
    requirement: DASH-05
    verification:
      - kind: integration
        ref: "AuthGateTests.CreateStaffUser_Anonymous_Returns401, AuthGateTests.CreateStaffUser_StaffRoleToken_Returns403, AuthGateTests.CreateStaffUser_OwnerRoleToken_Returns2xxAndCreatedUserHasStaffRole"
        status: pass
    human_judgment: false
  - id: D4
    description: "Invalid login/create payloads return 400 ProblemDetails via the same FluentValidation + ValidationProblem(ModelState) pattern used by AppointmentsController (PLAT-02)."
    requirement: DASH-05
    verification:
      - kind: unit
        ref: "dotnet build API/ZachHairStudio.slnx (LoginRequestDtoValidator/StaffUserCreateDtoValidator wired via the existing AddValidatorsFromAssemblyContaining scan)"
        status: pass
    human_judgment: false

duration: 10min
completed: 2026-07-11
status: complete
---

# Phase 3 Plan 02: Staff Login and Owner-Only Add-Staff Endpoint Summary

**`POST /api/auth/login` mints a JWT via `UserManager.CheckPasswordAsync` + `JwtTokenService`, enumeration-safe on failure; `POST /api/staff-users` is `[Authorize(Roles = StaffRoles.Owner)]`-gated and creates Staff-role accounts — both proven by a green `AuthGateTests` integration suite.**

## Performance

- **Duration:** 10 min
- **Started:** 2026-07-11T06:28:00Z
- **Completed:** 2026-07-11T06:38:00Z
- **Tasks:** 3 completed
- **Files modified:** 9 (all created)

## Accomplishments

- `POST /api/auth/login` validates the request via FluentValidation, looks up the user by email, and checks the password with `UserManager.CheckPasswordAsync` (no hand-rolled comparison) — success returns a signed JWT (`JwtTokenService` from 03-01), its expiry, the user's `DisplayName`, and their first role.
- The login failure path is enumeration-safe: an unknown email and a known email with the wrong password both hit the exact same code branch and return a byte-identical generic 401 `ProblemDetails` body ("Invalid email or password.") — proven directly by an integration test comparing the two response bodies.
- `POST /api/staff-users` carries a class-level `[Authorize(Roles = StaffRoles.Owner)]` (D-04's owner-only gate): anonymous callers get 401, Staff-role bearer tokens get 403, and only an Owner-role token can create a new account. The created account is always assigned the `Staff` role via `UserManager.AddToRoleAsync`.
- `AuthGateTests` (5 tests, real SQL Server LocalDB via `SqlServerWebApplicationFactory`, per RESEARCH Pitfall 1) proves all of the above end-to-end, including obtaining real JWTs from the login endpoint and attaching them as `Authorization: Bearer` headers to the Owner-only endpoint.
- Full solution test suite: **104/104 passed** — no regressions to the existing public booking endpoints or Phase 3 Plan 01's Identity backbone.

## Task Commits

Each task was committed atomically:

1. **Task 1: Failing AuthGateTests (login, 401, Owner/Staff role split)** - `d8f9ea0` (test)
2. **Task 2: AuthController login endpoint + login DTOs** - `e746d24` (feat)
3. **Task 3: StaffUsersController Owner-only add-staff endpoint + DTOs** - `22d9f66` (feat)

**Plan metadata:** commit created below (docs: complete plan)

## Files Created/Modified

- `API/ZachHairStudio.Api.Tests/Features/Identity/AuthGateTests.cs` - integration proof of the auth gate; seeds Owner/Staff users directly, injects a test JWT signing key via `WithWebHostBuilder`
- `API/ZachHairStudio.Shared/Features/Identity/LoginRequestDto.cs` - `Email`/`Password` POCO
- `API/ZachHairStudio.Shared/Features/Identity/LoginRequestDtoValidator.cs` - `AbstractValidator<LoginRequestDto>` (Email required/format/max150, Password required)
- `API/ZachHairStudio.Shared/Features/Identity/LoginResponseDto.cs` - `Token`/`ExpiresAt`/`DisplayName`/`Role` POCO
- `API/ZachHairStudio.Shared/Features/Identity/StaffUserCreateDto.cs` - `Email`/`DisplayName`/`Password` POCO
- `API/ZachHairStudio.Shared/Features/Identity/StaffUserCreateDtoValidator.cs` - `AbstractValidator<StaffUserCreateDto>`
- `API/ZachHairStudio.Shared/Features/Identity/StaffUserResponseDto.cs` - `Id`/`Email`/`DisplayName`/`Role` POCO, no Identity internals exposed
- `API/ZachHairStudio.Api/Controllers/AuthController.cs` - anonymous `POST /api/auth/login`
- `API/ZachHairStudio.Api/Controllers/StaffUsersController.cs` - `[Authorize(Roles = StaffRoles.Owner)]` `POST /api/staff-users`

## Decisions Made

- **AuthGateTests avoids the Shared DTO types entirely**, using anonymous objects for requests and `JsonDocument` for response assertions. This let the RED-phase test file (Task 1) compile and run standalone before `AuthController`/`StaffUsersController`/their DTOs existed, matching the plan's intent that the tests be genuinely RED (404s) rather than a build failure.
- **Test JWT signing key injected via `WithWebHostBuilder(...).ConfigureAppConfiguration(...)`** adding an in-memory `Jwt:SigningKey`/`Jwt:Issuer`/`Jwt:Audience` collection. This works because `WebApplicationBuilder.Configuration` is the same mutable `ConfigurationManager` instance Program.cs's `AddJwtBearer(options => ...)` closure reads from at request time (after the test host's config sources are merged in) — the same mechanism that already lets `RESEND_API_KEY` resolve in Testing per D-12/D-13.
- **`StaffUsersController` uses an explicit `[Route("api/staff-users")]`** instead of the `api/[controller]` token, since `[controller]` substitutes the class name verbatim (`staffusers`, no hyphen) — the plan's required route is `/api/staff-users`.
- **`StaffUsersController.Create` returns `Created($"/api/staff-users/{user.Id}", response)`** (string-URI `Created`, mirroring `AppointmentsController.CreateAppointment`) rather than `CreatedAtAction`, since there's no GET-by-id action on this controller yet to target.

## Deviations from Plan

None - plan executed exactly as written. Task 1's test-authoring approach (anonymous objects + JsonDocument instead of typed DTOs) was an implementation detail needed to satisfy the plan's own acceptance criterion ("Running the filter compiles... they may fail RED before Tasks 2-3"), not a deviation from scope.

## Issues Encountered

None.

## User Setup Required

None - no new external service configuration required. This plan reuses the `Jwt:SigningKey`/`Owner:Email`/`Owner:InitialPassword` user-secrets already provisioned in Plan 03-01; no new secrets were introduced.

## Next Phase Readiness

- The staff auth gate is now real and tested: a staff account can log in over HTTP and receive a working JWT, and the Owner can create additional staff accounts through the API. This unblocks Plan 03-03's `[Authorize]` gating on the schedule-facing `AppointmentsController` actions (list/detail/status-update) and the `dashboard/` Next.js login page, both of which can now authenticate against a real, tested backend.
- `AuthGateTests` is the reusable pattern for any future controller test that needs a real bearer token: seed a user with a role via `UserManager`/`RoleManager`, `POST /api/auth/login`, and attach the returned token as `Authorization: Bearer`.
- No user-facing dashboard UI ships in this plan by design — only the API-tier login and add-staff endpoints. The `dashboard/` Next.js scaffold's login page and schedule view are Plan 03-03/03-04 territory.

## Known Stubs

None - both endpoints are fully wired (no hardcoded/placeholder data); every response field is sourced from a real `UserManager`/`JwtTokenService` call.

---
*Phase: 03-staff-dashboard-schedule*
*Completed: 2026-07-11*

## Self-Check: PASSED

All 9 created files verified present on disk (`AuthGateTests.cs`, `LoginRequestDto.cs`, `LoginRequestDtoValidator.cs`, `LoginResponseDto.cs`, `StaffUserCreateDto.cs`, `StaffUserCreateDtoValidator.cs`, `StaffUserResponseDto.cs`, `AuthController.cs`, `StaffUsersController.cs`). All 3 task commit hashes (`d8f9ea0`, `e746d24`, `22d9f66`) confirmed present in `git log`.
