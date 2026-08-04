---
phase: 04-staff-management-services-availability
reviewed: 2026-07-26T00:00:00Z
depth: standard
files_reviewed: 33
files_reviewed_list:
  - API/ZachHairStudio.Api.Tests/Features/Availability/ConflictCheckLocalTimeTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Availability/ConflictCheckTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Availability/TimeOffTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Availability/WorkingHoursReplaceTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Services/ServiceImageUploadTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerTests.cs
  - API/ZachHairStudio.Api/Controllers/AvailabilityController.cs
  - API/ZachHairStudio.Api/Controllers/ServicesController.cs
  - API/ZachHairStudio.Api/Program.cs
  - API/ZachHairStudio.Shared/Features/Availability/AvailabilityConflictDto.cs
  - API/ZachHairStudio.Shared/Features/Availability/AvailabilityResponseDto.cs
  - API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs
  - API/ZachHairStudio.Shared/Features/Availability/SalonTimeZone.cs
  - API/ZachHairStudio.Shared/Features/Availability/TimeOffCreateDto.cs
  - API/ZachHairStudio.Shared/Features/Availability/TimeOffCreateDtoValidator.cs
  - API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDto.cs
  - API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDtoValidator.cs
  - API/ZachHairStudio.Shared/Features/Services/ServiceImageUploadDto.cs
  - API/ZachHairStudio.Shared/Features/Services/ServiceImageUploadDtoValidator.cs
  - API/ZachHairStudio.Shared/Features/Services/ServicesService.cs
  - API/ZachHairStudio.Shared/Result.cs
  - API/ZachHairStudio.Shared/ZachHairStudio.Shared.csproj
  - dashboard/.eslintrc.json
  - dashboard/app/availability/page.tsx
  - dashboard/app/schedule/page.tsx
  - dashboard/app/services/page.tsx
  - dashboard/components/ConfirmDialog.tsx
  - dashboard/components/ConflictList.tsx
  - dashboard/components/DashboardNav.tsx
  - dashboard/components/ImageUploadField.tsx
  - dashboard/components/ServiceForm.tsx
  - dashboard/components/StylistPicker.tsx
  - dashboard/components/TimeOffCalendar.tsx
  - dashboard/components/WeekStripEditor.tsx
  - dashboard/components/icons.tsx
  - dashboard/lib/api/schema.d.ts
  - dashboard/lib/useAvailability.ts
  - dashboard/lib/useServices.ts
findings:
  critical: 1
  warning: 3
  info: 5
  total: 9
status: issues_found
---

# Phase 04: Code Review Report

**Reviewed:** 2026-07-26T00:00:00Z
**Depth:** standard
**Files Reviewed:** 33 (backend Services/Availability features, dashboard Services/Availability UI, and the WeekStripEditor gap-closure fix)
**Status:** issues_found

## Summary

Reviewed the backend `AvailabilityService`/`ServicesService` write paths (working-hours replace, time-off add/remove, hard-blocking conflict scan, image upload), their controllers, DTOs/validators, the corresponding integration tests, and the dashboard's Services and Availability UI (including `WeekStripEditor.tsx`, the just-completed drag-commit gap-closure fix).

The conflict-scan logic (`AvailabilityService.FindConflictsAsync`/`SlotConflicts`), `SalonTimeZone`'s local-time conversion, and `WeekStripEditor.tsx`'s pointer-drag state machine are all correct and well-tested — no defects found there, and the previously-noted setState-in-render class of bug does not reappear (the project's new ESLint rule guarding against it is also respected everywhere in this phase's components).

One genuine logic bug was found in the dashboard's composite availability save (`saveAvailability`'s time-off delete loop): the documented "a 404 on delete is not a save failure" retry-safety behavior is silently defeated by a boolean-logic mistake, so a stale/already-deleted time-off id can permanently block Save Changes on retry. Three further Warnings cover missing overlap validation for time-off/working-hours (both server and client), and an unbounded on-disk file leak from image re-uploads. Five Info items note smaller consistency/maintainability gaps.

## Critical Issues

### CR-01: Time-off delete's "404 is not a failure" carve-out is defeated by `|| error`, can permanently block Save Changes

**File:** `dashboard/lib/useAvailability.ts:216-231`

**Issue:** In `saveAvailability`, the loop that deletes time-off ranges the staff member removed locally is meant to tolerate a 404 (the range was already deleted, e.g. by a previous partially-failed save attempt, or by another staff member) — the comment says so explicitly:

```ts
for (const timeOffId of removedIds) {
  const { response, error } = await api.DELETE(
    "/api/Availability/{stylistId}/time-off/{timeOffId}",
    { params: { path: { stylistId, timeOffId } } }
  );
  // A range already removed server-side (404) is not a save failure.
  if ((!response.ok && response.status !== 404) || error) {
    ...
    throw new ApiError(message, response.status || null);
  }
}
```

`AvailabilityController.RemoveTimeOff` returns `NotFound(new ProblemDetails { ... })` for an unknown id — a real JSON body. `openapi-fetch` populates `error` with the parsed body whenever `response.ok` is `false`, regardless of status code. So on a 404, `response.ok` is `false` and `error` is a truthy `ProblemDetails` object. The first clause `(!response.ok && response.status !== 404)` correctly evaluates to `false` (404 is excluded) — but the `|| error` term still makes the whole condition `true`, so the loop throws anyway. The 404 carve-out is dead code; every "already deleted" case is (incorrectly) treated as a hard failure.

This is reachable in a realistic scenario: any Save Changes attempt where a delete succeeds but a later step in the same save fails (network flake, a different delete, or the trailing POST for a new range) leaves that id already deleted server-side. The user's local state still lists it as "to remove" (because `mutate()` is only called on success), so retrying Save Changes retries the same delete, hits this bug, and fails again — the user is stuck retrying a save that can never succeed until they discard their edits and reload.

**Fix:** Drop the `|| error` term (or only consult `error` alongside the same `response.status !== 404` guard):

```ts
if (!response.ok && response.status !== 404) {
  let message = "Couldn't save availability. Try again.";
  try {
    message = await extractErrorMessage(response.clone());
  } catch {
    // keep default
  }
  throw new ApiError(message, response.status || null);
}
```

## Warnings

### WR-01: No overlap/duplicate validation for time-off ranges (server + misleading client UI)

**File:** `API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs:191-202`, `dashboard/components/TimeOffCalendar.tsx:220-228,237`

**Issue:** `AddTimeOffAsync` builds the proposed final time-off set by simply appending the new range to whatever is already persisted (`currentTimeOff.Append(new TimeOffRange(...))`) and only checks that set against *confirmed appointments* — it never checks the new range against the *existing time-off ranges themselves*. Nothing prevents a staff member from creating two overlapping or fully duplicate time-off blocks for the same stylist.

The client compounds this: in `TimeOffCalendar`, the day-picker button is only disabled when `!armed && !range` (line 237) — while "armed" (add-time-off mode active), a day that's already covered by an existing range is fully clickable, so a staff member can pick a start/end date range that overlaps an existing one. Worse, the cell's visual state (lines 220-228) checks `range` before `isPendingStart`, so a day inside an existing range shows the existing "dashed muted" style even after being clicked as the pending start — the gold "selected" highlight never appears, giving no feedback that a click registered at all, right before it silently creates an overlapping range.

**Fix:** Add an overlap check in `AddTimeOffAsync` (reject/merge new ranges that overlap `currentTimeOff`), and in `TimeOffCalendar`, disable day cells already covered by a range while armed (`disabled={(!armed && !range) || (armed && Boolean(range))}`) so the picker can't select an already-blocked day.

### WR-02: `WorkingHoursReplaceDtoValidator` never rejects overlapping segments on the same day

**File:** `API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDtoValidator.cs:14-30`

**Issue:** The validator's own doc comment states this is "Server-authoritative revalidation of every submitted segment... the client is never trusted for End > Start or 15-minute grid alignment," but it only checks `EndTime > StartTime` and 15-minute grid alignment per segment — nothing rejects two segments for the same `DayOfWeek` that overlap (e.g. `Monday 09:00-12:00` and `Monday 10:00-13:00`). The dashboard's `WeekStripEditor.mergeSegments` happens to prevent this client-side, but the server contradicts its own stated "never trust the client" intent: any other API caller (a script, a future admin tool) can persist overlapping/duplicate rows for the same stylist/day.

**Fix:** Add a validator rule (or a check in `ReplaceWorkingHoursAsync`) that rejects a payload containing two segments for the same `DayOfWeek` whose `[StartTime, EndTime)` ranges overlap.

### WR-03: Re-uploading a service image never deletes the previous file — unbounded orphaned-file growth

**File:** `API/ZachHairStudio.Api/Controllers/ServicesController.cs:119-161`, `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs:84-96`

**Issue:** `UploadImage` always writes the new file under a fresh `Path.GetRandomFileName()` and calls `SetImageAsync`, which only overwrites the `ImageUrl` string column — the previously-referenced file on disk is never looked up or deleted. `ServiceImageUploadTests.UploadImage_UploadedTwice_ImageUrlReflectsNewestFile` only asserts the pointer changed, not that the old file was cleaned up. Every re-upload (there is no cap on how many times an Owner can replace a service's photo) permanently leaves the previous file behind in `wwwroot/uploads/services/`, growing without bound over the life of the app.

**Fix:** Before (or after) writing the new file, read the service's current `ImageUrl`, resolve it back to a physical path under the uploads folder, and delete it (best-effort, matching the existing cleanup pattern) once the new `ImageUrl` is committed.

## Info

### IN-01: Bare `catch { }` on best-effort file cleanup swallows all errors with no logging

**File:** `API/ZachHairStudio.Api/Controllers/ServicesController.cs:149-156`

**Issue:** After `SetImageAsync` returns `NotFound`, the just-written file is deleted in a `try { System.IO.File.Delete(filePath); } catch { /* best-effort cleanup */ }`. This is a deliberate, documented best-effort delete, but swallowing every exception type with no logging means a real problem (e.g. a permissions issue on the uploads folder) is invisible to operators; only a code comment records the intent.

**Fix:** At minimum log the caught exception (e.g. via `ILogger`) before discarding it, so a persistent failure to clean up is discoverable.

### IN-02: Validators are run twice on every write (controller + service layer)

**File:** `API/ZachHairStudio.Api/Controllers/AvailabilityController.cs:60-65,103-108`, `API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs:108-113,168-173`, and the equivalent pattern in `ServicesController.cs`/`ServicesService.cs`

**Issue:** `AvailabilityController.ReplaceWorkingHours`/`AddTimeOff` (and `ServicesController.CreateService`/`UpdateService`) call `_workingHoursValidator.ValidateAsync(request)` (etc.) and return a `ValidationProblem` on failure — then `AvailabilityService.ReplaceWorkingHoursAsync`/`AddTimeOffAsync` (and `ServicesService.CreateAsync`/`UpdateAsync`) independently re-run the exact same validator again. This isn't incorrect today (both call sites use the same validator instance/rules), but it's duplicated logic that has to be kept in sync by hand — a future edit to add a rule to only one of the two call sites would silently create inconsistent enforcement between the controller-level 400 shape and the service-level `ValidationError` shape.

**Fix:** Pick one layer as authoritative (service layer is the natural choice since it also owns persistence) and have the controller rely on the service's `Result.IsValidationError()` path exclusively, or vice versa.

### IN-03: `IsSystemError()` branches in `AvailabilityController` are unreachable dead code

**File:** `API/ZachHairStudio.Api/Controllers/AvailabilityController.cs:90-93,133-136,157-160`

**Issue:** The controller checks `result.IsSystemError()` after every `AvailabilityService` call and returns a 500 `InconsistentDataProblem`, but none of `AvailabilityService`'s methods (`ReplaceWorkingHoursAsync`, `AddTimeOffAsync`, `RemoveTimeOffAsync`) ever construct a `Result.SystemError(...)` — every one of these branches is currently unreachable. This mirrors a pattern copied from `ScheduleController`, but here it's speculative defensive code with no corresponding producer, which can mask the fact that the "inconsistent data" scenario it's meant to guard against isn't actually handled anywhere.

**Fix:** Either wire up a real `SystemError` producer (e.g. a stylist that resolves in one query but not another mid-transaction) or remove the dead branches until they're needed.

### IN-04: `GetBySlugAsync` never honors an Owner's `includeInactive`, unlike the list endpoint

**File:** `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs:37-45`

**Issue:** `GetServicesAsync(includeInactive)` explicitly supports an Owner-only "show retired services too" mode (`ServicesController.GetServices`, DD-1). `GetBySlugAsync` has no equivalent — it unconditionally filters `service.IsActive`, so even an authenticated Owner gets a 404 fetching a single retired service by slug (e.g. via `GET /api/services/{slug}`, or the `CreatedAtAction`/`Location` header pattern if a service were retired shortly after creation). The current dashboard avoids this by editing from the already-fetched `includeInactive=true` list rather than re-fetching by slug, so there's no active user-facing break today, but the inconsistency is a latent trap for the next feature that calls this endpoint expecting parity with the list endpoint's authorization model.

**Fix:** Either accept an `includeInactive`/role parameter here too, or document explicitly that this endpoint is public-catalog-only by design.

### IN-05: `GetAvailabilityAsync`'s doc comment ("upcoming/active time-off") doesn't match the implementation (returns all rows, unfiltered by date)

**File:** `API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs:48-52,73-83`

**Issue:** The method's doc comment describes returning "their upcoming/active time-off blocks," but the query (`_dbContext.StylistTimeOff.Where(off => off.StylistId == stylistId)...`) applies no date filter at all — every time-off row ever created for the stylist, including ranges that ended months ago, is returned indefinitely. This is harmless functionally (the dashboard's `TimeOffCalendar` only renders whichever month is currently in view), but the comment overstates what the code does, and the unbounded historical set is returned to the client on every load.

**Fix:** Either filter to `off.EndsAt >= <now>` if "upcoming/active" is the intended contract, or correct the comment to describe the actual (full-history) behavior.

---

_Reviewed: 2026-07-26T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
