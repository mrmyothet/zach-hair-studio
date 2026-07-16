---
phase: 02-booking-core
plan: 07
subsystem: testing
tags: [xunit, ef-core, sql-server-localdb, datetimeoffset, timezone]

# Dependency graph
requires:
  - phase: 02-booking-core
    provides: The full booking-core write path (AppointmentsController, AppointmentsService, SlotService, SalonTimeZone, ResendEmailService) already shipped in earlier Phase 2 plans.
provides:
  - BookingDates test helper — single relative-to-now source for every create-path test's booking instant
  - WritePathOffsetTests — real-SQL proof the shipped create path stores the correct salon offset
  - SC5 DST-transition descope decision recorded in 02-VALIDATION.md
affects: [02-08 (human-verify pass depends on a green suite)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Relative-to-now test dates via a shared TestSupport.BookingDates helper instead of per-test absolute date literals, so create-path tests never age past the future-gated validator"

key-files:
  created:
    - API/ZachHairStudio.Api.Tests/TestSupport/BookingDates.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/WritePathOffsetTests.cs
  modified:
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/ConcurrencyTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerSlotsTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/ScheduleControllerTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/StatusUpdateTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs
    - .planning/phases/02-booking-core/02-VALIDATION.md

key-decisions:
  - "SC5's DST-transition clause is descoped for the Asia/Yangon deployment (fixed UTC+06:30, never observes DST); DstBoundaryTests, DstRoundTripTests, and the new WritePathOffsetTests remain as the standing DST/offset proofs."
  - "AnyStylistAssignmentTests was also date-bombed (it calls AppointmentsService.CreateAsync directly with a real AppointmentCreateDtoValidator, so it DOES cross the future-gated validator) — corrected the plan's original exclusion rationale for that file and repointed it to BookingDates too."

patterns-established:
  - "Test dates derive from BookingDates.NextBookableDate()/NextBookableSlot()/SlotOn(), never a hardcoded calendar literal, for any test that posts through the future-gated AppointmentCreateDtoValidator (directly or via HTTP)."

requirements-completed: [BOOK-03, BOOK-05]

coverage:
  - id: D1
    description: "Every Phase 2/3 create-path test derives its booking instant from a shared relative-to-now BookingDates helper instead of the hardcoded 2026-07-15 literal, so the suite passes regardless of the calendar date."
    requirement: "BOOK-05"
    verification:
      - kind: unit
        ref: "dotnet test API/ZachHairStudio.Api.Tests --filter \"FullyQualifiedName!~SqlServer\" (112 tests)"
        status: pass
      - kind: integration
        ref: "dotnet test API/ZachHairStudio.slnx (115 tests, includes real-SQL ConcurrencyTests/DstRoundTripTests/WritePathOffsetTests/SqlServerFixtureSmokeTests)"
        status: pass
    human_judgment: false
  - id: D2
    description: "WritePathOffsetTests proves POST /api/appointments persists Appointment.StartsAt (and its AppointmentSlot.SlotStart) at the salon's resolved offset (+06:30 for Asia/Yangon) through the shipped create path, on real SQL Server LocalDB."
    requirement: "BOOK-05"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Appointments/WritePathOffsetTests.cs#Post_ValidBooking_PersistsSalonOffsetThroughTheShippedCreatePath"
        status: pass
    human_judgment: false
  - id: D3
    description: "SC5's DST-transition clause is explicitly recorded as descoped for the Asia/Yangon deployment (no DST ever observed), naming the standing generic-DST proofs that remain."
    requirement: "BOOK-05"
    verification:
      - kind: other
        ref: ".planning/phases/02-booking-core/02-VALIDATION.md ## SC5 DST-Transition Clause — Descope Decision (Plan 02-07)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The confirmation email's five-field completeness (BOOK-03) stays regression-locked; ResendEmailBodyTests remains green and unmodified by this plan."
    requirement: "BOOK-03"
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Appointments/ResendEmailBodyTests.cs (unmodified, part of the 112-test green InMemory run)"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-07-16
status: complete
---

# Phase 02 Plan 07: Gap Closure — De-Date-Bomb Suite, Write-Path Offset Proof, SC5 Descope Summary

**Repointed every create-path test off a hardcoded 2026-07-15 literal onto a shared relative-to-now `BookingDates` helper, added a real-SQL `WritePathOffsetTests` proving the shipped create path stores the correct Asia/Yangon (+06:30) offset, and recorded SC5's DST-transition clause as a deliberate, documented descope for this deployment.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-16T14:20:00+07:00 (approx, first read)
- **Completed:** 2026-07-16T14:55:53+07:00
- **Tasks:** 3
- **Files modified:** 8 (2 created, 6 modified)

## Accomplishments
- Added `API/ZachHairStudio.Api.Tests/TestSupport/BookingDates.cs` — the single relative-to-now source for every create-path test's booking instant, resolved through `SalonTimeZone.FromOptions` (never a hardcoded offset).
- Repointed six test files (`AppointmentsControllerTests`, `ConcurrencyTests`, `AppointmentsControllerSlotsTests`, `ScheduleControllerTests`, `StatusUpdateTests`, and — as a deviation fix — `AnyStylistAssignmentTests`) off the `2026-07-15` date bomb; all now pass regardless of the calendar date.
- Added `WritePathOffsetTests` (real SQL Server LocalDB): posts through `POST /api/appointments` end-to-end and asserts the reloaded `Appointment.StartsAt.Offset` and `AppointmentSlot.SlotStart.Offset` equal the salon's resolved offset — closing the behavior_unverified SC5/BOOK-05 write-path proof gap.
- Recorded the SC5 DST-transition descope decision in `02-VALIDATION.md`, naming `DstBoundaryTests`, `DstRoundTripTests`, and `WritePathOffsetTests` as the standing proofs.
- Full solution suite (`dotnet test API/ZachHairStudio.slnx`) is green: 115/115 passing, including all real-SQL-backed classes.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add a relative-to-now booking-date helper and repoint the Phase 2 create-path tests** - `3120de2` (test)
2. **Task 2: Repoint the Phase 3 create-path tests that share the same date-bomb root cause** - `2122e62` (test)
3. **Task 3: Prove the shipped create path stores the correct salon offset, and record the SC5 DST descope decision** - `611e459` (test)

**Plan metadata:** pending (docs: complete plan, committed after this summary)

## Files Created/Modified
- `API/ZachHairStudio.Api.Tests/TestSupport/BookingDates.cs` - Static helper: `NextBookableDate()`, `NextBookableDate(int dayOffset)`, `NextBookableSlot(int hour, int minute)`, `SlotOn(DateOnly, int hour, int minute)` — all resolved through `SalonTimeZone.FromOptions`.
- `API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerTests.cs` - `Slot(hour, minute)` now delegates to `BookingDates.NextBookableSlot`.
- `API/ZachHairStudio.Api.Tests/Features/Appointments/ConcurrencyTests.cs` - `SlotInstant` now sourced from `BookingDates.NextBookableSlot(10)`.
- `API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerSlotsTests.cs` - `TestSunday` anchored to `DateTime.UtcNow` instead of a fixed `2026-07-01`.
- `API/ZachHairStudio.Api.Tests/Features/Appointments/ScheduleControllerTests.cs` - `Slot(dayOffset, hour, minute)` and `BaseDate` now derive from `BookingDates`; `from`/`to` query strings use `BaseDate:yyyy-MM-dd`.
- `API/ZachHairStudio.Api.Tests/Features/Appointments/StatusUpdateTests.cs` - Same pattern; `Slot` and `BaseDate` from `BookingDates`.
- `API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs` - Deviation fix: `BookingDate`/`SlotInstant` and the off-hours instant now sourced from `BookingDates` (this file calls `AppointmentsService.CreateAsync` directly with a real validator, so it was date-bombed too).
- `API/ZachHairStudio.Api.Tests/Features/Appointments/WritePathOffsetTests.cs` - New real-SQL test proving the shipped create path persists the correct salon offset.
- `.planning/phases/02-booking-core/02-VALIDATION.md` - New "SC5 DST-Transition Clause — Descope Decision (Plan 02-07)" subsection.

## Decisions Made
- SC5's DST-transition clause is descoped for the Asia/Yangon deployment (fixed UTC+06:30, never observes DST). DstBoundaryTests, DstRoundTripTests, and WritePathOffsetTests remain as the standing DST/offset proofs if the zone is ever reconfigured.
- `BookingDates.NextBookableDate()` anchors to today + 7 days advanced to the next Wednesday — comfortably future, inside the 60-day horizon, and a seeded Tue-Sat working day for all four stylists (no seeded `StylistTimeOff` to collide with).
- `ScheduleControllerTests`' two-different-days need is met by `BookingDates.NextBookableDate(int dayOffset)` rather than a second date source, keeping the helper the single source per the plan's guidance.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] AnyStylistAssignmentTests was also date-bombed, contrary to the plan's exclusion list**
- **Found during:** Task 3 (full-suite verification run after WritePathOffsetTests)
- **Issue:** The plan explicitly listed `AnyStylistAssignmentTests` as "not date-bombed" because it "calls the service/DbContext/helper directly and never crosses the future-gated validator." That premise was incorrect: the test builds a real `AppointmentCreateDtoValidator` and passes it into `AppointmentsService`, and `AppointmentsService.CreateAsync` runs that validator — so the same `BeInTheFuture` rule applies. Running the quick suite after Task 2 surfaced 5 failures with "StartsAt must be in the future."
- **Fix:** Repointed `BookingDate`/`SlotInstant` and the off-hours instant in `AnyStylistAssignmentTests.cs` to `BookingDates.NextBookableDate()` / `BookingDates.NextBookableSlot(...)`, same as the other files.
- **Files modified:** API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs
- **Verification:** `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName!~SqlServer"` — 112/112 passing (was 107/112 before the fix); full solution suite 115/115 passing.
- **Committed in:** 611e459 (part of Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 bug — Rule 1)
**Impact on plan:** Necessary correction to reach the plan's own stated success criterion ("Every date-bombed create-path test passes on a clock past 2026-07-15"); no scope creep beyond the plan's root-cause fix.

## Issues Encountered
None beyond the deviation above.

## User Setup Required
None - no external service configuration required. `RESEND_API_KEY` was already confirmed present in `dotnet user-secrets` per the plan's environment notes; no test in this plan calls the real Resend path (all inject a no-op or recording `IEmailService`).

## Next Phase Readiness
- The full solution suite (`dotnet test API/ZachHairStudio.slnx`) is green: 115/115 tests passing, including all real-SQL-Server-LocalDB-backed classes (`ConcurrencyTests`, `DstRoundTripTests`, `WritePathOffsetTests`, `SqlServerFixtureSmokeTests`).
- Plan 02-08 (human-verify pass) can now observe a genuinely green run rather than one blocked by calendar drift.
- No production source file was modified; `AppointmentCreateDtoValidator`'s live-`UtcNow` gate is unchanged and correct.

---
*Phase: 02-booking-core*
*Completed: 2026-07-16*

## Self-Check: PASSED

- FOUND: API/ZachHairStudio.Api.Tests/TestSupport/BookingDates.cs
- FOUND: API/ZachHairStudio.Api.Tests/Features/Appointments/WritePathOffsetTests.cs
- FOUND: .planning/phases/02-booking-core/02-07-SUMMARY.md
- FOUND commit: 3120de2
- FOUND commit: 2122e62
- FOUND commit: 611e459
