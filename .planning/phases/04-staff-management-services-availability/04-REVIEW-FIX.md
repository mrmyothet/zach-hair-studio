---
phase: 04-staff-management-services-availability
fixed_at: 2026-07-27T00:12:00Z
review_path: .planning/phases/04-staff-management-services-availability/04-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 04: Code Review Fix Report

**Fixed at:** 2026-07-27T00:12:00Z
**Source review:** .planning/phases/04-staff-management-services-availability/04-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 4 (1 Critical, 3 Warning — `fix_scope: critical_warning`, Info findings excluded)
- Fixed: 4
- Skipped: 0

## Fixed Issues

### CR-01: Time-off delete's "404 is not a failure" carve-out is defeated by `|| error`, can permanently block Save Changes

**Files modified:** `dashboard/lib/useAvailability.ts`
**Commit:** bc14251
**Applied fix:** Dropped the `|| error` term from the delete-loop's failure condition so it reads `if (!response.ok && response.status !== 404)`, restoring the documented 404 carve-out. Also removed the now-unused `error` destructure from that `api.DELETE(...)` call to keep the code clean (no other call site in the file depends on it).

## Warnings — Fixed

### WR-01: No overlap/duplicate validation for time-off ranges (server + misleading client UI)

**Files modified:** `API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs`, `dashboard/components/TimeOffCalendar.tsx`
**Commit:** e57563c
**Applied fix:**
- Server: in `AddTimeOffAsync`, added an overlap check against `currentTimeOff` (mirroring `SlotConflicts`'s half-open interval test, `start < otherEnd && end > otherStart`) before building the proposed final time-off set. An overlapping/duplicate new range now returns a `ValidationError` (surfaced as a 400 by `AvailabilityController`) instead of silently persisting.
- Client: `TimeOffCalendar`'s day-cell button now uses `disabled={(!armed && !range) || (armed && Boolean(range))}` (per the review's suggested fix), so a day already covered by an existing range can no longer be clicked while "Add Time Off" is armed. This also resolves the reported visual-feedback gap, since a covered day can no longer register a click as a pending start in the first place.
- Verified: `dotnet build` succeeds; existing `WorkingHoursReplaceTests`/availability suite unaffected (not directly exercised by this change, so relied on build + Tier-1 re-read).

### WR-02: `WorkingHoursReplaceDtoValidator` never rejects overlapping segments on the same day

**Files modified:** `API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDtoValidator.cs`
**Commit:** 3f5cb3b
**Applied fix:** Added a `RuleFor(x => x.Segments).Must(NotHaveOverlappingSegmentsOnSameDay)` rule. The new `NotHaveOverlappingSegmentsOnSameDay` helper groups segments by `DayOfWeek`, orders each group by `StartTime`, and rejects the payload if any two adjacent segments in a group overlap (`ordered[i].StartTime < ordered[i - 1].EndTime`), catching both overlaps and exact duplicates.
**Verified:** `dotnet build` succeeds; `dotnet test --filter FullyQualifiedName~WorkingHoursReplaceTests` — 6/6 passed (no regression in existing single-segment-per-day test coverage).

### WR-03: Re-uploading a service image never deletes the previous file — unbounded orphaned-file growth

**Files modified:** `API/ZachHairStudio.Api/Controllers/ServicesController.cs`, `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs`
**Commit:** 3134ca8
**Applied fix:** Added `ServicesService.GetImageUrlAsync(id)` to read a service's current `ImageUrl` without side effects. `ServicesController.UploadImage` now calls it before writing the new file, and — once `SetImageAsync` has committed the new `ImageUrl` — best-effort deletes the previous physical file (re-deriving the filename via `Path.GetFileName` rather than trusting the stored string directly, matching the existing path-traversal-safe pattern; only acts on values scoped to `/uploads/services/`).
**Verified:** `dotnet build` succeeds; `dotnet test --filter FullyQualifiedName~ServiceImageUploadTests` — 6/6 passed (existing "uploaded twice reflects newest file" test still passes; a dedicated "previous file deleted" test was not added here, as add-tests is out of scope for the fixer).

## Skipped Issues

None — all in-scope findings were fixed.

---

_Fixed: 2026-07-27T00:12:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
