---
phase: 01-service-catalog
plan: 02
subsystem: api
tags: [dotnet, aspnet-core, ef-core, fluentvalidation, service-catalog]

requires:
  - phase: 01-service-catalog
    provides: Service entity, DTOs, mappers, validators, and API test project from Plan 01
provides:
  - ServicesService feature layer owning Services DbContext access
  - ServicesController REST endpoints for list/detail/create/update
  - Program.cs validator and ServicesService DI registration
  - Services DbSet, EF configuration, unique slug index, and AddServices migration with 6 seed rows
  - WebApplicationFactory integration fixture with EF InMemory seed coverage
affects: [service-catalog, frontend-catalog, booking-core, staff-management]

tech-stack:
  added:
    - dotnet-ef 10.0.9 global tool update
  patterns:
    - Per-feature service layer with Result<T> translation
    - Dual validation flow: controller shapes ProblemDetails, service validates defensively
    - EF Core HasData seeding through migrations, not UseSeeding/UseAsyncSeeding
    - WebApplicationFactory test host replaces DbContext through ConfigureTestServices

key-files:
  created:
    - API/ZachHairStudio.Api.Tests/CustomWebApplicationFactory.cs
    - API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerTests.cs
    - API/ZachHairStudio.Shared/Features/Services/ServicesService.cs
    - API/ZachHairStudio.Api/Controllers/ServicesController.cs
    - API/ZachHairStudio.Shared/Migrations/20260707190502_AddServices.cs
    - API/ZachHairStudio.Shared/Migrations/20260707190502_AddServices.Designer.cs
  modified:
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Shared/Db/BookingDbContext.cs
    - API/ZachHairStudio.Shared/Migrations/BookingDbContextModelSnapshot.cs

key-decisions:
  - "ServicesController injects ServicesService and validators only; all Services DbContext access lives in ServicesService."
  - "Controller validation builds ASP.NET ModelState/ProblemDetails, while ServicesService repeats validation for non-HTTP callers."
  - "Seed data uses EF Core HasData and an AddServices migration because startup uses Migrate()."
  - "Integration tests use a Testing environment and EF InMemory EnsureCreated() to exercise HasData without SQL Server."

patterns-established:
  - "Feature controllers translate Result<T> from a service layer instead of querying BookingDbContext directly."
  - "FluentValidation write endpoints return ValidationProblem(ModelState) for rich client-readable errors."
  - "Reference/catalog seed data is declared in OnModelCreating and captured by migrations."

requirements-completed: [PLAT-01, PLAT-02, CAT-03]
coverage:
  - id: D1
    description: "ServicesController REST endpoints are backed by ServicesService rather than direct BookingDbContext access."
    requirement: PLAT-01
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerTests.cs#ServicesController_DoesNotDependOnBookingDbContext"
        status: pass
      - kind: integration
        ref: "dotnet test API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj --nologo"
        status: pass
    human_judgment: false
  - id: D2
    description: "Invalid service writes return ASP.NET ProblemDetails/ModelState errors."
    requirement: PLAT-02
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerTests.cs#CreateService_WithEmptyName_ReturnsBadRequestWithErrorsBody"
        status: pass
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs#CreateAsync_WithInvalidDto_ReturnsValidationErrorAndDoesNotWriteRow"
        status: pass
    human_judgment: false
  - id: D3
    description: "Services are persisted through a Services DbSet with list/detail service methods and API endpoints."
    requirement: CAT-03
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs"
        status: pass
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerTests.cs"
        status: pass
    human_judgment: false
  - id: D4
    description: "AddServices migration creates the Services table, unique Slug index, and 6 seed rows."
    requirement: CAT-03
    verification:
      - kind: other
        ref: "TASK3-ACCEPTANCE-PASSED migration content check"
        status: pass
    human_judgment: false
  - id: D5
    description: "The AddServices migration applies to a local SQL Server database."
    requirement: CAT-03
    verification:
      - kind: other
        ref: "dotnet ef database update --project API/ZachHairStudio.Shared --startup-project API/ZachHairStudio.Api --connection Server=(localdb)\\ZachHairStudio2025;Database=ZachHairStudioDev;..."
        status: pass
    human_judgment: false

duration: 101min
completed: 2026-07-08
status: complete
---

# Phase 1 Plan 02: Services API + Seed Migration Summary

**Services API with a dedicated service layer, FluentValidation ProblemDetails flow, and EF Core HasData catalog migration**

## Performance

- **Duration:** 101 min
- **Started:** 2026-07-08T01:36:00+07:00
- **Completed:** 2026-07-08T02:17:51+07:00
- **Tasks:** 3
- **Files modified:** 12

## Accomplishments

- Added RED/green coverage for `ServicesService`, `ServicesController`, the PLAT-01 reflection rule, invalid POST ProblemDetails, and seeded service reads.
- Implemented `ServicesService` as the only Services feature owner of `BookingDbContext` access, with active-only list/detail reads and `Result<T>` create/update methods.
- Added `ServicesController` list, detail, create, and update endpoints with controller-shaped validation errors and service-layer result translation.
- Added `Services` DbSet/configuration, a unique `Slug` index, and the `AddServices` migration with six seeded rows.

## Task Commits

Each task was committed atomically:

1. **Task 1: Write failing service + controller + PLAT-01 architecture tests (RED)** - `5a8b735` (test)
2. **Task 2: Implement ServicesService, ServicesController, and DI wiring (GREEN)** - `79e6107` (feat)
3. **Task 3: Add DbSet + unique slug index + HasData seed, generate migration** - `b3c992e` (feat)

## Files Created/Modified

- `API/ZachHairStudio.Api.Tests/CustomWebApplicationFactory.cs` - integration test host with EF InMemory replacement and seeded model initialization.
- `API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs` - service-layer behavior coverage.
- `API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerTests.cs` - endpoint and PLAT-01 reflection coverage.
- `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` - catalog service layer and Result<T> methods.
- `API/ZachHairStudio.Api/Controllers/ServicesController.cs` - REST endpoints and HTTP result translation.
- `API/ZachHairStudio.Api/Program.cs` - validator/service DI and partial Program marker.
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` - Services DbSet, EF constraints, unique slug index, and HasData rows.
- `API/ZachHairStudio.Shared/Migrations/20260707190502_AddServices.cs` - Services table, unique index, and InsertData migration.
- `API/ZachHairStudio.Shared/Migrations/BookingDbContextModelSnapshot.cs` - updated EF model snapshot.

## Seeded Catalog Rows

- `precision-cut`: $35, 45 minutes
- `color-and-highlights`: $80, 90 minutes
- `blowout-and-styling`: $55, 45 minutes
- `keratin-treatment`: $120, 120 minutes
- `scalp-treatment`: $65, 40 minutes
- `full-glam-package`: $199, 210 minutes

These prices and durations are owner-reviewable placeholders per D-15 and can be edited through the Phase 4 staff CRUD flow.

## Decisions Made

- Kept `ServicesController` thin: it injects `ServicesService` and validators, never `BookingDbContext`.
- Used manual ModelState population instead of `validation.AddToModelState(...)` because the project intentionally avoids deprecated `FluentValidation.AspNetCore`; the response shape is still ASP.NET ProblemDetails with an `errors` object.
- Added a `Testing` environment migration guard in `Program.cs` so `WebApplicationFactory` does not run SQL Server migrations against EF InMemory.
- Updated global `dotnet-ef` from 9.0.15 to 10.0.9 before scaffolding the EF Core 10 migration.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added Services DbSet during Task 2**
- **Found during:** Task 2
- **Issue:** The service/controller implementation and RED tests could not compile against `BookingDbContext.Services` before Task 3.
- **Fix:** Added the `DbSet<Service> Services` property in Task 2, leaving schema configuration and migration generation for Task 3.
- **Files modified:** `API/ZachHairStudio.Shared/Db/BookingDbContext.cs`
- **Verification:** Task 2 filtered service/controller tests passed.
- **Committed in:** `79e6107`

**2. [Rule 3 - Blocking] Adjusted WebApplicationFactory startup for EF InMemory**
- **Found during:** Task 2 and Task 3 test runs
- **Issue:** The test host initially tried to run SQL Server migration startup code or retained SQL Server provider configuration while using EF InMemory.
- **Fix:** Added a `Testing` environment migration guard, replaced DbContext registrations through `ConfigureTestServices`, removed EF provider configuration services, and called `EnsureCreated()` so HasData rows seed the InMemory store.
- **Files modified:** `API/ZachHairStudio.Api/Program.cs`, `API/ZachHairStudio.Api.Tests/CustomWebApplicationFactory.cs`
- **Verification:** Full API test suite passed with 49 tests.
- **Committed in:** `79e6107`, `b3c992e`

---

**Total deviations:** 2 auto-fixed blocking issues
**Impact on plan:** Both fixes were required to make the planned service/controller tests and seeded integration tests executable; no feature scope was added.

## Issues Encountered

- The default `MSSQLLocalDB` instance still fails inside the LocalDB API, and SQL Server 2012's `v11.0` instance cannot open the existing newer-format `ZachHairStudio.mdf`. A fresh SQL Server 2025 LocalDB instance named `ZachHairStudio2025` was created and the migration applied successfully to `ZachHairStudioDev`.
- The full API test suite passed: 49 tests green.
- The delayed backgrounded build checks completed successfully: `dotnet build API/ZachHairStudio.slnx --nologo` and `dotnet build API/ZachHairStudio.Api/ZachHairStudio.Api.csproj --no-restore --nologo`.
- Existing nullable warnings remain in `API/ZachHairStudio.Shared/Result.cs`; they predate this plan and did not block tests.

## Known Stubs

None. The seeded prices and durations are intentional owner-reviewable placeholder business values, not code stubs.

## User Setup Required

For this machine, use the working SQL Server 2025 LocalDB instance if running the API before `MSSQLLocalDB` is repaired:

`ConnectionStrings__DefaultConnection="Server=(localdb)\\ZachHairStudio2025;Database=ZachHairStudioDev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"`

## Next Phase Readiness

Plan 03 can build the public `/services` list and `/services/[slug]` detail pages against `GET /api/services` and `GET /api/services/{slug}`. The API contract, service-layer pattern, seeded slugs, and an applied local SQL Server database are ready.

## Self-Check: PASSED

- `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` exists.
- `API/ZachHairStudio.Api/Controllers/ServicesController.cs` exists.
- `API/ZachHairStudio.Shared/Migrations/20260707190502_AddServices.cs` exists.
- Task commits found: `5a8b735`, `79e6107`, `b3c992e`.
- Migration content check passed for `CreateTable`, unique `IX_Services_Slug`, and six seed slugs.
- Full API test suite passed with 49 tests.
- Full API solution build and API project build completed successfully.
- Migration applied successfully to `(localdb)\ZachHairStudio2025`, database `ZachHairStudioDev`.

---
*Phase: 01-service-catalog*
*Completed: 2026-07-08*
