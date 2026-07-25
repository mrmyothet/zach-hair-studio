# Phase 2: Booking Core - Pattern Map

**Mapped:** 2026-07-09
**Files analyzed:** 24
**Analogs found:** 21 / 24

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `API/ZachHairStudio.Shared/Features/Stylists/Stylist.cs` | model | CRUD | `Features/Services/Service.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Stylists/StylistResponseDto.cs` | model (DTO) | request-response | `Features/Services/ServiceResponseDto.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Stylists/StylistExtensions.cs` | utility (mapper) | transform | `Features/Services/ServiceExtensions.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Stylists/StylistsService.cs` | service | CRUD (read-only) | `Features/Services/ServicesService.cs` (read methods) | role-match |
| `API/ZachHairStudio.Api/Controllers/StylistsController.cs` | controller | request-response | `Controllers/ServicesController.cs` (GET actions) | exact |
| `API/ZachHairStudio.Shared/Features/Availability/StylistWorkingHours.cs` | model | CRUD | `Features/Services/Service.cs` | role-match |
| `API/ZachHairStudio.Shared/Features/Availability/StylistTimeOff.cs` | model | CRUD | `Features/Services/Service.cs` | role-match |
| `API/ZachHairStudio.Shared/Features/Availability/SlotService.cs` | service | transform (read/compute) | `Features/Services/ServicesService.cs` (query shape) + RESEARCH.md Pattern 2 | partial (novel grid math) |
| `API/ZachHairStudio.Shared/Features/Appointments/Appointment.cs` | model | CRUD | `Features/Bookings/Booking.cs` (being retired) + `Service.cs` | role-match |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentSlot.cs` | model | CRUD | `Features/Services/Service.cs` (entity+HasIndex pattern) | partial (novel unique-index child table) |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentStatus.cs` | model (enum) | — | `Features/Bookings/BookingStatus.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDto.cs` | model (DTO) | request-response | `Features/Services/ServiceCreateDto.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDtoValidator.cs` | utility (validator) | request-response | `Features/Services/ServiceCreateDtoValidator.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentResponseDto.cs` | model (DTO) | request-response | `Features/Services/ServiceResponseDto.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentExtensions.cs` | utility (mapper) | transform | `Features/Services/ServiceExtensions.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs` | service | CRUD + event-driven (retry-on-conflict insert) | `Features/Services/ServicesService.cs` (create flow) + RESEARCH.md Pattern 1 | partial (novel retry loop) |
| `API/ZachHairStudio.Shared/Features/Appointments/EmailService.cs` | service | event-driven (best-effort external call) | none in-repo | no analog — see RESEARCH.md Pattern 4 |
| `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs` | controller | request-response | `Controllers/ServicesController.cs` (POST + ValidationProblem flow) | exact |
| `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` (modified) | config | CRUD | itself (`OnModelCreating` Service block) | exact |
| `API/ZachHairStudio.Api/Program.cs` (modified) | config | — | itself (`ServicesService`/validator DI registration block) | exact |
| `API/ZachHairStudio.Api.Tests/SqlServerWebApplicationFactory.cs` (new) | test (fixture) | — | `CustomWebApplicationFactory.cs` | role-match (swap InMemory→SqlServer) |
| `API/ZachHairStudio.Api.Tests/Features/Appointments/*Tests.cs` | test | — | `Features/Services/ServicesServiceTests.cs`, `ServiceCreateDtoValidatorTests.cs`, `ServicesControllerTests.cs` | exact |
| `landing-page/lib/appointments.ts` | utility (API client) | request-response | `landing-page/lib/services.ts` | exact |
| `landing-page/components/AppointmentBookingForm.tsx` | component | request-response | `landing-page/components/BookingRequestForm.tsx` | role-match (progressive reveal is new) |
| `landing-page/app/book/page.tsx` (modified) | route (RSC page) | request-response | itself (current version) | exact |

**Deleted, not created (no pattern needed):** `Features/Bookings/*` (5 files), `Controllers/BookingsController.cs`, `landing-page/components/BookingRequestForm.tsx`, `createBooking`/`BookingRequest`/`BookingResponse` in `landing-page/lib/api.ts`.

## Pattern Assignments

### `API/ZachHairStudio.Shared/Features/Stylists/Stylist.cs` (model, CRUD)

**Analog:** `API/ZachHairStudio.Shared/Features/Services/Service.cs`

Copy the plain-POCO-with-DataAnnotations shape exactly:
```csharp
using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Services;

public class Service
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Slug { get; set; } = null!;

    [Required, StringLength(150)]
    public string Name { get; set; } = null!;
    ...
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
```
`Stylist` needs `Id, Slug, Name, IsActive, DisplayOrder` (per D-05) — same fields minus the description/category/duration/price fields. Namespace: `ZachHairStudio.Shared.Features.Stylists`.

---

### `API/ZachHairStudio.Shared/Features/Stylists/StylistsService.cs` (service, CRUD read-only)

**Analog:** `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` lines 23-28

```csharp
public async Task<IEnumerable<ServiceResponseDto>> GetActiveServicesAsync()
    => await _dbContext.Services
        .Where(service => service.IsActive)
        .OrderBy(service => service.DisplayOrder)
        .Select(service => service.ToDto())
        .ToListAsync();
```
Copy this exact `Where(IsActive).OrderBy(DisplayOrder).Select(ToDto).ToListAsync()` shape for `StylistsService.GetActiveStylistsAsync()`. Constructor injection of `BookingDbContext` only (no validators needed if Phase 2 exposes read-only stylist listing).

---

### `API/ZachHairStudio.Api/Controllers/StylistsController.cs` (controller, request-response)

**Analog:** `API/ZachHairStudio.Api/Controllers/ServicesController.cs` lines 1-31

```csharp
[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly ServicesService _servicesService;
    ...
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceResponseDto>>> GetServices()
    {
        var services = await _servicesService.GetActiveServicesAsync();
        return Ok(services);
    }
}
```
`StylistsController` copies this thin-GET-only shape: constructor-inject `StylistsService`, single `[HttpGet]` returning `Ok(stylists)`. No `BookingDbContext` in the controller (PLAT-01 boundary, verified Phase 1).

---

### `API/ZachHairStudio.Shared/Features/Appointments/Appointment.cs` and `AppointmentStatus.cs` (model, CRUD)

**Analog:** `API/ZachHairStudio.Shared/Features/Bookings/Booking.cs` (being retired) and `BookingStatus.cs` for the enum-as-string pattern; `Service.cs` for annotation style.

`BookingStatus` enum shape to copy for `AppointmentStatus` (Confirmed | Cancelled | Completed | NoShow per RESEARCH.md structure section):
```csharp
// Mirrors BookingStatus.cs — plain enum, EF Core stores it as a string via HasConversion<string>()
public enum AppointmentStatus
{
    Confirmed,
    Cancelled,
    Completed,
    NoShow
}
```
`Appointment` entity: `ServiceId` FK (int), `StylistId` FK (int), `DateTimeOffset StartsAt`, `AppointmentStatus Status`, client contact fields (`FirstName`, `LastName`, `Email`, `Phone` — same `[StringLength]` bounds as `Booking.cs`'s corresponding fields: 100/100/150/30), plus a `List<AppointmentSlot> Slots` navigation.

---

### `API/ZachHairStudio.Shared/Features/Appointments/AppointmentSlot.cs` (model, CRUD — novel unique-index child table)

**Analog:** RESEARCH.md Pattern 1 (no direct in-repo analog; `Service.cs`'s `HasIndex(e => e.Slug).IsUnique()` in `BookingDbContext.cs` line 44 is the closest existing unique-index precedent).

```csharp
// AppointmentSlot.cs — one row per occupied 15-minute grid cell
public class AppointmentSlot
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public int StylistId { get; set; }
    public DateTimeOffset SlotStart { get; set; }
}
```
Fluent config in `BookingDbContext.OnModelCreating` (mirror `Service.cs`'s `entity.HasIndex(e => e.Slug).IsUnique()` at line 44, extended to a composite key):
```csharp
entity.HasIndex(s => new { s.StylistId, s.SlotStart }).IsUnique();
entity.Property(s => s.SlotStart).HasColumnType("datetimeoffset(0)");
```

---

### `API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDto.cs` + `AppointmentCreateDtoValidator.cs` (DTO + validator)

**Analog:** `API/ZachHairStudio.Shared/Features/Services/ServiceCreateDto.cs` and `ServiceCreateDtoValidator.cs` (full files, lines 1-41).

Copy the FluentValidation `AbstractValidator<T>` shape verbatim:
```csharp
public class ServiceCreateDtoValidator : AbstractValidator<ServiceCreateDto>
{
    public ServiceCreateDtoValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(150)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase kebab-case.");
        ...
        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0)
            .LessThanOrEqualTo(480);
    }
}
```
`AppointmentCreateDtoValidator` needs equivalent rules for `ServiceId` (>0), `StylistId?` (nullable, >0 if present), `StartsAt` (must be in the future and on-grid — new rule, no existing analog; use `.Must(...)` predicate), `FirstName`/`LastName` (mirror `Booking.cs` field lengths: `MaximumLength(100)`), `Email` (`.EmailAddress()` — check if `Booking`'s validator/DataAnnotations used `[EmailAddress]`; FluentValidation equivalent is `.EmailAddress()`), `Phone` (`MaximumLength(30)`).

---

### `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs` (service, CRUD + retry-on-conflict)

**Analog:** `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` `CreateAsync` (lines 40-54) for the validate→build-entity→`SaveChangesAsync`→`Result<T>.Success` shape; **RESEARCH.md Pattern 1** (lines 242-288) for the novel candidate-retry loop this phase requires (no in-repo analog for this part — it is new).

Base shape to copy from `ServicesService.CreateAsync`:
```csharp
public async Task<Result<ServiceResponseDto>> CreateAsync(ServiceCreateDto request)
{
    var validation = await _createValidator.ValidateAsync(request);
    if (!validation.IsValid)
    {
        return Result<ServiceResponseDto>.ValidationError(
            string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
    }

    var service = request.ToEntity();
    _dbContext.Services.Add(service);
    await _dbContext.SaveChangesAsync();

    return Result<ServiceResponseDto>.Success(service.ToDto());
}
```
Extend per RESEARCH.md Pattern 1's `foreach (var stylistId in candidates)` retry loop — catch `DbUpdateException` where `SqlException.Number` is 2601 or 2627, detach entities, try next candidate; exhaust → `Result<T>.DuplicateRecordError(...)`. **Do not** wrap in a manual `BeginTransactionAsync()` (see RESEARCH.md Pitfall 2 — incompatible with `EnableRetryOnFailure` unless wrapped in `CreateExecutionStrategy().ExecuteAsync`). Fire the best-effort `_emailService.SendConfirmationAsync(...)` strictly after `SaveChangesAsync()` succeeds, per D-11.

---

### `API/ZachHairStudio.Shared/Features/Appointments/EmailService.cs` (service, event-driven external call)

**No in-repo analog** — this is genuinely new infrastructure (no HttpClient-wrapped external service exists yet). Use RESEARCH.md Pattern 4 (lines 375-417) directly:
```csharp
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResendEmailService> _logger;

    public async Task SendConfirmationAsync(Appointment appointment)
    {
        try
        {
            var payload = new { from = "...", to = appointment.ClientEmail, subject = "...", html = BuildConfirmationHtml(appointment) };
            using var response = await _httpClient.PostAsJsonAsync("emails", payload);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Resend confirmation email failed for appointment {AppointmentId}: {StatusCode}", appointment.Id, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend confirmation email threw for appointment {AppointmentId}", appointment.Id);
        }
    }
}
```
Register with `builder.Services.AddHttpClient<IEmailService, ResendEmailService>(...)` in `Program.cs` — follow the DI registration style already used for `AddScoped<ServicesService>()` (line 25) but as a typed `HttpClient`.

---

### `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs` (controller, request-response)

**Analog:** `API/ZachHairStudio.Api/Controllers/ServicesController.cs` `CreateService` action (lines 40-58) for the create+ValidationProblem flow.

```csharp
[HttpPost]
public async Task<ActionResult<ServiceResponseDto>> CreateService([FromBody] ServiceCreateDto request)
{
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
}

private void AddToModelState(ValidationResult validation)
{
    foreach (var error in validation.Errors)
        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
}
```
`AppointmentsController.CreateAppointment` copies this shape but adds a new branch: `if (result.IsDuplicateRecord()) return Conflict(new ProblemDetails { Detail = result.Message, Status = 409 });` (409 "slot taken" — new, not present in `ServicesController` since Phase 1 had no conflict case). Also add `[HttpGet("slots")]` calling `SlotService.GetOpenSlotsAsync(...)`, modeled on `ServicesController.GetServices()` (line 26-31).

---

### `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` (config, modified)

**Analog:** itself — the `Service` entity block in `OnModelCreating` (lines 34-131), specifically the `HasPrecision`, `HasMaxLength`, `HasIndex().IsUnique()`, and `HasData(...)` seeding conventions.

```csharp
modelBuilder.Entity<Service>(entity =>
{
    entity.Property(e => e.Slug).HasMaxLength(150);
    ...
    entity.Property(e => e.Price).HasPrecision(18, 2);
    entity.HasIndex(e => e.Slug).IsUnique();
    entity.HasData(new Service { Id = 1, ... }, ...);
});
```
Add equivalent `modelBuilder.Entity<Stylist>(...)` with `HasData` seeded from `landing-page/lib/data.ts`'s `team` array (`name: "Zin Min"`, `"May Yoon"`, `"Thiri Cho"`, `"Sai Min Htet"` — 4 members, lines 76-113 of `data.ts`) — generate slugs, `IsActive = true`, `DisplayOrder` by array index, per D-05. Add `modelBuilder.Entity<AppointmentSlot>(...)` per the unique-index excerpt above. Remove the `Booking` entity block (lines 20-32) and `DbSet<Booking> Bookings` (line 14) entirely per D-14; add `DbSet<Appointment>`, `DbSet<AppointmentSlot>`, `DbSet<Stylist>`, `DbSet<StylistWorkingHours>`, `DbSet<StylistTimeOff>`.

---

### `API/ZachHairStudio.Api/Program.cs` (config, modified)

**Analog:** itself — existing DI registration block (lines 24-28).

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<ServiceCreateDtoValidator>();
builder.Services.AddScoped<ServicesService>();
```
Add `AddScoped<StylistsService>()`, `AddScoped<SlotService>()`, `AddScoped<AppointmentsService>()`, and the `AddHttpClient<IEmailService, ResendEmailService>(...)` registration (see EmailService pattern above). Add a `Salon` options binding (`builder.Configuration.GetSection("Salon")`) for the IANA timezone id (D-16) and Resend config (`Resend:ApiKey` sourced from user-secrets/env var per D-13 — never hardcode into `appsettings.json`).

---

### `API/ZachHairStudio.Api.Tests/SqlServerWebApplicationFactory.cs` (test fixture, new)

**Analog:** `API/ZachHairStudio.Api.Tests/CustomWebApplicationFactory.cs` (full file, lines 1-43) — swap `UseInMemoryDatabase` for `UseSqlServer` per RESEARCH.md's Validation Architecture section (lines 617-621).

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"ZachHairStudioTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<BookingDbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<BookingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BookingDbContext>>();
            services.AddDbContext<BookingDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        dbContext.Database.EnsureCreated();
        return host;
    }
}
```
New fixture: same `ConfigureWebHost`/`UseEnvironment("Testing")` skeleton, but `options.UseSqlServer(localDbConnectionString)` with a per-run unique database name, and `dbContext.Database.Migrate()` (not `EnsureCreated()`) in `CreateHost` so the real unique index is exercised; add `DisposeAsync` override calling `EnsureDeleted()`. Requires adding `Microsoft.EntityFrameworkCore.SqlServer` package reference to the test project.

---

### `landing-page/lib/appointments.ts` (utility, API client)

**Analog:** `landing-page/lib/services.ts` (full file, lines 1-61).

```typescript
export const ServiceSchema = z.object({
  id: z.number(),
  slug: z.string(),
  ...
});
export const ServiceListSchema = z.array(ServiceSchema);
export type Service = z.infer<typeof ServiceSchema>;

export async function fetchServices(): Promise<Service[]> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/services`, {
      next: { revalidate: SERVICE_REVALIDATE_SECONDS },
    });
    if (!response.ok) throw new Error(`Services request failed with ${response.status}`);
    return ServiceListSchema.parse(await response.json());
  } catch {
    return [];
  }
}
```
Copy the Zod-schema + `API_BASE_URL` + try/catch-swallow-to-empty-array shape for `fetchOpenSlots(serviceId, stylistId, date)` (client-fetched, per D-15 — do NOT wrap in Next.js `next: { revalidate }` cache since slot data must always be fresh) and `createAppointment(dto)` (POST, mirrors the old `createBooking` in `landing-page/lib/api.ts` — but must NOT swallow errors, since a 409 must surface distinctly from a validation 400; extract status code and message like the old `extractErrorMessage` helper did).

---

### `landing-page/components/AppointmentBookingForm.tsx` (component, request-response)

**Analog:** `landing-page/components/BookingRequestForm.tsx` (full file) for the `"use client"`, `Field` sub-component, `inputClass`/`priceFormatter` constants, `submitted`/`submitting`/`error` state trio, and error-alert JSX block (lines 195-202).

```typescript
const [submitted, setSubmitted] = useState(false);
const [submitting, setSubmitting] = useState(false);
const [error, setError] = useState<string | null>(null);
...
{error && (
  <p role="alert" className="text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3">
    {error}
  </p>
)}
```
`AppointmentBookingForm` extends this into a progressive-reveal flow (service → stylist → date/slot grid → contact fields, per D-15) — new UI structure not present in the analog, but reuse the `Field` component, `inputClass` styling, and the submitted-confirmation-card pattern (lines 86-107) for the final on-screen confirmation (must render full appointment details per D-11/D-16, including the salon-local time with zone label — new formatting logic, no analog).

---

### `landing-page/app/book/page.tsx` (route, modified)

**Analog:** itself (current version, full file) — RSC data-fetch + component composition shape.

```typescript
export default async function BookPage({ searchParams }: Props) {
  const [{ service }, services] = await Promise.all([searchParams, fetchServices()]);
  return (
    <>
      <Navbar />
      <main>...
        {services.length === 0 ? (<empty state>) : (
          <BookingRequestForm services={services} initialServiceSlug={service} />
        )}
      </main>
      <Footer />
      <BackToTop />
    </>
  );
}
```
Keep the `Promise.all([searchParams, fetchServices()])` RSC pattern and the `?service={slug}` deep-link wiring (D-15 requires this keep working) — swap `BookingRequestForm` for `AppointmentBookingForm`.

---

## Shared Patterns

### Result<T> + ProblemDetails translation
**Source:** `API/ZachHairStudio.Shared/Result.cs` (full file) + `ServicesController.cs` lines 40-58
**Apply to:** `AppointmentsService`, `AppointmentsController`, `StylistsService`
```csharp
// Result.cs already has DuplicateRecordError — use it for the 409 "slot taken" case:
public static Result<T> DuplicateRecordError(string message = "Duplicate Record", T? data = default) =>
    new Result<T> { IsSuccess = false, Type = EnumRespType.DuplicateRecord, Data = data, Message = message };
```
Controller translation: `result.IsValidationError()` → `ValidationProblem`, `result.IsNotFound()` → `NotFound()`, and the **new** `result.IsDuplicateRecord()` → `Conflict(...)` (409) branch this phase introduces.

### FluentValidation + AbstractValidator
**Source:** `ServiceCreateDtoValidator.cs` (full file)
**Apply to:** `AppointmentCreateDtoValidator.cs`
Register via `AddValidatorsFromAssemblyContaining<...>()` in `Program.cs` (line 24) — assembly scanning already picks up any new validator automatically, no per-validator registration needed.

### Entity → DTO mapping via extension methods
**Source:** `ServiceExtensions.cs` (full file)
**Apply to:** `StylistExtensions.cs`, `AppointmentExtensions.cs`
`.ToDto()` / `.ToEntity()` fluent static methods on a `static class {Feature}Extensions` — no AutoMapper, no constructor-based mapping.

### EF Core HasData seeding
**Source:** `BookingDbContext.cs` lines 46-130 (`Service` `HasData`)
**Apply to:** `Stylist` (from `landing-page/lib/data.ts` `team` array), `StylistWorkingHours` (a sensible default weekly schedule per stylist — Claude's discretion, flag as owner-reviewable per RESEARCH.md A4)
Hardcoded `HasData(new Stylist { Id = 1, ... }, ...)` literal list, IDs assigned explicitly (EF Core `HasData` requires explicit keys, cannot rely on identity auto-increment).

### Controllers never touch DbContext directly (PLAT-01)
**Source:** `ServicesController.cs` (constructor-injects `ServicesService` only, no `BookingDbContext`) — contrast with the *retired* `BookingsController.cs` (constructor-injects `BookingDbContext` directly, lines 12-17), which is the anti-pattern this phase must NOT repeat for `AppointmentsController`/`StylistsController`.
**Apply to:** `AppointmentsController`, `StylistsController`

### Real-SQL-Server test fixture (novel, no in-repo precedent — RESEARCH.md gap)
**Source:** RESEARCH.md "Validation Architecture" section, `CustomWebApplicationFactory.cs` as structural analog
**Apply to:** `SqlServerWebApplicationFactory.cs`, concurrency tests (SC4), DST tests (SC5)

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `API/ZachHairStudio.Shared/Features/Appointments/EmailService.cs` | service | event-driven | No `HttpClient`-wrapped external service exists in the codebase yet — first of its kind. Use RESEARCH.md Pattern 4 verbatim. |
| `API/ZachHairStudio.Shared/Features/Availability/SlotService.cs` (grid-generation core) | service | transform | The DST-aware grid math (`ToSalonInstant`, `IsInvalidTime`/`IsAmbiguousTime` handling) is entirely new logic with no precedent anywhere in the repo. Use RESEARCH.md Patterns 2 and 3 verbatim as the primary source. |
| `API/ZachHairStudio.Api.Tests/SqlServerWebApplicationFactory.cs` (SQL-Server-specific parts) | test fixture | — | No real-SQL-Server-backed test fixture exists yet (existing fixture is InMemory-only); structural skeleton borrows from `CustomWebApplicationFactory.cs` but the `UseSqlServer`/`Migrate()`/`EnsureDeleted()` mechanics are new. |

## Metadata

**Analog search scope:** `API/ZachHairStudio.Shared/Features/`, `API/ZachHairStudio.Api/Controllers/`, `API/ZachHairStudio.Api/Program.cs`, `API/ZachHairStudio.Shared/Db/`, `API/ZachHairStudio.Api.Tests/`, `landing-page/components/`, `landing-page/lib/`, `landing-page/app/book/`
**Files scanned:** 18 (6 Phase-1 Services files, BookingsController, Booking feature files, BookingDbContext, Program.cs, CustomWebApplicationFactory, services.ts, BookingRequestForm.tsx, book/page.tsx, data.ts, Result.cs)
**Pattern extraction date:** 2026-07-09
