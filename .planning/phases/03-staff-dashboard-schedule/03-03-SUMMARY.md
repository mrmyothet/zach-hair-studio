---
phase: 03-staff-dashboard-schedule
plan: 03
subsystem: schedule
tags: [aspnet-core, efcore, fluentvalidation, jwt-authorize, result-pattern]

requires:
  - phase: 03-staff-dashboard-schedule
    provides: JWT bearer auth, ApplicationUser/StaffRoles, AuthController login (plans 03-01/03-02)
provides:
  - "GET /api/schedule?from=&to=&status= - staff-only appointment list for a salon-local date window, optionally filtered by AppointmentStatus"
  - "GET /api/schedule/{id} - staff-only full appointment detail including the status-audit line"
  - "PATCH /api/schedule/{id}/status - staff-only constrained status transition (Confirmed -> Completed/Cancelled/NoShow), server-enforced"
  - "AppointmentsService.ListByDateRangeAsync/GetByIdAsync/UpdateStatusAsync + AllowedTransitions map"
  - "Global JsonStringEnumConverter so AppointmentStatus round-trips as a string on request bodies, matching the existing response shape"
affects: [03-04, 04-staff-service-availability-management]

tech-stack:
  added: []
  patterns:
    - "Global System.Text.Json JsonStringEnumConverter registered via AddControllers().AddJsonOptions(...) so enum request fields (AppointmentStatusUpdateDto.NewStatus) accept the same string names the API already emits (AppointmentResponseDto.Status)."
    - "UpdateStatusAsync is the single reusable slot-release path: Cancel/NoShow call AppointmentSlots.RemoveRange(appointment.Slots) inline, matching Phase 2's cancel-frees-slots contract with no forked logic."

key-files:
  created:
    - API/ZachHairStudio.Api.Tests/Features/Appointments/ScheduleControllerTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/StatusUpdateTests.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentStatusUpdateDto.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentStatusUpdateDtoValidator.cs
    - API/ZachHairStudio.Api/Controllers/ScheduleController.cs
  modified:
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentResponseDto.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentExtensions.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs

key-decisions:
  - "Global JsonStringEnumConverter added to Program.cs's AddControllers() pipeline (Rule 1 fix) - without it, System.Text.Json rejects a client-sent string like \"Completed\" for the enum-typed AppointmentStatusUpdateDto.NewStatus, and every PATCH silently model-binds to 400 before reaching the controller body. This keeps enum input/output shape consistent: AppointmentResponseDto.Status already emits status names as strings, so accepting them as strings on the way in is the correct, non-breaking contract, not a workaround."
  - "AppointmentsService gained a SalonOptions constructor parameter (resolved to a SalonTimeZone once, mirroring SlotService) rather than re-deriving the salon timezone per call - the date-range window computation is the only place this phase needs it."
  - "ListByDateRangeAsync loads Service/Stylist via two small batched dictionary lookups after the appointment query, rather than an EF .Include() navigation join, since Appointment has no navigation properties to Service/Stylist (only StylistId int) - this mirrors CreateAsync's existing lookup style in the same file."

requirements-completed: [DASH-01, DASH-02, DASH-03, DASH-04]

coverage:
  - id: D1
    description: "GET /api/schedule?from=&to= returns appointments whose StartsAt falls in the salon-local day/week window, and excludes an appointment outside that window."
    requirement: DASH-01
    verification:
      - kind: integration
        ref: "ScheduleControllerTests.GetRange_ReturnsAppointmentsWithinWindow_ExcludesOutsideWindow"
        status: pass
    human_judgment: false
  - id: D2
    description: "GET /api/schedule/{id} returns one appointment's full detail (status-audit line null before any change); unknown id returns 404."
    requirement: DASH-02
    verification:
      - kind: integration
        ref: "ScheduleControllerTests.GetById_ReturnsFullDetailWithNullAuditFieldsBeforeAnyStatusChange, ScheduleControllerTests.GetById_UnknownId_Returns404"
        status: pass
    human_judgment: false
  - id: D3
    description: "PATCH /api/schedule/{id}/status moves Confirmed -> Completed/Cancelled/NoShow (200, audit fields populated), removes AppointmentSlot rows for Cancelled/NoShow, and rejects a transition from an already-terminal status with 400 while leaving status unchanged."
    requirement: DASH-03
    verification:
      - kind: integration
        ref: "StatusUpdateTests.PatchStatus_ConfirmedToCompleted_Returns200WithAuditFields, StatusUpdateTests.PatchStatus_ConfirmedToCancelledOrNoShow_Returns200AndRemovesSlots, StatusUpdateTests.PatchStatus_AlreadyTerminal_Returns400AndLeavesStatusUnchanged"
        status: pass
    human_judgment: false
  - id: D4
    description: "status=NoShow and status=Cancelled are independently filterable - each returns only its own status, never the other."
    requirement: DASH-04
    verification:
      - kind: integration
        ref: "StatusUpdateTests.GetRange_FilterByNoShow_ReturnsOnlyNoShow_NeverCancelled"
        status: pass
    human_judgment: false
  - id: D5
    description: "All /api/schedule endpoints (GET range, GET detail, PATCH status) reject anonymous requests with 401."
    requirement: DASH-05
    verification:
      - kind: integration
        ref: "ScheduleControllerTests.Get_Anonymous_Returns401, ScheduleControllerTests.Patch_Anonymous_Returns401"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-07-11
status: complete
---

# Phase 3 Plan 03: Staff Schedule Read + Status Update API Summary

**A staff-only `ScheduleController` (`GET /api/schedule` range/detail, `PATCH /api/schedule/{id}/status`) backed by three new `AppointmentsService` methods and a single server-enforced `AllowedTransitions` map — Cancel/NoShow free their `AppointmentSlot` rows through the exact same path, no-show and cancelled are independently queryable, and every action requires a valid staff JWT.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-11T06:40:00Z
- **Completed:** 2026-07-11T07:05:00Z
- **Tasks:** 3 completed
- **Files modified:** 10 (5 created, 5 modified)

## Accomplishments

- `AppointmentsService` gained `ListByDateRangeAsync` (salon-local day-window query, optional `AppointmentStatus` filter), `GetByIdAsync` (full detail), and `UpdateStatusAsync` (the single, server-enforced transition + slot-release path) — all returning `Result<T>`, never throwing for expected paths.
- A private static `AllowedTransitions` map is the ONLY place a status transition is decided: `Confirmed -> {Completed, Cancelled, NoShow}`, with the three terminal statuses carrying no outbound entries. `UpdateStatusAsync` re-reads the appointment's CURRENT status from the DB before checking this map, so a stale or forged client-echoed status can never force a disallowed transition.
- Cancelling or marking no-show calls `_dbContext.AppointmentSlots.RemoveRange(appointment.Slots)` inline inside `UpdateStatusAsync` — this IS the single reusable slot-release path Phase 2's cancel-frees-slots contract requires; there is no forked/duplicate removal logic anywhere else.
- `AppointmentResponseDto` now carries `StatusChangedAt`/`StatusChangedBy` (already columns on `Appointment` since plan 03-01); `AppointmentExtensions.ToDto` copies both, so the detail view always has a status-audit line once a change occurs.
- `ScheduleController` (`api/schedule`, class-level `[Authorize]`) exposes the range/detail/status-update actions using the exact `Result<T>` → `ProblemDetails` translation template `AppointmentsController` established: `IsValidationError()` → `ValidationProblem(ModelState)` (400), `IsNotFound()` → 404. The acting staff display name for the audit line is read from the JWT's `displayName` claim (falling back to `ClaimTypes.Name`), never from the request body.
- `ScheduleControllerTests` (5 tests) and `StatusUpdateTests` (5 tests) run against `SqlServerWebApplicationFactory` — real relational date-range filtering and the real `AppointmentSlot` unique index, not `InMemory` — proving DASH-01 through DASH-05 end-to-end, including the 401 gate on both GET and PATCH.
- Full solution test suite: **114/114 passed** — no regressions to public booking, concurrency/DST, or the Phase 3 auth-gate tests.

## Task Commits

Each task was committed atomically:

1. **Task 1: Failing ScheduleControllerTests + StatusUpdateTests** - `86680d9` (test)
2. **Task 2: AppointmentsService schedule queries + constrained status transitions** - `e096df3` (feat)
3. **Task 3: ScheduleController + status-update DTO** - `4a6027f` (feat)

**Plan metadata:** commit created below (docs: complete plan)

## Files Created/Modified

- `API/ZachHairStudio.Api.Tests/Features/Appointments/ScheduleControllerTests.cs` - DASH-01/02/05 integration proof (range window, detail, 401 gate) over real SQL Server
- `API/ZachHairStudio.Api.Tests/Features/Appointments/StatusUpdateTests.cs` - DASH-03/04 integration proof (transitions, slot-release, terminal-status rejection, no-show/cancelled separability)
- `API/ZachHairStudio.Shared/Features/Appointments/AppointmentStatusUpdateDto.cs` - single `NewStatus` field
- `API/ZachHairStudio.Shared/Features/Appointments/AppointmentStatusUpdateDtoValidator.cs` - `IsInEnum()` + must-not-be-Confirmed
- `API/ZachHairStudio.Api/Controllers/ScheduleController.cs` - `[Authorize]` GET range/detail + PATCH status
- `API/ZachHairStudio.Shared/Features/Appointments/AppointmentResponseDto.cs` - added `StatusChangedAt`/`StatusChangedBy`
- `API/ZachHairStudio.Shared/Features/Appointments/AppointmentExtensions.cs` - `ToDto` copies the new audit fields
- `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs` - `ListByDateRangeAsync`/`GetByIdAsync`/`UpdateStatusAsync` + `AllowedTransitions`, new `SalonOptions` constructor parameter
- `API/ZachHairStudio.Api/Program.cs` - registered a global `JsonStringEnumConverter` on the controllers' JSON options
- `API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs` - updated its direct `AppointmentsService` construction for the new `salonOptions` parameter (Rule 1, this plan's constructor change broke it)

## Decisions Made

- **Global `JsonStringEnumConverter`** registered via `builder.Services.AddControllers().AddJsonOptions(...)` in `Program.cs`. Without it, a JSON body of `{"newStatus":"Completed"}` fails System.Text.Json's default enum deserialization (which expects the underlying numeric value), and ASP.NET Core's `[ApiController]` model-binding failure short-circuits straight to a 400 before the controller action — or the validator — ever runs. Since `AppointmentResponseDto.Status` already serializes status names as strings on the way out, accepting the same string shape on the way in is the correct, symmetric contract, not a special case for this one DTO — it applies to every current and future enum-typed request field.
- **`AppointmentsService` takes `SalonOptions` (not `SalonTimeZone`) in its constructor** and builds the `SalonTimeZone` once internally — mirroring how `SlotService` is already constructed — so the DI container needs no new registration (the `SalonOptions` singleton already exists in `Program.cs` from Phase 2).
- **Service/Stylist lookups for `ListByDateRangeAsync` use two batched dictionary queries** rather than an EF `Include()` navigation, because `Appointment` stores `ServiceId`/`StylistId` as plain `int` foreign keys with no navigation properties defined — this mirrors `CreateAsync`'s existing lookup style in the same file rather than introducing a new navigation-based pattern.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Enum-typed request DTO fields fail JSON model binding without a string converter**
- **Found during:** Task 3 (`dotnet test --filter Schedule|StatusUpdate` verification — every `PATCH .../status` call returned 400 instead of 200/400-per-transition-rule)
- **Issue:** `AppointmentStatusUpdateDto.NewStatus` is `AppointmentStatus` (an enum). System.Text.Json's default behavior serializes/deserializes enums as their underlying numeric value unless a `JsonStringEnumConverter` is registered. Test/dashboard clients naturally send `{"newStatus":"Completed"}` (a string, matching the existing string-shaped `AppointmentResponseDto.Status` output) — this failed model binding and ASP.NET Core returned a generic 400 before the controller or FluentValidation ever ran, so even the "already-terminal → 400" test was accidentally green for the wrong reason.
- **Fix:** Added `options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())` to the `AddControllers().AddJsonOptions(...)` call in `Program.cs`. This is a global, additive change — it doesn't alter how any existing string-typed field (like `AppointmentResponseDto.Status`, which is a plain `string` property populated by `.ToString()`, not an enum) serializes.
- **Files modified:** `API/ZachHairStudio.Api/Program.cs`
- **Verification:** `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Schedule|FullyQualifiedName~StatusUpdate"` — 10/10 passed. Full suite `dotnet test API/ZachHairStudio.slnx` — 114/114 passed (no other test's JSON shape depends on numeric enum encoding).
- **Committed in:** `4a6027f` (Task 3)

**2. [Rule 1 - Bug] `AnyStylistAssignmentTests.BuildService` broke when `AppointmentsService`'s constructor gained a `SalonOptions` parameter**
- **Found during:** Task 2 (`dotnet build` verification step)
- **Issue:** `AppointmentsService`'s constructor now requires `SalonOptions` to build its internal `SalonTimeZone` for date-range queries (Task 2's own change). `AnyStylistAssignmentTests.BuildService` (an existing Phase 2 unit test helper, out of this plan's file list) constructs `AppointmentsService` directly and didn't pass the new argument, so the whole test project failed to compile.
- **Fix:** `BuildService` already had a local `salonOptions` variable in scope (used to build `SlotService`); passed the same instance through to the `AppointmentsService` constructor call.
- **Files modified:** `API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs`
- **Verification:** `dotnet build API/ZachHairStudio.slnx` succeeds; `AnyStylistAssignmentTests` continues to pass in the full suite run.
- **Committed in:** `e096df3` (Task 2)

---

**Total deviations:** 2 auto-fixed (1 JSON-binding bug affecting the plan's own new endpoint, 1 pre-existing-test compile break caused by this plan's constructor change)
**Impact on plan:** Both fixes were required for the plan's own acceptance criteria to pass; no scope creep, no architectural change.

## Issues Encountered

None beyond the two auto-fixed deviations above.

## User Setup Required

None. No new external service configuration, secrets, or migrations are introduced by this plan — it reuses the Identity/JWT backbone (plan 03-01) and login endpoint (plan 03-02) already wired up.

## Next Phase Readiness

- The staff schedule API surface (DASH-01 through DASH-04, reinforcing the DASH-05 gate) is complete and tested end-to-end against real SQL Server. Plan 03-04 (or a later dashboard-frontend plan) can now build the `dashboard/` Next.js day/week views and status-action UI directly against `GET /api/schedule`, `GET /api/schedule/{id}`, and `PATCH /api/schedule/{id}/status` — no further backend work is required for the schedule read/status-update slice.
- The global `JsonStringEnumConverter` now applies to every controller action in the API, not just `ScheduleController` — any future enum-typed request/response field will serialize/deserialize as its string name by default, matching the existing `AppointmentResponseDto.Status` shape. Worth keeping in mind if a future DTO deliberately wants numeric enum wire format (none currently do).
- `ListByDateRangeAsync`'s batched-dictionary Service/Stylist lookup pattern (rather than EF navigation `Include()`) is the template for any future staff-facing list endpoint over `Appointment`, since the entity has no navigation properties to `Service`/`Stylist`.

## Known Stubs

None — every endpoint is fully wired against real service-layer logic and the real database; no hardcoded/placeholder data anywhere in this plan's files.

---
*Phase: 03-staff-dashboard-schedule*
*Completed: 2026-07-11*

## Self-Check: PASSED

All 5 created files verified present on disk (`ScheduleControllerTests.cs`, `StatusUpdateTests.cs`, `AppointmentStatusUpdateDto.cs`, `AppointmentStatusUpdateDtoValidator.cs`, `ScheduleController.cs`). All 3 task commit hashes (`86680d9`, `e096df3`, `4a6027f`) confirmed present in `git log`.
