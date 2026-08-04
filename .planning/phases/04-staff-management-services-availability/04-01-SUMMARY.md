---
phase: 04-staff-management-services-availability
plan: 01
subsystem: api
tags: [aspnet-core, fluentvalidation, jwt, authorization, file-upload, identity]

# Dependency graph
requires:
  - phase: 03-staff-dashboard-schedule
    provides: ASP.NET Core Identity + JWT bearer auth, StaffRoles.Owner/Staff constants, Result<T>/ProblemDetails error-shape convention
provides:
  - Owner-only [Authorize] gate on ServicesController's CreateService/UpdateService write actions (action-level, GET stays anonymous)
  - POST /api/services/{id}/image upload endpoint with 5MB/MIME-allowlist validation and server-generated filenames
  - Static-file serving of wwwroot/uploads/services/ (created at startup, served publicly)
affects: [04-02, 04-03, 04-04, 04-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Action-level [Authorize(Roles=...)] on individual controller actions rather than class-level, when the controller mixes public GETs with gated writes"
    - "IFormFile upload validated via FluentValidation (size + content-type allowlist) before any disk write; server-generated Path.GetRandomFileName() + content-type-derived extension, never the client FileName"
    - "Explicit PhysicalFileProvider built after Directory.CreateDirectory + IWebHostEnvironment.WebRootPath backfill, instead of relying on the default env.WebRootFileProvider which is captured once at host-build time"

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Services/ServiceImageUploadDto.cs
    - API/ZachHairStudio.Shared/Features/Services/ServiceImageUploadDtoValidator.cs
    - API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Services/ServiceImageUploadTests.cs
  modified:
    - API/ZachHairStudio.Api/Controllers/ServicesController.cs
    - API/ZachHairStudio.Shared/Features/Services/ServicesService.cs
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Shared/ZachHairStudio.Shared.csproj
    - API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerTests.cs
    - .gitignore

key-decisions:
  - "Action-level (not class-level) [Authorize(Roles=Owner)] on ServicesController's CreateService/UpdateService/UploadImage only — a class-level attribute would 401 the public GetServices/GetService actions the landing page depends on (Pitfall 5)."
  - "IWebHostEnvironment.WebRootPath is explicitly backfilled in Program.cs (app.Environment.WebRootPath = webRootPath) in addition to building an explicit PhysicalFileProvider for UseStaticFiles — ASP.NET Core's HostingEnvironment.Initialize leaves WebRootPath empty (not just the file provider) when wwwroot doesn't exist at host-build time, and the property is read directly by DI-injected consumers like the controller."
  - "Path.GetRandomFileName() + a content-type-derived extension (never the client-supplied FileName) is the stored filename, per RESEARCH Pattern 3 / T-04-02."

requirements-completed: [MGMT-01]

coverage:
  - id: D1
    description: "Owner-only gate on ServicesController writes; anonymous/Staff callers rejected, Owner succeeds, public GETs stay anonymous"
    requirement: "MGMT-01"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs (7 tests)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Service image upload endpoint: allowed-type/size success sets a served /uploads/services/... ImageUrl; disallowed type/oversize rejected 400 before disk write; re-upload replaces the stored file"
    requirement: "MGMT-01"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServiceImageUploadTests.cs (6 tests)"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-07-25
status: complete
---

# Phase 4 Plan 01: Service Write Gate + Image Upload Summary

**Owner-only [Authorize] gate closes the previously-ungated ServicesController write surface; a new POST {id}/image endpoint stores uploaded images under a server-generated filename and serves them from a newly-registered wwwroot/uploads/services/ static-file root.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-25T14:00Z (approx.)
- **Completed:** 2026-07-25T14:22Z
- **Tasks:** 3 (RED / GREEN / static-file registration)
- **Files modified:** 8 created/modified across the 3 task commits, plus 1 pre-existing test fixed as a direct consequence (Rule 1)

## Accomplishments
- `ServicesController.CreateService`/`UpdateService` are now `[Authorize(Roles = StaffRoles.Owner)]` at the action level; `GetServices`/`GetService` remain anonymous — closes a real, previously-shipped security gap (anyone could POST/PUT services) without regressing the public catalog (CAT-01/CAT-02).
- New `POST /api/services/{id}/image` endpoint: FluentValidation enforces a 5MB cap and a `image/jpeg|image/png|image/webp` content-type allowlist before any byte touches disk; the stored filename is `Path.GetRandomFileName()` + a content-type-derived extension (never the client's `FileName` — path-traversal safe).
- `wwwroot/uploads/services/` is created at API startup and served via an explicit `PhysicalFileProvider`-backed `UseStaticFiles()` registration, placed before `UseAuthentication()` so uploaded images stay publicly readable.
- Full backend test suite: 129/129 green (63 in the Services feature area, including the two new integration test files totaling 13 tests).
- No EF Core migration was needed or added — `Service.ImageUrl` already existed from Phase 1.

## Task Commits

Each task was committed atomically:

1. **Task 1: RED — auth-gate + image-upload integration tests** - `04e950d` (test)
2. **Task 2: GREEN — Owner-gate writes + image upload DTO/validator/service/action** - `4351276` (feat)
3. **Task 3: Static-file serving + upload directory in Program.cs** - `89e63d7` (feat, includes the Rule 1 fix to `ServicesControllerTests` and the `.gitignore` entry)

_Note: no separate plan-metadata commit yet — this SUMMARY.md/STATE.md/ROADMAP.md update is the final commit for this plan._

## Files Created/Modified
- `API/ZachHairStudio.Shared/Features/Services/ServiceImageUploadDto.cs` - Wraps a single `IFormFile Image` property for `[FromForm]` binding
- `API/ZachHairStudio.Shared/Features/Services/ServiceImageUploadDtoValidator.cs` - 5MB size cap + jpeg/png/webp content-type allowlist
- `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` - Added `SetImageAsync(int id, string imageUrl)` following the existing `FindAsync -> mutate -> SaveChangesAsync -> Result<T>` shape
- `API/ZachHairStudio.Api/Controllers/ServicesController.cs` - Action-level Owner gate on `CreateService`/`UpdateService`; new `UploadImage` action (validate -> write to `WebRootPath/uploads/services` -> `SetImageAsync`)
- `API/ZachHairStudio.Api/Program.cs` - Creates `wwwroot/uploads/services/` at startup, backfills `IWebHostEnvironment.WebRootPath`, registers `UseStaticFiles` with an explicit `PhysicalFileProvider`
- `API/ZachHairStudio.Shared/ZachHairStudio.Shared.csproj` - Added `<FrameworkReference Include="Microsoft.AspNetCore.App" />` so `IFormFile` resolves in this plain-SDK project
- `API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs` - 7 integration tests: anonymous GET 200, POST/PUT 401/403/201/204 by role
- `API/ZachHairStudio.Api.Tests/Features/Services/ServiceImageUploadTests.cs` - 6 integration tests: allowed-type success (Theory over jpeg/png/webp), disallowed-type 400, oversized 400, re-upload replaces
- `API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerTests.cs` - Fixed `CreateService_WithEmptyName_ReturnsBadRequestWithErrorsBody` to authenticate as Owner (Rule 1, see below)
- `.gitignore` - Ignore the runtime `API/ZachHairStudio.Api/wwwroot/` upload directory

## Decisions Made
- Action-level (not class-level) `[Authorize(Roles = StaffRoles.Owner)]` on `ServicesController`'s writes, matching RESEARCH Pitfall 5's explicit guidance — a class-level attribute would have 401'd the public catalog GETs.
- `Path.GetRandomFileName() + extension` (extension derived from the validated `ContentType`, not the client `FileName`) as the stored filename — matches RESEARCH Pattern 3 / threat T-04-02 verbatim.
- Explicitly backfilled `app.Environment.WebRootPath` in addition to constructing an explicit `PhysicalFileProvider` for `UseStaticFiles` (see Deviations — this was necessary, not just defensive).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added FrameworkReference to Microsoft.AspNetCore.App in ZachHairStudio.Shared.csproj**
- **Found during:** Task 2 (writing `ServiceImageUploadDto`)
- **Issue:** `ServiceImageUploadDto` needs `IFormFile` (`Microsoft.AspNetCore.Http`), but `ZachHairStudio.Shared.csproj` uses the plain `Microsoft.NET.Sdk` (not `Sdk.Web`), so ASP.NET Core's shared-framework assemblies aren't referenced implicitly — this would fail to compile.
- **Fix:** Added `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `ZachHairStudio.Shared.csproj`. No new NuGet package — this is a framework reference to the already-installed .NET 10 ASP.NET Core shared runtime.
- **Files modified:** `API/ZachHairStudio.Shared/ZachHairStudio.Shared.csproj`
- **Verification:** `dotnet build API/ZachHairStudio.slnx` succeeds with 0 errors.
- **Committed in:** `4351276` (Task 2 commit)

**2. [Rule 1 - Bug] Fixed the static-file/WebRootPath NullFileProvider footgun**
- **Found during:** Task 3, when `ServiceImageUploadTests` still failed with `ArgumentNullException` on some (not all) test runs after wiring `UseStaticFiles()`.
- **Issue:** ASP.NET Core's `HostingEnvironment.Initialize` leaves `IWebHostEnvironment.WebRootPath` **empty** (not just the file provider) when `wwwroot/` doesn't exist yet at host-build time — this repo ships no `wwwroot/`. The plan's guidance to fall back to `ContentRootPath + "wwwroot"` and call `Directory.CreateDirectory` only fixed the static-file *middleware's* file provider (via an explicitly-constructed `PhysicalFileProvider`); it did not fix the `IWebHostEnvironment.WebRootPath` *property* itself, which `ServicesController.UploadImage` also reads directly via DI, causing an intermittent `Path.Combine(null, ...)` crash depending on whether an earlier test run had already created the directory on disk.
- **Fix:** Explicitly assign `app.Environment.WebRootPath = webRootPath;` in `Program.cs` right after resolving/creating the directory, so every DI-injected `IWebHostEnvironment` consumer (including the controller) sees the correct path from that point forward — not just the static-file middleware.
- **Files modified:** `API/ZachHairStudio.Api/Program.cs`
- **Verification:** `dotnet test --filter "FullyQualifiedName~Services.ServiceImageUploadTests"` — 6/6 green, run multiple times without flakiness.
- **Committed in:** `89e63d7` (Task 3 commit)

**3. [Rule 1 - Bug] Fixed a pre-existing test broken by the intentional Owner-gate change**
- **Found during:** Task 3's full-suite verification pass.
- **Issue:** `ServicesControllerTests.CreateService_WithEmptyName_ReturnsBadRequestWithErrorsBody` posted anonymously and asserted 400 (validation failure). Once `CreateService` correctly requires Owner auth (this plan's intended change), the anonymous request now correctly returns 401 before reaching validation — a direct, expected consequence of MGMT-01's gate, not a bug in the new code, but the existing test needed updating to keep asserting the validation-shape behavior it was written for.
- **Fix:** Added a `CreateOwnerClientAsync()` helper (seeds an Owner user via `UserManager`/`RoleManager`, logs in, attaches the bearer token) and updated the test to call it before posting the empty-name payload.
- **Files modified:** `API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerTests.cs`
- **Verification:** `dotnet test --filter "FullyQualifiedName~Services"` — 63/63 green.
- **Committed in:** `89e63d7` (Task 3 commit)

**4. [Rule 2 - Missing Critical] Ignored the runtime wwwroot/ directory**
- **Found during:** Task 3, post-commit cleanup — `dotnet test` runs against real `WebApplicationFactory` hosts write real files under `API/ZachHairStudio.Api/wwwroot/uploads/services/` on disk (test image bytes), which showed up as untracked files.
- **Issue:** Leaving this untracked would either accumulate as noise in `git status` for every future test run, or accidentally get committed via a broad `git add`.
- **Fix:** Deleted the test-generated `wwwroot/` directory and added `API/ZachHairStudio.Api/wwwroot/` to `.gitignore` (the directory itself is recreated at startup by `Program.cs`).
- **Files modified:** `.gitignore`
- **Verification:** `git status --short` clean after a fresh `dotnet test` run.
- **Committed in:** `89e63d7` (Task 3 commit)

---

**Total deviations:** 4 auto-fixed (1 blocking/Rule 3, 2 bug/Rule 1, 1 missing-critical/Rule 2)
**Impact on plan:** All four were necessary for correctness (compile, no flaky NullReferenceException, existing test suite integrity, clean git state). No scope creep — no new endpoints or behavior beyond what the plan specified.

## Issues Encountered
- The `IWebHostEnvironment.WebRootPath` empty-string footgun (Deviation 2) was genuinely subtle — it only manifested intermittently depending on test execution order (a test running after an earlier test's host had already created `wwwroot/` on disk would incidentally get a correct `WebRootPath` from the framework itself, masking the bug). Caught only because the plan's own verify step re-ran the full `ServiceImageUploadTests` suite rather than a single case.
- `dotnet ef migrations list` failed against the configured Azure SQL connection string (`ConnectionStrings:DefaultConnection` in user-secrets points at `zachhairstudio.database.windows.net`, and this machine's IP isn't on the firewall allowlist) — this is a pre-existing, documented environment issue (STATE.md), unrelated to this plan. Verified "no new migration" instead via `git status`/`ls` on `API/ZachHairStudio.Shared/Migrations/`, which is sufficient and doesn't require live DB connectivity.

## User Setup Required
None - no external service configuration required. `Jwt:SigningKey`/`RESEND_API_KEY` were already present in this machine's `dotnet user-secrets` from Phase 3.

## Next Phase Readiness
- The Owner-only write gate and image upload endpoint are ready for Plan 02 (the dashboard `Services` page) to consume — `POST /api/services`, `PUT /api/services/{id}`, and `POST /api/services/{id}/image` are all stable, tested contracts.
- No blockers. The `AvailabilityController`/`AvailabilityService` work (MGMT-02/MGMT-03) in later waves of this phase is independent of this plan's changes and can proceed in parallel per the phase's wave plan.
- Dashboard OpenAPI client regeneration (per the `openapi-client` skill) is needed before Plan 02 can call the new `POST {id}/image` endpoint with full typing — flagged for whichever plan builds the dashboard Services page.

---
*Phase: 04-staff-management-services-availability*
*Completed: 2026-07-25*

## Self-Check: PASSED

All 11 files referenced above (created/modified) exist on disk, and all 3 task commit hashes (`04e950d`, `4351276`, `89e63d7`) are present in git history.
