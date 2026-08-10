---
phase: 07-accounts-retention
plan: 01
subsystem: auth
tags: [identity, jwt, client-role, register, landing-page, localStorage]

requires:
  - phase: 03-staff-dashboard
    provides: ApplicationUser, IdentitySeeder Owner/Staff, AuthController login, JwtTokenService, dashboard/lib/auth.ts pattern
provides:
  - StaffRoles.Client seeded on shared Identity store
  - POST /api/auth/register returning Client-role LoginResponseDto JWT
  - landing-page zhs.client.auth session + /account login|register|shell
  - Navbar Account vs Log In toggle
affects: [07-02 account history, 07-03 self-service, 07-04 loyalty]

tech-stack:
  added: []
  patterns:
    - Client self-register on shared AspNet Identity with StaffRoles.Client only
    - Landing JWT localStorage mirror of dashboard with separate storage key
    - AUTH_UPDATED_EVENT for same-tab Navbar session refresh

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Identity/RegisterRequestDto.cs
    - API/ZachHairStudio.Shared/Features/Identity/RegisterRequestDtoValidator.cs
    - API/ZachHairStudio.Api.Tests/Features/Identity/ClientAuthTests.cs
    - landing-page/lib/auth.ts
    - landing-page/app/account/login/page.tsx
    - landing-page/app/account/register/page.tsx
    - landing-page/app/account/page.tsx
  modified:
    - API/ZachHairStudio.Shared/Features/Identity/StaffRoles.cs
    - API/ZachHairStudio.Shared/Features/Identity/IdentitySeeder.cs
    - API/ZachHairStudio.Api/Controllers/AuthController.cs
    - API/ZachHairStudio.Api.Tests/Features/Identity/IdentitySeederTests.cs
    - landing-page/components/Navbar.tsx

key-decisions:
  - "DisplayName defaults to email local-part when omitted on register"
  - "getSession/setSession reject non-Client roles so staff tokens cannot drive landing account UI"
  - "AUTH_UPDATED_EVENT mirrors cart notify pattern for same-tab Navbar Account/Log In updates"

patterns-established:
  - "Client auth E2E: Register → LoginResponseDto → zhs.client.auth → requireAuth → /account"
  - "Register role-assign failure rolls back via DeleteAsync (StaffUsersController pattern)"

requirements-completed: [ACCT-01, ACCT-05]

coverage:
  - id: D1
    description: Client registers with email+password and receives Client-role JWT
    requirement: ACCT-01
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Identity/ClientAuthTests.cs#Register_ValidCredentials_Returns200WithClientRoleJwt
        status: pass
    human_judgment: false
  - id: D2
    description: Client login returns Client-role JWT after register
    requirement: ACCT-01
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Identity/ClientAuthTests.cs#Login_AfterRegister_Returns200WithClientRole
        status: pass
    human_judgment: false
  - id: D3
    description: Client role exists on shared Identity schema; no seeded Client users
    requirement: ACCT-05
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Identity/IdentitySeederTests.cs#SeedAsync_OnFreshDatabase_CreatesBothRolesAndExactlyOneOwner
        status: pass
    human_judgment: false
  - id: D4
    description: Landing-page stores JWT under zhs.client.auth and exposes /account login|register|shell with Navbar Account vs Log In
    requirement: ACCT-01
    verification: []
    human_judgment: true
    rationale: End-to-end browser smoke (register → Navbar Account → logout → Log In) requires human or browser automation not in this plan's automated verify

duration: 7min
completed: 2026-08-10
status: complete
---

# Phase 7 Plan 01: Client Identity Register/Login Summary

**Client role on shared Identity plus register/login JWT path and landing-page `/account` auth with Navbar Account toggle via `zhs.client.auth`.**

## Performance

- **Duration:** 7 min
- **Started:** 2026-08-10T09:59:18Z
- **Completed:** 2026-08-10T10:06:30Z
- **Tasks:** 3/3
- **Files modified:** 12

## Accomplishments

- Seeded `StaffRoles.Client` on the existing AspNet Roles store (no second auth stack; no Client user seed)
- `POST /api/auth/register` creates ApplicationUser, assigns Client, returns `LoginResponseDto` JWT in one round-trip
- Landing-page `/account/login`, `/account/register`, minimal `/account` shell + Navbar Account vs Log In using `zhs.client.auth`

## Task Commits

Each task was committed atomically:

1. **Task 1: RED — ClientAuthTests + IdentitySeeder Client role assert** - `dfb930a` (test)
2. **Task 2: GREEN — Client role, Register API, seeder** - `5b8fc49` (feat)
3. **Task 3: Landing auth UI + Navbar Account vs Log In** - `fc2986f` (feat)

**Plan metadata:** `5c27b35` (docs: complete plan)

## Files Created/Modified

- `API/ZachHairStudio.Shared/Features/Identity/StaffRoles.cs` — added `Client = "Client"`
- `API/ZachHairStudio.Shared/Features/Identity/IdentitySeeder.cs` — role loop includes Client
- `API/ZachHairStudio.Shared/Features/Identity/RegisterRequestDto.cs` — Email/Password/ConfirmPassword (+ optional DisplayName)
- `API/ZachHairStudio.Shared/Features/Identity/RegisterRequestDtoValidator.cs` — FluentValidation PLAT-02
- `API/ZachHairStudio.Api/Controllers/AuthController.cs` — Register action + Login unchanged
- `API/ZachHairStudio.Api.Tests/Features/Identity/ClientAuthTests.cs` — register/login/mismatch/duplicate
- `API/ZachHairStudio.Api.Tests/Features/Identity/IdentitySeederTests.cs` — Client role + empty Client users
- `landing-page/lib/auth.ts` — session helpers + registerClient/loginClient
- `landing-page/app/account/login/page.tsx` — Sign In UI
- `landing-page/app/account/register/page.tsx` — Create Account UI
- `landing-page/app/account/page.tsx` — minimal shell + Log out
- `landing-page/components/Navbar.tsx` — PersonIcon Account/Log In left of Cart

## Decisions Made

- Optional DisplayName omitted → ApplicationUser.DisplayName = email local-part before `@`
- Landing `getSession`/`setSession` require `role === "Client"` (reject/clear staff tokens)
- Dispatched `AUTH_UPDATED_EVENT` on set/clear so Navbar updates in the same tab without reload

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Navbar missing on `/account/*` pages**
- **Found during:** Task 3 (Landing auth UI)
- **Issue:** Account routes did not render Navbar (other landing pages include it per-page), so D-07 Account/Log In could not appear on auth flows
- **Fix:** Added Navbar + Footer to login, register, and account shell pages
- **Files modified:** `landing-page/app/account/login/page.tsx`, `register/page.tsx`, `page.tsx`
- **Verification:** grep Navbar on account pages; UI-READY gate
- **Committed in:** `fc2986f`

**2. [Rule 1 - Bug] Same-tab Navbar session stale after login/logout**
- **Found during:** Task 3
- **Issue:** `storage` events do not fire in the same tab; Navbar would stay on Log In after register until refresh
- **Fix:** `AUTH_UPDATED_EVENT` on setSession/clearSession; Navbar listens like cart updates
- **Files modified:** `landing-page/lib/auth.ts`, `landing-page/components/Navbar.tsx`
- **Verification:** event wiring present; human smoke still recommended
- **Committed in:** `fc2986f`

## TDD Gate Compliance

1. RED: `dfb930a` — `test(07-01): add failing Client auth + seeder Client role asserts`
2. GREEN: `5b8fc49` — `feat(07-01): add Client role, register API, and seeder`
3. No REFACTOR commit (not needed)

## Known Stubs

None that block ACCT-01/ACCT-05. Account shell intentionally has no Bookings/Orders tabs (deferred to 07-02).

## Threat Flags

None beyond plan threat model. Register is a new unauthenticated network endpoint already covered by T-07-01..T-07-03 (Identity hash, Client-only role assign, ValidationProblem on duplicates).

## Next Phase Ready

- 07-02 can build claim-by-email + Bookings/Orders history on Client JWT + ownership gates

## Self-Check: PASSED

- All key artifacts FOUND on disk
- Commits FOUND: dfb930a, 5b8fc49, fc2986f
- ClientAuthTests + IdentitySeederTests green against Docker SQL Server (`ConnectionStrings__DefaultConnection`)
