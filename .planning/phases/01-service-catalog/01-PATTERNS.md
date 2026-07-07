# Phase 1: Service Catalog - Pattern Map

**Mapped:** 2026-07-07
**Files analyzed:** 20
**Analogs found:** 17 / 20

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `API/ZachHairStudio.Shared/Features/Services/Service.cs` | model | CRUD | `API/ZachHairStudio.Shared/Features/Bookings/Booking.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Services/ServiceCreateDto.cs` | model (DTO) | request-response | `.../Bookings/BookingCreateDto.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Services/ServiceUpdateDto.cs` | model (DTO) | request-response | `.../Bookings/BookingCreateDto.cs` | role-match (no Update DTO precedent exists) |
| `API/ZachHairStudio.Shared/Features/Services/ServiceResponseDto.cs` | model (DTO) | request-response | `.../Bookings/BookingResponseDto.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Services/ServiceExtensions.cs` | utility (mapper) | transform | `.../Bookings/BookingExtensions.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Services/ServiceCreateDtoValidator.cs` | utility (validator) | request-response | none in repo (RESEARCH.md Code Examples) | no analog — new pattern |
| `API/ZachHairStudio.Shared/Features/Services/ServiceUpdateDtoValidator.cs` | utility (validator) | request-response | none in repo (RESEARCH.md Code Examples) | no analog — new pattern |
| `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` | service | CRUD | none in repo — controller currently owns this logic (`BookingsController.cs`); RESEARCH.md Pattern 1 is the template | no analog — new pattern (extract from Bookings controller logic) |
| `API/ZachHairStudio.Api/Controllers/ServicesController.cs` | controller | request-response | `API/ZachHairStudio.Api/Controllers/BookingsController.cs` | role-match (shape copies, but DbContext access must NOT be copied — see Anti-Pattern below) |
| `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` (modify: add DbSet, config, seed) | config | CRUD | itself, existing `Booking` config block | exact |
| `API/ZachHairStudio.Api/Program.cs` (modify: DI registrations) | config | event-driven (startup) | itself | exact |
| `API/ZachHairStudio.Shared/Migrations/<ts>_AddServices.cs` | migration | batch | existing Bookings-era migrations dir | role-match |
| `landing-page/app/services/page.tsx` | component (RSC page) | request-response | `landing-page/app/page.tsx` (composition root, no data fetch precedent) | role-match |
| `landing-page/app/services/[slug]/page.tsx` | component (RSC page) | request-response | none — first dynamic route in repo | no analog — new pattern (RESEARCH.md Pattern 4) |
| `landing-page/lib/services.ts` | service (data-fetching + Zod) | request-response | `landing-page/lib/api.ts` | role-match (api.ts is client POST; services.ts is RSC GET) |
| `landing-page/lib/data.ts` (modify: remove `services`, `serviceOptions`) | config/data | CRUD | itself | exact |
| `landing-page/components/Services.tsx` (modify: API-backed) | component | request-response | itself (current static version) | exact |
| `landing-page/components/Contact.tsx` (modify: dropdown from API, `?service=slug` preselect) | component | request-response | itself (current static version) | exact |
| `landing-page/components/SectionHeading.tsx` | component | — | itself (reused as-is) | exact |
| `landing-page/lib/formatDuration.ts` (new util, Pitfall 5) | utility | transform | none — new shared helper | no analog — new pattern |

## Pattern Assignments

### `API/ZachHairStudio.Shared/Features/Services/Service.cs` (model, CRUD)

**Analog:** `API/ZachHairStudio.Shared/Features/Bookings/Booking.cs`

**Full pattern** (entire file, 34 lines):
```csharp
using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Bookings;

public class Booking
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string FirstName { get; set; } = null!;
    // ... other [Required]/[StringLength]/[Phone]/[EmailAddress] annotated properties
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Computed property pattern — mirror for any derived display fields
    public string CustomerName => $"{FirstName} {LastName}";
}
```

**Apply to Service.cs:** Plain POCO with DataAnnotations for defense-in-depth (even though FluentValidation is primary per D-16/PLAT-02), `Id` as int PK, default values set inline (`IsActive = true`, `CreatedAt = DateTime.UtcNow` not needed here since Service has no CreatedAt in CONTEXT.md — omit unless added). Fields per CONTEXT.md D-05..D-11: `Slug`, `Name`, `ShortDescription`, `LongDescription`, `Category` (string), `DurationMinutes` (int), `Price` (decimal), `ImageUrl` (string?), `IsActive` (bool, default true), `DisplayOrder` (int).

---

### `API/ZachHairStudio.Shared/Features/Services/ServiceCreateDto.cs` / `ServiceResponseDto.cs` (DTOs, request-response)

**Analog:** `BookingCreateDto.cs` (lines 1-27), `BookingResponseDto.cs` (lines 1-16)

**Core pattern:** Create DTO excludes server-set fields (Id, Status→here IsActive/DisplayOrder defaults); Response DTO includes everything the client needs including computed fields:
```csharp
public class BookingResponseDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    // ...
    public string CustomerName => $"{FirstName} {LastName}";
}
```
**Apply:** `ServiceResponseDto` mirrors `Service` entity fields (id, slug, name, shortDescription, longDescription, category, durationMinutes, price, imageUrl, displayOrder — omit isActive from public response per D-09, since it's filtered server-side already). `ServiceUpdateDto` has no existing analog in the repo (Bookings has no full-update DTO, only a status-only endpoint) — model it on `ServiceCreateDto` but include all mutable fields including `IsActive`/`DisplayOrder` for staff-facing Phase 4 reuse.

---

### `API/ZachHairStudio.Shared/Features/Services/ServiceExtensions.cs` (mapper, transform)

**Analog:** `BookingExtensions.cs` (full file, 34 lines)

```csharp
namespace ZachHairStudio.Shared.Features.Bookings;

public static class BookingExtensions
{
    public static BookingResponseDto ToDto(this Booking booking)
        => new BookingResponseDto { Id = booking.Id, FirstName = booking.FirstName, /* ... */ };

    public static Booking ToEntity(this BookingCreateDto createDto)
        => new Booking { FirstName = createDto.FirstName, /* ... */ Status = BookingStatus.Pending, CreatedAt = DateTime.UtcNow };
}
```
**Apply:** Static class `ServiceExtensions` in `ZachHairStudio.Shared.Features.Services` namespace, with `ToDto()` on `Service`, `ToEntity()` on `ServiceCreateDto`, and (new, no precedent) an `ApplyTo(Service entity)` extension on `ServiceUpdateDto` for the PUT path — follow the same flat property-copy style, setting `IsActive = true` and `DisplayOrder` from create input directly (not hardcoded, since D-10 makes DisplayOrder client-settable) rather than defaulted like Booking's `Status`.

---

### `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` (service, CRUD) — NEW PATTERN

**No direct analog** — this is the first service-layer class in the codebase (PLAT-01 activates it). Use RESEARCH.md Pattern 1 verbatim as the template (already vetted against `Result<T>` shape below and FluentValidation official docs):

```csharp
public class ServicesService
{
    private readonly BookingDbContext _dbContext;
    private readonly IValidator<ServiceCreateDto> _createValidator;

    public ServicesService(BookingDbContext dbContext, IValidator<ServiceCreateDto> createValidator)
    {
        _dbContext = dbContext;
        _createValidator = createValidator;
    }

    public async Task<IEnumerable<ServiceResponseDto>> GetActiveServicesAsync()
        => await _dbContext.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .Select(s => s.ToDto())
            .ToListAsync();

    public async Task<Result<ServiceResponseDto>> GetBySlugAsync(string slug)
    {
        var service = await _dbContext.Services.FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);
        return service is null
            ? Result<ServiceResponseDto>.NotFoundError($"Service '{slug}' not found")
            : Result<ServiceResponseDto>.Success(service.ToDto());
    }
    // CreateAsync / UpdateAsync follow the same Result<T> pattern (see RESEARCH.md Pattern 1)
}
```

**Extraction source for query logic:** `BookingsController.GetBookings()` (lines 19-28) and `GetBooking(id)` (lines 30-40) show the existing `_dbContext.X.Where/OrderBy/Select/FirstOrDefaultAsync` query idiom to move into the service.

---

### `API/ZachHairStudio.Api/Controllers/ServicesController.cs` (controller, request-response)

**Analog:** `BookingsController.cs` (full file, 71 lines) — copy the controller *shape* (constructor injection, `[ApiController]`/`[Route("api/[controller]")]`, action method signatures returning `ActionResult<T>`), but **do not** copy the DbContext dependency:

```csharp
[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly BookingDbContext _dbContext;   // <-- DO NOT REPLICATE for ServicesController (PLAT-01/D-19)
    public BookingsController(BookingDbContext dbContext) { _dbContext = dbContext; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingResponseDto>>> GetBookings()
    {
        var bookings = await _dbContext.Bookings.OrderByDescending(b => b.CreatedAt).Select(b => b.ToDto()).ToListAsync();
        return Ok(bookings);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingResponseDto>> GetBooking(int id)
    {
        var booking = await _dbContext.Bookings.FindAsync(id);
        if (booking is null) return NotFound();
        return Ok(booking.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<BookingResponseDto>> CreateBooking([FromBody] BookingCreateDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var booking = request.ToEntity();
        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking.ToDto());
    }
}
```

**Apply to ServicesController:** inject `ServicesService` instead of `BookingDbContext`. Use RESEARCH.md Pattern 2 for the `CreateService`/`UpdateService` actions (`ValidateAsync` → `AddToModelState` → `ValidationProblem(ModelState)`, else delegate to service and translate `Result<T>` → `ActionResult`). `GetServices()`/`GetService(slug)` call `ServicesService.GetActiveServicesAsync()`/`GetBySlugAsync(slug)` and return `Ok(...)` / `NotFound()` per `Result<T>.IsNotFound()`.

---

### `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` (modify)

**Analog:** itself, lines 15-32 (existing `OnModelCreating` block for `Booking`)

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Booking>(entity =>
    {
        entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        entity.Property(e => e.FirstName).HasMaxLength(100);
        // ...
    });
    base.OnModelCreating(modelBuilder);
}
```
**Apply:** Add `public DbSet<Service> Services => Set<Service>();` alongside `Bookings`. Add a second `modelBuilder.Entity<Service>(entity => { ... entity.HasIndex(e => e.Slug).IsUnique(); ... entity.HasData(...seed rows from lib/data.ts per D-12...); });` block before `base.OnModelCreating(modelBuilder)`. See RESEARCH.md Pattern 3 for the exact `HasData()` seeding syntax and Pitfall 3 for the unique-index requirement on `Slug`.

---

### `API/ZachHairStudio.Api/Program.cs` (modify)

**Analog:** itself, lines 8-24 (existing DI registration block)

```csharp
builder.Services.AddDbContext<BookingDbContext>(options => options.UseSqlServer(connectionString, ...));
builder.Services.AddControllers();
```
**Apply:** Add `builder.Services.AddValidatorsFromAssemblyContaining<ServiceCreateDtoValidator>();` (FluentValidation.DependencyInjectionExtensions) and `builder.Services.AddScoped<ServicesService>();` in the same block, before `builder.Build()`. No change needed to the `db.Database.Migrate()` call (lines 28-32) — `HasData()` seeding rides along automatically.

---

### `landing-page/lib/services.ts` (service, request-response) — NEW PATTERN

**Analog:** `landing-page/lib/api.ts` (full file, 84 lines) for the API_BASE_URL constant, typed request/response shapes, and `extractErrorMessage` error-parsing convention:

```typescript
const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236"
).replace(/\/$/, "");

export type BookingResponse = { id: number; firstName: string; /* ... */ };

export async function createBooking(data: BookingRequest): Promise<BookingResponse> {
  let res: Response;
  try {
    res = await fetch(`${API_BASE_URL}/api/bookings`, { method: "POST", ... });
  } catch {
    throw new Error("We couldn't reach the booking service...");
  }
  if (!res.ok) throw new Error(await extractErrorMessage(res));
  return res.json();
}
```
**Apply:** Reuse the same `API_BASE_URL` constant (duplicate it or extract to a shared `lib/config.ts` — Claude's discretion). Add `fetchServices()`/`fetchServiceBySlug(slug)` as RSC-side `fetch()` calls with `next: { revalidate: 60 }` (per RESEARCH.md Pattern 4 and D-03), each piped through a Zod schema `.parse()` (RESEARCH.md Code Examples → `ServiceSchema`/`ServiceListSchema`) instead of returning `res.json()` directly. Do NOT reuse `extractErrorMessage` for these GET calls (it's designed for form-submission error display) — a thrown Zod/fetch error is sufficient for RSC pages (no user-facing form here).

---

### `landing-page/app/services/page.tsx` and `[slug]/page.tsx` (RSC pages, request-response) — NEW PATTERN

**No existing page analog** (repo only has `app/page.tsx`, a single composed homepage with no data fetching). Use RESEARCH.md Pattern 4 directly:
```tsx
async function getServices() {
  const res = await fetch(`${API_BASE_URL}/api/services`, { next: { revalidate: 60 } });
  const json = await res.json();
  return ServiceListSchema.parse(json);
}

export default async function ServicesPage() {
  const services = await getServices();
  return <CatalogGrid services={services} />;
}
```
Reuse `SectionHeading` (full file, `landing-page/components/SectionHeading.tsx`) for page titles, matching the `eyebrow`/`title`/`highlight`/`subtitle` prop shape already used by `Services.tsx` (lines 8-13).

---

### `landing-page/components/Services.tsx` and `Contact.tsx` (modify)

**Analog:** themselves (current static versions), full files.

**Services.tsx core pattern to preserve** (lines 15-47): the `.map()` over a services array rendering card markup — keep card markup/classes, swap `services` import from `@/lib/data` to a call into `lib/services.ts`'s `fetchServices()` (server component — component itself would need `async function Services()`).

**Contact.tsx service dropdown pattern to preserve** (lines 157-173): `<select name="service">` mapping over an options array with `value`/`label`. Swap `serviceOptions` (from `lib/data.ts`, to be removed per D-14) for API-fetched services rendered as `{service.name} – ${service.price}`. For the `?service=slug` preselect (D-04), read `useSearchParams()` (Contact.tsx is already `"use client"`, line 1) and set as `defaultValue` on the select.

---

## Shared Patterns

### Result<T> error/success wrapper
**Source:** `API/ZachHairStudio.Shared/Result.cs` (full file, 57 lines)
**Apply to:** `ServicesService` — every method returns `Result<T>` via static factories `Success()`, `NotFoundError()`, `ValidationError()`. Controller checks `result.IsSuccess`/`result.IsNotFound()` to pick the `ActionResult`.
```csharp
public static Result<T> Success(T data, string message = "Success") => ...
public static Result<T> NotFoundError(string message = "Not Found", T? data = default) => ...
public static Result<T> ValidationError(string message, T? data = default) => ...
```

### ProblemDetails/ModelState error shape (frontend already parses it)
**Source:** `landing-page/lib/api.ts`, `extractErrorMessage()` (lines 63-83)
**Apply to:** `ServicesController` write endpoints — always return errors via `BadRequest(ModelState)`/`ValidationProblem(ModelState)` so the existing `{ errors: {...} }` / `{ title: "..." }` shape stays valid; do not invent a new envelope.

### DbContext-owns-migrations-and-config pattern
**Source:** `BookingDbContext.cs` (full file)
**Apply to:** Add `Service` DbSet + fluent config + `HasData()` seed in the same `OnModelCreating` override, following the existing `entity.Property(...).HasMaxLength(...)` style already used for `Booking`.

### FluentValidation manual invocation (no automatic MVC filter)
**Source:** RESEARCH.md Pattern 1/2 and Anti-Patterns section (no repo precedent exists yet)
**Apply to:** `ServicesService` and `ServicesController` both call `IValidator<T>.ValidateAsync()` explicitly — never install `FluentValidation.AspNetCore`.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `ServiceCreateDtoValidator.cs` / `ServiceUpdateDtoValidator.cs` | utility (validator) | request-response | First FluentValidation validators in the repo — use RESEARCH.md Code Examples verbatim as the template (rule-per-field `RuleFor(...)` chains) |
| `ServicesService.cs` | service | CRUD | First per-feature service-layer class (PLAT-01 activation) — extracted from `BookingsController`'s inline DbContext queries per RESEARCH.md Pattern 1 |
| `app/services/[slug]/page.tsx` | component (RSC) | request-response | First dynamic App Router route in the repo — use RESEARCH.md Pattern 4 |
| `lib/formatDuration.ts` | utility | transform | No existing formatting helpers to copy; write fresh per Pitfall 5 (single source of truth for "45 min" style formatting shared by list + detail views) |

## Metadata

**Analog search scope:** `API/ZachHairStudio.Shared/Features/Bookings/`, `API/ZachHairStudio.Api/Controllers/`, `API/ZachHairStudio.Shared/Db/`, `API/ZachHairStudio.Api/Program.cs`, `API/ZachHairStudio.Shared/Result.cs`, `landing-page/lib/`, `landing-page/components/`, `landing-page/app/`
**Files scanned:** 13 (all Bookings feature files, Result.cs, BookingDbContext.cs, Program.cs, data.ts, api.ts, Services.tsx, SectionHeading.tsx, Contact.tsx, app/page.tsx listing)
**Pattern extraction date:** 2026-07-07
