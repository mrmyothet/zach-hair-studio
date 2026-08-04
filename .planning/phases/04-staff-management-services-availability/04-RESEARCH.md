# Phase 4: Staff Management (Services & Availability) - Research

**Researched:** 2026-07-24
**Domain:** ASP.NET Core CRUD + file upload + availability-conflict scheduling logic, consumed by a Next.js/React staff dashboard
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** **Owner-only** service create/edit/retire. Staff role uses schedule
  and availability; catalog merchandising stays with Owner (same Owner-gated
  pattern as Phase 3 add-staff).
- **D-02:** **Soft-retire** via existing `Service.IsActive = false`. Public
  catalog and booking continue to filter active services. No hard delete.
- **D-03:** **File upload** for service images from the dashboard. Owner
  uploads an image; API stores it and sets `ImageUrl`. This delivers Phase 1's
  deferred "image management arrives with Phase 4 CRUD" promise (D-08 there).
- **D-04:** **Edit price/duration freely** with no warn/block when future
  appointments exist. Existing appointments keep their already-written
  `AppointmentSlot` cells (duration at book time). Response DTOs that join
  live `Service` will show updated name/price/duration on reads — accepted.
- **D-05:** **Visual week strip** for recurring weekly hours — drag/select
  ranges per weekday on a compact week grid, persisted to
  `StylistWorkingHours` (same table Phase 2 `SlotService` reads).
- **D-06:** **No separate Break entity.** Recurring midday "breaks" (e.g.
  lunch) are modeled as **gaps in the week-strip hours** (split segments or
  shorter day span). Do not extend `StylistTimeOff` into a recurring weekly
  pattern this phase.
- **D-07:** **`StylistTimeOff` is one-off / date-range only** (vacation, sick,
  holiday, ad-hoc blocks). Entry UX is a **calendar overlay** where staff
  paint blocked ranges on a month/week calendar next to the hours strip.
- **D-08:** Slot math stays `hours − timeOff − bookedCells` — no second
  availability system (Phase 2 D-06 / roadmap constraint).
- **D-09:** **Hard block** conflicting availability saves. Refuse the write;
  staff must cancel/reschedule conflicting Confirmed appointments first. No
  Owner override, no partial apply.
- **D-10:** Conflicts are **Confirmed** appointments only (Cancelled/NoShow
  already release slots; Completed is historical and out of scope for the
  check).
- **D-11:** Surfaced as an **inline conflict list**: client name, service,
  stylist, salon-local time — enough to act without deep-linking into
  schedule (deep links are nice-to-have, not required).
- **D-12:** Conflict check runs on **both** shrinking/removing weekly hours
  **and** adding/extending time off.
- **D-13:** **Any authenticated staff** may edit **any** stylist's
  availability (hours + time off). Services remain Owner-only (D-01).
- **D-14:** **Stylist picker only** — choose among existing active stylists.
  No create/edit/retire stylist UI this phase.
- **D-15:** **Defer stylist↔service capability matrix** — all stylists still
  perform all services (Phase 2 D-08 stays deferred; not in MGMT-*).
- **D-16:** Dashboard nav: **two items** — **Services** (Owner-only; hide or
  403 for Staff) and **Availability** (all staff), alongside existing
  Schedule / add-staff.

### Claude's Discretion

- Image storage backend (local `wwwroot`/static folder vs blob), allowed MIME
  types, max size, and whether `ImageUrl` stays a public path vs signed URL.
- Exact week-strip interaction (drag vs click-paint), closed-day affordance,
  and how multiple ranges per weekday map to `StylistWorkingHours` rows.
- Conflict API shape (400 vs 409 ProblemDetails), look-ahead window for
  scanning Confirmed appointments, and timezone labeling consistent with
  Asia/Yangon (Phase 2 D-16).
- Authz wiring: `[Authorize(Roles = Owner)]` on service write endpoints;
  `[Authorize]` on availability write endpoints; gate existing unauthenticated
  Service POST/PUT.
- OpenAPI client regen for new/extended endpoints; empty/loading/error states
  matching Phase 3 utilitarian dashboard chrome (D-15 there).
- Whether retiring a service should be blocked when it is the only remaining
  bookable service (edge case) — default: allow soft-retire.

### Deferred Ideas (OUT OF SCOPE)

- **Stylist↔service capability matrix** — parked from Phase 2 D-08; discussed
  in Phase 4 and deferred again. Remains "all stylists perform all services"
  until a later phase/backlog item.
- **Stylist create / soft-retire UI** — picker-only this phase; roster CRUD
  is a future capability.
- **Owner override on conflicting availability** — rejected; hard block only.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MGMT-01 | Staff can create, edit, and retire services (name, description, duration, price) | `Service`/`ServicesService`/`ServicesController` already exist and just need Owner-role gating (action-level, not class-level — Pitfall 5) plus a new image-upload endpoint (`IFormFile` + `UseStaticFiles`, no new package); soft-retire is just `UpdateAsync` with `IsActive=false` (already supported by `ServiceUpdateDto`) |
| MGMT-02 | Staff can manage stylist availability (working hours, breaks, time off) feeding the P2 slot logic | New `AvailabilityController`/`AvailabilityService` writing directly into the existing `StylistWorkingHours`/`StylistTimeOff` tables `SlotService` already reads — see Architecture Patterns' Recommended Project Structure and System Architecture Diagram; breaks modeled as segment gaps per D-06, no schema change |
| MGMT-03 | Availability edits are checked against existing confirmed bookings and surface conflicts | See Common Pitfalls (Pitfalls 1–3) and Architecture Pattern 4 for the conflict-detection algorithm (full-proposed-final-state evaluation, Confirmed-only filter, `SalonTimeZone`-mediated local-time comparison) and Validation Architecture's `ConflictCheckTests` test map row |

</phase_requirements>

## Summary

Phase 4 is almost entirely a **wiring and query-writing** phase, not a new-technology phase. Every
persistence model it touches already exists and ships in production code from Phases 1–2:
`Service` (with `ImageUrl` already a column), `StylistWorkingHours`, `StylistTimeOff`, `Appointment`
+ `AppointmentSlot` + `AppointmentStatus.Confirmed`. There is no ORM, auth, or validation library to
introduce — FluentValidation, EF Core 10 / SQL Server, ASP.NET Core Identity + JWT bearer auth, and
the `Result<T>` + ProblemDetails error-shape convention are all already wired and must be reused
verbatim. The only genuinely new backend surface is (1) an image-upload endpoint using the framework's
built-in `IFormFile` + `UseStaticFiles`/`PhysicalFileProvider` (no new NuGet package), and (2) two new
availability write endpoints (working-hours replace, time-off CRUD) plus one conflict-checking query
that must reuse `SlotService`'s exact cell-matching logic rather than re-deriving it.

The single highest-risk piece of this phase is the **availability-conflict check** (MGMT-03): it must
scan `Confirmed`-only appointments (`AppointmentSlot` rows joined to `Appointment.Status`), compare
each cell's *salon-local* day/time against the *proposed* (not diffed) final `StylistWorkingHours` +
`StylistTimeOff` state, and hard-block the save with a structured conflict list if anything falls
outside. Getting the local-time conversion wrong, or diffing old-vs-new instead of validating the
final state, is the most likely source of a subtly-wrong implementation.

**Primary recommendation:** Add two new feature-folder write surfaces
(`API/ZachHairStudio.Shared/Features/Services/` extensions for image upload + Owner-role gating, and
a new `API/ZachHairStudio.Shared/Features/Availability/` write path) that call into `SlotService`'s
existing `SalonTimeZone`/grid helpers for all local-time math — never re-derive DayOfWeek/TimeOnly
conversion independently. Evaluate conflicts against the **full proposed final state**, not an
old-vs-new diff. No new NuGet or npm packages are required this phase.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Service CRUD + soft-retire (MGMT-01) | API / Backend (`ServicesService`, `ServicesController`) | Frontend Server (dashboard form) | Business rules (validation, `IsActive` filtering) belong server-side; dashboard is a thin form over the existing DTO contract |
| Service image upload/storage | API / Backend (new upload endpoint + `wwwroot`/static folder) | CDN / Static (served via `UseStaticFiles`) | File persistence and validation (MIME/size) must be server-enforced; serving is a static-file concern layered on top |
| Availability editor — working hours (MGMT-02) | API / Backend (new write endpoint over `StylistWorkingHours`) | Browser / Client (`WeekStripEditor` drag-paint UI) | The persisted model and the read path (`SlotService`) already live in the API; the client only paints/serializes ranges |
| Availability editor — time off (MGMT-02) | API / Backend (new write endpoint over `StylistTimeOff`) | Browser / Client (`TimeOffCalendar`) | Same split as working hours — server owns persistence + validation |
| Conflict detection on availability save (MGMT-03) | API / Backend (new query inside the availability write path) | — | Must run server-side against authoritative `Appointment`/`AppointmentSlot` data; a client-side check could never be trusted and would double the slot-math logic |
| Open-slot computation (already exists, must stay authoritative) | API / Backend (`SlotService`) | — | Untouched this phase — availability writes must feed the *same* tables this already reads, per D-08 |
| Dashboard nav + role visibility (D-16) | Frontend Server (Next.js dashboard, client component) | API / Backend (`[Authorize(Roles=...)]` as the real gate) | UI hiding is UX only; the controller-level role check is the actual security boundary |
| Auth (JWT, Owner/Staff roles) | API / Backend (ASP.NET Core Identity + JWT, already built) | Browser / Client (`requireAuth()`, `localStorage` session) | No new work — Phase 4 only adds `[Authorize]`/`[Authorize(Roles=Owner)]` attributes to new/existing controllers |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core / `Microsoft.AspNetCore.Http.IFormFile` | 10 (in-box, no package) | Multipart file upload binding | Framework-native `IFormFile` model binding is the standard ASP.NET Core mechanism for file uploads; no third-party upload library needed [CITED: github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/mvc/models/file-uploads.md] |
| `Microsoft.Extensions.FileProviders.PhysicalFileProvider` + `app.UseStaticFiles(StaticFileOptions)` | 10 (in-box, no package) | Serve uploaded images back out under a stable `ImageUrl` path | Standard ASP.NET Core pattern for serving files from a folder other than `wwwroot`, or a subfolder within it [CITED: github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/fundamentals/static-files.md] |
| FluentValidation | already in repo (`ServiceCreateDtoValidator` etc.) | DTO validation for new/extended DTOs (image metadata, hours/time-off write DTOs) | PLAT-02 mandates FluentValidation project-wide; matches every existing validator [VERIFIED: `API/ZachHairStudio.Shared/Features/Services/ServiceCreateDtoValidator.cs`] |
| EF Core 10 / SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`) | 10.0.9 (already pinned) | Persistence for `Service`, `StylistWorkingHours`, `StylistTimeOff` | Already the project's only data layer; `BookingDbContext` already exposes all three `DbSet`s [VERIFIED: `API/ZachHairStudio.Shared/Db/BookingDbContext.cs`] |
| ASP.NET Core Identity + JWT bearer (`[Authorize]`, `[Authorize(Roles = "Owner")]`) | already in repo | Gate new/existing write endpoints | `StaffUsersController`/`ScheduleController` already establish both the class-level `[Authorize]` (any staff) and `[Authorize(Roles = StaffRoles.Owner)]` (Owner-only) patterns to copy exactly [VERIFIED: `API/ZachHairStudio.Api/Controllers/StaffUsersController.cs`, `ScheduleController.cs`] |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `openapi-typescript` + `openapi-fetch` | 7.13.0 / 0.17.0 (already in `dashboard/package.json`) | Regenerate/extend the typed TS client after new endpoints exist | Run after every API contract change, per the `openapi-client` skill |
| SWR | 2.4.2 (already in `dashboard/package.json`) | Data fetching/polling for the new Services list and Availability page | Matches `useSchedule.ts`'s existing SWR hook pattern — mirror it for `useServices`/`useAvailability` hooks |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Local `wwwroot`/static-folder image storage | Azure Blob Storage / S3-compatible object storage | Blob storage scales better and survives redeploys without a persistent disk, but breaks the "LocalDB + `next dev` + `dotnet run` is enough" dev-simplicity constraint and adds a new dependency + credentials to manage. Local storage is the CLAUDE.md-consistent default; flagged as Claude's Discretion in CONTEXT.md and confirmed here as the recommendation. |
| Diffing old-vs-new availability state for conflicts | Recomputing conflicts against the full proposed final state | Diffing requires tracking "what changed" and is more error-prone (must correctly classify shrink vs. grow per segment); evaluating the full final state against confirmed appointments is simpler, is provably equivalent (an appointment could not already be conflicting against the *pre-edit* state, since it was booked through `SlotService`), and is what this research recommends. |
| A dedicated `Break` entity (rejected by D-06) | Gaps in week-strip segments | Explicitly decided against in CONTEXT.md; documented here only so the planner doesn't reintroduce it. |

**Installation:**
```bash
# No new packages required this phase — IFormFile, UseStaticFiles, FluentValidation, EF Core,
# and ASP.NET Core Identity/JWT are already present in API/ZachHairStudio.Api and
# API/ZachHairStudio.Shared. dashboard/ already has openapi-fetch, openapi-typescript, and swr.
```

**Version verification:** No new package versions to verify — this phase adds zero new
dependencies to either `API/ZachHairStudio.Api.csproj`, `API/ZachHairStudio.Shared.csproj`, or
`dashboard/package.json`. Existing pinned versions (EF Core 10.0.9, Swashbuckle 10.0.1, Next.js
15.3.0, openapi-fetch 0.17.0) are unaffected by this phase's scope.

## Package Legitimacy Audit

**No new packages are introduced by this phase.** Every capability MGMT-01/02/03 need
(`IFormFile`, `PhysicalFileProvider`, `UseStaticFiles`, FluentValidation, EF Core, ASP.NET Core
Identity/JWT, `openapi-fetch`/`openapi-typescript`/`swr`) is already a dependency of the repo as of
Phase 1–3. The Package Legitimacy Gate is not applicable — there is nothing to check via
`npm view`/`pip index versions`/`cargo search` or the `package-legitimacy check` seam.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| *(none — no new packages)* | — | — | — | — | — | N/A |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Owner/Staff browser (dashboard/)
        │
        ├─ /services  (Owner-only) ──┐
        │                            │
        └─ /availability (any staff) │
                                      ▼
                    ┌─────────────────────────────────┐
                    │  Next.js dashboard (typed client) │
                    │  api.POST/PUT/GET via openapi-fetch│
                    └───────────────┬────────────────────┘
                                    │  Authorization: Bearer <JWT>
                                    ▼
        ┌───────────────────────────────────────────────────────────┐
        │ ASP.NET Core API (ZachHairStudio.Api)                     │
        │                                                            │
        │  ServicesController          AvailabilityController (new) │
        │  [Authorize(Roles=Owner)]    [Authorize] (any staff)       │
        │   ├─ POST/PUT (existing,     ├─ PUT /working-hours/{id}   │
        │   │   now gated)             │    (replace week for       │
        │   ├─ POST /{id}/image (new)  │    stylist)                │
        │   │   IFormFile → validate   ├─ POST/DELETE /time-off     │
        │   │   → save to static dir   │    (add/remove range)      │
        │   │   → set ImageUrl         │  → ConflictCheckService    │
        │   │                          │     (new, in Availability  │
        │   │                          │      feature folder)      │
        │   ▼                          ▼                             │
        │ ServicesService          AvailabilityService (new)        │
        │  (existing, extended       - validates DTOs                │
        │   with Retire helper)      - loads Confirmed appts+slots   │
        │                            - reuses SalonTimeZone for      │
        │                              local-time conversion         │
        │                            - either persists (SaveChanges) │
        │                              or returns 409 conflict list  │
        │                                       │                    │
        │                                       ▼                    │
        │                          BookingDbContext (EF Core)        │
        │            Services · StylistWorkingHours · StylistTimeOff│
        │            · Appointments · AppointmentSlots (unchanged)   │
        └───────────────────────────────────────────────────────────┘
                                    │
                                    ▼
                    SlotService.GetOpenSlotsAsync (UNCHANGED — Phase 2)
                    reads the same StylistWorkingHours/StylistTimeOff rows
                    the new write path just persisted, so public booking
                    reflects staff edits immediately (D-08).
```

### Recommended Project Structure
```
API/ZachHairStudio.Shared/Features/
├── Services/
│   ├── Service.cs                          # unchanged
│   ├── ServiceCreateDto.cs / ServiceUpdateDto.cs   # unchanged shape
│   ├── ServiceImageUploadDto.cs            # NEW — wraps IFormFile + metadata
│   ├── ServiceImageUploadDtoValidator.cs   # NEW — MIME allowlist + size limit
│   ├── ServicesService.cs                  # extended: SetImageAsync, Retire is just UpdateAsync(IsActive=false)
│   └── ServiceExtensions.cs                # unchanged
├── Availability/
│   ├── StylistWorkingHours.cs / StylistTimeOff.cs / SlotService.cs / SalonTimeZone.cs   # unchanged (read path)
│   ├── WorkingHoursReplaceDto.cs           # NEW — full week of segments for one stylist
│   ├── WorkingHoursReplaceDtoValidator.cs  # NEW
│   ├── TimeOffCreateDto.cs                 # NEW
│   ├── TimeOffCreateDtoValidator.cs        # NEW
│   ├── AvailabilityConflictDto.cs          # NEW — client/service/stylist/local-time row shape (D-11)
│   └── AvailabilityService.cs              # NEW — orchestrates replace + conflict check
API/ZachHairStudio.Api/Controllers/
├── ServicesController.cs                   # add [Authorize(Roles=Owner)] class-level + POST {id}/image
├── AvailabilityController.cs                # NEW — [Authorize] class-level, staff-only, any stylist
dashboard/
├── app/services/page.tsx                    # NEW (Owner-only, mirrors staff/new/page.tsx)
├── app/availability/page.tsx                # NEW (all staff)
├── components/ServiceForm.tsx, ImageUploadField.tsx, StylistPicker.tsx,
│              WeekStripEditor.tsx, TimeOffCalendar.tsx, ConflictList.tsx, DashboardNav.tsx  # NEW
├── lib/useServices.ts, useAvailability.ts   # NEW SWR hooks mirroring useSchedule.ts
```

### Pattern 1: Owner-only vs. any-staff controller gating
**What:** Class-level `[Authorize]` for "any authenticated staff," class-level
`[Authorize(Roles = StaffRoles.Owner)]` for Owner-only — never a per-action attribute when the whole
controller shares one gate.
**When to use:** `ServicesController` write actions → Owner-only (D-01). New `AvailabilityController`
→ any staff (D-13). `ScheduleController` already demonstrates the any-staff form; `StaffUsersController`
already demonstrates the Owner-only form.
**Example:**
```csharp
// Source: API/ZachHairStudio.Api/Controllers/StaffUsersController.cs (existing, verified)
[ApiController]
[Route("api/staff-users")]
[Authorize(Roles = StaffRoles.Owner)]
public class StaffUsersController : ControllerBase { /* ... */ }

// Source: API/ZachHairStudio.Api/Controllers/ScheduleController.cs (existing, verified)
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScheduleController : ControllerBase { /* ... */ }
```
Note: `ServicesController`'s `GET` actions must stay anonymous (public catalog browsing, CAT-01)
even after the class picks up an Owner-only gate — so the gate must be applied per-action
(`[Authorize(Roles = StaffRoles.Owner)]` on `CreateService`/`UpdateService`/the new image-upload
action only), NOT at the class level, unlike `StaffUsersController` which has no public actions.

### Pattern 2: `Result<T>` + ProblemDetails error shape (reuse, don't reinvent)
**What:** Service methods return `Result<T>` (`Success`/`ValidationError`/`NotFoundError`/
`DuplicateRecordError`/`SystemError`); controllers translate each `Result` state to the matching
HTTP status + `ProblemDetails`.
**When to use:** Every new service method in `AvailabilityService` and the image-upload path. The
conflict-blocked save is a new `Result` outcome — recommend adding
`Result<T>.ConflictError(message, data)` (or reuse `DuplicateRecordError`'s 409 mapping) rather than
inventing a parallel error-handling path.
**Example:**
```csharp
// Source: API/ZachHairStudio.Api/Controllers/AppointmentsController.cs (existing, verified)
if (result.IsDuplicateRecord())
{
    return Conflict(new ProblemDetails
    {
        Title = "Slot taken",
        Detail = result.Message,
        Status = StatusCodes.Status409Conflict,
    });
}
```

### Pattern 3: File upload with random filename + explicit allowlist
**What:** Never trust the client-supplied file name for storage; validate content-type/extension
against an explicit allowlist and size against an explicit max before touching disk.
**When to use:** The new service-image upload endpoint.
**Example:**
```csharp
// Source: github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/blazor/file-uploads.md [CITED]
var trustedFileNameForFileStorage = Path.GetRandomFileName();
var path = Path.Combine(env.ContentRootPath, "wwwroot", "uploads", "services",
    trustedFileNameForFileStorage + extension);
await using var fs = new FileStream(path, FileMode.Create);
await file.CopyToAsync(fs);
```

### Pattern 4: Salon-local time conversion — always through `SalonTimeZone`
**What:** Every conversion between a stored `DateTimeOffset` instant and salon-local wall-clock
(`DayOfWeek`, `TimeOnly`) must go through the existing `SalonTimeZone` helper — never
`DateTime.Now`, never a hardcoded UTC+6:30 offset.
**When to use:** The conflict-check query, when converting each `AppointmentSlot.SlotStart` (UTC
instant) into the local weekday + time-of-day to compare against proposed `StylistWorkingHours`.
**Example:**
```csharp
// Source: API/ZachHairStudio.Shared/Features/Availability/SalonTimeZone.cs (existing, verified)
// SalonTimeZone has no "instant -> local wall clock" method yet (only the reverse,
// local -> instant). The conflict check will need the INVERSE conversion — recommend
// adding a small `ToSalonLocal(DateTimeOffset instant)` method alongside the existing
// `ToSalonInstant(DateTime localWallClock)`, using TimeZoneInfo.ConvertTime, so both
// directions live in the same single-source-of-truth helper (Pitfall 5's rule cuts both ways).
```

### Anti-Patterns to Avoid
- **A second availability model/table:** D-08 is explicit — the new write path must target the
  exact same `StylistWorkingHours`/`StylistTimeOff` tables `SlotService` already reads. Do not add a
  parallel "editable schedule" table and sync it.
- **Diffing old-vs-new hours/time-off to find conflicts:** unnecessarily complex and error-prone.
  Validate the full proposed final state against Confirmed appointments instead (see Common Pitfalls).
- **Trusting the client's local-time math for the week-strip:** the frontend paints in salon-local
  time for UX, but the API must independently validate/convert everything server-side — never trust
  an echoed `DayOfWeek`/`TimeOnly` pairing without revalidating against the salon timezone.
- **A per-image resize/thumbnail pipeline:** out of scope. No image-processing library is needed —
  store the uploaded file as-is and let the `<img>` tag/CSS handle sizing (matches the UI-SPEC's
  fixed 160×160 box).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Salon-local <-> UTC conversion | A second ad-hoc offset calculation for the conflict check | `SalonTimeZone` (extend with the missing instant→local direction) | Already handles DST/ambiguous-time edge cases correctly (Phase 2's `DstBoundaryTests`/`DstRoundTripTests` prove it); a second implementation risks silently drifting from the one `SlotService` uses |
| File upload validation | Hand-rolled multipart parsing | ASP.NET Core's built-in `[FromForm] IFormFile` model binding | Framework-native, already battle-tested, no new dependency |
| Auth/roles | A custom claims-checking middleware | `[Authorize]` / `[Authorize(Roles = StaffRoles.Owner)]` | Already wired end-to-end (JWT issuance, validation, role claims) since Phase 3 |
| Error response shape | A new error envelope for conflicts | `Result<T>` + `ProblemDetails` (extend with a Conflict/409 case) | Every other endpoint in the codebase already follows this shape; a new shape would be inconsistent and harder for the dashboard's shared `extractErrorMessage` helper to parse |
| Typed API client | Hand-written `fetch` calls in `dashboard/` | Regenerate via `openapi-typescript` + `openapi-fetch` (the `openapi-client` skill) | OpenAPI is the declared source of truth (CLAUDE.md); hand-written fetches drift |

**Key insight:** This phase's complexity is almost entirely in getting the *existing* availability
model's semantics right (local time, grid alignment, Confirmed-only filtering) — not in choosing new
tools. Every "don't hand-roll" item above already has a working, tested implementation elsewhere in
this exact codebase; the job is to extend those, not replace them.

## Runtime State Inventory

> Not applicable — this is a net-new-capability phase (new write endpoints over existing tables), not
> a rename/refactor/migration phase. No stored data, live service config, OS-registered state, secrets,
> or build artifacts carry an old name that needs updating.

## Common Pitfalls

### Pitfall 1: Diffing old-vs-new availability state instead of validating the final state
**What goes wrong:** An implementation that tries to compute "which hours were removed" and "which
time-off ranges were added" as two separate diff operations, then checks each diff independently,
is significantly more complex and prone to missing a case (e.g., a stylist's hours are simultaneously
shrunk AND a time-off range is added in the same save — D-12 requires both to be checked together).
**Why it happens:** "Check what changed" feels like the natural framing of D-12's wording ("shrinking/
removing weekly hours" and "adding/extending time off").
**How to avoid:** Evaluate the conflict check against the **full proposed final state** in one pass:
for every Confirmed `AppointmentSlot` belonging to the target stylist, check (a) is this cell covered
by any segment in the *proposed* `StylistWorkingHours` for its local weekday, and (b) does this cell
fall inside any range in the *proposed* final `StylistTimeOff` set (existing + new). If either check
fails, it's a conflict — regardless of whether that specific row changed this save. This is
mathematically equivalent to a true diff (an appointment could never have been bookable inside
pre-existing time off or outside pre-existing hours, since `SlotService` enforces that at booking
time) but is far simpler to implement and to unit test.
**Warning signs:** Conflict-check code that tracks "was this StylistWorkingHours row present before"
separately from "is it present after."

### Pitfall 2: Comparing `AppointmentSlot.SlotStart` (UTC instant) against `TimeOnly` without local conversion
**What goes wrong:** `StylistWorkingHours.StartTime`/`EndTime` are `TimeOnly` in **salon-local** wall
clock. `AppointmentSlot.SlotStart` is a UTC-anchored `DateTimeOffset`. Comparing them directly (e.g.
`slot.SlotStart.TimeOfDay` against `hours.StartTime`) silently uses whatever offset the
`DateTimeOffset` happens to carry — which may not be the salon's offset — producing wrong conflict
results that won't be caught by simple manual testing in the salon's own timezone.
**Why it happens:** `DateTimeOffset.TimeOfDay`/`.DayOfWeek` reads the *stored* offset's wall-clock
component, not necessarily the salon's.
**How to avoid:** Always convert via `SalonTimeZone` (extend it with an instant→local method using
`TimeZoneInfo.ConvertTime`), mirroring how `SlotService.ToSalonInstant` is the single source of truth
for the reverse direction. Never call `.DayOfWeek`/`.TimeOfDay` on a raw `DateTimeOffset` in the
conflict-check code path.
**Warning signs:** Conflict-check unit tests that only exercise UTC-offset-zero scenarios and never
a genuinely offset salon timezone.

### Pitfall 3: Forgetting Completed appointments still hold `AppointmentSlot` rows
**What goes wrong:** Only `Cancelled`/`NoShow` transitions remove `AppointmentSlot` rows
(`AppointmentsService.UpdateStatusAsync`); `Completed` appointments keep theirs. A conflict query
that joins `AppointmentSlots` without filtering `Appointment.Status == Confirmed` will falsely flag
historical Completed appointments as conflicts (D-10 explicitly excludes them).
**Why it happens:** It's tempting to query `AppointmentSlots` directly (as `SlotService` does for
booked-cell exclusion) without remembering that table doesn't carry a status column itself — status
lives on the parent `Appointment`.
**How to avoid:** The conflict query must join/filter on `Appointment.Status == AppointmentStatus.Confirmed`
explicitly, exactly as `AppointmentsService.UpdateStatusAsync`'s cancel/no-show path already
demonstrates the Confirmed-vs-other distinction matters.
**Warning signs:** A conflict-check test suite that never seeds a Completed appointment to prove it's
excluded.

### Pitfall 4: Storing the uploaded image inside the API's process working directory instead of `ContentRootPath`
**What goes wrong:** Using a relative path (e.g. `"uploads/services/..."`) resolves against whatever
the current working directory happens to be when the process starts, which differs between
`dotnet run` (project dir) and a published/deployed process — files "disappear" outside dev.
**Why it happens:** Relative paths work fine locally and the bug only surfaces in a different launch
context.
**How to avoid:** Always resolve upload paths via `IWebHostEnvironment.WebRootPath` (i.e.
`wwwroot/uploads/services/...`) or `ContentRootPath`, injected via DI — never a bare relative string.
**Warning signs:** `Path.Combine` calls that don't start from an injected `IWebHostEnvironment`/`IHostEnvironment` property.

### Pitfall 5: `ServicesController`'s existing GET actions accidentally getting Owner-gated
**What goes wrong:** Adding a class-level `[Authorize(Roles = StaffRoles.Owner)]` to
`ServicesController` (mirroring `StaffUsersController`) would break the **public**, unauthenticated
`GET /api/services` and `GET /api/services/{slug}` endpoints the landing page depends on (CAT-01/
CAT-02) — a regression, not a new requirement.
**Why it happens:** `StaffUsersController` is a clean precedent for class-level Owner gating, but it
has no public actions to protect against; `ServicesController` does.
**How to avoid:** Apply `[Authorize(Roles = StaffRoles.Owner)]` at the **action level** on
`CreateService`/`UpdateService`/the new image-upload action only, leaving `GetServices`/`GetService`
un-annotated (anonymous).
**Warning signs:** A landing-page smoke test (or manual check) suddenly getting a 401 on the
homepage's service list after this phase's auth wiring lands.

## Code Examples

Verified patterns from the actual codebase (not hypothetical):

### Owner-only vs. any-staff class-level authorization
```csharp
// Source: API/ZachHairStudio.Api/Controllers/StaffUsersController.cs (verified in repo)
[ApiController]
[Route("api/staff-users")]
[Authorize(Roles = StaffRoles.Owner)]
public class StaffUsersController : ControllerBase { /* ... */ }
```

### Any-authenticated-staff class-level authorization
```csharp
// Source: API/ZachHairStudio.Api/Controllers/ScheduleController.cs (verified in repo)
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScheduleController : ControllerBase { /* ... */ }
```

### Reading the authenticated staff member's display name from JWT claims
```csharp
// Source: API/ZachHairStudio.Api/Controllers/ScheduleController.cs (verified in repo)
var displayName = User.FindFirst(JwtTokenService.DisplayNameClaimType)?.Value
    ?? User.FindFirst(ClaimTypes.Name)?.Value
    ?? "Unknown";
```

### 409 conflict response shape (reuse for MGMT-03's hard block)
```csharp
// Source: API/ZachHairStudio.Api/Controllers/AppointmentsController.cs (verified in repo)
if (result.IsDuplicateRecord())
{
    return Conflict(new ProblemDetails
    {
        Title = "Slot taken",
        Detail = result.Message,
        Status = StatusCodes.Status409Conflict,
    });
}
```

### File upload with server-controlled filename (framework pattern)
```csharp
// Source: github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/blazor/file-uploads.md [CITED]
var trustedFileNameForFileStorage = Path.GetRandomFileName();
var path = Path.Combine(env.ContentRootPath, env.EnvironmentName, "unsafe_uploads",
    trustedFileNameForFileStorage);
await using FileStream fs = new(path, FileMode.Create);
await file.CopyToAsync(fs);
```

### Serving uploaded files from a custom static folder
```csharp
// Source: github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/fundamentals/static-files.md [CITED]
app.UseStaticFiles(); // still serves wwwroot at "/", e.g. /uploads/services/xyz.jpg
// If storing outside wwwroot, add a second registration:
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads"
});
```

### Existing `AppointmentSlot` unique-index pattern the conflict check must respect
```csharp
// Source: API/ZachHairStudio.Shared/Db/BookingDbContext.cs (verified in repo)
modelBuilder.Entity<AppointmentSlot>(entity =>
{
    entity.HasIndex(s => new { s.StylistId, s.SlotStart }).IsUnique();
    entity.Property(s => s.SlotStart).HasColumnType("datetimeoffset(0)");
});
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| N/A — Phase 1 shipped `Service.ImageUrl` as a nullable string with no write path | Phase 4 adds the write path (upload endpoint + storage) that finally populates it | This phase | Fulfills Phase 1's D-08 "image management arrives with Phase 4 CRUD" deferred promise — no schema change needed, `ImageUrl` and its `HasMaxLength(500)` config already exist |
| N/A — `ServicesController` write actions currently ungated | Owner-role gate added this phase | This phase | Closes a real, currently-shipped security gap (anyone can currently POST/PUT services) — flag as security-relevant even though it's simultaneously a planned feature |

**Deprecated/outdated:** Nothing in this domain is deprecated — the whole stack (EF Core 10, ASP.NET
Core 10, FluentValidation, Identity+JWT) is the project's current, actively-maintained baseline as of
this research date.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Default business-hours window 06:00–22:00 for the week-strip UI (carried from 04-UI-SPEC.md, not a locked salon-hours fact) | Architecture Patterns / dashboard structure | Low — it's a layout default only; actual persisted hours come from whatever staff paint, not from this window |
| A2 | Local static-folder (`wwwroot/uploads/services/`) image storage, not blob storage | Standard Stack / Alternatives Considered | Medium — if the salon later needs multi-instance/scale-out deployment, local disk storage won't survive a redeploy without a persistent volume; flagged explicitly as Claude's Discretion in CONTEXT.md, this research just confirms the dev-simplicity-consistent default |
| A3 | Allowed image MIME types `image/jpeg`, `image/png`, `image/webp`, max 5MB (from 04-UI-SPEC.md's stated default, not an upstream product decision) | Common Pitfalls / Code Examples | Low — easy to change in one validator later; not a schema-level commitment |
| A4 | Conflict check recommended as full-final-state validation (not diff) is semantically equivalent to a true before/after diff | Common Pitfalls Pitfall 1 | Low-medium — the equivalence argument relies on `SlotService` always being the sole path appointments were booked through, which is true today; if a future phase adds another booking path that bypasses `SlotService`, this equivalence could break |
| A5 | `Result<T>` needs a new `ConflictError` case (or reuse of `DuplicateRecordError`'s existing 409 mapping) for the availability conflict response — no such case exists in `Result.cs` today | Architecture Patterns Pattern 2 | Low — purely an implementation-detail choice the planner/executor can resolve either way without affecting behavior |
| A6 | `SalonTimeZone` needs a new instant→local-wall-clock method (`ToSalonLocal`) since only the reverse (`ToSalonInstant`) exists today | Architecture Patterns Pattern 4, Common Pitfalls Pitfall 2 | Medium — this is the crux of the conflict check's correctness; if the executor instead hand-rolls the conversion inline, it risks diverging from the tested DST-handling behavior `SlotService`/`SalonTimeZone` already prove out |

**If this table is empty:** N/A — see rows above; all are either UI-spec-inherited defaults already
flagged there, or small implementation-shape choices with low blast radius.

## Open Questions

1. **Exact shape of the "replace working hours for a stylist" write DTO.**
   - What we know: `StylistWorkingHours` rows are `{StylistId, DayOfWeek, StartTime, EndTime}` with
     no unique constraint preventing multiple segments per day (needed for D-06's gap-as-break model).
   - What's unclear: Whether the write endpoint should accept "replace the entire week in one PUT"
     (delete-all-then-insert for that stylist) vs. a more granular per-segment CRUD. The UI-SPEC's
     single combined "Save Changes" button (submitting both hours and time-off together) strongly
     implies a whole-week replace semantics.
   - Recommendation: Model the write endpoint as `PUT /api/availability/{stylistId}/working-hours`
     accepting the full list of segments for that stylist (all 7 days), and implement it as
     delete-existing-then-insert-new inside the same transaction as the conflict check — simplest to
     reason about and matches the "one authoritative moment to evaluate the whole new state" framing
     in the UI-SPEC's Component Patterns section.

2. **Whether `AvailabilityConflictDto` needs a stable identifier for "deep link into schedule" (UI-SPEC calls this nice-to-have, not required).**
   - What we know: D-11 requires client name, service, stylist, salon-local time — no deep link
     required this phase.
   - What's unclear: Whether to include `AppointmentId` in the conflict row anyway (cheap to add,
     costs nothing, and unlocks the nice-to-have deep link without extra backend work later).
   - Recommendation: Include `AppointmentId` in the conflict DTO even though the UI doesn't
     currently render it as a link — trivial now, avoids an API-contract change later if deep-linking
     is added.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 10 | API build/run, EF migrations | ✓ (existing repo builds against it) | 10 | — |
| SQL Server LocalDB / SQL Server | `BookingDbContext`, migrations | ✓ (already configured; STATE.md confirms LocalDB works, plus an Azure SQL override) | `(localdb)\MSSQLLocalDB` | Azure SQL via `ConnectionStrings__DefaultConnection` env override (already documented in STATE.md) |
| Node.js 18+ / npm | `dashboard/` dev server, typed-client regen | ✓ (existing `landing-page`/`dashboard` already run) | project-pinned via `package.json` | — |
| `dotnet-ef` CLI matching EF Core 10 | Any migration this phase needs (likely none — no schema change expected since `ImageUrl` already exists) | Unverified in this pass — `ef-migrations` SKILL.md flags the environment may still have 9.0.15 | — | `dotnet tool update --global dotnet-ef --version "10.*"` per the `ef-migrations` skill, only needed if a migration is actually required |
| A writable `wwwroot/uploads/services/` (or equivalent) directory at deploy time | Image upload storage | Not yet created — no `wwwroot/` currently exists under `API/ZachHairStudio.Api/` | — | Create the folder as part of this phase's setup; no external tool needed (plain `Directory.CreateDirectory` on first use or at startup) |

**Missing dependencies with no fallback:** none identified.

**Missing dependencies with fallback:** `dotnet-ef` version mismatch (if it recurs) has a documented
one-line fix in the `ef-migrations` skill; this phase likely needs zero migrations since `Service.ImageUrl`,
`StylistWorkingHours`, and `StylistTimeOff` all already exist with no schema gap identified.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + `Microsoft.AspNetCore.Mvc.Testing` 10.0.9, run against **real SQL Server** via `SqlServerWebApplicationFactory`/`CustomWebApplicationFactory` (verified: no InMemory provider used for behavioral tests — `Microsoft.EntityFrameworkCore.InMemory` is referenced but the project's own comments/tests explicitly avoid it for Identity-relational and unique-index behavior) |
| Config file | `API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` |
| Quick run command | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Services\|FullyQualifiedName~Availability"` |
| Full suite command | `dotnet test API/ZachHairStudio.slnx` (or the API test project directly) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MGMT-01 | Owner can create/edit/retire a service; non-Owner and anonymous callers are rejected on write actions; GET stays anonymous | integration (WebApplicationFactory) | `dotnet test --filter FullyQualifiedName~Services.ServicesControllerAuthTests` | ❌ Wave 0 — extend `ServicesControllerTests.cs` with role-gated cases; existing file has no auth tests yet |
| MGMT-01 | Image upload accepts allowed MIME types under the size limit and sets `ImageUrl`; rejects disallowed types/oversized files | integration | `dotnet test --filter FullyQualifiedName~Services.ServiceImageUploadTests` | ❌ Wave 0 — new test file |
| MGMT-02 | Working-hours replace persists into `StylistWorkingHours` and `SlotService.GetOpenSlotsAsync` reflects it immediately (same-model proof, D-08) | integration | `dotnet test --filter FullyQualifiedName~Availability.WorkingHoursReplaceTests` | ❌ Wave 0 — new test file; should assert against `SlotService` output directly, not just the write endpoint's 200 |
| MGMT-02 | Time-off create/remove persists into `StylistTimeOff` and blocks/unblocks the corresponding open slots | integration | `dotnet test --filter FullyQualifiedName~Availability.TimeOffTests` | ❌ Wave 0 |
| MGMT-03 | Saving working hours that would exclude an existing Confirmed appointment's slot is hard-blocked with a conflict list; Cancelled/NoShow/Completed appointments never appear in the conflict list | integration | `dotnet test --filter FullyQualifiedName~Availability.ConflictCheckTests` | ❌ Wave 0 — highest-priority new test file (sharpest-edge behavior per phase description) |
| MGMT-03 | Saving new/extended time off that overlaps an existing Confirmed appointment is hard-blocked | integration | `dotnet test --filter FullyQualifiedName~Availability.ConflictCheckTests` | ❌ Wave 0 (same file as above) |
| MGMT-03 (DST correctness, mirroring Phase 2's proof pattern) | Conflict check correctly attributes a slot to the right local weekday/time across the salon's fixed UTC+6:30 offset | unit | `dotnet test --filter FullyQualifiedName~Availability.ConflictCheckLocalTimeTests` | ❌ Wave 0 — should follow the existing `DstBoundaryTests.cs`/`DstRoundTripTests.cs` pattern even though Asia/Yangon has no DST (SC5 descope precedent from Phase 2 Plan 07 applies identically here) |

### Sampling Rate
- **Per task commit:** `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Services|FullyQualifiedName~Availability"`
- **Per wave merge:** `dotnet test API/ZachHairStudio.slnx` (full backend suite; add a `dashboard/` typecheck — `npm run build` — once the new pages exist, since there's no dashboard test runner configured)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `API/ZachHairStudio.Api.Tests/Features/Services/ServiceImageUploadTests.cs` — covers MGMT-01 image upload validation
- [ ] `API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerAuthTests.cs` (or extend `ServicesControllerTests.cs`) — covers the Owner-role gate added this phase (currently untested since the endpoints are currently ungated)
- [ ] `API/ZachHairStudio.Api.Tests/Features/Availability/WorkingHoursReplaceTests.cs` — covers MGMT-02
- [ ] `API/ZachHairStudio.Api.Tests/Features/Availability/TimeOffTests.cs` — covers MGMT-02
- [ ] `API/ZachHairStudio.Api.Tests/Features/Availability/ConflictCheckTests.cs` — covers MGMT-03 (highest priority — this is the phase's sharpest correctness edge)
- [ ] No new test framework install needed — xUnit + `Mvc.Testing` + the existing `SqlServerWebApplicationFactory` fixture already cover everything this phase needs

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes (already built) | JWT bearer via ASP.NET Core Identity — reuse, no new auth mechanism |
| V3 Session Management | yes (already built) | `dashboard/lib/auth.ts`'s ~12h JWT lifetime in `localStorage`; unchanged this phase |
| V4 Access Control | **yes — this phase's primary new surface** | `[Authorize(Roles = StaffRoles.Owner)]` on Service write/image-upload actions (action-level, not class-level — see Pitfall 5); `[Authorize]` on the new Availability controller (any staff, D-13) |
| V5 Input Validation | yes | FluentValidation for all new DTOs (hours/time-off/image-upload metadata); explicit MIME-type allowlist + size cap for uploads, not just a client-side check |
| V6 Cryptography | no new surface | No new secrets/crypto introduced this phase |
| V12 File and Resources (image upload specifically) | yes — new this phase | Server-generated random filename (never the client's `FileName`), extension/content-type allowlist, size cap enforced server-side, storage path resolved via `IWebHostEnvironment` (never a raw relative path) |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Currently-ungated `ServicesController` POST/PUT (pre-existing gap this phase must close) | Tampering / Elevation of Privilege | Action-level `[Authorize(Roles = StaffRoles.Owner)]` on write actions only, GET stays anonymous |
| Path traversal via a malicious `FileName` in the image upload | Tampering | Never use the client-supplied filename for storage — generate via `Path.GetRandomFileName()` and validate the resolved extension against an allowlist [CITED: aspnetcore.docs file-uploads.md] |
| Unrestricted file type/size upload (e.g., an executable or oversized file disguised with an image extension) | Denial of Service / Tampering | Validate declared `ContentType` AND actual size server-side (not just the `<input accept>` attribute, which is client-side only and trivially bypassed); reject before writing to disk |
| Any staff account editing any stylist's availability with no per-stylist ownership check (D-13, intentional) | Elevation of Privilege (by design, accepted) | This is an explicit, documented decision (D-13) — not a gap to fix, but the planner should not add an unrequested per-stylist ownership restriction |
| A stale/forged conflict-check bypass (client sends the hours/time-off payload but a malicious client skips the conflict check client-side) | Tampering | The conflict check MUST run server-side inside the same request that persists the change — never trust a client-side "no conflicts" flag; this mirrors the existing booking path's "never trust the echoed slot" principle (`AppointmentsService.CreateAsync`'s comment) |

## Sources

### Primary (HIGH confidence)
- `API/ZachHairStudio.Shared/Features/Services/*.cs` — Service entity, DTOs, validators, service layer, extensions (read directly)
- `API/ZachHairStudio.Shared/Features/Availability/*.cs` — StylistWorkingHours, StylistTimeOff, SlotService, SalonTimeZone, SalonOptions, OpenSlotDto (read directly)
- `API/ZachHairStudio.Shared/Features/Appointments/*.cs` — Appointment, AppointmentSlot, AppointmentStatus, AppointmentsService, AppointmentResponseDto/Extensions (read directly)
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` — full `OnModelCreating` schema, seed data, unique indexes (read directly)
- `API/ZachHairStudio.Api/Controllers/*.cs` — ServicesController, StylistsController, StaffUsersController, AppointmentsController, ScheduleController (read directly)
- `API/ZachHairStudio.Api/Program.cs` — DI wiring, JWT/Identity/CORS setup, migration-on-startup (read directly)
- `API/ZachHairStudio.Shared/Result.cs` — the project's error-result convention (read directly)
- `dashboard/app/schedule/page.tsx`, `dashboard/app/staff/new/page.tsx`, `dashboard/components/ConfirmDialog.tsx`, `dashboard/components/icons.tsx`, `dashboard/lib/auth.ts`, `dashboard/lib/useSchedule.ts`, `dashboard/lib/api/client.ts` — dashboard patterns to mirror (read directly)
- `API/ZachHairStudio.Api.Tests/**` — existing test conventions, xUnit + Mvc.Testing + SqlServerWebApplicationFactory (read directly)
- `.claude/skills/{dev,ef-migrations,feature-scaffold,openapi-client}/SKILL.md` — project skill conventions (read directly)

### Secondary (MEDIUM confidence)
- `/dotnet/aspnetcore.docs` (Context7) — `IFormFile` upload pattern with random-filename storage, `UseStaticFiles`/`PhysicalFileProvider` for serving files outside `wwwroot` [CITED: github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/blazor/file-uploads.md, github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/mvc/models/file-uploads.md, github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/fundamentals/static-files.md]

### Tertiary (LOW confidence)
- None — every claim in this research is either grounded in a directly-read repository file or an
  official ASP.NET Core docs citation via Context7. Items in the Assumptions Log are flagged
  explicitly rather than presented as verified.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new dependencies; every library is already pinned and in use in this exact repo
- Architecture: HIGH — every pattern to extend (auth gating, `Result<T>`/ProblemDetails, feature folders, SlotService's role as sole read authority) is directly observed in the codebase, not inferred
- Pitfalls: HIGH — all five pitfalls are derived from specific, cited lines of existing code (e.g., `SalonTimeZone`'s one-directional conversion, `UpdateStatusAsync`'s Cancelled/NoShow-only slot release) rather than generic ASP.NET Core folklore

**Research date:** 2026-07-24
**Valid until:** 30 days (stable stack, no fast-moving dependencies; re-verify if `.NET`, EF Core, or Next.js receive a major version bump before planning executes)
