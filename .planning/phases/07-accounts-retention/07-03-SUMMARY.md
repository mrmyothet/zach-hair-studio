---
phase: 07-accounts-retention
plan: 03
subsystem: api
tags: [account, cancel, reschedule, ownership, appointments, transaction, jwt, landing-page]

requires:
  - phase: 07-accounts-retention
    provides: AccountController Client-role history, Appointment.ClientUserId ownership
provides:
  - CancelForClientAsync + RescheduleForClientAsync (book-new then cancel-old txn)
  - POST /api/account/bookings/{id}/cancel and /reschedule
  - AccountBookingActions Cancel confirm + Reschedule slot chips on /account/bookings
affects: [07-04 loyalty]

tech-stack:
  added: []
  patterns:
    - Client cancel reuses AllowedTransitions Confirmed→Cancelled + AppointmentSlots RemoveRange
    - Reschedule uses CreateExecutionStrategy + BeginTransactionAsync (OrdersService); never cancel-first
    - Ownership solely from ClaimTypes.NameIdentifier; cross-client → 404 ProblemDetails

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Appointments/ClientRescheduleRequestDto.cs
    - API/ZachHairStudio.Shared/Features/Appointments/ClientRescheduleRequestDtoValidator.cs
    - API/ZachHairStudio.Api.Tests/Features/Account/ClientRescheduleTests.cs
    - landing-page/components/AccountBookingActions.tsx
  modified:
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs
    - API/ZachHairStudio.Api/Controllers/AccountController.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs
    - landing-page/lib/account.ts
    - landing-page/app/account/bookings/page.tsx

key-decisions:
  - "Reschedule book-new shares TryBookNewAsync with guest CreateAsync; ClientUserId set on new row"
  - "Until-start gate compares StartsAt <= DateTimeOffset.UtcNow (same clock as create validator)"
  - "UI actions only for upcoming Confirmed; past/terminal rows view-only"

patterns-established:
  - "Account mutation Result mapping mirrors AppointmentsController (Validation/404/409)"
  - "AccountApiError.isConflict for Phase 2 taken-slot recovery on reschedule"

requirements-completed: [ACCT-04]

coverage:
  - id: D1
    description: Owning Client cancels Confirmed→Cancelled with AppointmentSlot release
    requirement: ACCT-04
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/ClientRescheduleTests.cs#Cancel_OwnerConfirmed_Returns200_CancelsAndReleasesSlots
        status: pass
    human_judgment: false
  - id: D2
    description: Owning Client reschedules via one transactional book-new then cancel-old
    requirement: ACCT-04
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/ClientRescheduleTests.cs#Reschedule_OwnerConfirmed_BooksNewThenCancelsOld
        status: pass
    human_judgment: false
  - id: D3
    description: Cross-client cancel/reschedule returns 404; Staff JWT returns 403
    requirement: ACCT-04
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/ClientRescheduleTests.cs#Cancel_NonOwner_Returns404_LeavesTargetUnchanged
        status: pass
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/ClientRescheduleTests.cs#Cancel_StaffJwt_Returns403
        status: pass
    human_judgment: false
  - id: D4
    description: Past StartsAt rejected; taken target slot returns 409 without cancelling old
    requirement: ACCT-04
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/ClientRescheduleTests.cs#Cancel_PastStartsAt_Returns400
        status: pass
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/ClientRescheduleTests.cs#Reschedule_TargetSlotTaken_Returns409_KeepsOriginalConfirmed
        status: pass
    human_judgment: false
  - id: D5
    description: Account Bookings UI Cancel confirm + Reschedule slot flow on upcoming Confirmed
    requirement: ACCT-04
    verification: []
    human_judgment: true
    rationale: Browser smoke for confirm panel, slot chips, and 409 recovery copy is not covered by API tests

duration: 12min
completed: 2026-08-10
status: complete
---

# Phase 7 Plan 03: Self-Service Cancel + Reschedule Summary

**Ownership-gated client cancel (Confirmed→Cancelled + slot release) and transactional book-new-then-cancel-old reschedule with Account Bookings UI.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-08-10T10:15:52Z
- **Completed:** 2026-08-10T10:27:30Z
- **Tasks:** 3/3
- **Files modified:** 9

## Accomplishments

- `CancelForClientAsync` / `RescheduleForClientAsync` with D-09–D-12 gates (ownership, Confirmed, until-start)
- Reschedule uses `CreateExecutionStrategy` + `BeginTransactionAsync`; 409 leaves old Confirmed
- Landing `/account/bookings` Cancel confirm + Reschedule slot chips for upcoming Confirmed only

## Task Commits

Each task was committed atomically:

1. **Task 1: RED — ClientRescheduleTests** - `ed20708` (test)
2. **Task 2: GREEN — Cancel/Reschedule API** - `acec6f5` (feat)
3. **Task 3: Account Bookings UI** - `d7b8a33` (feat)

**Plan metadata:** (pending docs commit)

## Files Created/Modified

- `ClientRescheduleRequestDto(+Validator).cs` — StartsAt + optional StylistId (PLAT-02)
- `AppointmentsService.cs` — CancelForClient / RescheduleForClient / shared TryBookNewAsync
- `AccountController.cs` — POST cancel + reschedule (Client role, NameIdentifier ownership)
- `ClientRescheduleTests.cs` — ACCT-04 / D-09–D-12 integration coverage (SqlServer)
- `AccountBookingActions.tsx` + `account.ts` + bookings page — UI-SPEC Cancel/Reschedule

## Decisions Made

- Shared `TryBookNewAsync` for guest create and client reschedule (single open-slot + unique-index path)
- Until-start uses `StartsAt <= UtcNow` consistent with create validator clock
- UI mounts actions only when `isUpcomingConfirmed`; server remains authoritative

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] AppointmentsService ctor broke unit test BuildService**
- **Found during:** Task 2
- **Issue:** Added `IValidator<ClientRescheduleRequestDto>` parameter; `AnyStylistAssignmentTests.BuildService` failed to compile
- **Fix:** Pass `new ClientRescheduleRequestDtoValidator()` into the manual construction
- **Files modified:** `API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs`
- **Committed in:** `acec6f5`

**2. [Rule 1 - Bug] Shared-fixture unique-index collisions in ClientRescheduleTests**
- **Found during:** Task 2 (GREEN run)
- **Issue:** Multiple tests reused `(StylistId, SlotStart)` cells; later seeds failed after earlier tests left Confirmed slots
- **Fix:** Assigned distinct stylist/hour pairs per scenario (and kept past-start jitter)
- **Files modified:** `ClientRescheduleTests.cs`
- **Committed in:** `acec6f5`

**3. [Rule 2 - Correctness] RED NonOwner 404 asserted ProblemDetails title**
- **Found during:** Task 1
- **Issue:** Missing routes also return 404, so NonOwner tests passed before implementation
- **Fix:** Require body contains `Appointment not found` (ownership-gate ProblemDetails)
- **Files modified:** `ClientRescheduleTests.cs`
- **Committed in:** `ed20708` / refined in `acec6f5`

## TDD Gate Compliance

- RED: `ed20708` test(07-03)
- GREEN: `acec6f5` feat(07-03)
- No refactor commit required

## Known Stubs

None — cancel/reschedule helpers and UI are wired to live account endpoints.

## Threat Flags

None beyond plan threat model (T-07-10–T-07-15 mitigated by ownership, Client role, until-start, and reschedule transaction).

## Self-Check: PASSED

- All key artifacts present on disk
- Commits `ed20708`, `acec6f5`, `d7b8a33` present in git log
- `ClientRescheduleTests`: 9/9 passed
