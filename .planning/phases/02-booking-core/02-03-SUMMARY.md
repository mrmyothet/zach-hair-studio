---
phase: 02-booking-core
plan: 03
subsystem: api
tags: [ef-core, datetimeoffset, timezoneinfo, dst, aspnet-core, dependency-injection]

# Dependency graph
requires:
  - phase: 02-booking-core (plan 01)
    provides: Stylist/StylistWorkingHours/StylistTimeOff/Appointment/AppointmentSlot schema, Salon:IanaTimeZoneId config, PLAT-01 controller boundary
provides:
  - SalonTimeZone.ToSalonInstant — the single wall-clock -> DateTimeOffset conversion for the whole booking domain
  - SlotService.GetOpenSlotsAsync — DST-safe 15-minute grid open-slot query (working hours minus time off minus booked cells)
  - OpenSlotDto
  - GET /api/appointments/slots endpoint (AppointmentsController)
  - SalonOptions relocated to Shared (ZachHairStudio.Shared.Features.Availability) so Shared-project services can consume salon config directly
affects: ["02-04 (appointment write path must reuse SalonTimeZone.ToSalonInstant and SlotService.GetOpenSlotsAsync for server-side slot validation)", "02-05 (the /book slot grid UI consumes GET /api/appointments/slots)"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Single wall-clock->DateTimeOffset conversion helper (SalonTimeZone), reused everywhere instead of re-deriving offsets (Pitfall 5)"
    - "Server-evaluated EF Core queries (stylists/working hours/time off/booked cells) followed by in-memory grid math — not client-side-evaluation, since the grid math doesn't translate to SQL"
    - "IOptions<T> -> plain T singleton bridge in Program.cs so Shared-project services can depend on config POCOs directly without referencing Microsoft.Extensions.Options"

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Availability/SalonTimeZone.cs
    - API/ZachHairStudio.Shared/Features/Availability/SalonOptions.cs
    - API/ZachHairStudio.Shared/Features/Availability/OpenSlotDto.cs
    - API/ZachHairStudio.Shared/Features/Availability/SlotService.cs
    - API/ZachHairStudio.Api/Controllers/AppointmentsController.cs
    - API/ZachHairStudio.Api.Tests/Features/Availability/SlotServiceTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Availability/DstBoundaryTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerSlotsTests.cs
  modified:
    - API/ZachHairStudio.Api/Program.cs

key-decisions:
  - "Fall-back ambiguity policy: an ambiguous local time (fall-back, occurs twice) resolves deterministically to the standard-time offset — the numerically smaller of TimeZoneInfo.GetAmbiguousTimeOffsets(...). A spring-forward local time that does not exist returns null and is skipped from the candidate grid."
  - "SalonOptions relocated from Program.cs (Api project) into ZachHairStudio.Shared.Features.Availability so SlotService (Shared project) can depend on it directly, honoring the Api->Shared dependency direction (Shared must never reference Api)."
  - "SlotService takes plain SalonOptions (not IOptions<SalonOptions>) via constructor injection; Program.cs bridges IOptions<SalonOptions> to a singleton SalonOptions instance, avoiding a Microsoft.Extensions.Options package dependency in the Shared class library."
  - "Grid-cell duration math treats each 15-minute cell as an absolute-time span (DateTimeOffset.AddMinutes on the UTC instant), not a wall-clock span — this is deliberately correct across DST because AppointmentSlot.SlotStart is compared/stored as an absolute instant."
  - "Booking-horizon/lead-time bounds were not part of this plan's scope (Claude's Discretion item in 02-CONTEXT.md) — no lead-time or max-horizon filtering was added to GetOpenSlotsAsync. This is deferred to Plan 04 (appointment create validation) or later, and is not itself a stub since the plan's behavior contract (D-01/D-02/D-06/D-07) does not require it."

patterns-established:
  - "SlotService is a read-only compute service: constructor-injects BookingDbContext + SalonOptions only, no validators, mirrors the query-then-in-memory-compute shape from 02-RESEARCH.md Pattern 2."
  - "AppointmentsController constructor-injects SlotService only — never BookingDbContext (PLAT-01), same boundary as StylistsController/ServicesController."

requirements-completed: [BOOK-01, BOOK-05, BOOK-06]

coverage:
  - id: D1
    description: "GET /api/appointments/slots?serviceId=&stylistId=&date= returns open 15-minute-grid start times reflecting working hours minus time off minus already-booked cells"
    requirement: "BOOK-01"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerSlotsTests.cs#GetSlots_ReturnsOkWithOffsetCarryingStartTimesWithinWorkingHours"
        status: pass
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/SlotServiceTests.cs#GetOpenSlotsAsync_BookedCell_RemovesOverlappingCandidateStarts"
        status: pass
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/SlotServiceTests.cs#GetOpenSlotsAsync_TimeOffInterval_RemovesOverlappingCandidateStarts"
        status: pass
    human_judgment: false
  - id: D2
    description: "No stylistId returns the union of candidates across active stylists (deduped/ordered); a stylistId filters to that stylist and narrows the result set"
    requirement: "BOOK-06"
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/SlotServiceTests.cs#GetOpenSlotsAsync_NoStylistId_ReturnsUnionAcrossActiveStylists_StylistIdFiltersToOne"
        status: pass
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerSlotsTests.cs#GetSlots_StylistIdFilter_NarrowsResultSet"
        status: pass
    human_judgment: false
  - id: D3
    description: "A service needing N cells (ceil(DurationMinutes/15)) is only offered a start when all N consecutive cells are free; the Scalp Treatment's 40 minutes reserves 3 cells (45 min), not 2"
    requirement: "BOOK-01"
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/SlotServiceTests.cs#GetOpenSlotsAsync_ScalpTreatment40Minutes_Reserves3CellsNot2"
        status: pass
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/SlotServiceTests.cs#GetOpenSlotsAsync_NinetyMinuteService_Reserves6Cells"
        status: pass
    human_judgment: false
  - id: D4
    description: "SalonTimeZone.ToSalonInstant resolves DST-correct offsets on both sides of the 2026 spring-forward and fall-back transitions; a spring-forward gap time is skipped (null) and a fall-back ambiguous time resolves to the standard-time offset"
    requirement: "BOOK-05"
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/DstBoundaryTests.cs#ToSalonInstant_ResolvesCorrectOffsetAcrossDstBoundary"
        status: pass
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/DstBoundaryTests.cs#ToSalonInstant_SpringForwardGap_ReturnsNull"
        status: pass
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/DstBoundaryTests.cs#ToSalonInstant_FallBackAmbiguousTime_ResolvesToStandardOffset"
        status: pass
    human_judgment: false
  - id: D5
    description: "AppointmentsController never injects or references BookingDbContext (PLAT-01)"
    verification:
      - kind: other
        ref: "grep -n BookingDbContext API/ZachHairStudio.Api/Controllers/AppointmentsController.cs returns nothing"
        status: pass
    human_judgment: false
  - id: D6
    description: "No hardcoded salon offset literal; the offset is always resolved per-instant via TimeZoneInfo.IsInvalidTime/IsAmbiguousTime/GetUtcOffset"
    verification:
      - kind: other
        ref: "grep -n 'IsInvalidTime|IsAmbiguousTime|GetUtcOffset' API/ZachHairStudio.Shared/Features/Availability/SalonTimeZone.cs shows all three calls"
        status: pass
    human_judgment: false

duration: 17min
completed: 2026-07-09
status: complete
---

# Phase 2 Plan 3: Open-Slot Query (SlotService + SalonTimeZone) Summary

**DST-safe open-slot query — SalonTimeZone's single wall-clock->DateTimeOffset helper plus SlotService's 15-minute grid math (working hours minus time off minus booked cells) — exposed via GET /api/appointments/slots.**

## Performance

- **Duration:** 17 min
- **Started:** 2026-07-09T15:30:00Z (approx., first file reads)
- **Completed:** 2026-07-09T15:47:00Z
- **Tasks:** 2
- **Files modified:** 9 (8 created, 1 modified)

## Accomplishments
- `SalonTimeZone.ToSalonInstant(DateTime)` is now the single, centralized wall-clock -> `DateTimeOffset` conversion for the salon's configured IANA timezone — resolving `TimeZoneInfo.IsInvalidTime` (spring-forward gap -> `null`, candidate skipped) and `IsAmbiguousTime` (fall-back -> deterministic standard-time offset), never hardcoding an offset (BOOK-05).
- `SlotService.GetOpenSlotsAsync(serviceId, stylistId?, date)` computes the open 15-minute grid: server-evaluated queries for active stylists, that day's `StylistWorkingHours`, overlapping `StylistTimeOff`, and already-occupied `AppointmentSlots`, followed by in-memory grid generation using `Math.Ceiling(DurationMinutes / 15.0)` cell counts (BOOK-01, D-01, D-02, D-06).
- `GET /api/appointments/slots` (new `AppointmentsController`, GET-only in this plan) returns `OpenSlotDto[]` with offset-carrying `DateTimeOffset` start times; `AppointmentsController` constructor-injects `SlotService` only, never `BookingDbContext` (PLAT-01).
- "Any stylist" (`stylistId` omitted) returns the deduped, ordered union of candidate starts across active stylists with `StylistId`/`StylistName` left `null`; supplying `stylistId` filters to that stylist and populates the concrete `StylistId`/`StylistName` on each slot (BOOK-06, D-07).
- Relocated `SalonOptions` from `Program.cs` (Api project) into `ZachHairStudio.Shared.Features.Availability` (deviation, see below) so the Shared-project `SlotService` can consume it without an illegal Shared->Api dependency, and wired `Program.cs` to bridge `IOptions<SalonOptions>` to a plain singleton `SalonOptions` instance.

## Task Commits

Each task was committed atomically (Task 1 followed the TDD RED/GREEN gate sequence):

1. **Task 1: SalonTimeZone conversion helper + SlotService grid generation, test-first**
   - `7146866` (test) — `SlotServiceTests` + `DstBoundaryTests` added first (RED gate)
   - `ce10073` (feat) — `SalonTimeZone`, `OpenSlotDto`, `SlotService`, `SalonOptions` relocation implemented (GREEN gate)
2. **Task 2: Expose GET /api/appointments/slots and register SlotService in DI** - `8fb12ae` (feat)

_No plan-metadata commit in this worktree — the orchestrator commits STATE.md/ROADMAP.md centrally after the wave merges._

## Files Created/Modified

**Created:**
- `API/ZachHairStudio.Shared/Features/Availability/SalonTimeZone.cs` - the single DST-aware wall-clock->`DateTimeOffset` conversion helper
- `API/ZachHairStudio.Shared/Features/Availability/SalonOptions.cs` - salon config POCO (relocated from `Program.cs`)
- `API/ZachHairStudio.Shared/Features/Availability/OpenSlotDto.cs` - open-slot response shape (`StartsAt`, `StylistId?`, `StylistName?`)
- `API/ZachHairStudio.Shared/Features/Availability/SlotService.cs` - the open-slot grid query/compute service
- `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs` - `GET /api/appointments/slots`
- `API/ZachHairStudio.Api.Tests/Features/Availability/SlotServiceTests.cs` - cell-count, booked-cell, time-off, union/filter tests
- `API/ZachHairStudio.Api.Tests/Features/Availability/DstBoundaryTests.cs` - `[Theory]` over the 2026 DST transitions + gap/ambiguity edge cases
- `API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerSlotsTests.cs` - endpoint integration tests (InMemory fixture)

**Modified:**
- `API/ZachHairStudio.Api/Program.cs` - removed the inline `SalonOptions` class (now in Shared), registered `AddScoped<SlotService>()` and the `IOptions<SalonOptions>` -> `SalonOptions` singleton bridge

## Decisions Made
- **Fall-back ambiguity policy (recorded per plan's `<output>` instruction):** an ambiguous local time (occurs twice during fall-back) resolves deterministically to the **standard-time offset** — `TimeZoneInfo.GetAmbiguousTimeOffsets(...).Min()`. This is the same policy documented in `02-RESEARCH.md` Pattern 3 and is now the actual shipped behavior, proven by `DstBoundaryTests.ToSalonInstant_FallBackAmbiguousTime_ResolvesToStandardOffset`.
- **Booking-horizon / lead-time assumption:** none was applied in this plan. `GetOpenSlotsAsync` has no minimum-lead-time or maximum-booking-horizon filter — it returns every grid-valid candidate within the queried date's working hours, regardless of how far in the past or future that date is relative to "now". This matches 02-CONTEXT.md's framing of booking-horizon rules as Claude's Discretion, not yet decided; it remains open for Plan 04 (appointment create) or a later phase to add if the owner wants same-day/lead-time restrictions.
- Grid-cell duration math treats each 15-minute cell as an **absolute-time span** (`DateTimeOffset.AddMinutes` on the resolved UTC instant) rather than re-deriving a wall-clock end time through `SalonTimeZone` a second time — this is both simpler and correct, since `AppointmentSlot.SlotStart` is stored and compared as an absolute instant (`datetimeoffset(0)`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Relocated `SalonOptions` from the Api project into Shared**
- **Found during:** Task 1 (writing `SlotService`, which per the plan's action step must consume the salon IANA timezone id via `IOptions`/config)
- **Issue:** `SalonOptions` was defined inline at the bottom of `API/ZachHairStudio.Api/Program.cs` (Api project). `SlotService` lives in `ZachHairStudio.Shared` (per this plan's `files_modified`), and `ZachHairStudio.Shared` must never reference `ZachHairStudio.Api` (the dependency direction is Api -> Shared only, per `.claude/CLAUDE.md`'s architecture constraint and the codebase's existing project references). Consuming `SalonOptions` as originally defined would have required a Shared->Api reference, which does not compile in this solution's project-reference graph.
- **Fix:** Moved the `SalonOptions` class verbatim into `API/ZachHairStudio.Shared/Features/Availability/SalonOptions.cs`. `Program.cs` now references it via `using ZachHairStudio.Shared.Features.Availability;` and keeps the same `builder.Services.Configure<SalonOptions>(builder.Configuration.GetSection("Salon"))` binding. To avoid also pulling a `Microsoft.Extensions.Options` package dependency into the Shared class library (which currently has none), `SlotService` takes a plain `SalonOptions` via constructor injection, and `Program.cs` bridges `IOptions<SalonOptions>` to a singleton plain `SalonOptions` instance (`AddSingleton(sp => sp.GetRequiredService<IOptions<SalonOptions>>().Value)`).
- **Files modified:** `API/ZachHairStudio.Api/Program.cs`, `API/ZachHairStudio.Shared/Features/Availability/SalonOptions.cs` (new)
- **Verification:** `dotnet build API/ZachHairStudio.slnx` succeeds with 0 errors; `SlotService`/`AppointmentsController` resolve `SalonOptions` correctly at runtime, proven by `AppointmentsControllerSlotsTests` passing against the full DI graph.
- **Committed in:** `ce10073` (Task 1 GREEN commit)

---

**Total deviations:** 1 auto-fixed (1 blocking — architectural dependency-direction fix, not a new feature)
**Impact on plan:** Necessary for Task 1 to compile at all; no scope creep. All other plan behavior (grid math, DST handling, endpoint shape) was implemented exactly as specified.

## Issues Encountered
None — all acceptance criteria and verification commands passed on the first implementation pass (11/11 new unit tests, 2/2 new integration tests, 64/64 total non-SqlServer suite green).

## User Setup Required

None - no external service configuration required. This plan only adds server-side compute logic and a GET endpoint; no new secrets, connection strings, or dashboard configuration.

## Next Phase Readiness

- `SalonTimeZone.ToSalonInstant` is ready for Plan 04's appointment-create path to reuse for the identical wall-clock -> `DateTimeOffset` conversion — critical so a slot a client saw in the GET response maps to the exact instant the POST later writes (Pitfall 5, per this plan's `key_links`).
- `SlotService.GetOpenSlotsAsync` is ready for Plan 04's `AppointmentsService` to call for server-side revalidation of a requested slot before insert (the read side is proven; the write side's candidate-stylist retry loop against the real unique index is Plan 04 scope, run against the real-SQL-Server `SqlServerWebApplicationFactory` fixture built in Plan 02).
- `GET /api/appointments/slots` is live and ready for Plan 05's `/book` slot grid to consume.
- **Concern for the owner / next planning pass:** as noted in Plan 01's summary, the seeded `StylistWorkingHours` (Tue-Sat 09:00-18:00, identical for all 4 stylists) remains a placeholder — `SlotService` computes correctly against whatever hours exist, but the current schedule itself is not yet a real one.
- **Booking-horizon / lead-time rules** (same-day booking allowed? minimum lead time?) are still an open, undecided item per `02-CONTEXT.md`'s Claude's Discretion list — `GetOpenSlotsAsync` currently has no such filter. Flag for Plan 04 or a later owner decision.

---
*Phase: 02-booking-core*
*Completed: 2026-07-09*

## Self-Check: PASSED

- All 8 created files verified present on disk (SalonTimeZone, SalonOptions, OpenSlotDto, SlotService, AppointmentsController, 3 test files).
- All 3 task/gate commit hashes (`7146866`, `ce10073`, `8fb12ae`) verified present in `git log --oneline --all`.
