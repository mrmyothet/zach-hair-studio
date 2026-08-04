---
phase: 260725-mx3
plan: 01
subsystem: api
tags: [aspnetcore, efcore, openapi-typescript, react, swr, authorization]

requires:
  - phase: 04-staff-management-services-availability
    provides: ServicesController Owner-role gating pattern (action-level [Authorize]), soft-retire (IsActive) field on the Service entity
provides:
  - "GET /api/Services accepts an opt-in includeInactive query flag, honored only for an authenticated Owner"
  - "ServiceResponseDto.IsActive (nullable, JSON-omitted when null) surfaces retirement status on the Owner listing path"
  - "Dashboard /services page derives Retired state from server truth instead of session-local React state"
affects: [dashboard-services-management, api-services-catalog]

tech-stack:
  added: []
  patterns:
    - "Single list method with a conditional-filter parameter (GetServicesAsync(bool includeInactive)) instead of forked queries"
    - "Nullable DTO field + JsonIgnoreCondition.WhenWritingNull to add privileged-only fields without changing the public response shape"
    - "Controller-side role check ANDed into a local boolean before it reaches the service layer, so the service layer never inspects the raw request flag (fail-closed by construction)"

key-files:
  created: []
  modified:
    - API/ZachHairStudio.Shared/Features/Services/ServiceResponseDto.cs
    - API/ZachHairStudio.Shared/Features/Services/ServiceExtensions.cs
    - API/ZachHairStudio.Shared/Features/Services/ServicesService.cs
    - API/ZachHairStudio.Api/Controllers/ServicesController.cs
    - API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs
    - dashboard/lib/api/schema.d.ts
    - dashboard/lib/useServices.ts
    - dashboard/app/services/page.tsx
    - dashboard/components/ServiceForm.tsx

key-decisions:
  - "DD-1: non-Owner or anonymous callers passing includeInactive=true get the flag silently ignored (200, active-only body), not a 403 — avoids advertising a privileged mode on a deliberately anonymous endpoint and stays fail-closed by construction (relaxed filter only reachable inside the role check)"
  - "DD-2: ServiceResponseDto.IsActive is bool? with [JsonIgnore(Condition = WhenWritingNull)], populated only on the includeInactive path, so the default catalog response stays byte-identical"

patterns-established:
  - "Owner-gated read filters on otherwise-anonymous GET endpoints: AND the query flag with User.IsInRole(...) in the controller, pass only the computed value to the service layer"

requirements-completed: [MGMT-01]

coverage:
  - id: D1
    description: "ServicesService.GetServicesAsync(includeInactive) returns Active-only by default and Active+Inactive ordered by DisplayOrder when true, with IsActive populated only on the includeInactive path"
    requirement: "MGMT-01"
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs#GetServicesAsync_WithIncludeInactive_ReturnsActiveAndInactiveOrderedByDisplayOrder"
        status: pass
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs#GetServicesAsync_ReturnsOnlyActiveServicesOrderedByDisplayOrder"
        status: pass
    human_judgment: false
  - id: D2
    description: "GET /api/Services?includeInactive=true is honored only for an authenticated Owner; anonymous, Staff, and Owner-without-flag callers never see retired rows"
    requirement: "MGMT-01"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs#GetServices_OwnerWithIncludeInactive_ReturnsRetiredService"
        status: pass
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs#GetServices_AnonymousWithIncludeInactive_OmitsRetiredService"
        status: pass
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs#GetServices_StaffRoleWithIncludeInactive_OmitsRetiredService"
        status: pass
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs#GetServices_OwnerWithoutIncludeInactive_OmitsRetiredService"
        status: pass
    human_judgment: false
  - id: D3
    description: "Dashboard /services page shows retired services with a Retired badge and working Reactivate button after a full page reload (server truth, not session state)"
    requirement: "MGMT-01"
    verification: []
    human_judgment: true
    rationale: "Requires a running full stack (API + dashboard) and a real browser reload to observe; the plan's own verification section lists this as an optional manual smoke test, not exercised by an automated harness in this execution."

duration: 50min
completed: 2026-07-25
status: complete
---

# Quick Task 260725-mx3: Owner-gated includeInactive filter Summary

**Added an Owner-only `includeInactive` query parameter to `GET /api/Services` and rewired the dashboard `/services` page to read retirement state from the server instead of session-local React state, closing the Phase 4 gap where a retired service vanished on page reload.**

## Performance

- **Duration:** 50 min
- **Started:** 2026-07-25T16:00:23+08:00
- **Completed:** 2026-07-25T16:50:08+08:00
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments

- `ServicesService.GetServicesAsync(bool includeInactive = false)` replaces `GetActiveServicesAsync()` as the single list method — one conditional filter, no forked query.
- `ServiceResponseDto.IsActive` is a nullable, JSON-omitted-when-null field, populated only on the Owner-gated listing path (DD-2) — the default anonymous catalog response is unchanged.
- `ServicesController.GetServices` accepts `[FromQuery] bool includeInactive`, ANDs it with `User.IsInRole(StaffRoles.Owner)`, and only the computed boolean reaches the service layer (DD-1, fail-closed by construction).
- 5 new backend tests (1 service-layer + 4 controller: Owner-with-flag, anonymous-with-flag, Staff-with-flag, Owner-without-flag) — full suite now 157/157 passing (152 baseline + 5 new).
- `dashboard/lib/api/schema.d.ts` regenerated from the live OpenAPI document (never hand-edited).
- `dashboard/app/services/page.tsx` no longer tracks retirements in local React state — Retired badge and Reactivate button are driven by `row.isActive === false` from the server response, which survives a page reload.

## Task Commits

Each task was committed atomically (TDD tasks have separate test/feat commits):

1. **Task 1: Add includeInactive support to the Services contract and service layer**
   - `828ddcb` test(260725-mx3): add failing test for GetServicesAsync includeInactive filter
   - `ab4b78e` feat(260725-mx3): add includeInactive filter to Services contract and service layer
2. **Task 2: Gate the flag to authenticated Owners in the controller and prove it with tests**
   - `f4b5ebc` test(260725-mx3): add failing tests for Owner-gated includeInactive controller flag
   - `8362df3` feat(260725-mx3): gate includeInactive flag to authenticated Owners
3. **Task 3: Regenerate the typed client and rewire the dashboard to server truth**
   - `2facace` feat(260725-mx3): rewire dashboard services page to server-truth retirement state

_TDD tasks (1 and 2) each have a test → feat commit pair; no refactor commit was needed for either._

## Files Created/Modified

- `API/ZachHairStudio.Shared/Features/Services/ServiceResponseDto.cs` - Added nullable `IsActive` with `JsonIgnoreCondition.WhenWritingNull`
- `API/ZachHairStudio.Shared/Features/Services/ServiceExtensions.cs` - `ToDto` gained an optional `includeStatus` parameter
- `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` - `GetActiveServicesAsync()` renamed/parameterized to `GetServicesAsync(bool includeInactive = false)`
- `API/ZachHairStudio.Api/Controllers/ServicesController.cs` - `GetServices` accepts `includeInactive`, gated by `User.IsInRole(StaffRoles.Owner)`
- `API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs` - Renamed call site, added includeInactive coverage
- `API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs` - `UpdateDtoFor` gained `isActive` param, added `CreateRetiredServiceAsync` helper + 4 new tests
- `dashboard/lib/api/schema.d.ts` - Regenerated from `http://localhost:5236/openapi/v1.json`
- `dashboard/lib/useServices.ts` - `fetchServices`/`useServices` accept `includeInactive`, distinct SWR cache keys
- `dashboard/app/services/page.tsx` - Removed `retiredOverrides` state; retired derivation now `row.isActive === false`
- `dashboard/components/ServiceForm.tsx` - Updated `initialIsActive` JSDoc to describe the server-truth source

## Decisions Made

- DD-1 and DD-2 were locked in the plan and implemented as specified (see frontmatter `key-decisions`).
- No new decisions were made during execution beyond what the plan already settled.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated ServicesController's call site during Task 1**
- **Found during:** Task 1 (Add includeInactive support to the Services contract and service layer)
- **Issue:** Task 1's own `dotnet build` verification step would fail because `ServicesController.GetServices()` still called the just-renamed `GetActiveServicesAsync()`. The plan's Task 1 `<files>` list didn't include the controller (that full rewrite — the query parameter and DD-1 gating — is Task 2's scope), but leaving the stale call site in place blocks Task 1's own build gate.
- **Fix:** Updated the single call site from `_servicesService.GetActiveServicesAsync()` to `_servicesService.GetServicesAsync()` (no query parameter added yet — default behavior, i.e. active-only, is unchanged). Task 2 then added the `includeInactive` parameter and Owner gating on top of this same line.
- **Files modified:** API/ZachHairStudio.Api/Controllers/ServicesController.cs
- **Verification:** `dotnet build` succeeds with 0 errors; Task 1's own grep check confirms `GetActiveServicesAsync` no longer exists anywhere in the solution.
- **Committed in:** ab4b78e (Task 1 feat commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary to keep Task 1's own build gate green; no scope creep — the change is a same-line rename with identical default behavior, and the full Owner-gating rewrite still landed in Task 2 as planned.

## Issues Encountered

None. The API started cleanly against a one-off `(localdb)\MSSQLLocalDB` connection-string override (the tracked user-secrets Azure SQL string doesn't reach this machine), the OpenAPI schema regenerated without errors, and the API process was stopped afterward without leaving anything running.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The D-02 soft-retire loop is closed end to end: an Owner can retire a service, reload `/services`, and still see and reactivate it.
- `git diff --stat -- landing-page/` is empty — the public catalog and its zod-parsed schema are untouched.
- Backend suite: 157/157 passing. Dashboard: `npm run build` and `npm run lint` both clean.
- The plan's optional manual smoke test (live browser retire → reload → reactivate, then confirm the landing page catalog omits the retired service) was not exercised in this automated execution — flagged as `human_judgment: true` (D3) in the coverage block above for a verifier/UAT pass if desired.

## Self-Check: PASSED

All 10 code files plus this SUMMARY.md confirmed present on disk; all 5 task commit hashes (828ddcb, ab4b78e, f4b5ebc, 8362df3, 2facace) confirmed present in git log.

---
*Quick task: 260725-mx3*
*Completed: 2026-07-25*
