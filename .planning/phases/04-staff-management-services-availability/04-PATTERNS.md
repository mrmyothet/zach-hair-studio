# Phase 4: Staff Management (Services & Availability) - Pattern Map

**Mapped:** 2026-07-24
**Files analyzed:** 24
**Analogs found:** 20 / 24

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `API/ZachHairStudio.Api/Controllers/ServicesController.cs` (modify: gate + image action) | controller | request-response | itself (existing file) + `StaffUsersController.cs` (Owner-role gate shape) | exact (self-modify) |
| `API/ZachHairStudio.Shared/Features/Services/ServiceImageUploadDto.cs` | model/DTO | file-I/O | `ServiceUpdateDto.cs` (DTO shape) | role-match |
| `API/ZachHairStudio.Shared/Features/Services/ServiceImageUploadDtoValidator.cs` | utility (validator) | file-I/O | `ServiceCreateDtoValidator.cs` | role-match |
| `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` (modify: add `SetImageAsync`, retire = `UpdateAsync(IsActive=false)`) | service | CRUD | itself (existing file) | exact (self-modify) |
| `API/ZachHairStudio.Api/Controllers/AvailabilityController.cs` (new) | controller | request-response | `ScheduleController.cs` (any-staff `[Authorize]` class gate + Result-to-ProblemDetails translation) | exact |
| `API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs` (new) | service | CRUD + event-driven (conflict gate) | `AppointmentsService.UpdateStatusAsync` (Confirmed-aware mutation + Result outcomes) + `ServicesService.cs` (CRUD shape) | role-match |
| `API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDto.cs` | model/DTO | CRUD | `ServiceUpdateDto.cs` | role-match |
| `API/ZachHairStudio.Shared/Features/Availability/WorkingHoursReplaceDtoValidator.cs` | utility (validator) | CRUD | `ServiceCreateDtoValidator.cs` | role-match |
| `API/ZachHairStudio.Shared/Features/Availability/TimeOffCreateDto.cs` | model/DTO | CRUD | `ServiceUpdateDto.cs` | role-match |
| `API/ZachHairStudio.Shared/Features/Availability/TimeOffCreateDtoValidator.cs` | utility (validator) | CRUD | `ServiceCreateDtoValidator.cs` | role-match |
| `API/ZachHairStudio.Shared/Features/Availability/AvailabilityConflictDto.cs` | model/DTO | request-response | none exact — `AppointmentResponseDto`/`Extensions` (join shape) | partial match |
| `API/ZachHairStudio.Shared/Features/Availability/SalonTimeZone.cs` (modify: add `ToSalonLocal`) | utility | transform | itself (existing file) | exact (self-modify) |
| `API/ZachHairStudio.Shared/Result.cs` (modify: optional `ConflictError` case) | utility | — | itself (existing file) | exact (self-modify) |
| `dashboard/app/services/page.tsx` (new) | component (page) | CRUD | `dashboard/app/staff/new/page.tsx` (Owner-gate, Field/inputClass, ApiError handling) | exact |
| `dashboard/app/availability/page.tsx` (new) | component (page) | CRUD | `dashboard/app/schedule/page.tsx` (requireAuth/session bootstrap, header, SWR-hook consumption, ConfirmDialog wiring) | role-match |
| `dashboard/components/ServiceForm.tsx` (new) | component | CRUD | `dashboard/app/staff/new/page.tsx` (form body/Field/inputClass, single create+edit mode precedent) | exact |
| `dashboard/components/ImageUploadField.tsx` (new) | component | file-I/O | none — net-new interaction (dashed-box upload); nearest shell precedent is `ImageUploadField`'s 160px box mirrors `ConfirmDialog`'s card container styling only | no analog (net-new) |
| `dashboard/components/StylistPicker.tsx` (new) | component | CRUD (selection) | `dashboard/components/WeekChips.tsx` (chip-row selection pattern) | role-match |
| `dashboard/components/WeekStripEditor.tsx` (new) | component | streaming/event-driven (drag-paint) | `dashboard/components/DayGrid.tsx` (proportional time-grid rendering, 15-min grid alignment) | role-match |
| `dashboard/components/TimeOffCalendar.tsx` (new) | component | event-driven (calendar paint) | none close — net-new month-grid interaction; nearest precedent is `DayGrid.tsx`'s grid-cell rendering approach only | no analog (net-new) |
| `dashboard/components/ConflictList.tsx` (new) | component | request-response (render server conflict payload) | `dashboard/components/AppointmentDetailPanel.tsx` (list/panel rendering of appointment fields incl. salon-local time formatting) | role-match |
| `dashboard/components/DashboardNav.tsx` (new, extracted) | component | — | `dashboard/app/schedule/page.tsx` lines 155-181 (inline `<header>` block to be extracted) | exact (refactor source) |
| `dashboard/lib/useServices.ts` (new) | hook | CRUD | `dashboard/lib/useSchedule.ts` (SWR fetch wrapper, `ApiError`/`handleUnauthorized`/`extractErrorMessage` pattern) | exact |
| `dashboard/lib/useAvailability.ts` (new) | hook | CRUD | `dashboard/lib/useSchedule.ts` | exact |

## Pattern Assignments

### `API/ZachHairStudio.Api/Controllers/ServicesController.cs` (controller, request-response)

**Analog:** itself (existing, verified) + `API/ZachHairStudio.Api/Controllers/StaffUsersController.cs` for the Owner-role gate shape.

**Current shape** (lines 1-24, imports/ctor):
```csharp
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly ServicesService _servicesService;
    private readonly IValidator<ServiceCreateDto> _createValidator;
    private readonly IValidator<ServiceUpdateDto> _updateValidator;
    // ctor DI as-is
```

**Critical pitfall (Pitfall 5, research):** Do NOT add a class-level `[Authorize(Roles = StaffRoles.Owner)]` — that would 401/403 the public `GetServices`/`GetService` GET actions the landing page depends on. Apply the attribute **per-action** only on `CreateService`, `UpdateService`, and the new image-upload action:
```csharp
using Microsoft.AspNetCore.Authorization;
using ZachHairStudio.Shared.Features.Identity; // StaffRoles

[HttpPost]
[Authorize(Roles = StaffRoles.Owner)]
public async Task<ActionResult<ServiceResponseDto>> CreateService([FromBody] ServiceCreateDto request) { /* unchanged body */ }

[HttpPut("{id}")]
[Authorize(Roles = StaffRoles.Owner)]
public async Task<IActionResult> UpdateService(int id, [FromBody] ServiceUpdateDto request) { /* unchanged body */ }

[HttpPost("{id}/image")]
[Authorize(Roles = StaffRoles.Owner)]
public async Task<ActionResult<ServiceResponseDto>> UploadImage(int id, [FromForm] ServiceImageUploadDto request) { /* new */ }
```

**Error-handling pattern already in file** (lines 40-58, reuse verbatim for the new image action):
```csharp
var validation = await _createValidator.ValidateAsync(request);
if (!validation.IsValid)
{
    AddToModelState(validation);
    return ValidationProblem(ModelState);
}
var result = await _servicesService.CreateAsync(request);
if (result.IsValidationError())
{
    ModelState.AddModelError(string.Empty, result.Message);
    return ValidationProblem(ModelState);
}
return CreatedAtAction(nameof(GetService), new { slug = result.Data.Slug }, result.Data);
```

---

### New `API/ZachHairStudio.Api/Controllers/AvailabilityController.cs`

**Analog:** `API/ZachHairStudio.Api/Controllers/ScheduleController.cs` (verified, any-staff class gate + Result-to-ProblemDetails translation + reading the acting staff's display name from JWT claims).

**Class-level gate pattern** (lines 16-19):
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]   // any authenticated staff, D-13 — no per-stylist ownership restriction
public class AvailabilityController : ControllerBase
```

**ProblemDetails/Result translation pattern** (lines 54-75, copy for NotFound/SystemError):
```csharp
if (result.IsNotFound())
{
    return NotFound(new ProblemDetails
    {
        Title = "Stylist not found",
        Detail = result.Message,
        Status = StatusCodes.Status404NotFound,
    });
}
if (result.IsSystemError())
{
    return InconsistentDataProblem(result.Message);
}
return Ok(result.Data);
```

**Conflict 409 shape** (Pattern 2 from RESEARCH.md, from `AppointmentsController.cs`, verified):
```csharp
if (result.IsDuplicateRecord()) // or a new ConflictError case — see Result.cs below
{
    return Conflict(new ProblemDetails
    {
        Title = "Availability change conflicts with confirmed appointments",
        Detail = result.Message,
        Status = StatusCodes.Status409Conflict,
    });
}
```

**Reading the acting staff's identity from JWT claims** (lines 93-95, reuse verbatim if any audit trail is added to availability writes):
```csharp
var displayName = User.FindFirst(JwtTokenService.DisplayNameClaimType)?.Value
    ?? User.FindFirst(ClaimTypes.Name)?.Value
    ?? "Unknown";
```

---

### `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` (service, CRUD) — extend

**Analog:** itself (existing, verified).

**Existing CRUD shape to extend with `SetImageAsync`** (lines 40-75):
```csharp
public async Task<Result<ServiceResponseDto>> UpdateAsync(int id, ServiceUpdateDto request)
{
    var service = await _dbContext.Services.FindAsync(id);
    if (service is null)
    {
        return Result<ServiceResponseDto>.NotFoundError($"Service '{id}' not found.");
    }
    var validation = await _updateValidator.ValidateAsync(request);
    if (!validation.IsValid)
    {
        return Result<ServiceResponseDto>.ValidationError(
            string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
    }
    request.ApplyTo(service);
    await _dbContext.SaveChangesAsync();
    return Result<ServiceResponseDto>.Success(service.ToDto());
}
```
Retire is simply calling this existing `UpdateAsync` with a DTO carrying `IsActive = false` — no new method needed (per D-02/D-04 and RESEARCH.md's explicit recommendation). `SetImageAsync(int id, string imageUrl)` should follow the identical `FindAsync` → mutate → `SaveChangesAsync` → `Result<T>.Success` shape.

**File-upload path pattern (net-new; no analog in repo)** — RESEARCH.md Pattern 3, framework-cited:
```csharp
// Source: github.com/dotnet/aspnetcore.docs file-uploads.md [CITED]
var trustedFileNameForFileStorage = Path.GetRandomFileName();
var path = Path.Combine(env.WebRootPath, "uploads", "services",
    trustedFileNameForFileStorage + extension);
await using var fs = new FileStream(path, FileMode.Create);
await file.CopyToAsync(fs);
```
Resolve `path` via injected `IWebHostEnvironment.WebRootPath` — never a bare relative string (Pitfall 4).

**Validator analog** — `API/ZachHairStudio.Shared/Features/Services/ServiceCreateDtoValidator.cs` (verified, lines 1-41) for the `ServiceImageUploadDtoValidator` MIME/size-allowlist shape:
```csharp
public class ServiceCreateDtoValidator : AbstractValidator<ServiceCreateDto>
{
    public ServiceCreateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DurationMinutes).GreaterThan(0).LessThanOrEqualTo(480);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        // ... mirror this RuleFor-per-field style for MIME allowlist + max-size on ServiceImageUploadDto
    }
}
```

---

### New `API/ZachHairStudio.Shared/Features/Availability/AvailabilityService.cs`

**Analogs:** `AppointmentsService.UpdateStatusAsync` (Confirmed-aware, transactional mutation shape) + `SlotService.cs` (the read-path math this write path must feed, never re-derive) + `SalonTimeZone.cs` (local-time conversion, single source of truth).

**Confirmed-only awareness pattern** (from `AppointmentsService.cs` lines 250-287, verified):
```csharp
public async Task<Result<AppointmentResponseDto>> UpdateStatusAsync(
    int id, AppointmentStatus newStatus, string staffDisplayName)
{
    var appointment = await _dbContext.Appointments
        .Include(appointment => appointment.Slots)
        .FirstOrDefaultAsync(appointment => appointment.Id == id);
    // ... status-transition validation, then:
    if (newStatus is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
    {
        _dbContext.AppointmentSlots.RemoveRange(appointment.Slots);
    }
    appointment.Status = newStatus;
    await _dbContext.SaveChangesAsync();
    return Result<AppointmentResponseDto>.Success(appointment.ToDto(service, stylist));
}
```
The conflict check must instead **join** `AppointmentSlots` to `Appointment.Status == AppointmentStatus.Confirmed` explicitly (Pitfall 3 — the slot table itself carries no status column).

**`SlotService.GetOpenSlotsAsync`'s local-time/grid math** (lines 25-119, verified) is the single source of truth to reuse, not re-derive:
```csharp
var dayStartLocal = date.ToDateTime(TimeOnly.MinValue);
var dayStartUtc = _salonTimeZone.ToSalonInstant(dayStartLocal);
// working hours / time off / booked cells all queried directly off StylistWorkingHours,
// StylistTimeOff, AppointmentSlots — same tables the new write path must target (D-08)
```

**`SalonTimeZone.ToSalonInstant`** (lines 28-45, verified) is the existing local→instant conversion; RESEARCH.md Assumption A6/Pitfall 2 requires adding the missing **inverse** direction (`ToSalonLocal(DateTimeOffset instant)`) in this same file using `TimeZoneInfo.ConvertTime`, so both directions stay single-sourced:
```csharp
public class SalonTimeZone
{
    private readonly TimeZoneInfo _timeZoneInfo;
    public DateTimeOffset? ToSalonInstant(DateTime localWallClock) { /* existing, verified */ }
    // NEW: public DateTime ToSalonLocal(DateTimeOffset instant) =>
    //     TimeZoneInfo.ConvertTime(instant, _timeZoneInfo).DateTime;
}
```

**Entity shapes the write DTOs map onto** (both verified, unchanged):
```csharp
// StylistWorkingHours.cs
public class StylistWorkingHours
{
    public int Id { get; set; }
    public int StylistId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
// StylistTimeOff.cs
public class StylistTimeOff
{
    public int Id { get; set; }
    public int StylistId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    [StringLength(200)] public string? Reason { get; set; }
}
```

**Conflict-check algorithm (Pitfall 1, mandatory shape):** Evaluate against the **full proposed final state** in one pass — do NOT diff old-vs-new. For every Confirmed `AppointmentSlot` belonging to the target stylist: (a) is this cell covered by any segment in the *proposed* `StylistWorkingHours` for its local weekday (converted via `ToSalonLocal`), and (b) does this cell fall inside any range in the *proposed* final `StylistTimeOff` set. Either failing = conflict, regardless of whether that row changed this save.

---

### `Result<T>` (utility) — extend for conflict responses

**Analog:** `API/ZachHairStudio.Shared/Result.cs` (existing, verified, lines 1-56). Reuse `DuplicateRecordError` (409-mapped) or add a new `ConflictError` case following the exact same static-factory pattern:
```csharp
public static Result<T> DuplicateRecordError(string message = "Duplicate Record", T? data = default) =>
    new Result<T> { IsSuccess = false, Type = EnumRespType.DuplicateRecord, Data = data, Message = message };
// Optional NEW case, same shape:
// public static Result<T> ConflictError(string message, T? data = default) =>
//     new Result<T> { IsSuccess = false, Type = EnumRespType.Conflict, Data = data, Message = message };
```
`Data` on the conflict result should carry the `IReadOnlyList<AvailabilityConflictDto>` so the controller can serialize it directly in the 409 body.

---

### `dashboard/app/services/page.tsx` (component/page, CRUD)

**Analog:** `dashboard/app/staff/new/page.tsx` (existing, verified in full).

**Owner-gate bootstrap pattern** (lines 46-54, copy verbatim, swap redirect target if non-Owner):
```tsx
useEffect(() => {
  if (!requireAuth()) return;
  const session = getSession();
  if (!session || session.role !== "Owner") {
    router.replace("/schedule");
    return;
  }
  setReady(true);
}, [router]);
```

**`inputClass` / `Field` wrapper** (lines 15-33, reuse verbatim — do not redefine a second input style, per UI-SPEC's `ServiceForm` note):
```tsx
const inputClass =
  "w-full bg-surface border border-border hover:border-gold-dark/40 focus:border-gold-dark rounded-xl px-4 py-3 text-ink placeholder:text-muted/60 text-sm outline-none transition-colors";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="text-muted text-xs uppercase tracking-wider block mb-2">{label}</label>
      {children}
    </div>
  );
}
```

**Submit/error/success handling pattern** (lines 56-112, mirror for `ServiceForm`'s save):
```tsx
try {
  const { response } = await api.POST("/api/staff-users", { body: { /* ... */ } });
  if (response.status === 401) {
    handleUnauthorized("Your session has ended. Log in again to continue.");
    return;
  }
  if (!response.ok) {
    let message = "Could not add staff member.";
    try { message = await extractErrorMessage(response.clone()); } catch {}
    throw new ApiError(message, response.status || null);
  }
  setSuccess("...");
} catch (err) {
  if (err instanceof ApiError) setError(err.message);
  else if (err instanceof TypeError) setError("We couldn't reach the booking system...");
  else setError("...");
} finally {
  setSubmitting(false);
}
```

---

### `dashboard/app/availability/page.tsx` (component/page, CRUD, all staff)

**Analog:** `dashboard/app/schedule/page.tsx` (existing, verified) for session bootstrap, SWR-hook consumption, and `ConfirmDialog` wiring — no Owner check needed here (D-13, any staff).

**Session bootstrap** (lines 51-55, no role check — unlike Services page):
```tsx
useEffect(() => {
  if (!requireAuth()) return;
  setSession(getSession());
  setReady(true);
}, []);
```

**SWR-hook consumption pattern** (line 83, mirror for `useAvailability`):
```tsx
const { appointments, isLoading, error, mutate, lastUpdatedAt } = useSchedule({
  from: range.from, to: range.to, includeCancelled,
});
```

---

### `dashboard/components/DashboardNav.tsx` (new, extracted)

**Analog/refactor source:** `dashboard/app/schedule/page.tsx` lines 155-181 (verified) — this exact `<header>` block is the extraction source; every other dashboard page duplicating it should be replaced with `<DashboardNav />`.

```tsx
<header className="border-b border-border bg-surface-alt px-4 md:px-6 py-3 flex flex-wrap items-center gap-3 justify-between">
  <h1 className="font-serif text-2xl font-semibold tracking-tight">Zach Hair Studio</h1>
  <div className="flex items-center gap-3">
    <p className="text-sm text-muted">
      {session?.displayName}{session?.role ? ` · ${session.role}` : ""}
    </p>
    {isOwner ? (
      <Link href="/staff/new" className="min-h-11 inline-flex items-center px-3 rounded-xl border border-border text-sm text-ink hover:border-gold-dark/40">
        Add staff
      </Link>
    ) : null}
    <button type="button" onClick={handleLogout} aria-label="Log out"
      className="min-h-11 min-w-11 inline-flex items-center justify-center rounded-xl border border-border text-ink">
      <LogOutIcon className="h-5 w-5" />
    </button>
  </div>
</header>
```
Per UI-SPEC's Component Patterns: insert a nav-link row (**Schedule · Services · Availability**) between the wordmark and the session/actions cluster; Services omitted entirely from DOM for Staff role (D-16); active link `text-gold-dark font-semibold`, inactive `text-ink hover:text-gold-dark`.

---

### `dashboard/lib/useServices.ts` / `useAvailability.ts` (hooks, CRUD)

**Analog:** `dashboard/lib/useSchedule.ts` (existing, verified in full) — copy the SWR + `ApiError`/`handleUnauthorized`/`extractErrorMessage` wrapper shape:
```tsx
async function fetchSchedule(from: string, to: string): Promise<AppointmentResponseDto[]> {
  const { data, response, error } = await api.GET("/api/Schedule", { params: { query: { from, to } } });
  if (response.status === 401) {
    handleUnauthorized("Your session has ended. Log in again to continue.");
    throw new ApiError("Unauthorized", 401);
  }
  if (!response.ok || error) {
    let message = "Couldn't load the schedule.";
    try { message = await extractErrorMessage(response.clone()); } catch {}
    throw new ApiError(message, response.status || null);
  }
  return data ?? [];
}

export function useSchedule({ from, to }: UseScheduleArgs) {
  const { data, error, isLoading, isValidating, mutate } = useSWR(
    from && to ? (["schedule", from, to] as const) : null,
    ([, f, t]) => fetchSchedule(f, t),
    { refreshInterval: 60_000, revalidateOnFocus: true, shouldRetryOnError: false }
  );
  return { appointments: data ?? [], isLoading, isValidating, error: /* normalize */, mutate };
}
```
`useServices` needs no polling (services rarely change mid-session, unlike schedule); `useAvailability` should fetch per-selected-stylist (key on `["availability", stylistId]`), mirroring the `["schedule", from, to]` compound-key pattern.

---

### `dashboard/components/ConfirmDialog.tsx` — reuse verbatim for service retire

**Analog:** existing `dashboard/components/ConfirmDialog.tsx` (verified in full) — the retire confirmation reuses this component unmodified, adding a new `CONFIRM_COPY.Retired` entry alongside the existing `Cancelled`/`NoShow` entries:
```tsx
export const CONFIRM_COPY = {
  Cancelled: { title: "Cancel Appointment", body: "...", confirmLabel: "Cancel Appointment" },
  NoShow: { title: "Mark as No-Show", body: "...", confirmLabel: "Mark as No-Show" },
  // NEW:
  // Retired: { title: "Retire Service", body: "Retire {name}? It won't be bookable on the public site until you reactivate it.", confirmLabel: "Retire Service" },
} as const;
```

---

## Shared Patterns

### Owner-only vs any-staff controller gating
**Source:** `API/ZachHairStudio.Api/Controllers/StaffUsersController.cs` (class-level Owner-only) and `API/ZachHairStudio.Api/Controllers/ScheduleController.cs` (class-level any-staff).
**Apply to:** `AvailabilityController` (class-level `[Authorize]`); `ServicesController`'s write actions (action-level `[Authorize(Roles = StaffRoles.Owner)]` only — never class-level, per Pitfall 5).

### `Result<T>` + ProblemDetails error shape
**Source:** `API/ZachHairStudio.Shared/Result.cs` + `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs`'s 409 mapping.
**Apply to:** Every new service method in `AvailabilityService` and the image-upload path; the conflict-blocked save is a new `Result` outcome.

### Salon-local time conversion
**Source:** `API/ZachHairStudio.Shared/Features/Availability/SalonTimeZone.cs` (extend with `ToSalonLocal`).
**Apply to:** The conflict-check query — never call `.DayOfWeek`/`.TimeOfDay` on a raw `DateTimeOffset` (Pitfall 2).

### FluentValidation-per-field RuleFor style
**Source:** `API/ZachHairStudio.Shared/Features/Services/ServiceCreateDtoValidator.cs`.
**Apply to:** `ServiceImageUploadDtoValidator`, `WorkingHoursReplaceDtoValidator`, `TimeOffCreateDtoValidator`.

### `inputClass` / `Field` wrapper + ApiError/extractErrorMessage handling
**Source:** `dashboard/app/staff/new/page.tsx`.
**Apply to:** `ServiceForm.tsx`, `services/page.tsx`, `availability/page.tsx`.

### SWR hook shape (`ApiError`/`handleUnauthorized`/`extractErrorMessage`)
**Source:** `dashboard/lib/useSchedule.ts`.
**Apply to:** `useServices.ts`, `useAvailability.ts`.

### `ConfirmDialog` + `CONFIRM_COPY`
**Source:** `dashboard/components/ConfirmDialog.tsx`.
**Apply to:** Service retire confirmation (new `CONFIRM_COPY.Retired` entry, no new dialog component).

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `dashboard/components/ImageUploadField.tsx` | component | file-I/O | No existing dashboard component uploads a file; this is the first `IFormFile`-backed UI in the app. Follow UI-SPEC's prescriptive 160x160 dashed-box shape and the framework file-upload pattern (Pattern 3) rather than an in-repo analog. |
| `dashboard/components/TimeOffCalendar.tsx` | component | event-driven (calendar paint) | No existing month-grid/calendar component exists in `dashboard/`; `DayGrid.tsx` renders a single day's proportional time grid, which is a distant precedent at best (grid-cell rendering only, not month/date-range painting). Genuinely net-new interaction per UI-SPEC's own "backstop"/"unresolved" flags on this component. |
| `dashboard/components/WeekStripEditor.tsx` (drag-paint interaction specifically) | component | event-driven (drag-paint) | `DayGrid.tsx` provides the closest precedent for proportional time-based rendering, but the click-drag-to-paint-a-range interaction itself (vs. `DayGrid`'s read-only appointment block rendering) is new. Executor's discretion per CONTEXT.md/UI-SPEC Open Question 3. |
| `API/ZachHairStudio.Shared/Features/Availability/AvailabilityConflictDto.cs` | model/DTO | request-response | No existing DTO joins client+service+stylist+time in one flat row; `AppointmentResponseDto`/`AppointmentExtensions` is the nearest partial precedent (same underlying entities) but shapes a full appointment, not a conflict summary row. |

## Metadata

**Analog search scope:** `API/ZachHairStudio.Api/Controllers/`, `API/ZachHairStudio.Shared/Features/{Services,Availability,Appointments}/`, `API/ZachHairStudio.Shared/Result.cs`, `dashboard/app/{schedule,staff/new}/`, `dashboard/components/`, `dashboard/lib/`
**Files scanned:** ~20 (read in full or targeted sections)
**Pattern extraction date:** 2026-07-24
