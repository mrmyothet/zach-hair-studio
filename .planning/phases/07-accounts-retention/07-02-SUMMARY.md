---
phase: 07-accounts-retention
plan: 02
subsystem: api
tags: [account, ownership, idor, claim-by-email, bookings, orders, jwt, landing-page]

requires:
  - phase: 07-accounts-retention
    provides: StaffRoles.Client, POST /api/auth/register, landing zhs.client.auth + /account shell
provides:
  - Nullable Appointment.ClientUserId with Restrict FK migration
  - AccountController Client-role bookings/orders/claim APIs (NameIdentifier ownership)
  - Claim-by-email preview + confirm/skip (D-04)
  - Landing Bookings|Orders tabs + ClaimHistoryPanel
affects: [07-03 self-service cancel/reschedule, 07-04 loyalty]

tech-stack:
  added: []
  patterns:
    - Ownership filters solely from ClaimTypes.NameIdentifier; cross-client → 404 ProblemDetails
    - Claim UPDATE only Email-match AND null FK after explicit Confirm
    - Landing account history Bearer-only fetch (no ownerId/clientId scoping)

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Account/AccountService.cs
    - API/ZachHairStudio.Shared/Features/Account/ClaimPreviewDto.cs
    - API/ZachHairStudio.Shared/Features/Account/ClaimRequestDto.cs
    - API/ZachHairStudio.Shared/Features/Account/ClaimRequestDtoValidator.cs
    - API/ZachHairStudio.Api/Controllers/AccountController.cs
    - API/ZachHairStudio.Api.Tests/Features/Account/AccountBookingsTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Account/AccountOrdersTests.cs
    - API/ZachHairStudio.Shared/Migrations/20260810101049_AddAppointmentClientUserId.cs
    - landing-page/lib/account.ts
    - landing-page/app/account/bookings/page.tsx
    - landing-page/app/account/orders/page.tsx
    - landing-page/components/ClaimHistoryPanel.tsx
    - landing-page/components/AccountShell.tsx
  modified:
    - API/ZachHairStudio.Shared/Features/Appointments/Appointment.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentResponseDto.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentExtensions.cs
    - API/ZachHairStudio.Shared/Db/BookingDbContext.cs
    - API/ZachHairStudio.Api/Program.cs
    - landing-page/app/account/page.tsx
    - landing-page/app/account/register/page.tsx

key-decisions:
  - "Appointment.ClientUserId FK OnDelete Restrict (do not cascade-delete appointments)"
  - "/account redirects to /account/bookings; tabs deep-link Bookings|Orders"
  - "Claim skip posts confirm=false (explicit no-op) then navigates to account"

patterns-established:
  - "Account API: [Authorize(Roles=Client)] + int.Parse(NameIdentifier) owner scope"
  - "IDOR: missing and cross-client detail both return identical 404 without PII"
  - "landing-page/lib/account.ts mirrors cart.ts AccountApiError + Bearer helpers"

requirements-completed: [ACCT-02, ACCT-03, ACCT-06]

coverage:
  - id: D1
    description: Client lists only own bookings via GET /api/account/bookings (date-desc)
    requirement: ACCT-02
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/AccountBookingsTests.cs#ClaimConfirmTrue_AttachesGuestBookings_ListShowsOnlyOwnedDateDesc
        status: pass
    human_judgment: false
  - id: D2
    description: Client lists only own orders via GET /api/account/orders (date-desc by PlacedAtUtc)
    requirement: ACCT-03
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/AccountOrdersTests.cs#ClaimConfirmTrue_AttachesGuestOrders_ListShowsOnlyOwnedDateDesc
        status: pass
    human_judgment: false
  - id: D3
    description: Cross-client booking/order detail returns 404 without leaking other-user PII
    requirement: ACCT-06
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/AccountBookingsTests.cs#GetBooking_CrossClient_Returns404WithoutLeakingPii
        status: pass
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/AccountOrdersTests.cs#GetOrder_CrossClient_Returns404WithoutLeakingPii
        status: pass
    human_judgment: false
  - id: D4
    description: Claim-by-email confirm attaches guest rows; skip leaves FKs null; Staff 403 / anon 401
    requirement: ACCT-06
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/AccountBookingsTests.cs#ClaimConfirmFalse_LeavesFkNull_ListEmpty
        status: pass
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/AccountBookingsTests.cs#GetBookings_StaffJwt_Returns403
        status: pass
    human_judgment: false
  - id: D5
    description: Landing Bookings|Orders tabs + ClaimHistoryPanel after register
    requirement: ACCT-02
    verification: []
    human_judgment: true
    rationale: Browser smoke for claim UX and tab navigation is not covered by API tests

duration: 8min
completed: 2026-08-10
status: complete
---

# Phase 7 Plan 02: Ownership-Gated History + Claim Summary

**Client JWT history APIs filter solely by NameIdentifier (IDOR → 404), optional claim-by-email after register, and landing Bookings|Orders tabs with date-desc lists.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-08-10T10:06:46Z
- **Completed:** 2026-08-10T10:14:30Z
- **Tasks:** 3/3
- **Files modified:** 20

## Accomplishments

- Nullable `Appointment.ClientUserId` + EF migration `AddAppointmentClientUserId` (Restrict FK)
- `AccountController` / `AccountService`: Client-role list/detail + claim-preview/claim; Staff 403; anon 401
- Landing `/account/bookings` + `/account/orders` tabs, ClaimHistoryPanel on register success path

## Task Commits

Each task was committed atomically:

1. **Task 1: RED — AccountBookingsTests + AccountOrdersTests (IDOR)** - `41268d8` (test)
2. **Task 2: GREEN — ClientUserId, AccountService, AccountController, claim** - `e768100` (feat)
3. **Task 3: Landing Bookings|Orders tabs + claim panel** - `52df7b6` (feat)

**Plan metadata:** `b24aa4d` (docs: complete plan)

## Files Created/Modified

- `Appointment.cs` / DTO / Extensions — optional `ClientUserId`
- `BookingDbContext.cs` — Restrict FK to ApplicationUser
- `Features/Account/*` — AccountService + claim DTOs/validator
- `AccountController.cs` — `api/account` Client-role routes
- `Program.cs` — `AddScoped<AccountService>()`
- `AccountBookingsTests.cs` / `AccountOrdersTests.cs` — ACCT-02/03/06 + D-04
- `landing-page/lib/account.ts` — Bearer history/claim helpers
- `AccountShell.tsx` + bookings/orders pages + ClaimHistoryPanel
- Register page wires claim panel after successful register

## Decisions Made

- Appointment ownership FK uses `OnDelete(DeleteBehavior.Restrict)` so deleting a user cannot cascade-delete appointments
- `/account` redirects to `/account/bookings` (D-06 default tab)
- Skip posts `{ confirm: false }` so the server records an explicit no-op claim path

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] AccountShell extracted for shared tabs**
- **Found during:** Task 3
- **Issue:** Plan listed page files but shared tab chrome would otherwise be duplicated across bookings/orders
- **Fix:** Added `AccountShell.tsx` (Bookings|Orders chips + logout header) used by both history pages
- **Files modified:** `landing-page/components/AccountShell.tsx`, bookings/orders pages
- **Verification:** UI-READY gate; tabs deep-link correctly
- **Committed in:** `52df7b6`

## TDD Gate Compliance

1. RED: `41268d8` — failing AccountBookingsTests + AccountOrdersTests
2. GREEN: `e768100` — Account APIs + ClientUserId (14/14 tests pass)
3. No REFACTOR commit (not needed)

## Known Stubs

None that block ACCT-02/03/06. Cancel/Reschedule controls intentionally omitted (07-03). Loyalty strip intentionally omitted (07-04).

## Threat Flags

None beyond plan threat model (T-07-06..T-07-10). New `/api/account/*` surface is Client-role gated with NameIdentifier ownership filters covered by IDOR tests.

## Next Phase Ready

- 07-03 can add ownership-gated cancel/reschedule on top of Account booking detail
- 07-04 can add LoyaltyLedger + strip above history tabs

## Self-Check: PASSED

- All key artifacts FOUND on disk
- Commits FOUND: 41268d8, e768100, 52df7b6
- AccountBookingsTests + AccountOrdersTests green against Docker SQL Server (`ConnectionStrings__DefaultConnection`)
