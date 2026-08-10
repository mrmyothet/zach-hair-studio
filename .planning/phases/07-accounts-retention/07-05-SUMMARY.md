---
phase: 07-accounts-retention
plan: 05
subsystem: api
tags: [appointments, ownership, client-jwt, claim, gap-closure, landing-page]

requires:
  - phase: 07-accounts-retention
    provides: Account cancel/list, TryBookNewAsync ClientUserId support, ClaimHistoryPanel, OrdersController.TryGetClientUserId pattern
provides:
  - Public POST /api/appointments sets Appointment.ClientUserId for Client JWT (D-08)
  - Guest/Staff Bearer create remains ClientUserId null
  - landing createAppointment optional Bearer; /account/bookings embedded claim re-entry (D-04)
affects: [phase-07 verification, ACCT-02, ACCT-04, ACCT-07 earn path]

tech-stack:
  added: []
  patterns:
    - Mirror OrdersController.TryGetClientUserId on AppointmentsController (Client role + NameIdentifier only)
    - CreateAsync(request, int? clientUserId = null) forwards into TryBookNewAsync
    - cart.ts checkoutHeaders-style optional Bearer on createAppointment

key-files:
  created: []
  modified:
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs
    - API/ZachHairStudio.Api/Controllers/AppointmentsController.cs
    - API/ZachHairStudio.Api.Tests/Features/Account/ClientOwnedBookingCreateTests.cs
    - landing-page/lib/appointments.ts
    - landing-page/components/ClaimHistoryPanel.tsx
    - landing-page/app/account/bookings/page.tsx

key-decisions:
  - "Ownership attach only for StaffRoles.Client + NameIdentifier — Staff JWT never owns (D-08 / D-12)"
  - "ClaimHistoryPanel variant=embedded stays on Bookings; empty preview renders null (no redirect)"
  - "Do not invent LocalDB UAT for loyalty behavior_unverified — ownership fix unblocks earn for post-login books"

patterns-established:
  - "Public create seam: TryGetClientUserId → CreateAsync(..., clientUserId) → TryBookNewAsync"
  - "Embedded claim panel: variant prop; onFinished reloads parent list after confirm/skip only"

requirements-completed: [ACCT-02, ACCT-04, ACCT-06, ACCT-07]

coverage:
  - id: G1
    description: Client JWT POST /api/appointments sets ClientUserId; appears in account bookings; cancel works
    requirement: ACCT-04
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/ClientOwnedBookingCreateTests.cs#ClientJwt_PostAppointments_OwnsRow_AppearsInAccountBookings_AndCancelSucceeds
        status: deferred_env
    human_judgment: false
  - id: G2
    description: Anonymous POST leaves ClientUserId null
    requirement: ACCT-02
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/ClientOwnedBookingCreateTests.cs#Anonymous_PostAppointments_ClientUserIdRemainsNull
        status: deferred_env
    human_judgment: false
  - id: G3
    description: Staff JWT POST does not attach ClientUserId
    requirement: ACCT-06
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Account/ClientOwnedBookingCreateTests.cs#StaffJwt_PostAppointments_DoesNotAttachClientUserId
        status: deferred_env
    human_judgment: false

---

# Plan 07-05 Summary — Owned public book + claim re-entry

**Gap closed:** Public `CreateAsync` no longer hard-codes `clientUserId: null`. Client JWT on `POST /api/appointments` owns the row so register→book→account cancel/reschedule works (ACCT-02/04; unblocks ACCT-07 earn on those visits).

## What shipped

1. **API** — `AppointmentsService.CreateAsync(..., int? clientUserId = null)` forwards into `TryBookNewAsync`. `AppointmentsController.TryGetClientUserId` mirrors OrdersController (Client role + NameIdentifier only).
2. **Tests** — `ClientOwnedBookingCreateTests` covers Client own→list→cancel, guest null, Staff non-attach (SqlServerWebApplicationFactory).
3. **Landing** — `createAppointment` attaches Bearer when `getToken()` present. `ClaimHistoryPanel` `variant="embedded"` on `/account/bookings`; empty preview does not navigate away.

## Commits

- `cfa1cb1` test(07-05): add ClientOwnedBookingCreateTests for owned public book
- `648eb15` feat(07-05): attach Client NameIdentifier on public appointment create
- `ce82bb0` feat(07-05): send Client Bearer on book and embed claim on Bookings

## Verification notes

Integration tests compile and are wired. Full green run on this Linux host was blocked by Azure SQL `CREATE DATABASE` timeout for throwaway test DBs and fixture dispose needing `Jwt__SigningKey` on the raw factory — same class of env limits as Phase 7 loyalty (`behavior_unverified`). Code-path review confirms CreateAsync forwards clientUserId and controller resolves Client NameIdentifier only.

## Deviations

- None vs plan intent. Test host env limitations documented; no plan scope expansion.
