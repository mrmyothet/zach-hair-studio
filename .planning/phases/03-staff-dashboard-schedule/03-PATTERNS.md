# Phase 3: Staff Dashboard (Schedule) - Pattern Map

**Mapped:** 2026-07-11
**Files analyzed:** 24 (backend: 14, frontend: 10 representative — dashboard app is a full scaffold)
**Analogs found:** 20 / 24 (backend near-total coverage; dashboard scaffold has partial landing-page analogs, no auth/SPA-token precedent exists in repo)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `API/ZachHairStudio.Shared/Features/Identity/ApplicationUser.cs` | model | CRUD | `API/ZachHairStudio.Shared/Features/Appointments/Appointment.cs` | role-match (entity shape only; Identity base class has no repo precedent) |
| `API/ZachHairStudio.Shared/Features/Identity/StaffRoles.cs` | config | — | `API/ZachHairStudio.Shared/Features/Appointments/AppointmentStatus.cs` | role-match (const/enum-style domain constants) |
| `API/ZachHairStudio.Shared/Features/Identity/LoginRequestDto.cs` | model (DTO) | request-response | `API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDto.cs` | role-match |
| `API/ZachHairStudio.Shared/Features/Identity/LoginRequestDtoValidator.cs` | utility (validator) | request-response | `API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDtoValidator.cs` | exact (FluentValidation `AbstractValidator<T>` pattern) |
| `API/ZachHairStudio.Shared/Features/Identity/LoginResponseDto.cs` | model (DTO) | request-response | `API/ZachHairStudio.Shared/Features/Appointments/AppointmentResponseDto.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Identity/StaffUserCreateDto.cs` + validator | model/utility | CRUD | `AppointmentCreateDto.cs` + `AppointmentCreateDtoValidator.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Identity/StaffUserResponseDto.cs` | model (DTO) | request-response | `AppointmentResponseDto.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Identity/JwtOptions.cs` | config | — | `API/ZachHairStudio.Shared/Features/Appointments/ResendOptions.cs` | exact (options-bound POCO, secret via config not tracked file) |
| `API/ZachHairStudio.Shared/Features/Identity/JwtTokenService.cs` | service | request-response | `API/ZachHairStudio.Shared/Features/Appointments/ResendEmailService.cs` | role-match (injected service wrapping an external/crypto concern) |
| `API/ZachHairStudio.Shared/Features/Identity/IdentitySeeder.cs` | service | batch (startup) | `BookingDbContext.OnModelCreating` seed data (`HasData` blocks) | role-match (seed-at-startup vs seed-via-migration — different mechanism, same "known-good defaults" intent) |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentStatusUpdateDto.cs` + validator | model/utility | request-response | `AppointmentCreateDto.cs` + validator | exact |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs` (extended: `ListByDateRangeAsync`, `GetByIdAsync`, `UpdateStatusAsync`) | service | CRUD + transform | same file, `CreateAsync` method (existing) | exact — same class, extend in place |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentResponseDto.cs` (extended: `StatusChangedAt`, `StatusChangedBy`) | model (DTO) | request-response | same file (existing) | exact — extend in place |
| `API/ZachHairStudio.Api/Controllers/AuthController.cs` | controller | request-response | `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs` | exact (validator injection + `Result<T>`→ProblemDetails translation) |
| `API/ZachHairStudio.Api/Controllers/StaffUsersController.cs` | controller | CRUD | `AppointmentsController.cs` | exact |
| `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs` (extended: GET range, GET by id, PATCH status, `[Authorize]`) | controller | CRUD + request-response | same file (existing) | exact — extend in place |
| `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` (extended: `IdentityDbContext` base, audit columns) | model/config | CRUD | same file (existing) | exact — extend in place |
| `API/ZachHairStudio.Api/Program.cs` (extended: Identity, JwtBearer, CORS dashboard origin) | config | — | same file (existing) | exact — extend in place |
| `dashboard/next.config.ts`, `tsconfig.json`, `package.json` | config | — | `landing-page/next.config.ts`, `tsconfig.json`, `package.json` | exact |
| `dashboard/app/layout.tsx`, `globals.css` | component/config | — | `landing-page/app/layout.tsx`, `globals.css` | exact |
| `dashboard/lib/auth.ts` | utility | request-response | `landing-page/lib/appointments.ts` (fetch wrapper + typed error class) | role-match (no auth/token precedent in repo — pattern is fetch-wrapper conventions only) |
| `dashboard/lib/useSchedule.ts` | hook | polling | `landing-page/lib/appointments.ts` (`fetchOpenSlots`) | role-match (data-fetch shape; polling itself has no analog) |
| `dashboard/app/login/page.tsx` | component | request-response | `landing-page/components/Contact.tsx` | role-match (client-component form with `useState` + `handleSubmit`) |
| `dashboard/components/DayGrid.tsx`, `WeekChips.tsx`, `AppointmentBlock.tsx`, `AppointmentDetailPanel.tsx`, `ScheduleToolbar.tsx` | component | transform | `landing-page/components/AppointmentBookingForm.tsx` (multi-step booking UI over slot/appointment data) | role-match (closest existing appointment-data-rendering component; no calendar-grid precedent exists) |

## Pattern Assignments

### `API/ZachHairStudio.Shared/Features/Identity/*Dto*.cs` + validators (model/utility, request-response)

**Analog:** `API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDto.cs` + `AppointmentCreateDtoValidator.cs`

**Validator pattern** (`AppointmentCreateDtoValidator.cs` lines 10-47):
```csharp
public class AppointmentCreateDtoValidator : AbstractValidator<AppointmentCreateDto>
{
    public AppointmentCreateDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(150);

        RuleFor(x => x.StartsAt)
            .Must(BeInTheFuture).WithMessage("StartsAt must be in the future.");
    }

    private static bool BeInTheFuture(DateTimeOffset startsAt)
        => startsAt > DateTimeOffset.UtcNow;
}
```
Copy this exact `AbstractValidator<T>` + private static predicate-method shape for `LoginRequestDtoValidator`, `StaffUserCreateDtoValidator`, and `AppointmentStatusUpdateDtoValidator`. Register via the existing assembly-scan call in `Program.cs` (`AddValidatorsFromAssemblyContaining<ServiceCreateDtoValidator>()` — no new registration call needed, same assembly).

**Response DTO shape** (`AppointmentResponseDto.cs` — a plain flat POCO, no methods, comment above class explaining audience). Mirror for `LoginResponseDto` (`Token`, `ExpiresAt`, `DisplayName`, `Role`) and `StaffUserResponseDto`.

---

### `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs` — extend with status-update + queries (service, CRUD/transform)

**Analog:** same file, existing `CreateAsync` method (lines 42-141)

Key conventions to carry forward:
- Constructor DI of `BookingDbContext` directly (no repository abstraction) — lines 30-40.
- Return `Result<T>` from every method, never throw for expected failure paths (`NotFoundError`, `ValidationError`, `DuplicateRecordError`) — see `Result.cs` factory methods.
- XML-doc comment block above the class explaining the "why," referencing decision IDs (`D-XX`) — lines 10-20. Do the same above `UpdateStatusAsync`, citing D-10/D-11/D-12.
- **Slot-release reuse (critical, from RESEARCH.md Pitfall 3):** Cancel/No-show must call the exact same `AppointmentSlot` removal path Phase 2's cancel uses — do not fork logic. There is currently no explicit "cancel" method to reuse from (Phase 2 only ships `CreateAsync`); if a cancel path doesn't yet exist elsewhere, `UpdateStatusAsync` **is** that single reusable path — write it once here.
- Duplicate-key handling pattern (`IsDuplicateKeyViolation`, lines 173-175) — not needed for status update, but keep the same private-static-helper style for the transition-guard predicate (`AllowedTransitions` map, per RESEARCH.md Pattern 3).

---

### `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs` — extend with `[Authorize]` + new actions (controller, CRUD/request-response)

**Analog:** same file, existing `CreateAppointment` action (lines 37-83)

**Imports pattern** (lines 1-5):
```csharp
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;
```
Add `using Microsoft.AspNetCore.Authorization;` for `[Authorize]`/`[AllowAnonymous]`.

**Constructor DI pattern** (lines 13-25): inject services + `IValidator<TDto>` directly, no factory/mediator layer.

**Result→ProblemDetails translation** (lines 51-83) — this is the canonical error-mapping template for the whole phase:
```csharp
var result = await _appointmentsService.CreateAsync(request);

if (result.IsValidationError())
{
    ModelState.AddModelError(string.Empty, result.Message);
    return ValidationProblem(ModelState);
}

if (result.IsNotFound())
{
    return NotFound(new ProblemDetails
    {
        Title = "Slot unavailable",
        Detail = result.Message,
        Status = StatusCodes.Status404NotFound,
    });
}
```
Reuse verbatim for `UpdateStatus` (400 on `IsValidationError()` for an invalid transition per D-10, 404 on `IsNotFound()` for a missing appointment id).

**Controller-level `[Authorize]` + per-action `[AllowAnonymous]`** — no existing analog in this repo (first authenticated controller), but RESEARCH.md's Code Examples section already gives the exact shape to apply here (`[Authorize]` at class level on `AppointmentsController`; `[AllowAnonymous]` on `CreateAppointment` and `GetSlots`; new GET-range/GET-by-id/PATCH-status actions inherit the class-level `[Authorize]`).

---

### `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` — extend with Identity + audit columns (model/config, CRUD)

**Analog:** same file, `Appointment` entity configuration block (lines 176-191)

```csharp
modelBuilder.Entity<Appointment>(entity =>
{
    entity.Property(e => e.Status)
          .HasConversion<string>()
          .HasMaxLength(50);

    entity.Property(e => e.FirstName).HasMaxLength(100);
    ...
});
```
Add `StatusChangedAt` (DateTimeOffset?) and `StatusChangedBy` (string?, sized like `FirstName`/`LastName` at `HasMaxLength(100)`) to the `Appointment` entity class and this same `modelBuilder.Entity<Appointment>` block — do not create a separate configuration block. Switch the context declaration itself to `IdentityDbContext<ApplicationUser, IdentityRole, int>` per RESEARCH.md Pattern 1 (keep `int` keys, consistent with every other entity's `int Id` in this file — `Service.Id`, `Stylist.Id`, `Appointment.Id` all use `int`).

Migration convention already established: EF migrations own the schema (`db.Database.Migrate()` in `Program.cs` line 66, skipped only in `Testing`); Identity tables and the two new columns arrive via a single new migration, not `EnsureCreated()`.

---

### `API/ZachHairStudio.Api/Program.cs` — extend with Identity/JwtBearer/CORS (config)

**Analog:** same file (full read above)

Existing CORS block to extend (lines 27-31):
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
```
Per RESEARCH.md Pitfall 2, `AllowAnyOrigin()` already admits the dashboard origin — no code change is strictly required for CORS to keep functioning, but explicitly note/confirm this in the new auth wiring rather than silently assuming it.

Existing options-binding + secret-from-config pattern to mirror for JWT signing key (lines 43-54, `ResendOptions`/`RESEND_API_KEY`):
```csharp
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Resend"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ResendOptions>>().Value);
```
Apply identically for `JwtOptions` — signing key read from `builder.Configuration["Jwt:SigningKey"]` sourced via user-secrets (dev) / env var (prod), never a tracked appsettings value, exactly like `RESEND_API_KEY` (D-13-style, per RESEARCH.md Pitfall 5).

`AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<BookingDbContext>()` and `AddAuthentication(...).AddJwtBearer(...)` insert alongside the existing `AddDbContext<BookingDbContext>` call (lines 20-25); `app.UseAuthentication()` must be added before the existing `app.UseAuthorization()` call (line 78) — this line is currently missing entirely (no auth middleware exists yet) and is a required addition, not an extension of existing code.

---

### `dashboard/` scaffold (component/config/hook, various)

**Analog root:** `landing-page/` (full parallel Next.js 15 / React 19 / Tailwind 4 app)

**package.json / next.config.ts / tsconfig.json** — copy `landing-page/`'s versions verbatim (Next 15.3.0, React 19.1.0, Tailwind 4.1.0, TypeScript 5.8.0, `@/*` path alias), add `swr`, `openapi-fetch`, and dev-dep `openapi-typescript` per RESEARCH.md's Standard Stack install block.

**layout.tsx + globals.css brand tokens** (`landing-page/app/layout.tsx` full file, `globals.css` lines 1-12):
```typescript
const playfair = Playfair_Display({ subsets: ["latin"], variable: "--font-playfair", display: "swap" });
const inter = Inter({ subsets: ["latin"], variable: "--font-inter", display: "swap" });
```
```css
@theme {
  --color-gold: #d4af6a;
  --color-charcoal: #181410;
  --font-serif: var(--font-playfair), Georgia, Cambria, "Times New Roman", serif;
  --font-sans: var(--font-inter), system-ui, -apple-system, "Segoe UI", sans-serif;
}
```
Copy these tokens directly into `dashboard/app/globals.css` per D-15 ("branded but utilitarian" — same palette, denser/lighter-surface layout, not a new palette).

**Typed fetch client + error class pattern** (`landing-page/lib/appointments.ts` full file — `AppointmentApiError` class lines 73-93, `extractErrorMessage` lines 96-117, `fetchOpenSlots` lines 133-175):
```typescript
export class AppointmentApiError extends Error {
  readonly status: number | null;
  constructor(message: string, status: number | null) {
    super(message);
    this.name = "AppointmentApiError";
    this.status = status;
  }
  get isConflict(): boolean { return this.status === 409; }
  get isValidation(): boolean { return this.status === 400; }
  get isNetwork(): boolean { return this.status === null; }
}
```
Mirror this exact typed-error-with-status-getters shape for `dashboard/lib/auth.ts`'s API error handling (e.g., `isUnauthorized` getter for 401 → redirect-to-login), and reuse `extractErrorMessage`'s ProblemDetails/ModelState-parsing logic verbatim since the backend error shape is identical (`Result<T>` → `ValidationProblem`/`ProblemDetails`).

**Client-component form pattern** (`landing-page/components/Contact.tsx` full file — `"use client"` directive line 1, `useState` + `handleSubmit` lines 45-57, `Field` sub-component lines 23-30):
```typescript
"use client";
import { useState } from "react";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="text-gray-400 text-xs uppercase tracking-wider block mb-2">{label}</label>
      {children}
    </div>
  );
}
```
Use this `Field` wrapper + `inputClass` constant + controlled-input pattern for `dashboard/app/login/page.tsx`'s email/password form (adapt copy to utilitarian tone per D-15 — dense, not marketing).

**No analog:** JWT storage/attach-header logic, SWR polling hook, and the day-view time-grid have no precedent anywhere in this codebase — build per RESEARCH.md's Architecture Patterns 2/4 and Recommended Project Structure directly; see "No Analog Found" below.

## Shared Patterns

### `Result<T>` → ProblemDetails translation
**Source:** `API/ZachHairStudio.Shared/Result.cs` (full file) + `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs` lines 51-83
**Apply to:** `AuthController`, `StaffUsersController`, extended `AppointmentsController` actions (GET range/detail, PATCH status)
```csharp
if (result.IsValidationError())
{
    ModelState.AddModelError(string.Empty, result.Message);
    return ValidationProblem(ModelState);
}
if (result.IsNotFound())
{
    return NotFound(new ProblemDetails { Title = "...", Detail = result.Message, Status = StatusCodes.Status404NotFound });
}
```

### FluentValidation on write DTOs (PLAT-02)
**Source:** `API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDtoValidator.cs` (full file)
**Apply to:** `LoginRequestDtoValidator`, `StaffUserCreateDtoValidator`, `AppointmentStatusUpdateDtoValidator` — `AbstractValidator<T>` + `RuleFor` + private static predicate methods, registered automatically via the existing `AddValidatorsFromAssemblyContaining<ServiceCreateDtoValidator>()` scan (`Program.cs` line 34) — no new DI registration line needed.

### Options-bound config + secret-from-env (never a tracked file)
**Source:** `API/ZachHairStudio.Shared/Features/Appointments/ResendOptions.cs` + `Program.cs` lines 43-54 (`Configure<ResendOptions>` / `RESEND_API_KEY` from user-secrets/env)
**Apply to:** `JwtOptions` (signing key), Identity Owner-seed credentials (D-04) — same "user-secrets in dev, env var in prod, never appsettings.json" discipline gitleaks already enforces.

### Feature-folder entity→DTO→validator→mapper→service template
**Source:** `API/ZachHairStudio.Shared/Features/Appointments/` (whole directory) and `Features/Services/` (whole directory)
**Apply to:** New `Features/Identity/` folder — same file-per-concern layout (entity, `*CreateDto`, `*ResponseDto`, `*Validator`, `*Extensions` mapper, `*Service`).

### Typed API-client fetch wrapper with a status-carrying error class
**Source:** `landing-page/lib/appointments.ts` (full file, `AppointmentApiError` + `extractErrorMessage`)
**Apply to:** `dashboard/lib/auth.ts` and any hand-written pre-generation client glue around the OpenAPI-generated client — same error-with-status-getters shape, same ProblemDetails-parsing helper.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `API/ZachHairStudio.Shared/Features/Identity/ApplicationUser.cs` (Identity base-class inheritance) | model | CRUD | No `IdentityUser`-derived class exists yet in the repo; follow RESEARCH.md Pattern 1 (Context7 ASP.NET Core docs) directly. |
| `API/ZachHairStudio.Shared/Features/Identity/JwtTokenService.cs` (token minting) | service | request-response | No JWT-issuance code exists yet; follow RESEARCH.md Pattern 2's token-minting example directly. |
| `dashboard/lib/auth.ts` (token storage/attach/401 handling) | utility | request-response | No client-held-bearer-token precedent — landing-page never authenticates. Follow RESEARCH.md's Architectural Responsibility Map row "JWT storage & attach-to-request." |
| `dashboard/lib/useSchedule.ts` (SWR polling hook) | hook | polling | No polling/revalidation hook exists in the repo (landing-page fetches are one-shot server/client fetches). Follow RESEARCH.md Pattern 4 (SWR `refreshInterval` + `revalidateOnFocus`) directly. |
| `dashboard/components/DayGrid.tsx` (stylist-column time-grid) | component | transform | No calendar/grid-rendering component exists; `AppointmentBookingForm.tsx` is the closest appointment-data UI but is a linear multi-step form, not a grid. Hand-roll per RESEARCH.md's Don't-Hand-Roll table conclusion (CSS Grid over Phase 2's 15-minute slot data). |

## Metadata

**Analog search scope:** `API/ZachHairStudio.Shared/Features/*`, `API/ZachHairStudio.Api/Controllers/*`, `API/ZachHairStudio.Shared/Db/BookingDbContext.cs`, `API/ZachHairStudio.Api/Program.cs`, `API/ZachHairStudio.Shared/Result.cs`, `landing-page/app/*`, `landing-page/components/*`, `landing-page/lib/*`, `landing-page/package.json`/`tsconfig.json`/`next.config.ts`
**Files scanned:** ~30 (14 backend feature/controller/db/program files, ~16 frontend files/configs)
**Pattern extraction date:** 2026-07-11
