---
phase: 02-booking-core
plan: 01
subsystem: database
tags: [ef-core, sql-server, migrations, aspnet-core, dtos]

# Dependency graph
requires:
  - phase: 01-service-catalog
    provides: BookingDbContext, Service entity/DTO/controller pattern (feature-folder shape to mirror), PLAT-01 controller-never-touches-DbContext boundary
provides:
  - Stylist, StylistWorkingHours, StylistTimeOff, Appointment, AppointmentSlot entities
  - StylistResponseDto, StylistExtensions.ToDto mapper
  - StylistsService, StylistsController (GET /api/stylists)
  - AppointmentSlots unfiltered UNIQUE(StylistId, SlotStart) index — the DB-level double-booking guarantee
  - Salon:IanaTimeZoneId config + SalonOptions class
  - AddBookingCore migration (drops Bookings, creates 5 new tables, seeds 4 Stylists + 20 StylistWorkingHours rows)
affects: [02-02 (slot query), 02-03/02-04 (appointment write + retry loop), 02-05 (stylist picker UI)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Feature-folder POCO+DTO+Extensions mirrors Services exactly (Stylist/StylistResponseDto/StylistExtensions)"
    - "EF Core HasData for reference/seed data (Stylist, StylistWorkingHours) — no UseSeeding/UseAsyncSeeding"
    - "Unfiltered composite unique index as the DB-level concurrency guarantee (no app-level locking)"
    - "Options-pattern config binding (SalonOptions) instead of magic-string IConfiguration reads"

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Stylists/Stylist.cs
    - API/ZachHairStudio.Shared/Features/Stylists/StylistResponseDto.cs
    - API/ZachHairStudio.Shared/Features/Stylists/StylistExtensions.cs
    - API/ZachHairStudio.Shared/Features/Stylists/StylistsService.cs
    - API/ZachHairStudio.Api/Controllers/StylistsController.cs
    - API/ZachHairStudio.Shared/Features/Availability/StylistWorkingHours.cs
    - API/ZachHairStudio.Shared/Features/Availability/StylistTimeOff.cs
    - API/ZachHairStudio.Shared/Features/Appointments/Appointment.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentSlot.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentStatus.cs
    - API/ZachHairStudio.Api.Tests/Features/Stylists/StylistsControllerTests.cs
    - API/ZachHairStudio.Shared/Migrations/20260709144653_AddBookingCore.cs
  modified:
    - API/ZachHairStudio.Shared/Db/BookingDbContext.cs
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Api/appsettings.json
    - API/ZachHairStudio.Api/appsettings.Development.json

key-decisions:
  - "AppointmentSlots unique index has NO HasFilter predicate — the unfiltered index is the SC4/BOOK-04 double-booking guarantee at the DB level (D-03, D-04)."
  - "EF Core's HasIndex().IsUnique() emits CREATE UNIQUE INDEX, which raises SQL Server error 2601 on violation, not 2627 — corrects CONTEXT.md D-03. Plan 04's retry/conflict handling must catch both defensively."
  - "Booking entity and every consumer (BookingsController, Admin BookingController + views + nav link) retired wholesale in this plan per D-14, not half-migrated."
  - "Salon timezone bound via a SalonOptions class (IOptions<SalonOptions>) instead of raw IConfiguration reads, so later plans (SlotService, AppointmentsService) get a typed DI dependency."
  - "Full retirement of the ZachHairStudio.Admin MVC scaffold remains Phase 8 scope — only the Booking-coupled files were deleted here to keep the solution building."

patterns-established:
  - "Read-only feature slice (Stylists): Service constructor-injects BookingDbContext only, Controller constructor-injects the Service only (never BookingDbContext) — the PLAT-01 boundary."
  - "New entity + HasData seed pattern for owner-editable reference content (Stylist mirrors Service's HasData block)."

requirements-completed: [BOOK-04, BOOK-05, BOOK-06]

coverage:
  - id: D1
    description: "GET /api/stylists returns the 4 seeded active stylists ordered by DisplayOrder"
    requirement: "BOOK-06"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Stylists/StylistsControllerTests.cs#GetStylists_ReturnsOkWithSeededStylistsOrderedByDisplayOrder"
        status: pass
    human_judgment: false
  - id: D2
    description: "StylistsController never injects BookingDbContext (PLAT-01)"
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Stylists/StylistsControllerTests.cs#StylistsController_DoesNotDependOnBookingDbContext"
        status: pass
    human_judgment: false
  - id: D3
    description: "AppointmentSlots carries an unfiltered UNIQUE(StylistId, SlotStart) index over a datetimeoffset(0) column — the DB-level double-booking guarantee (D-03, D-04, BOOK-04)"
    requirement: "BOOK-04"
    verification:
      - kind: other
        ref: "grep of BookingDbContext.cs and the AddBookingCore migration for HasIndex/CreateIndex with unique:true and zero filter: occurrences"
        status: pass
    human_judgment: false
  - id: D4
    description: "Appointment.StartsAt and all availability instants are DateTimeOffset, not DateTime (BOOK-05, D-16)"
    requirement: "BOOK-05"
    verification:
      - kind: other
        ref: "grep of Appointment.cs/AppointmentSlot.cs/StylistTimeOff.cs for DateTimeOffset declarations; migration columns typed datetimeoffset/datetimeoffset(0)"
        status: pass
    human_judgment: false
  - id: D5
    description: "The legacy Booking entity, BookingsController, and Admin BookingController are retired wholesale with no residue (D-14)"
    verification:
      - kind: other
        ref: "grep -rn 'class Booking\\b|BookingsController|Admin.Controllers.BookingController' API/ returns nothing"
        status: pass
    human_judgment: false
  - id: D6
    description: "AddBookingCore migration applies cleanly to (localdb)\\MSSQLLocalDB and the solution still builds"
    verification:
      - kind: integration
        ref: "dotnet ef database update (exit 0) followed by dotnet build API/ZachHairStudio.slnx (0 warnings, 0 errors)"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-07-09
status: complete
---

# Phase 2 Plan 1: Booking Core Data Model Summary

**Booking domain schema (Stylist/Availability/Appointment/AppointmentSlot) with an unfiltered DB-level UNIQUE(StylistId, SlotStart) index, a seeded stylist-list read endpoint, and wholesale retirement of the legacy free-text Booking entity via the AddBookingCore EF Core migration.**

## Performance

- **Duration:** 35 min
- **Started:** 2026-07-09T21:15:34+07:00 (first task commit)
- **Completed:** 2026-07-09T21:50:45+07:00
- **Tasks:** 3
- **Files modified:** 20 (12 created, 5 modified, 8 deleted — see Task Commits)

## Accomplishments
- Created the five new booking-domain entities (`Stylist`, `StylistWorkingHours`, `StylistTimeOff`, `Appointment`, `AppointmentSlot`) plus the `AppointmentStatus` enum, all using `DateTimeOffset` for every instant (BOOK-05, D-16).
- Wired `BookingDbContext` with the five new `DbSet`s, an **unfiltered** composite `UNIQUE(StylistId, SlotStart)` index on `AppointmentSlots` (the SC4/BOOK-04 double-booking guarantee), a `datetimeoffset(0)` column type on `SlotStart`, and `HasData` seeding for 4 stylists + a Tue–Sat 09:00–18:00 default weekly schedule per stylist.
- Shipped a thin, read-only `GET /api/stylists` endpoint: `StylistsService` (injects `BookingDbContext` only) → `StylistsController` (injects `StylistsService` only, never `BookingDbContext` — PLAT-01), returning the 4 seeded stylists ordered by `DisplayOrder`.
- Bound `Salon:IanaTimeZoneId` (`America/New_York`) into a typed `SalonOptions` class registered via `IOptions<SalonOptions>` so later plans (slot generation, DST handling) have a single config source.
- Retired the legacy `Booking` domain wholesale (D-14): deleted `Features/Bookings/*` (5 files), `BookingsController`, the Admin `BookingController` + its two views, and the "View Bookings" nav link — the solution still builds green.
- Generated and applied the `AddBookingCore` migration to `(localdb)\MSSQLLocalDB`: drops `Bookings`, creates the five new tables, and seeds 4 `Stylists` + 20 `StylistWorkingHours` rows via `InsertData`.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the booking domain entities and the Stylist read DTO/mapper** - `59f2081` (feat)
2. **Task 2: Wire the DbContext, StylistsService/Controller + DI + Salon config, retire legacy Booking path** - `83182b6` (feat)
3. **Task 3 [BLOCKING]: Add and apply the AddBookingCore EF Core migration** - `45e4747` (feat)

_No plan-metadata commit in this worktree — the orchestrator commits STATE.md/ROADMAP.md centrally after the wave merges._

## Files Created/Modified

**Created:**
- `API/ZachHairStudio.Shared/Features/Stylists/Stylist.cs` - Stylist entity (Id, Slug, Name, IsActive, DisplayOrder)
- `API/ZachHairStudio.Shared/Features/Stylists/StylistResponseDto.cs` - Read DTO (no IsActive)
- `API/ZachHairStudio.Shared/Features/Stylists/StylistExtensions.cs` - `.ToDto()` mapper
- `API/ZachHairStudio.Shared/Features/Stylists/StylistsService.cs` - `GetActiveStylistsAsync()` read service
- `API/ZachHairStudio.Api/Controllers/StylistsController.cs` - `GET /api/stylists`
- `API/ZachHairStudio.Shared/Features/Availability/StylistWorkingHours.cs` - recurring weekly hours (D-06)
- `API/ZachHairStudio.Shared/Features/Availability/StylistTimeOff.cs` - time-off exceptions (D-06)
- `API/ZachHairStudio.Shared/Features/Appointments/Appointment.cs` - core appointment entity
- `API/ZachHairStudio.Shared/Features/Appointments/AppointmentSlot.cs` - one row per occupied 15-min grid cell
- `API/ZachHairStudio.Shared/Features/Appointments/AppointmentStatus.cs` - Confirmed/Cancelled/Completed/NoShow enum
- `API/ZachHairStudio.Api.Tests/Features/Stylists/StylistsControllerTests.cs` - GET 200 + PLAT-01 reflection test
- `API/ZachHairStudio.Shared/Migrations/20260709144653_AddBookingCore.cs` (+ `.Designer.cs`) - the schema-push migration

**Modified:**
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` - removed `Booking` DbSet/config, added 5 new DbSets, unfiltered unique index, HasData seeds
- `API/ZachHairStudio.Api/Program.cs` - registered `StylistsService`, bound `SalonOptions` from `Salon` config section
- `API/ZachHairStudio.Api/appsettings.json`, `appsettings.Development.json` - added `Salon:IanaTimeZoneId` = `America/New_York` (no secrets)
- `API/ZachHairStudio.Shared/Migrations/BookingDbContextModelSnapshot.cs` - regenerated by EF tooling

**Deleted (D-14, wholesale Booking retirement):**
- `API/ZachHairStudio.Shared/Features/Bookings/Booking.cs`, `BookingCreateDto.cs`, `BookingResponseDto.cs`, `BookingExtensions.cs`, `BookingStatus.cs`
- `API/ZachHairStudio.Api/Controllers/BookingsController.cs`
- `API/ZachHairStudio.Admin/Controllers/BookingController.cs`, `Views/Booking/Index.cshtml`, `Views/Booking/Details.cshtml`
- "View Bookings" anchor removed from `API/ZachHairStudio.Admin/Views/Home/Index.cshtml`

## Decisions Made
- The `AppointmentSlots` unique index carries **no** `HasFilter` predicate — this unfiltered index IS the SC4/BOOK-04 double-booking guarantee at the database level, exactly as CONTEXT.md D-03/D-04 require.
- **Correction to CONTEXT.md D-03:** EF Core's `HasIndex().IsUnique()` on SQL Server emits `CREATE UNIQUE INDEX`, which raises SQL error **2601** on violation, not 2627 (2627 is for `PRIMARY KEY`/`UNIQUE CONSTRAINT` objects specifically). Plan 04's retry/conflict-catch logic should defensively catch both 2601 and 2627, but neither should be added to `EnableRetryOnFailure`'s `errorNumbersToAdd` — they are data-integrity violations, not transient faults.
- Salon timezone config is bound through a typed `SalonOptions` class (`IOptions<SalonOptions>`) rather than raw `IConfiguration["Salon:IanaTimeZoneId"]` string lookups, giving later plans (SlotService's DST-aware grid math) a clean DI-injectable dependency.
- Retired the Admin `BookingController` + views + nav link as a required consequence of dropping the `Booking` DbSet (without this, `dotnet build API/ZachHairStudio.slnx` fails). Full retirement of the `ZachHairStudio.Admin` MVC scaffold remains Phase 8 scope per the roadmap — only the Booking-coupled files were touched here.

## Deviations from Plan

None - plan executed exactly as written. The plan's own action steps already anticipated and directed the Admin-scaffold deletions and the SC4 index-error correction; no undirected auto-fixes were required.

## Owner-Reviewable Placeholder (flagged per plan, mirrors Phase 1 D-15 precedent)

**`StylistWorkingHours` seed data** (`BookingDbContext.cs`, `HasData` block): all 4 active stylists were seeded with an identical **Tuesday–Saturday 09:00–18:00** weekly schedule (20 rows, Ids 1–20). This is a placeholder default, not a real schedule — the owner should review and adjust per-stylist hours (e.g., different days off, different opening times) before Phase 2's booking flow goes live. This mirrors the seed-price placeholder precedent flagged in Phase 1 (D-15).

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required. This plan only touches `appsettings.json`/`appsettings.Development.json` for the non-secret `Salon:IanaTimeZoneId` value; no user-secrets or environment variables needed.

## Next Phase Readiness

- The schema is ready for Plan 03 (open-slot query — `SlotService` reads `Stylist`, `StylistWorkingHours`, `StylistTimeOff`, `AppointmentSlot`) and Plan 04 (appointment write + retry-on-conflict loop, which depends on the `IX_AppointmentSlots_StylistId_SlotStart` unique index existing at the DB level).
- `GET /api/stylists` is live and returns the 4 seeded stylists — the first observable end-to-end read path for the phase's vertical slice.
- The `Salon:IanaTimeZoneId` config (`America/New_York`) is available via `IOptions<SalonOptions>` for every DateTimeOffset conversion later plans need.
- **Concern for the owner / next planning pass:** the seeded `StylistWorkingHours` schedule (Tue–Sat 09:00–18:00 for all 4 stylists) is a placeholder and should be reviewed before the booking flow is user-facing.
- The `landing-page` frontend's `createBooking`/`BookingRequest`/`BookingResponse` client code (in `lib/api.ts`) and `BookingRequestForm.tsx` still reference the now-deleted `POST /api/bookings` endpoint — per `02-PATTERNS.md`, these are slated for deletion/replacement in a later plan (`AppointmentBookingForm.tsx` + `lib/appointments.ts`), not this one. No frontend changes were in this plan's `files_modified` scope.

---
*Phase: 02-booking-core*
*Completed: 2026-07-09*

## Self-Check: PASSED

- All 12 created files verified present on disk (entities, DTO/mapper, service/controller, test, migration).
- All 3 deleted-file claims verified absent (Booking.cs, BookingsController.cs, Admin BookingController.cs).
- All 4 task/summary commit hashes (`59f2081`, `83182b6`, `45e4747`, `ee6ea9b`) verified present in `git log --oneline --all`.
