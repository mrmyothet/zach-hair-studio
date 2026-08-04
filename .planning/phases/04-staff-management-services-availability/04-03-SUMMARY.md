---
phase: 04-staff-management-services-availability
plan: 03
subsystem: api
tags: [dotnet, aspnetcore, efcore, fluentvalidation, availability, slots]

requires:
  - phase: 04-staff-management-services-availability
    provides: "04-01 Owner-gated Services write path + StaffRoles/JWT auth patterns"
provides:
  - "PUT /api/availability/{stylistId}/working-hours — whole-week replace (delete-then-insert) against StylistWorkingHours"
  - "POST /api/availability/{stylistId}/time-off and DELETE /api/availability/{stylistId}/time-off/{timeOffId} against StylistTimeOff"
  - "AvailabilityService writing only to the tables SlotService.GetOpenSlotsAsync reads (D-08 same-model proof, test-enforced)"
  - "AvailabilityController: any-authenticated-staff class-level [Authorize], no Owner gate, no per-stylist ownership check (D-13)"
affects: [04-04, 04-05, dashboard-availability-page]

tech-stack:
  added: []
  patterns:
    - "Whole-week replace via single implicit-transaction SaveChangesAsync (delete existing + insert new), mirroring AppointmentsService's EnableRetryOnFailure-safe no-manual-transaction pattern"
    - "Availability write tests assert against GET /api/appointments/slots output, not just write-endpoint status codes, to prove same-model reflection (D-08)"

key-files:
  created:
    - API/ZachHairStudio.Api.Tests/Features/Availability/WorkingHoursReplaceTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Availability/TimeOffTests.cs
    - API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDto.cs
    - API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDtoValidator.cs
    - API/ZachHairStudio.Shared/Features/Availability/TimeOffCreateDto.cs
    - API/ZachHairStudio.Shared/Features/Availability/TimeOffCreateDtoValidator.cs
    - API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs
    - API/ZachHairStudio.Api/Controllers/AvailabilityController.cs
  modified:
    - API/ZachHairStudio.Api/Program.cs

key-decisions:
  - "PUT working-hours returns 204 NoContent (replace semantics, matches ServicesController.UpdateService); POST time-off returns 201 Created with a Location header built from a string URI (not CreatedAtAction) since there is no corresponding GET action to resolve against; DELETE time-off returns 204 NoContent."
  - "Test suites seed a dedicated 15-minute-duration Service per test (not the seeded 45/90/120min catalog) so grid-cell math is exact (cellsNeeded == 1) for adjacency/gap/empty/idempotency assertions."
  - "RED-phase test files use anonymous request objects (not WorkingHoursReplaceDto/TimeOffCreateDto, which don't exist until Task 2) so the test files compile standalone before AvailabilityService/AvailabilityController exist — mirrors the Phase 3 AuthGateTests precedent."

patterns-established:
  - "Availability write path (any-staff, no per-resource ownership check) is the second any-staff class-level [Authorize] controller after ScheduleController — distinct from ServicesController's action-level Owner-only gate."

requirements-completed: [MGMT-02]

coverage:
  - id: D1
    description: "Any authenticated staff can PUT a stylist's whole week of working hours; SlotService.GetOpenSlotsAsync reflects the new window immediately (D-08 same-model proof), including adjacency (touching segments -> contiguous slots), gap-as-break (no slots in a gap), empty week (all days closed), and idempotent resubmission (no duplicate rows)."
    requirement: "MGMT-02"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/WorkingHoursReplaceTests.cs"
        status: pass
    human_judgment: false
  - id: D2
    description: "Any authenticated staff can POST a one-off time-off block for a stylist and DELETE it; SlotService blocks the corresponding slots while the time off exists and unblocks them on delete."
    requirement: "MGMT-02"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/TimeOffTests.cs"
        status: pass
    human_judgment: false
  - id: D3
    description: "Availability write endpoints require authentication (anonymous -> 401) and are open to any staff role, not Owner-gated (D-13)."
    requirement: "MGMT-02"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/WorkingHoursReplaceTests.cs#Put_Anonymous_Returns401, TimeOffTests.cs#Post_Anonymous_Returns401"
        status: pass
    human_judgment: false

duration: 30min
completed: 2026-07-25
status: complete
---

# Phase 4 Plan 03: Availability Write Path (Working Hours + Time Off) Summary

**Any-staff PUT/POST/DELETE against StylistWorkingHours/StylistTimeOff, proven by asserting GET /api/appointments/slots reflects each write through the same SlotService grid (D-08).**

## Performance

- **Duration:** ~30 min
- **Tasks:** 3 completed (RED test, GREEN service, controller)
- **Files modified:** 9 (2 test files created, 5 new Shared feature files, 1 controller created, 1 Program.cs edit)

## Accomplishments

- `AvailabilityService.ReplaceWorkingHoursAsync` performs a whole-week delete-then-insert against `StylistWorkingHours` in one atomic `SaveChangesAsync` (no manual transaction, matching the `EnableRetryOnFailure` constraint already documented for `AppointmentsService`).
- `AvailabilityService.AddTimeOffAsync` / `RemoveTimeOffAsync` write directly to `StylistTimeOff`.
- `AvailabilityController` gates all three actions behind `[Authorize]` at the class level with no Owner role and no per-stylist ownership check (D-13) — mirroring `ScheduleController`'s any-staff gate, not `ServicesController`'s Owner-only gate.
- 9 new integration tests prove reflection through the real `GET /api/appointments/slots` endpoint (the same `SlotService` the public booking flow uses), not just write-endpoint status codes — covering anonymous 401, adjacency (touching segments), gap-as-break, empty-week, idempotent resubmission, and time-off block/unblock.
- No new table, entity, or DbSet was introduced — the plan's D-08 prohibition ("never write to a table the open-slot query does not read") is structurally satisfied and test-enforced.

## Task Commits

Each task was committed atomically:

1. **Task 1: RED — working-hours replace + time-off tests proving SlotService reflection** - `23c4285` (test)
2. **Task 2: GREEN — DTOs, validators, AvailabilityService (no conflict check yet)** - `cbeb84f` (feat)
3. **Task 3: AvailabilityController (any-staff gate) + Result translation** - `00bf2e8` (feat)

**Plan metadata:** (this commit)

## Files Created/Modified

- `API/ZachHairStudio.Api.Tests/Features/Availability/WorkingHoursReplaceTests.cs` - 6 tests: anonymous 401, replace narrows slots, touching-segment adjacency, gap-as-break, empty-week, idempotent resubmission (row-count assertion via direct DbContext query)
- `API/ZachHairStudio.Api.Tests/Features/Availability/TimeOffTests.cs` - 3 tests: anonymous 401, post-blocks/delete-restores slots, delete-unknown-404
- `API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDto.cs` - `WorkingHoursReplaceDto` (Segments list) + `WorkingHoursSegmentDto` (DayOfWeek/StartTime/EndTime)
- `API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDtoValidator.cs` - per-segment `ChildRules`: EndTime>StartTime, 15-minute grid alignment on both
- `API/ZachHairStudio.Shared/Features/Availability/TimeOffCreateDto.cs` - StartsAt/EndsAt/Reason
- `API/ZachHairStudio.Shared/Features/Availability/TimeOffCreateDtoValidator.cs` - EndsAt>StartsAt, Reason max 200
- `API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs` - `ReplaceWorkingHoursAsync`, `AddTimeOffAsync`, `RemoveTimeOffAsync`
- `API/ZachHairStudio.Api/Controllers/AvailabilityController.cs` - PUT working-hours (204), POST time-off (201), DELETE time-off (204)
- `API/ZachHairStudio.Api/Program.cs` - `AvailabilityService` registered scoped, next to `SlotService`

## Decisions Made

- PUT working-hours returns 204 NoContent (replace semantics, matches `ServicesController.UpdateService`'s NoContent pattern).
- POST time-off returns 201 Created via `Created($"/api/availability/{stylistId}/time-off/{id}", data)` — a string-URI `Created(...)`, not `CreatedAtAction`, since there is no `GET` action for a single time-off row to resolve the route against.
- Test suites seed a dedicated 15-minute-duration `Service` row per test (distinct random slug, `Id` outside the seeded 1-6 range) instead of reusing the seeded catalog's 45/90/120-minute services, so `cellsNeeded == 1` and every grid-boundary assertion (adjacency, gap, empty, idempotency) is exact rather than approximated.
- RED-phase tests (Task 1) use anonymous C# objects for request bodies (typed `TimeOnly`/`DayOfWeek` values, not hand-formatted strings) so client-side `System.Text.Json` serialization round-trips correctly against the server's built-in `TimeOnly` converter and global `JsonStringEnumConverter`, without depending on the not-yet-existing `WorkingHoursReplaceDto`/`TimeOffCreateDto` types.

## Deviations from Plan

None - plan executed exactly as written. The plan's `<assumption_delta_decision>` (no-change: this is the existing single availability model made staff-editable, not a second store) held throughout; no new entity/table was introduced and both services write exclusively to `StylistWorkingHours`/`StylistTimeOff`.

## Issues Encountered

None. Baseline was 129/129 tests green; full suite after this plan is 138/138 green (129 baseline + 9 new Availability tests, 0 regressions).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The availability write path is ready for Plan 04/05 to build the dashboard `Availability` page against (`useAvailability` hook, week-strip editor, time-off calendar per 04-PATTERNS.md).
- Plan 05 (MGMT-03) still needs to add the conflict check against Confirmed appointments before shrinking/removing hours or adding time off — deliberately deferred per this plan's scope ("no conflict check yet").
- The plan's `prohibitions` entry (never write to a table the open-slot query doesn't read) is verified: `AvailabilityService` touches only `StylistWorkingHours`/`StylistTimeOff`, and every test proves the change through the real `GET /api/appointments/slots` → `SlotService` path rather than mocking it.

---
*Phase: 04-staff-management-services-availability*
*Completed: 2026-07-25*

## Self-Check: PASSED

All 9 files created/modified by this plan were found on disk, and all 3 task commits
(23c4285, cbeb84f, 00bf2e8) were found in git history.
