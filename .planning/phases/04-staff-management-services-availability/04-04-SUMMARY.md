---
phase: 04-staff-management-services-availability
plan: 04
subsystem: ui
tags: [nextjs, react, tailwind, openapi-fetch, swr, dashboard, aspnetcore, efcore]

# Dependency graph
requires:
  - phase: 04-03
    provides: "PUT working-hours / POST+DELETE time-off write endpoints against StylistWorkingHours/StylistTimeOff, any-staff [Authorize] gate (D-13)"
  - phase: 04-02
    provides: "DashboardNav (Availability link already wired), useServices/useSchedule SWR hook pattern, ConfirmDialog, icons.tsx"
provides:
  - "GET /api/Availability/{stylistId} (AvailabilityResponseDto) — read path Plan 03 didn't ship; reads only StylistWorkingHours/StylistTimeOff (D-08, no new store)"
  - "useAvailability hook (SWR, keyed on stylistId) + saveAvailability (whole-week PUT + time-off add/remove diff, single Save Changes moment for D-12)"
  - "StylistPicker — self-contained fetch + all E4 states (loading/error/empty/populated, chip-or-select, pre-select-when-one)"
  - "WeekStripEditor — 7-row click-drag weekly hours painter, 15-min snap, D-06 gap-as-break via segment merge"
  - "TimeOffCalendar — single-month grid, armed click-start/click-end paint flow, dashed-muted bands, per-range Remove + reason"
  - "/availability page — requireAuth (no Owner gate), DashboardNav, StylistPicker + WeekStripEditor + TimeOffCalendar + one Save Changes button, marked seam for Plan 05's conflict panel"
affects: [04-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "DayOfWeek (and other server enums) are typed `number` by Swashbuckle but actually serialize as their .NET name string via the global JsonStringEnumConverter — confirmed against a live GET response; cast with `as unknown as components[\"schemas\"][\"DayOfWeek\"]` on write, same pattern already used for AppointmentStatus in scheduleStatus.ts."
    - "Controlled-value calendar/editor components (WeekStripEditor, TimeOffCalendar) hold no server-truth state themselves — the page hydrates local edit state from useAvailability once per stylist selection (a hydratedKeyRef guard), so background SWR revalidation never clobbers in-progress edits; only an explicit Save Changes re-hydrates, from its own mutate() return value."
    - "Time-off ranges are whole-day blocks constructed client-side as salon-local midnight-to-midnight (exclusive end) using a hardcoded +06:30 offset — safe because Asia/Yangon never observes DST (STATE.md, Phase 2 Plan 07 decision)."

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Availability/AvailabilityResponseDto.cs
    - dashboard/lib/useAvailability.ts
    - dashboard/components/StylistPicker.tsx
    - dashboard/components/WeekStripEditor.tsx
    - dashboard/components/TimeOffCalendar.tsx
    - dashboard/app/availability/page.tsx
  modified:
    - API/ZachHairStudio.Api/Controllers/AvailabilityController.cs
    - API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs
    - dashboard/lib/api/schema.d.ts

key-decisions:
  - "Added GET /api/availability/{stylistId} to AvailabilityController/AvailabilityService — Plan 03 shipped only PUT/POST/DELETE, and useAvailability structurally cannot read a stylist's current hours/time-off without a read endpoint. Reads exclusively from StylistWorkingHours/StylistTimeOff (D-08 preserved, no new store); [ProducesResponseType] added so the regenerated OpenAPI schema types the response body correctly (without it, Swashbuckle documented the 200 with no content, same class of doc-gap as the existing 201-documented-as-200 issue from Plan 02)."
  - "Kept the UI-SPEC's wider 06:00-22:00 default week-strip window (not narrowed to the seeded 09:00-18:00) — confirmed against BookingDbContext's seed data (every stylist, every day, 09:00-18:00) and chose to give staff painting room beyond that placeholder rather than clamp the editor to it (UI-SPEC Open Question 1)."
  - "WeekStripEditor uses click-drag with 15-minute snap (UI-SPEC's recommended mechanic, Open Question 3); TimeOffCalendar uses an explicit 'Add Time Off' button that arms a click-start/click-end flow (the Copywriting Contract requires a distinct, non-gold 'Add Time Off' CTA, and arming avoids accidental repaints from stray day-cell clicks)."
  - "Time-off Remove/reason-edit UX is a below-grid list (all ranges, Remove always available; reason editable only for not-yet-saved ranges) rather than purely in-grid hover controls — a 40x40 cell is too small for a reliable hover-remove/reason-input target; the grid itself still renders the required dashed-muted band with a truncated reason label and full text via `title` (matches the existing truncate+title-tooltip precedent from Plan 02's services list)."
  - "saveAvailability sequences one PUT (whole-week hours replace) then per-range DELETE/POST calls for the time-off diff (removed-then-added), all before a single mutate() re-hydration — satisfies D-12's 'one save moment' at the UI level even though the backend has no single combined endpoint (Plan 03 exposes three separate routes)."

patterns-established:
  - "Availability editor components are fully controlled (value/onChange) and stateless about server truth — the page owns hydration timing, distinct from Plan 02's ServiceForm which manages its own persisted/not-persisted state internally."

requirements-completed: [MGMT-02]

coverage:
  - id: D1
    description: "Any authenticated staff can open /availability, pick a stylist (or land on the only stylist pre-selected), and see that stylist's current weekly hours and time off loaded via the new GET endpoint."
    requirement: "MGMT-02"
    verification:
      - kind: other
        ref: "cd dashboard && npm run build -- typechecks StylistPicker.tsx, useAvailability.ts, availability/page.tsx against the regenerated schema.d.ts — pass"
        status: pass
      - kind: manual_procedural
        ref: "Manually verified during this plan: logged in as Owner, GET /api/Availability/1 returned {workingHours:[],timeOff:[]} before any writes, then reflected a PUT/GET round trip with dayOfWeek as the string \"Monday\" on the wire"
        status: pass
    human_judgment: true
    rationale: "Full page load/selection UX against the live API needs a human pass in a browser; no dashboard test runner is configured (RESEARCH Validation Architecture)."
  - id: D2
    description: "WeekStripEditor: 7 rows, 40px height, 24px/hour, 06:00-22:00 window, 15-minute snap; click-drag paints a gold-dark segment; dragging again on the same day adds a non-contiguous segment (D-06 gap-as-break) via overlap-merge; empty day shows the Closed overlay; loading disables drag."
    requirement: "MGMT-02"
    verification:
      - kind: other
        ref: "cd dashboard && npm run build && npm run lint — pass"
        status: pass
      - kind: manual_procedural
        ref: "As staff: paint two non-contiguous segments on one weekday, confirm both render without overlap and a gap between them; paint nothing on another weekday and confirm the Closed overlay"
        status: unknown
    human_judgment: true
    rationale: "Pixel-level drag interaction and the E5 overflow backstop (multiple non-contiguous segments) require a human/browser pass; no dashboard test runner configured."
  - id: D3
    description: "TimeOffCalendar: single-month grid with month nav; Add Time Off arms a click-start/click-end range paint; painted ranges render as dashed-muted bands (never gold, never red) with a Remove control and optional reason; empty state shows 'No time off scheduled.' with the grid still fully rendered."
    requirement: "MGMT-02"
    verification:
      - kind: other
        ref: "cd dashboard && npm run build && npm run lint — pass"
        status: pass
      - kind: manual_procedural
        ref: "As staff: Add Time Off, paint a 3-day range, confirm the dashed-muted band across all 3 cells, set a reason, Remove it, confirm the grid returns to empty with 'No time off scheduled.'"
        status: unknown
    human_judgment: true
    rationale: "Visual band rendering and the E6 overflow assumption (many overlapping ranges) require a human/browser pass; no dashboard test runner configured."
  - id: D4
    description: "A single Save Changes button submits the whole week's hours (PUT) and the time-off diff (POST new ranges, DELETE removed ranges) together; success shows 'Availability saved.' and re-hydrates from the fresh GET; a non-conflict failure shows a generic banner with a marked seam for Plan 05's conflict panel."
    requirement: "MGMT-02"
    verification:
      - kind: other
        ref: "cd dashboard && npm run build — pass; manually verified PUT/POST/DELETE against the live API (LocalDB) via curl during this plan, including a PUT->GET round trip and a full write-then-cleanup cycle"
        status: pass
      - kind: manual_procedural
        ref: "As staff: change hours + add/remove time off, Save Changes, confirm the flash and that a public /book slot query for that stylist reflects the change"
        status: unknown
    human_judgment: true
    rationale: "End-to-end save + public-slot reflection needs a live browser/API pass; the write path's own reflection-through-slots proof already lives in Plan 03's integration tests (WorkingHoursReplaceTests.cs, TimeOffTests.cs)."

duration: 45min
completed: 2026-07-25
status: complete
---

# Phase 4 Plan 04: Availability Editor (Weekly Hours + Time Off) Summary

**Staff-facing `/availability` page — stylist picker, click-drag weekly-hours strip, click-to-paint time-off calendar, and one combined Save Changes — backed by a new GET /api/availability/{stylistId} read endpoint the write-only Plan 03 API didn't expose.**

## Performance

- **Duration:** ~45 min (includes discovering and closing the Plan 03 read-path gap, starting the API against LocalDB to regenerate the OpenAPI client, and a manual curl-based verification of the wire format)
- **Completed:** 2026-07-25
- **Tasks:** 3
- **Files modified:** 9 (3 backend, 6 frontend)

## Accomplishments

- Closed a read-path gap left by Plan 03: added `GET /api/availability/{stylistId}` (`AvailabilityResponseDto`) so the dashboard can load a stylist's current hours + time off — writes-only endpoints existed, nothing to read them back. Verified against a live API instance that the response round-trips correctly, including confirming `DayOfWeek` serializes as its string name ("Monday") on the wire despite the Swashbuckle-generated schema typing it as `number`.
- `useAvailability` (SWR, keyed on stylistId) + `saveAvailability` (sequences the whole-week PUT, then the time-off DELETE/POST diff) give the page a single async entry point for both load and save.
- `StylistPicker` is fully self-contained: fetches `/api/Stylists`, owns loading/error/empty/populated states, pre-selects when there's exactly one stylist, collapses to a `<select>` under 768px.
- `WeekStripEditor` implements the full click-drag paint mechanic with 15-minute snapping, overlap-merging (so a re-drag over an existing segment never produces duplicate/overlapping rows), a hover-revealed remove control, and the Closed overlay for empty weekdays.
- `TimeOffCalendar` implements an armed click-start/click-end range paint, dashed-muted bands (explicitly never gold or red), a below-grid list giving every range a Remove control and — for not-yet-saved ranges only — an editable reason field.
- `/availability` wires it all together behind `requireAuth` with no Owner gate (D-13), inside the existing `DashboardNav` (whose Availability link Plan 02 had already wired), with one gold-dark Save Changes button and a clearly-commented seam for Plan 05's conflict panel.
- `npm run build` and `npm run lint` both pass clean; the full `dotnet test` suite is still 138/138 green after the backend addition.

## Task Commits

Each task was committed atomically:

1. **Task 1: Regenerate client + useAvailability hook + StylistPicker (plus the GET endpoint it needed)** - `b8f2cf6` (feat)
2. **Task 2: WeekStripEditor (drag-paint weekly hours)** - `ae0c963` (feat)
3. **Task 3: TimeOffCalendar + /availability page + single Save Changes** - `c85825b` (feat)

**Plan metadata:** (this commit)

## Files Created/Modified

- `API/ZachHairStudio.Shared/Features/Availability/AvailabilityResponseDto.cs` - `AvailabilityResponseDto` (WorkingHours + TimeOff) and `TimeOffResponseDto` (adds Id for DELETE targeting)
- `API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs` - `GetAvailabilityAsync` added alongside the existing write methods
- `API/ZachHairStudio.Api/Controllers/AvailabilityController.cs` - `GET {stylistId}` action with `[ProducesResponseType]` for accurate OpenAPI docs
- `dashboard/lib/api/schema.d.ts` - Regenerated: adds all four `/api/Availability/...` paths
- `dashboard/lib/useAvailability.ts` - `useAvailability` SWR hook + `saveAvailability` batched save
- `dashboard/components/StylistPicker.tsx` - Self-contained stylist fetch + all E4 states
- `dashboard/components/WeekStripEditor.tsx` - Click-drag weekly-hours painter
- `dashboard/components/TimeOffCalendar.tsx` - Month-grid time-off painter
- `dashboard/app/availability/page.tsx` - Page shell wiring picker + editors + Save Changes

## Decisions Made

See `key-decisions` in frontmatter — summarized: added the missing GET endpoint (Rule 2/3, structurally required), kept the wider 06:00-22:00 week-strip window over the narrower seeded 09:00-18:00 default, used click-drag for hours and an armed click-start/click-end flow for time off, moved time-off Remove/reason controls into a below-grid list for reliability at 40x40 cell size, and sequenced the save as PUT-then-diff rather than a single combined backend call (Plan 03 exposes three separate routes, not one).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2/3 - Missing critical / blocking] Added `GET /api/availability/{stylistId}` — no read endpoint existed**
- **Found during:** Task 1, before writing `useAvailability`
- **Issue:** Plan 03 shipped only `PUT working-hours`, `POST time-off`, `DELETE time-off`. There was no way for the dashboard to read a stylist's current hours/time-off, and the plan's own action text explicitly anticipated this ("if a GET... is not present, derive it... do NOT invent a second store"). Without a read path, `useAvailability` — an acceptance criterion of this plan — could not exist.
- **Fix:** Added `AvailabilityService.GetAvailabilityAsync` (reads `StylistWorkingHours`/`StylistTimeOff` directly, no new store) and a `GET {stylistId}` action on `AvailabilityController`, gated the same as the existing any-staff writes (D-13). Added `[ProducesResponseType(typeof(AvailabilityResponseDto), 200)]` so the regenerated OpenAPI schema correctly types the response (otherwise Swashbuckle documents the 200 as content-less).
- **Files modified:** `API/ZachHairStudio.Shared/Features/Availability/AvailabilityResponseDto.cs` (new), `AvailabilityService.cs`, `AvailabilityApi/Controllers/AvailabilityController.cs`
- **Verification:** `dotnet build` clean, `dotnet test` 138/138 green (no regressions), and a manual curl round trip (login as Owner, PUT working-hours, GET back the same segment, confirmed `dayOfWeek` serializes as `"Monday"` not a number) before regenerating the OpenAPI client.
- **Committed in:** `b8f2cf6` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 2/3, backend read-path addition)
**Impact on plan:** Necessary for this plan's own acceptance criteria (`useAvailability` returning hours + timeOff) to be achievable at all. No scope creep beyond what the plan itself flagged as a possible gap; writes still touch only `StylistWorkingHours`/`StylistTimeOff` (D-08 preserved).

## Issues Encountered

- The regenerated `schema.d.ts` initially typed the new GET's 200 response as content-less (`content?: never`) because the controller action had no `[ProducesResponseType]` attribute — Swashbuckle only documents a response body it can infer from a typed return, and `Ok(result.Data)` alone isn't enough. Adding the attribute and regenerating fixed it; this is the same class of Swashbuckle documentation quirk noted in Plan 02 (image-upload endpoint documented as `application/x-www-form-urlencoded`) and Plan 03 (POST time-off documented as 200 when it actually returns 201).
- Confirmed a second Swashbuckle quirk directly relevant to this plan's data shape: `DayOfWeek` (and other server enums) are documented as `number` in `schema.d.ts`, but the global `JsonStringEnumConverter` in `Program.cs` actually serializes them as their .NET name string on the wire. Verified via a live curl round trip rather than assuming; `useAvailability`/`saveAvailability` cast explicitly (`as unknown as DayOfWeekName` / `as unknown as components["schemas"]["DayOfWeek"]`), matching the existing `scheduleStatus.ts` precedent for `AppointmentStatus`.
- Had to `taskkill` a leftover `dotnet run` process mid-plan (Windows file-lock on `ZachHairStudio.Api.exe`) before a second `dotnet build` could succeed after adding the `[ProducesResponseType]` attribute — no code impact, just a local build-server restart.

## Known Stubs

None. `WeekStripEditor` and `TimeOffCalendar` both read real data through `useAvailability` and write real data through `saveAvailability`; no hardcoded/mock data paths.

## Threat Flags

None beyond the plan's own `<threat_model>` (T-04-07, T-04-14 — both already mitigated per the plan's own read of Plan 03's server-side revalidation and `[Authorize]` gate; the new GET endpoint inherits the same class-level `[Authorize]`, so it is not separately exposed to anonymous callers).

## User Setup Required

None - no external service configuration required. Reused the existing Owner seed credentials (from Phase 3/4 Plan 01, via `dotnet user-secrets`) for manual API verification during this plan; no new secrets were added.

## Next Phase Readiness

- The availability editor is functionally complete against the Plan 03 write path plus this plan's new GET read path: pick a stylist, paint hours, paint time off, Save Changes, see the flash or a generic error banner.
- Plan 05 (MGMT-03) can now build directly on the marked seam in `/availability/page.tsx` — the `saveError` banner block has an explicit comment showing where the 409 "Can't Save — Conflicting Appointments" panel replaces/augments the generic failure message once the backend conflict check exists.
- The E5 overflow backstop (multiple non-contiguous segments rendering without overlap) and the E6 overflow assumption (many overlapping time-off ranges) are both implemented per this plan's chosen approach (segment-merge for E5; per-range dashed bands for E6) but, per the plan's own verification section, remain unverified by an automated check — flagged for the manual verification pass noted in the coverage table above.
- Only the manual/interactive verification steps (coverage D1-D4 above) remain, since no dashboard test runner is configured for this project (consistent with Plan 02's precedent).

---
*Phase: 04-staff-management-services-availability*
*Completed: 2026-07-25*

## Self-Check: PASSED

All 9 files created/modified by this plan were found on disk, and all 3 task commits
(b8f2cf6, ae0c963, c85825b) were found in git history.
