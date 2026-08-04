---
phase: 04-staff-management-services-availability
plan: 05
subsystem: api
tags: [dotnet, aspnetcore, efcore, availability, conflict-check, transactions, nextjs, react]

# Dependency graph
requires:
  - phase: 04-staff-management-services-availability
    provides: "04-03 availability write path (PUT working-hours, POST/DELETE time-off) + 04-04 GET read path and the /availability editor page with a marked conflict-panel seam"
provides:
  - "SalonTimeZone.ToSalonLocal(DateTimeOffset instant) — the inverse of ToSalonInstant, converting a UTC/offset instant to salon-local wall-clock time"
  - "A hard-blocking conflict scan in AvailabilityService, run before EITHER a working-hours or a time-off write persists, evaluating the FULL proposed final state (not an old-vs-new diff) against every Confirmed appointment for the stylist"
  - "AvailabilityConflictDto (clientName, serviceName, stylistName, salonLocalTime, appointmentId) — D-11-scoped, no PII beyond that"
  - "Result<T>.ConflictError + a T-independent Conflicts side-channel on Result<T>"
  - "AvailabilityController 409 ProblemDetails with a 'conflicts' extension"
  - "dashboard ConflictList component wired into /availability — the MGMT-03 loop complete end-to-end"
affects: [dashboard-availability-page]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Manual transaction wrapped in Database.CreateExecutionStrategy().ExecuteAsync(...) — the officially-supported EF Core pattern for combining an explicit BeginTransactionAsync with EnableRetryOnFailure, used here (unlike every prior write path in this codebase, which deliberately avoided manual transactions for the same EnableRetryOnFailure reason) because the conflict scan and the persist genuinely need one atomic unit."
    - "Result<T>.Conflicts is a side-channel property independent of T — lets Result<StylistTimeOff> (whose success Data must stay the created entity) also carry an IReadOnlyList<AvailabilityConflictDto> on the conflict path, without forcing every Result<T> generic instantiation through the same T."
    - "Full-proposed-final-state conflict evaluation: a working-hours write checks the submitted hours against the CURRENTLY persisted time off; a time-off write checks the CURRENTLY persisted hours against the existing time-off set plus the new range being added. Each write always evaluates against the true current DB state at that moment, so a multi-step Save Changes (PUT then diff) still catches every conflict at the step that introduces it."

key-files:
  created:
    - API/ZachHairStudio.Api.Tests/Features/Availability/ConflictCheckTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Availability/ConflictCheckLocalTimeTests.cs
    - API/ZachHairStudio.Shared/Features/Availability/AvailabilityConflictDto.cs
    - dashboard/components/ConflictList.tsx
  modified:
    - API/ZachHairStudio.Shared/Features/Availability/SalonTimeZone.cs
    - API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs
    - API/ZachHairStudio.Shared/Result.cs
    - API/ZachHairStudio.Api/Controllers/AvailabilityController.cs
    - dashboard/lib/api/schema.d.ts
    - dashboard/lib/useAvailability.ts
    - dashboard/app/availability/page.tsx

key-decisions:
  - "Result<T>.Conflicts is a new property independent of the T generic, not a repurposing of Data — AddTimeOffAsync's success path must keep returning the created StylistTimeOff (the frontend/tests read its Id), so the conflict list couldn't be shoehorned into the same Data field without breaking that existing, already-green contract."
  - "The scan + persist run inside Database.CreateExecutionStrategy().ExecuteAsync(...) wrapping a manual BeginTransactionAsync — the one write path in this codebase that needs a real transaction (every other write path avoids manual transactions specifically because they're incompatible with EnableRetryOnFailure unless wrapped this way)."
  - "Removing time off is never conflict-scanned — it can only widen availability, never orphan a Confirmed appointment."
  - "AvailabilityConflictDto.SalonLocalTime carries the appointment's UTC instant (Appointment.StartsAt), not a pre-formatted string — the dashboard formats it via the existing formatSalonDateTime helper, matching AppointmentDetailPanel's precedent, rather than introducing server-side date formatting."
  - "The 409 body is ProblemDetails with conflicts stuffed into .Extensions — OpenAPI/Swashbuckle has no static shape for a ProblemDetails extension property, so dashboard/lib/useAvailability.ts defines a local AvailabilityConflict TS type matching AvailabilityConflictDto by hand (the plan's documented escape hatch) rather than blocking on generator behavior."
  - "Added [ProducesResponseType] for the real 204/201 success codes alongside the new 409 on both write actions — without it, Swashbuckle drops the previously-inferred (if mis-documented, per Plan 03/04) 200 doc entirely once any explicit ProducesResponseType is present, which would have regressed the regenerated schema.d.ts typing for the success path."

patterns-established:
  - "This is the first write path in the availability feature (and one of very few in the codebase) that needs an explicit multi-statement transaction — the CreateExecutionStrategy().ExecuteAsync + BeginTransactionAsync pattern is now the reference for any future write that must combine a read-based precondition check with a conditional persist."

requirements-completed: [MGMT-03]

coverage:
  - id: D1
    description: "Saving working hours that would leave a Confirmed appointment's slot outside the new hours is hard-blocked (409) with a conflict list carrying client name, service, stylist, and salon-local time; the hours are left completely unchanged (no partial apply)."
    requirement: "MGMT-03"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/ConflictCheckTests.cs#Put_ShrinkingHoursExcludesConfirmedAppointment_Returns409WithConflictShape_AndNoPartialApply"
        status: pass
    human_judgment: false
  - id: D2
    description: "Adding time off that overlaps a Confirmed appointment is hard-blocked (409) the same way, and no time-off row persists."
    requirement: "MGMT-03"
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/ConflictCheckTests.cs#Post_TimeOffOverlapsConfirmedAppointment_Returns409_AndNoPartialApply"
        status: pass
    human_judgment: false
  - id: D3
    description: "Only Confirmed appointments ever conflict: Cancelling or marking No-show releases the slot so the same shrink then succeeds; a Completed appointment (which still retains its AppointmentSlot rows) is never flagged."
    requirement: "MGMT-03"
    verification:
      - kind: integration
        ref: "ConflictCheckTests.cs#Put_AfterCancelOrNoShowReleasesSlot_SameShrinkSucceeds (Theory: Cancelled, NoShow), #Put_CompletedAppointment_NeverAppearsInConflictList_ShrinkSucceeds"
        status: pass
    human_judgment: false
  - id: D4
    description: "Boundary correctness: a Confirmed slot ending exactly at the new closing time is allowed; a slot one 15-minute grid cell past the boundary is blocked."
    requirement: "MGMT-03"
    verification:
      - kind: integration
        ref: "ConflictCheckTests.cs#Put_BoundaryExactlyAtNewClose_IsAllowed, #Put_BoundaryOneCellPastNewClose_IsBlocked"
        status: pass
    human_judgment: false
  - id: D5
    description: "SalonTimeZone.ToSalonLocal correctly resolves the salon-local weekday/time for every AppointmentSlot.SlotStart against the salon's real fixed UTC+06:30 Asia/Yangon offset (never DST), including round-tripping with ToSalonInstant and midnight-boundary rollover."
    requirement: "MGMT-03"
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Availability/ConflictCheckLocalTimeTests.cs"
        status: pass
    human_judgment: false
  - id: D6
    description: "Idempotency and the empty case: resubmitting the same conflicting save twice returns the identical conflict set both times with no partial apply; saving availability with zero Confirmed appointments always succeeds with no conflict panel."
    requirement: "MGMT-03"
    verification:
      - kind: integration
        ref: "ConflictCheckTests.cs#Put_ConflictingSaveRepeatedTwice_ReturnsSameConflictSet_NeverPartiallyApplies, #Put_NoConfirmedAppointments_SucceedsWithNoConflictPanel"
        status: pass
    human_judgment: false
  - id: D7
    description: "The dashboard renders the rose 'Can't Save — Conflicting Appointments' panel inline below Save Changes on a blocked save, with one row per conflict, internal scroll past ~6 rows, Save Changes staying enabled for an in-place retry, and the panel visually distinct from the generic network/500 banner; a later successful save clears it."
    verification:
      - kind: other
        ref: "cd dashboard && npm run build && npm run lint — both pass clean"
        status: pass
      - kind: manual_procedural
        ref: "As staff: book a Confirmed appointment, shrink hours to exclude it, confirm the rose panel renders with the correct row shape and stays after Save Changes is clicked again; cancel the appointment and Save Changes again to confirm the panel clears and the success flash shows"
        status: unknown
    human_judgment: true
    rationale: "Visual rendering and interactive retry-after-block behavior need a human/browser pass; no dashboard test runner is configured for this project (consistent with every prior Phase 3/4 plan's precedent)."

duration: 55min
completed: 2026-07-25
status: complete
---

# Phase 4 Plan 05: Availability Conflict Check (MGMT-03) Summary

**Server-side hard-block on availability saves that would orphan a Confirmed appointment — full-proposed-final-state conflict scan wrapped in an execution-strategy transaction, plus the dashboard's rose "Can't Save — Conflicting Appointments" panel.**

## Performance

- **Duration:** ~55 min
- **Completed:** 2026-07-25
- **Tasks:** 3 (RED tests, GREEN scan/transaction/409, dashboard ConflictList wiring)
- **Files modified:** 11 (4 backend created/modified for tests+DTO, 4 backend modified for the scan/Result/controller, 3 frontend created/modified)

## Accomplishments

- `SalonTimeZone.ToSalonLocal` — the inverse of the existing `ToSalonInstant` — resolves any UTC/offset instant to its correct salon-local weekday/time via `TimeZoneInfo.ConvertTime`, proven against the salon's real fixed UTC+06:30 Asia/Yangon zone (never DST) including round-tripping and local-midnight rollover.
- `AvailabilityService.ReplaceWorkingHoursAsync` and `AddTimeOffAsync` now each run a conflict scan before persisting, evaluating the FULL proposed final state (not an old-vs-new diff): the hours write checks the submitted hours against the currently-persisted time off; the time-off write checks the currently-persisted hours against the existing time-off set plus the new range being added. Both writes are wrapped in `Database.CreateExecutionStrategy().ExecuteAsync(...)` around a manual `BeginTransactionAsync` — the officially-supported EF Core pattern for combining a real transaction with `EnableRetryOnFailure` — so a conflict rolls back cleanly and nothing partially applies (D-09).
- The scan joins `Appointment.Status == Confirmed` explicitly (never inferred from `AppointmentSlot` presence, since `AppointmentSlot` carries no status column and a Completed appointment still retains its slot rows), and only Confirmed appointments are ever flagged.
- `AvailabilityConflictDto` carries exactly the D-11 fields (clientName, serviceName, stylistName, salonLocalTime, appointmentId) — no email/phone.
- `Result<T>` gained a `ConflictError` factory and a `Conflicts` side-channel property that is independent of `T` — necessary because `AddTimeOffAsync`'s success `Data` must stay the created `StylistTimeOff` entity (the response body callers depend on) while its conflict path needs an `IReadOnlyList<AvailabilityConflictDto>`, a shape `T` can't hold on both branches.
- `AvailabilityController` translates a conflict `Result` into a 409 `ProblemDetails` with a `conflicts` extension property, extending `AppointmentsController`'s existing `Conflict(...)` 409 pattern; `[ProducesResponseType]` attributes were added for both the real success codes (204/201, correcting a prior Swashbuckle mis-documentation as 200) and the new 409.
- The dashboard's `ConflictList` component renders the rose "Can't Save — Conflicting Appointments" panel inline below Save Changes (never a modal), one row per conflict in the exact D-11 row shape, scrolling internally past ~6 rows; `useAvailability.ts`'s `saveAvailability` now throws a dedicated `AvailabilityConflictError` on a 409 from either write call, and `availability/page.tsx` catches it separately from the generic `ApiError` path so the two failure states stay visually distinct (E7) and Save Changes remains enabled for an in-place retry.
- 14 new backend tests (`ConflictCheckTests.cs`, `ConflictCheckLocalTimeTests.cs`) prove the hard block, Confirmed-only scoping, exact-boundary correctness, no-partial-apply, idempotency, and local-time correctness. Full backend suite: 152/152 green (138 baseline + 14 new, 0 regressions). Dashboard `npm run build` and `npm run lint` both pass clean.

## Task Commits

Each task was committed atomically:

1. **Task 1: RED — conflict-check + local-time correctness tests** - `0a888e4` (test)
2. **Task 2: GREEN — ToSalonLocal + conflict scan + Result/controller 409** - `9da7d26` (feat)
3. **Task 3: ConflictList panel wired into the availability page** - `3e2943c` (feat)

**Plan metadata:** (this commit)

## Files Created/Modified

- `API/ZachHairStudio.Api.Tests/Features/Availability/ConflictCheckTests.cs` - 8 integration tests over real SQL Server LocalDB: hours-shrink 409 + no-partial-apply, time-off-overlap 409 + no-partial-apply, Cancel/NoShow-releases-slot (Theory), Completed-never-flagged, exact-boundary-allowed, one-cell-past-blocked, zero-appointments-succeeds, idempotent-repeat
- `API/ZachHairStudio.Api.Tests/Features/Availability/ConflictCheckLocalTimeTests.cs` - Unit tests for `SalonTimeZone.ToSalonLocal` against the real Asia/Yangon fixed offset: weekday/time conversion, round-trip with `ToSalonInstant`, no-DST-drift across the year
- `API/ZachHairStudio.Shared/Features/Availability/SalonTimeZone.cs` - Added `ToSalonLocal(DateTimeOffset instant)`
- `API/ZachHairStudio.Shared/Features/Availability/AvailabilityConflictDto.cs` - New: clientName, serviceName, stylistName, salonLocalTime, appointmentId
- `API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs` - `FindConflictsAsync`/`SlotConflicts` scan; both writes now run inside `CreateExecutionStrategy().ExecuteAsync` + manual transaction
- `API/ZachHairStudio.Shared/Result.cs` - `ConflictError` factory, `Conflicts` side-channel property, `IsConflict()`, new `EnumRespType.Conflict`
- `API/ZachHairStudio.Api/Controllers/AvailabilityController.cs` - `ConflictProblem` helper (409 ProblemDetails + `conflicts` extension); `[ProducesResponseType]` for 204/201/409
- `dashboard/lib/api/schema.d.ts` - Regenerated: working-hours PUT and time-off POST now correctly document 204/201 (was mis-documented as 200) plus the new 409
- `dashboard/lib/useAvailability.ts` - `AvailabilityConflict` type, `AvailabilityConflictError`, 409 detection in both write calls of `saveAvailability`
- `dashboard/components/ConflictList.tsx` - New: the rose conflict panel
- `dashboard/app/availability/page.tsx` - Wired `ConflictList` into the Plan 04 seam; `conflicts` state; `AvailabilityConflictError` caught separately from the generic save-error path

## Decisions Made

See `key-decisions` in frontmatter — summarized: `Result<T>.Conflicts` is a `T`-independent side-channel (not a repurposing of `Data`, which `AddTimeOffAsync` still needs for its created-entity response); the scan+persist pair is the one write path in this codebase that genuinely needs a manual transaction, wrapped correctly via `CreateExecutionStrategy().ExecuteAsync` to stay compatible with `EnableRetryOnFailure`; removing time off is never scanned (it can only widen availability); the conflict DTO carries a raw UTC instant, not a pre-formatted string, matching the existing `AppointmentDetailPanel` formatting precedent; the 409 body's `conflicts` extension isn't representable in the generated OpenAPI schema, so the frontend defines a matching TS type by hand per the plan's documented escape hatch; `[ProducesResponseType]` was added for the real success codes too, to avoid Swashbuckle silently dropping the previously-inferred (if imperfect) success-response documentation once any explicit `ProducesResponseType` attribute is present.

## Deviations from Plan

None — plan executed exactly as written. The `[ProducesResponseType]` additions for the 204/201 success codes (alongside the plan's required 409) were a necessary consequence of adding the 409 attribute at all — Swashbuckle stops inferring the default response once any `ProducesResponseType` is present on an action — not a scope change; this is documented above as a decision rather than a deviation since it was required to keep the regenerated `schema.d.ts` from regressing the (already imperfect, per Plan 03/04) success-path typing.

## Issues Encountered

- A leftover `dotnet run` process from earlier manual verification held a file lock on `ZachHairStudio.Shared.dll`, blocking `dotnet test` after the RED→GREEN revert/restore cycle. Resolved with `taskkill` before proceeding; no code impact.
- Regenerating `schema.d.ts` against the running API (with only the 409 `[ProducesResponseType]` added) caused Swashbuckle to drop the previously-inferred 200 documentation for both endpoints entirely rather than adding 409 alongside it — resolved by also adding explicit `[ProducesResponseType]` attributes for the real success codes (204 for the hours PUT, 201 for the time-off POST), which additionally corrected a pre-existing mis-documentation (both were previously typed as a content-less 200).

## Known Stubs

None. Every new surface (conflict scan, 409 response, `ConflictList`) reads/writes real data through the existing `AvailabilityService`/`AvailabilityController`/`useAvailability` stack; no hardcoded or mock data paths.

## Threat Flags

None beyond the plan's own `<threat_model>` (T-04-08, T-04-09, T-04-10 — all mitigated as designed: the check runs server-side inside the same transaction that persists; `AvailabilityConflictDto` exposes only the D-11 fields; scan+persist share one transaction so a concurrent confirm during the scan is caught within that same unit of work, to the extent SQL Server's default isolation level allows).

## User Setup Required

None - no external service configuration required. Reused the existing `dotnet user-secrets` (RESEND_API_KEY, Owner credentials) and the established LocalDB connection-string override for the one-off local API run used to regenerate `schema.d.ts`.

## Next Phase Readiness

- MGMT-03 is complete end-to-end: the phase's sharpest correctness edge (hard-blocking a save that would orphan a Confirmed appointment) is proven server-side by 14 integration/unit tests and wired all the way through to an actionable, in-place dashboard panel.
- This closes out Phase 4 (staff-management-services-availability) — MGMT-01 (Plan 01/02), MGMT-02 (Plan 03/04), and MGMT-03 (this plan) are all now implemented and tested.
- The manual/interactive verification for D7 (visual panel rendering, in-place retry UX) remains, consistent with every prior Phase 3/4 plan's precedent (no dashboard test runner configured) — recommended as part of this phase's end-of-phase UAT pass.
- The `CreateExecutionStrategy().ExecuteAsync` + manual-transaction pattern introduced here is now the reference for any future write path in this codebase that needs to combine a read-based precondition check with a conditional persist under `EnableRetryOnFailure`.

---
*Phase: 04-staff-management-services-availability*
*Completed: 2026-07-25*

## Self-Check: PASSED

All 11 files created/modified by this plan were found on disk, and all 3 task
commits (0a888e4, 9da7d26, 3e2943c) were found in git history.
