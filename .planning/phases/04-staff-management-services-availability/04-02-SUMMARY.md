---
phase: 04-staff-management-services-availability
plan: 02
subsystem: ui
tags: [nextjs, react, tailwind, openapi-fetch, swr, dashboard]

# Dependency graph
requires:
  - phase: 04-01
    provides: "Owner-only [Authorize] gate on ServicesController writes, POST /api/services/{id}/image upload endpoint, static-file serving of wwwroot/uploads/services/"
provides:
  - "DashboardNav — shared header (Schedule/Services/Availability nav row + existing session/Add-staff/logout cluster) extracted from schedule/page.tsx, Services link hidden (not disabled) for Staff sessions (D-16)"
  - "/services Owner-only page: full E1 list states (empty/loading/error/populated), Retire (via ConfirmDialog) and Reactivate (immediate) actions"
  - "ServiceForm — single create/edit component with slug auto-derivation, field-required Save gating, and a stay-open-after-create flow that unlocks image upload"
  - "ImageUploadField — 160x160 dashed box with empty/uploading/populated/error states, direct multipart fetch to the Plan 01 image endpoint, onError placeholder fallback"
  - "CONFIRM_COPY.Retired factory entry in ConfirmDialog.tsx"
  - "PlusIcon, ImageIcon, TrashIcon in icons.tsx"
  - "dashboard/lib/api/schema.d.ts regenerated with the Services paths + POST {id}/image"
affects: [04-04, 04-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "DashboardNav is the single shared dashboard header; every page swaps its inline <header> for it (schedule/page.tsx done this plan; availability will follow in 04-04)"
    - "Owner-gate bootstrap for a page: requireAuth() then router.replace('/schedule') if session.role !== 'Owner', mirrored from staff/new/page.tsx"
    - "Multipart file uploads bypass the typed openapi-fetch client and use a direct fetch() with the auth token attached manually, when the .NET OpenAPI doc documents an IFormFile [FromForm] endpoint as application/x-www-form-urlencoded (a Swashbuckle quirk, not the real wire format)"
    - "A form component branches its POST-vs-PUT submit logic on local 'has this row been persisted yet' state, not on a static create/edit mode prop, so a create flow can transition into update calls without unmounting (needed to unlock image upload right after Save)"

key-files:
  created:
    - dashboard/components/DashboardNav.tsx
    - dashboard/lib/useServices.ts
    - dashboard/app/services/page.tsx
    - dashboard/components/ServiceForm.tsx
    - dashboard/components/ImageUploadField.tsx
  modified:
    - dashboard/lib/api/schema.d.ts
    - dashboard/app/schedule/page.tsx
    - dashboard/components/icons.tsx
    - dashboard/components/ConfirmDialog.tsx

key-decisions:
  - "GET /api/Services only returns Active rows and no backend filter param exists to fetch retired ones (confirmed against ServicesService.GetActiveServicesAsync); since this plan is frontend-only, retired/reactivated services are tracked in local component state for the session (services/page.tsx's retiredOverrides map) instead of inventing a new API surface, per the plan's explicit executor's-choice note."
  - "slug (required by ServiceCreateDto/ServiceUpdateDto, `^[a-z0-9]+(?:-[a-z0-9]+)*$`) is not a UI-SPEC field. ServiceForm derives it from Name via slugify() on first create and then holds it fixed for the life of the form/edit session, so an edited Name never silently changes an already-public service URL."
  - "ServiceForm's Save button never changes IsActive — that's exclusively the list's Retire/Reactivate actions. The caller passes initialIsActive in; the form always echoes it back unchanged on every PUT, since ServiceUpdateDto.IsActive is a non-nullable bool that would silently default to false (retiring the service) if omitted."
  - "Image Remove is handled by ServiceForm (which owns the full field set), not ImageUploadField, because PUT /api/Services/{id} takes the complete ServiceUpdateDto — a partial {imageUrl: null} body would blow away IsActive/DisplayOrder to their C# defaults."

requirements-completed: [MGMT-01]

coverage:
  - id: D1
    description: "DashboardNav renders Schedule/Availability for all staff and Services only for Owner sessions; schedule/page.tsx now uses it instead of its own inline header"
    requirement: "MGMT-01"
    verification:
      - kind: other
        ref: "cd dashboard && npm run build (typecheck across schedule/page.tsx + DashboardNav.tsx) — pass"
        status: pass
      - kind: manual_procedural
        ref: "Log in as Staff, confirm no Services link in the header and GET /services redirects to /schedule; log in as Owner, confirm the Services link appears"
        status: unknown
    human_judgment: true
    rationale: "Role-visibility and redirect behavior are visual/interactive; no dashboard test runner is configured (RESEARCH Validation Architecture) so this needs a human pass."
  - id: D2
    description: "/services list: empty/loading/error/populated E1 states render per the UI-SPEC copy; retired rows show after active ones with a muted chip; long text truncates with a title tooltip; a service missing an image shows a placeholder thumbnail"
    requirement: "MGMT-01"
    verification:
      - kind: other
        ref: "cd dashboard && npm run build — pass"
        status: pass
      - kind: manual_procedural
        ref: "As Owner: view /services with 0, 1, and many services; disconnect the API to see the error state; confirm truncation on a long name"
        status: unknown
    human_judgment: true
    rationale: "Visual state coverage (empty/loading/error/populated/truncation) requires human verification; no dashboard test runner configured."
  - id: D3
    description: "Retire routes through ConfirmDialog with CONFIRM_COPY.Retired's copy; Reactivate is an immediate button with no dialog; both persist via PUT /api/Services/{id}"
    requirement: "MGMT-01"
    verification:
      - kind: other
        ref: "cd dashboard && npm run build — pass"
        status: pass
      - kind: manual_procedural
        ref: "As Owner: Retire a service (confirm dialog copy, row moves to the Retired group), then Reactivate it (no dialog, row rejoins the Active group)"
        status: unknown
    human_judgment: true
    rationale: "End-to-end state transition against the live API needs a human pass; no dashboard test runner configured."
  - id: D4
    description: "ServiceForm handles create and edit in one component: Save disabled until required fields are filled (create), edit mode pre-fills every field, FluentValidation/network errors surface in a top-of-card banner with Save re-enabled to retry"
    requirement: "MGMT-01"
    verification:
      - kind: other
        ref: "cd dashboard && npm run build — pass"
        status: pass
      - kind: manual_procedural
        ref: "As Owner: create a service with a duplicate slug or out-of-range duration to see the validation banner; edit an existing service and confirm all fields pre-fill"
        status: unknown
    human_judgment: true
    rationale: "Form validation/error-banner behavior against the live API needs a human pass; no dashboard test runner configured."
  - id: D5
    description: "ImageUploadField: empty/uploading/populated/error states; client-side type+size gate; upload/replace/remove against POST/PUT /api/Services/{id}(/image); onError falls back to the placeholder for a 404'd stored URL"
    requirement: "MGMT-01"
    verification:
      - kind: other
        ref: "cd dashboard && npm run build — pass"
        status: pass
      - kind: manual_procedural
        ref: "As Owner: upload a JPG/PNG/WebP under 5MB (success), try a .gif or 6MB file (inline error), then Replace and Remove an uploaded image"
        status: unknown
    human_judgment: true
    rationale: "Multipart upload UX and the onError placeholder fallback need a human pass; no dashboard test runner configured."

duration: 45min
completed: 2026-07-25
status: complete
---

# Phase 4 Plan 02: Services Management UI + Shared DashboardNav Summary

**Owner-facing /services CRUD page (list, create/edit form, retire/reactivate, image upload) plus a DashboardNav extracted from schedule/page.tsx's inline header so Services/Availability are reachable from anywhere in the dashboard.**

## Performance

- **Duration:** ~45 min (includes `npm install` for `dashboard/`, starting the API against LocalDB to regenerate the OpenAPI client, and a manual owner-token API probe to confirm 201/204 response shapes)
- **Completed:** 2026-07-25
- **Tasks:** 3
- **Files modified:** 9 (4 modified, 5 created)

## Accomplishments
- Regenerated `dashboard/lib/api/schema.d.ts` against the running API (LocalDB, since the user-secrets connection string points at an Azure SQL server this machine's IP isn't allowlisted for — a pre-existing, documented environment issue) so `POST /api/Services/{id}/image` and the rest of the Services surface are typed.
- Extracted `DashboardNav` from `schedule/page.tsx`'s inline `<header>`: wordmark, a Schedule/Services(Owner-only)/Availability nav row with `usePathname`-driven active-link styling, and the existing session/Add-staff/logout cluster, unchanged. `schedule/page.tsx` now renders `<DashboardNav />` and dropped its now-dead `session`/`isOwner`/`handleLogout` state.
- `/services` (Owner-gated, Staff redirected to `/schedule`): full E1 list states, a Name/Category/Duration/Price/Status/Actions table with a small thumbnail-or-placeholder avatar per row, Retire (ConfirmDialog + new `CONFIRM_COPY.Retired`) and Reactivate (immediate, no dialog).
- `ServiceForm` + `ImageUploadField`: a single create/edit form (reusing `staff/new/page.tsx`'s `inputClass`/`Field` pattern) that stays open after a successful create so the now-existing service id unlocks image upload in the same session, plus a dashed 160x160 upload box with client-side type/size validation, upload/replace/remove, and a 404-safe `onError` placeholder fallback.
- `npm run build` (typecheck across all new/modified pages and components) and `npm run lint` both pass clean on the final committed state.

## Task Commits

Each task was committed atomically:

1. **Task 1: Regenerate OpenAPI client + extract shared DashboardNav** - `53532c0` (feat)
2. **Task 2: useServices hook + /services list page + Retire copy** - `d2dc3d7` (feat)
3. **Task 3: ServiceForm (create/edit) + ImageUploadField** - `85aa510` (feat)

_Note: no separate plan-metadata commit yet — this SUMMARY.md/STATE.md/ROADMAP.md update is the final commit for this plan._

## Files Created/Modified
- `dashboard/components/DashboardNav.tsx` - Shared header: wordmark, role-gated nav row, session/Add-staff/logout cluster
- `dashboard/lib/useServices.ts` - SWR fetch of `GET /api/Services` (Active only, no polling), mirroring `useSchedule`'s error/auth handling
- `dashboard/app/services/page.tsx` - Owner-gated list page with E1 states, Retire/Reactivate, and the create/edit form toggle
- `dashboard/components/ServiceForm.tsx` - Single create/edit form component with slug derivation and stay-open-after-create flow
- `dashboard/components/ImageUploadField.tsx` - Dashed-box image upload/replace/remove control
- `dashboard/lib/api/schema.d.ts` - Regenerated: adds `/api/Services`, `/api/Services/{slug}`, `/api/Services/{id}`, `/api/Services/{id}/image`
- `dashboard/app/schedule/page.tsx` - Inline header replaced with `<DashboardNav />`; dead session/logout state removed
- `dashboard/components/icons.tsx` - Added `PlusIcon`, `ImageIcon`, `TrashIcon`
- `dashboard/components/ConfirmDialog.tsx` - Added `CONFIRM_COPY.Retired` factory entry (needs the service name interpolated, unlike the existing static `Cancelled`/`NoShow` entries)

## Decisions Made
- **Retired-service visibility (UI-SPEC Open Question #5):** `GET /api/Services` only returns Active rows and this plan is frontend-only (no backend files in scope), so a new API filter param was explicitly out of bounds per the plan's own guidance. Chose local session-state tracking (`retiredOverrides` in `services/page.tsx`) over "render only Active rows" so Retire/Reactivate stay reachable within a session, at the cost of retired services disappearing from view on page reload until the backend gains a way to list them — flagged below as a scope-limited stub.
- **Slug auto-derivation:** `slug` is required by `ServiceCreateDto`/`ServiceUpdateDto` (`^[a-z0-9]+(?:-[a-z0-9]+)*$`) but absent from the UI-SPEC's field list. Derived from Name via a local `slugify()` in `ServiceForm`, computed once at first create and held fixed afterward (edit mode always reuses `service.slug`) so editing a service's Name never changes its already-public detail-page URL.
- **Create-then-continue-editing flow:** `ServiceForm`'s submit branches on local `serviceId` state (has this row been persisted?) rather than the static `mode` prop, and the form stays mounted after a successful create instead of auto-closing. This is what makes "gate image upload until the service exists" (plan's explicit note) actually usable — the Owner can add an image immediately after Save without leaving the page.
- **IsActive echo-back:** `ServiceUpdateDto.IsActive` is a non-nullable bool on the wire; every `PUT /api/Services/{id}` from `ServiceForm` explicitly sends the caller-supplied `initialIsActive` so an ordinary field edit can never accidentally retire (or reactivate) a service as a side effect.
- **Multipart upload bypasses the typed client:** the .NET OpenAPI doc documents `POST /api/Services/{id}/image`'s body as `application/x-www-form-urlencoded` (a Swashbuckle artifact of `[FromForm] IFormFile` binding — the real wire format is multipart and works fine). `ImageUploadField` uses a direct `fetch()` with the bearer token attached manually rather than fighting `openapi-fetch`'s inferred body type for that one call.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `dashboard/node_modules` was never installed**
- **Found during:** Task 1's own verify step (`npm run build`)
- **Issue:** `next` wasn't resolvable — `dashboard/` had a `package.json` but no `node_modules`, so the plan's own verify command couldn't run at all.
- **Fix:** `npm install` in `dashboard/`. No `package.json`/`package-lock.json` changes (only newly-created `node_modules/`, which is gitignored).
- **Files modified:** none tracked (`dashboard/node_modules/` is gitignored)
- **Verification:** `npm run build` succeeds afterward.
- **Committed in:** not applicable (gitignored, nothing to commit)

**2. [Rule 3 - Blocking] API wouldn't start against the user-secrets connection string**
- **Found during:** Task 1, starting the API to regenerate the OpenAPI client per the `openapi-client`/`dev` skills
- **Issue:** `dotnet user-secrets` holds `ConnectionStrings:DefaultConnection` pointed at Azure SQL (`zachhairstudio.database.windows.net`), and this machine's IP isn't on that server's firewall allowlist (a pre-existing, STATE.md-documented issue from Phase 4 Plan 01's own notes).
- **Fix:** Started the API with a one-off `ConnectionStrings__DefaultConnection` environment-variable override pointing at `(localdb)\MSSQLLocalDB`, matching CLAUDE.md's documented local dev path. No files changed — the override was only for this session's `dotnet run` process.
- **Files modified:** none
- **Verification:** API started, served `/openapi/v1.json` with 200, and the regenerated schema includes the expected Services paths.
- **Committed in:** not applicable (runtime-only override, no file change)

---

**Total deviations:** 2 auto-fixed (both Rule 3/blocking, both environment-setup issues with no tracked-file impact)
**Impact on plan:** Neither changed any application code or scope — both were prerequisites for running the plan's own verify commands.

## Issues Encountered
- `POST /api/Services` returns `201 Created` but the OpenAPI doc only documents a `200` response (same pattern already noted in Phase 3's `staff/new/page.tsx`) — confirmed via a manual owner-token `curl`/`openapi-fetch` probe that `data` is still populated at runtime for the 201, so `ServiceForm` checks `response.ok` rather than relying on the documented status, consistent with the existing precedent.
- Manually created two throwaway services (`test-x`, `test-y`) via `curl` while probing the create-response shape; both were immediately soft-retired (`IsActive: false`) via `PUT` afterward so they don't pollute the LocalDB seed data, following the app's existing soft-delete convention (no hard-delete endpoint exists).

## Known Stubs
- **Retired services are invisible after a page reload.** `GET /api/Services` has no way to request inactive rows, and this plan's scope is frontend-only, so `services/page.tsx` tracks retired-this-session services in local component state only. A service retired in a previous session can still be reactivated directly via `PUT /api/Services/{id}` (e.g. by a future Plan 01-style backend change or a manual API call), but the current `/services` UI has no way to surface it for the Owner to find and reactivate. Resolving this requires a backend change (a `GET /api/Services?includeInactive=true`-style filter, gated to Owner) that is out of this plan's declared `files_modified` scope — flagged for a future backend plan, not blocking MGMT-01's core create/edit/retire/image loop.

## Threat Flags

None beyond the plan's own `<threat_model>` (T-04-05, already tracked in 04-02-PLAN.md's frontmatter as `unverified`/`flagged`).

## User Setup Required
None - no external service configuration required. The API's existing `Jwt:SigningKey`/`RESEND_API_KEY`/Owner seed credentials (from Phase 3/4 Plan 01) were reused as-is for manual verification during this plan; no new secrets were added.

## Next Phase Readiness
- `DashboardNav` is ready for Plan 04 (`04-04-PLAN.md`, the Availability page) to reuse — it already renders the nav row and Owner-only cluster generically; Availability just needs to add its own `href` entry and swap its own inline header for `<DashboardNav />`, matching what this plan did for `schedule/page.tsx`.
- The Services catalog CRUD + image loop (create → edit → retire → reactivate → upload/replace/remove image) is functionally complete against the Plan 01 backend contracts; only the manual/interactive verification steps (coverage D1-D5 above) remain, since no dashboard test runner is configured for this project.
- **Flag for a future plan:** a backend `includeInactive`-style filter on `GET /api/Services` (Owner-gated) would remove the Known Stubs limitation above and let retired services persist across sessions/page reloads.

---
*Phase: 04-staff-management-services-availability*
*Completed: 2026-07-25*

## Self-Check: PASSED

All 9 files referenced above (created/modified) exist on disk, and all 3 task commit hashes (`53532c0`, `d2dc3d7`, `85aa510`) are present in git history.
