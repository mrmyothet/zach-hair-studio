---
status: complete
phase: 02-booking-core
source: [02-01-SUMMARY.md, 02-02-SUMMARY.md, 02-03-SUMMARY.md, 02-04-SUMMARY.md, 02-05-SUMMARY.md, 02-06-SUMMARY.md, 02-07-SUMMARY.md]
started: 2026-07-22T15:32:17Z
updated: 2026-07-23T03:05:00Z
mvp_mode: true
---

## Current Test
<!-- OVERWRITE each test - shows where we are -->

[testing complete]

## Tests

### 1. Cold Start Smoke Test
expected: From a killed state, `dotnet run` boots the API and applies the AddBookingCore migration against LocalDB with no error; `next dev` serves /book; the slots endpoint returns live data on first request.
result: pass
source: automated (verified by executor)
notes: |
  Verified live: API "Now listening on http://localhost:5236", Application started,
  Hosting environment: Development. Migrations applied (__EFMigrationsLock acquired
  and released in log). openapi/v1.json 200; /api/services 200 (6 seeded services);
  /api/stylists 200 (4 stylists); /api/appointments/slots returns live slots with
  correct +06:30 Asia/Yangon offsets at 15-min increments from 09:00.
  Landing page: next dev Ready in 1377ms; /, /services, /book all HTTP 200.

  DEVIATION (owner-directed): ran against **Azure SQL**
  (zachhairstudio.database.windows.net / ZachHairStudio), NOT (localdb)\MSSQLLocalDB
  as this test's text specifies. Owner explicitly chose to keep Azure SQL.

  Environment blockers cleared to reach boot (all owner-side, no code changes):
  1. Azure SQL firewall rejected client IP 27.130.42.248 (err 40615) — owner added rule.
  2. Serverless DB auto-paused (err 40613) — resumed on retry.
  3. user-secrets Jwt:SigningKey missing (OptionsValidationException) — owner restored.
  4. user-secrets Resend key — owner restored.
  NOTE: the API crashes on any DB/options failure at startup (Program.cs:131 Migrate(),
  :154 Run()) rather than degrading — worth considering as hardening (Phase 8 LAUNCH-03/04).

### 2. Pick a service on /book
expected: Navigate to /book. The progressive-reveal form shows a service picker. Selecting a service (e.g. from the seeded catalog) reveals the next step (stylist/slot selection) — no page reload, no console error.
result: pass

### 3. See real open slots
expected: After choosing a service and a date, the slot grid shows concrete open time slots that reflect stylist working hours and already-booked cells (booked/time-off cells are absent, not selectable). Slots are salon-local (Asia/Yangon) times.
result: pass
notes: |
  Verified after the owner-directed seven-day-opening change (migration
  OpenSalonEveryDay, applied 2026-07-23). API confirms 34 slots each for
  Sun 2026-07-26, Mon 2026-07-27, Tue 2026-07-28, all first slot 09:00+06:30.

### 4. Filter slots by preferred stylist
expected: Selecting a specific stylist chip re-fetches and narrows the slot grid to that stylist's availability. Clearing back to "any" restores the union across stylists.
result: pass

### 5. Confirm a booking (on-screen)
expected: Pick a slot, fill contact details, submit. An on-screen confirmation panel appears showing all five fields: service name, the concrete stylist, salon-local date, salon-local time WITH zone label, duration, and price. No refresh needed.
result: pass
notes: |
  BOOK-02 + the on-screen half of BOOK-03 confirmed by the owner against the
  running stack (Azure SQL). Confirms 02-VERIFICATION.md's SC2 finding that the
  confirmation panel is self-sufficient (all five fields present) — the D-11
  load-bearing artifact holds.

### 6. Confirmation email content
expected: The confirmation email actually arrives (Resend), AND its body carries all five fields per 02-VALIDATION.md line 85: service, stylist, salon-local time WITH zone label, duration, and price.
result: pass
notes: |
  RESOLVES the single blocking gap recorded in 02-VERIFICATION.md (dated
  2026-07-10), which reported the email carried only 2 of 5 required fields.
  That gap is now STALE — the code has since been fixed.

  Code-verified in ResendEmailService.cs (SendConfirmationAsync):
    - line 59  zoneLabel = FormatZoneLabel(appointment.StartsAt.Offset)
    - line 60  duration  = $"{service.DurationMinutes} min"
    - line 61  price     = service.Price.ToString("C0", UsCulture)
    - line 66  headline renders "{when} {zoneLabel}"
    - lines 68-72  <ul> carries Service, Stylist, When+zoneLabel (salon local
      time), Duration, Price — all five fields present.
  Owner independently confirmed receipt of the real email.

  ACTION: 02-VERIFICATION.md's `gaps` block and `status: gaps_found` are now
  out of date and should be re-verified before the phase is judged incomplete.

### 7. Double-booking is rejected
expected: Two attempts to take the same stylist/slot (or booking a slot that just got taken) result in exactly one success; the losing attempt gets a clear "slot taken" message and the form recovers (contact details preserved), not a generic crash.
result: pass
notes: |
  Owner-confirmed at the UI level (BOOK-04). Complements the automated proof
  already on record: ConcurrencyTests.TwoSimultaneousRequestsForSameSlot_
  ExactlyOne201AndOne409 fires two real concurrent HTTP POSTs against real
  SQL Server and asserts exactly one 201 + one 409 + exactly one AppointmentSlot
  row, backed by the unfiltered unique index on (StylistId, SlotStart).

### 8. [DEFERRED — technical] Backend test suite runs green
expected: With no process holding a lock on ZachHairStudio.Shared.dll, `dotnet test API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` passes (SUMMARYs report 94/94). The initial verification could NOT run this (leftover locked process), so it is unconfirmed by direct execution.
result: pass
notes: |
  FINAL: 116 passed, 0 failed, 0 skipped of 116. Suite fully green.
  (Progression this session: 112/4 -> 114/2 -> 116/0.)

  FIRST direct execution of the suite — closes 02-VERIFICATION.md's
  "Human Verification Required #3" (it could not run due to a locked process).

  Two failures found, both in Phase 3 IdentitySeederTests, both PRE-EXISTING and
  unrelated to Phase 2 or to the seven-day-opening change:
    - SeedAsync_OnFreshDatabase_CreatesBothRolesAndExactlyOneOwner
    - SeedAsync_RunTwice_IsIdempotent
  Both fail Assert.Single(ownersInRole) seeing 2 owners:
  [owner-repair@seeder-test.local, owner@seeder-test.local].

  ROOT CAUSE (diagnosed, not guessed): IdentitySeederTests uses
  IClassFixture<CustomWebApplicationFactory>, which allocates ONE InMemory
  database per test CLASS. SeedAsync_ExistingOwnerMissingRole_RepairsMembership
  (IdentitySeederTests.cs:86) creates a second Owner
  (owner-repair@seeder-test.local) in that shared DB and never removes it, so
  whenever it runs before the other two, their "exactly one Owner" assertion sees
  2. Order-dependent test-isolation defect in TEST code — no shipped product
  behavior is wrong.

  SEPARATELY FIXED IN THIS SESSION (regressions from the seven-day change, now green):
    - AppointmentsControllerSlotsTests.GetSlots_ReturnsOkWithOffsetCarrying...
    - AppointmentsControllerSlotsTests.GetSlots_StylistIdFilter_NarrowsResultSet
  Both had relied on "Sunday has no seeded working hours". They now clear the
  target day and seed only what they assert on (new ClearWorkingHoursForDayAsync
  helper), making them independent of the owner-editable placeholder schedule.
  The second also had a latent bug: it seeded a 30-min window (09:00-09:30) while
  querying serviceId=1 (Precision Cut, 45 min), so no candidate start could fit —
  widened to 09:00-10:00.

### 9. [DEFERRED — technical] SC5 DST write-path
expected: The DST-transition proof runs through the shipped write path (POST /api/appointments), OR the owner accepts SC5's DST clause as deliberately descoped for the Asia/Yangon (fixed +06:30, no DST) deployment — a documented judgment call, not a silent gap.
result: pass
resolution: descoped
notes: |
  Owner formally accepts the DESCOPE of SC5's DST-transition clause for this
  deployment. Rationale on record: Asia/Yangon has been fixed at UTC+06:30 since
  1920 and never observes DST, so "verified correct across a DST-transition date"
  cannot occur for the salon as configured. The DST-sensitive logic is confined to
  SalonTimeZone.ToSalonInstant, which IS unit-tested across gap/ambiguity cases
  (DstBoundaryTests). Consistent with the existing Phase 2 Plan 07 decision.

  This closes 02-VERIFICATION.md's "Human Verification Required #1" and its
  behavior_unverified_items entry. The DateTimeOffset half of BOOK-05 is
  separately confirmed live: slots return +06:30 offsets (Test 1/3).

## Summary

total: 9
passed: 9
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

- gap_id: G-02-8
  truth: "The backend test suite runs green (dotnet test passes)"
  status: resolved
  resolved_by: "In-session fix — IdentitySeederTests.cs now deletes the orphan Owner it creates"
  resolved_at: 2026-07-23
  verification: "dotnet test API/ZachHairStudio.Api.Tests — 116 passed, 0 failed of 116"
  reason: "Originally: 114 passed, 2 failed of 116. IdentitySeederTests saw 2 Owners where it asserts exactly 1."
  severity: minor
  test: 8
  root_cause: "IdentitySeederTests uses IClassFixture<CustomWebApplicationFactory> — one InMemory DB shared by the whole class. SeedAsync_ExistingOwnerMissingRole_RepairsMembership (IdentitySeederTests.cs:86) creates a second Owner (owner-repair@seeder-test.local) in that shared DB and never cleans it up, so when it runs before the other two tests their Assert.Single(ownersInRole) sees 2. Order-dependent test-isolation defect; no product behavior is wrong."
  artifacts:
    - path: "API/ZachHairStudio.Api.Tests/Features/Identity/IdentitySeederTests.cs"
      issue: "Line 86 creates owner-repair@seeder-test.local in the class-shared InMemory DB with no cleanup; lines 42 and 61 assert exactly one Owner."
  missing:
    - "Isolate the repair test — either delete the orphan Owner at the end of SeedAsync_ExistingOwnerMissingRole_RepairsMembership, or give each test its own database instead of a class-scoped fixture."
  scope_note: "Phase 3 (DASH-05) test code, surfaced during Phase 2 UAT. Not a Phase 2 defect and not caused by the seven-day-opening change."
  debug_session: ""
