# Phase 1: Service Catalog - Research

**Researched:** 2026-07-07
**Domain:** ASP.NET Core feature-layer architecture (service layer + FluentValidation) and Next.js 15 App Router read-heavy content (RSC + ISR), applied to a new `Service` catalog entity
**Confidence:** HIGH

## Summary

Phase 1 is the template-setting phase: everything after it copies the pattern established here. The work splits into two halves that must land together. On the backend, a new `Features/Services/` slice mirrors `Features/Bookings/` almost exactly (entity, DTOs, extension mappers, `DbSet`, controller) but adds two things Bookings never had: a `ServicesService` class that owns all `BookingDbContext` access, and FluentValidation validators invoked *manually* inside that service (automatic MVC integration via `FluentValidation.AspNetCore` was deprecated when FluentValidation went to v11 — confirmed against the FluentValidation docs). The service returns the already-shipped-but-unused `Result<T>` wrapper; the controller's only job is translating `Result<T>` into `ActionResult` (`ValidationError` → 400 ProblemDetails via `ValidationProblem(ModelState)`, `NotFoundError` → 404, `Success` → 200/201). On the frontend, `landing-page` gets a `/services` list route and `/services/[slug]` detail route as React Server Components that `fetch()` the API with `next: { revalidate: <seconds> }` (Next.js 15's App Router ISR mechanism), and a Zod schema parses the JSON response before it's handed to JSX — establishing the frontend validation pattern with zero user-facing forms in this phase.

The highest-leverage research finding is a seeding-mechanism trap: EF Core 9 introduced `UseSeeding`/`UseAsyncSeeding`, which the training-data-fluent mind reaches for by default, but those hooks only fire through `EnsureCreated()` — this project's `Program.cs` calls `db.Database.Migrate()`. The correct mechanism here is `HasData()` inside `OnModelCreating`, which EF Core's migration generator turns into `InsertData`/`UpdateData` operations baked into the migration itself. This is exactly what CONTEXT.md's D-13 specifies, and this research confirms it's the *only* one of the two seeding APIs compatible with the existing startup code — no `Program.cs` change needed for seeding to work.

**Primary recommendation:** Scaffold `Features/Services/` mirroring Bookings exactly for entity/DTO/mapper shape, but insert a `ServicesService` (using `IValidator<T>` injected via `FluentValidation.DependencyInjectionExtensions`, calling `ValidateAsync` manually) between the controller and `BookingDbContext`. Seed via `HasData()` in `OnModelCreating`, not `UseSeeding`. On the frontend, fetch with `next: { revalidate }` from Server Components and validate the JSON with Zod before render — do not add a new error-response shape; reuse the ProblemDetails/ModelState wire format `lib/api.ts`'s `extractErrorMessage()` already parses.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Service list/detail data query & filtering (IsActive, DisplayOrder, category grouping) | API / Backend | Database / Storage | `ServicesService` owns the EF Core query; DB provides the sort/filter via indexed columns |
| Write-path validation (name required, price ≥ 0, slug format) | API / Backend | — | FluentValidation runs server-side in `ServicesService` before any DB write — client-side checks are never trusted |
| Response-shape validation (Zod parsing API JSON) | Frontend Server (SSR) | — | Zod schemas run in the RSC data-fetching layer (`lib/`) before JSX consumes the data |
| List/detail page rendering | Frontend Server (SSR) | CDN / Static | React Server Components render HTML; Next.js ISR (`next: { revalidate }`) caches the rendered output at the edge |
| Static service images (`ImageUrl`) | CDN / Static | Frontend Server (SSR) | Files served from `landing-page/public/`; no upload pipeline this phase |
| Booking CTA pre-selection (`?service=slug`) | Browser / Client | Frontend Server (SSR) | Query-string read happens client-side in `Contact.tsx`; the link itself is server-rendered |
| Service persistence & seed data | Database / Storage | — | EF Core `HasData()` + migrations own schema and initial rows |
| Homepage subset curation (which services show, in what order) | API / Backend | Frontend Server (SSR) | `DisplayOrder`/`IsActive` filtering happens in the query; SSR just renders whatever the API returns |

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Catalog lives at a dedicated `/services` route on `landing-page/`. The homepage keeps a compact Services section — now API-backed, showing a subset — that links to the full catalog page.
- **D-02:** Detail URLs are slug-based: `/services/precision-cut`. The `Service` entity carries a unique slug column.
- **D-03:** Catalog pages fetch in React Server Components with a short revalidate window (ISR). No client-side loading spinners for read-only content; SEO-friendly.
- **D-04:** Detail-page booking CTA links to the existing contact form with the service pre-selected (e.g., `/#contact?service=slug`). Phase 2 swaps this target to the real slot-based flow.
- **D-05:** Category is a simple string/enum field on `Service` (e.g., Cuts, Color, Treatments), used to group the catalog page into sections. No separate Category entity/FK.
- **D-06:** Single fixed decimal `Price`, displayed as-is. Variable-price work is modeled as separate service entries (e.g., "Color — Short Hair"). Phase 2 receipts need one concrete price per service.
- **D-07:** `DurationMinutes` is a plain int (45, 90, …). Display formatting ("1h 30m") is a frontend concern. Phase 2 slot math consumes this directly.
- **D-08:** Nullable `ImageUrl` string pointing at static files in `landing-page/public/`. No upload pipeline in this phase — image management arrives with Phase 4 CRUD.
- **D-09:** `IsActive` bool (default true) from day one. Public list/detail queries filter to active services. Anticipates Phase 4 "retire service" without a later migration/query rework.
- **D-10:** Explicit `DisplayOrder` int column controls catalog ordering (merchandising, not alphabetical). Seeded now, staff-editable in Phase 4.
- **D-11:** Two description fields: a short teaser (~200 chars, for list cards) and a longer detail-page description.
- **D-12:** Initial catalog content migrates from the static services in `landing-page/lib/data.ts` — the site keeps saying what it says today, now from the database — enriched with duration, price, category, slug, and image.
- **D-13:** Seeding runs through the EF migration pipeline (`HasData` in `OnModelCreating` or an explicit seed migration), so every environment that runs migrations gets the catalog. Fits existing startup `db.Database.Migrate()` and the `ef-migrations` skill.
- **D-14:** Service entries are retired from `lib/data.ts` entirely: the homepage Services section, `/services` pages, AND the Contact form's service dropdown all read from the API. One source of truth. (Team/reviews/branches data stays static.)
- **D-15:** Seed durations/prices are plausible salon values chosen by Claude and explicitly flagged in the plan/summary as owner-reviewable placeholders (editable via Phase 4 CRUD later). Do not block on real numbers.
- **D-16:** Phase 1 ships POST/PUT service endpoints with full FluentValidation, exercised via Swagger/tests. They are unauthenticated until Phase 3's auth gate — same dev-only exposure as today's booking API; nothing is publicly deployed yet. Phase 4 adds the dashboard UI on top of these endpoints.
- **D-17:** `ServicesService` methods return the existing (currently unused) `Result<T>` from `API/ZachHairStudio.Shared/Result.cs`. Controllers translate ValidationError → 400 ProblemDetails and NotFound → 404. This activates the shipped pattern and sets the template every later feature follows.
- **D-18:** Zod enters on the frontend as response validation: Zod schemas parse/validate service API responses in the frontend data layer. Establishes the frontend validation pattern even though this phase has no public write forms.
- **D-19:** The existing `BookingsController` (which calls `DbContext` directly) is NOT refactored in Phase 1. Phase 2 rebuilds booking wholesale. PLAT-01's "controllers never query DbContext directly" is established and verified on the new Services feature.

### Claude's Discretion

- Exact FluentValidation rules per field (lengths, price bounds, slug format).
- Whether the API client for the new endpoints is OpenAPI-generated (via the `openapi-client` skill) or extends the hand-written `lib/api.ts` — the OpenAPI-as-source-of-truth constraint applies either way.
- Homepage subset size, empty states, sorting within categories, and visual details of the catalog pages (consistent with existing Tailwind theme and `SectionHeading` styling).

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PLAT-01 | API features are served through a per-feature service layer; controllers do not call `DbContext` directly | `ServicesService` pattern below (Architecture Patterns → Pattern 1); verified by a reflection-based unit test in Validation Architecture |
| PLAT-02 | Input validation runs through a dedicated validation layer (FluentValidation on API; Zod on frontend) | FluentValidation manual-invocation pattern (Don't Hand-Roll, Code Examples); Zod response-parsing pattern (Code Examples) |
| CAT-01 | Client can browse a list of services showing name, description, duration, and price | `/services` RSC page + `GET /api/services` (Architecture Patterns → Recommended Project Structure) |
| CAT-02 | Client can open a service detail page for a single service | `/services/[slug]` RSC page + `GET /api/services/{slug}` |
| CAT-03 | Services are backed by a `Service` entity with list + detail API endpoints | Standard Stack → Service entity shape; EF Core migration/seed pattern |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| FluentValidation | 12.1.1 | Validation rules for `ServiceCreateDto`/`ServiceUpdateDto` | Already the project's stated PLAT-02 validation layer; latest stable confirmed on NuGet `[VERIFIED: nuget.org registry]` |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | Registers all validators via `AddValidatorsFromAssemblyContaining<T>()` | Official DI registration package per FluentValidation docs `[CITED: github.com/fluentvalidation/fluentvalidation/blob/main/docs/di.md]`; confirmed on NuGet `[VERIFIED: nuget.org registry]` |
| zod | 4.4.3 | Parses/validates service API responses in the frontend data layer | Locked by D-18; de-facto standard TS runtime validator, 211M weekly downloads `[VERIFIED: npm registry + package-legitimacy check: OK]` |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 (already installed) | Persists `Service` entity | Matches existing pinned version in `ZachHairStudio.Shared.csproj` `[VERIFIED: repo file]` |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| openapi-typescript | 7.13.0 | Generates TS types from the OpenAPI document | If choosing the OpenAPI-generated client path (Claude's discretion); matches the `openapi-client` skill `[VERIFIED: npm registry + package-legitimacy check: OK]` |
| openapi-fetch | 0.17.0 | Thin typed fetch wrapper consuming the generated schema | Paired with `openapi-typescript` per the `openapi-client` skill `[VERIFIED: npm registry + package-legitimacy check: OK]` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Manual `ValidateAsync` call inside `ServicesService` | `FluentValidation.AspNetCore` automatic MVC filter | The automatic-validation ASP.NET MVC integration was deprecated by the FluentValidation project starting at v11 — it is no longer the documented path in the official docs `[CITED: github.com/fluentvalidation/fluentvalidation/blob/main/docs/aspnet.md]`. Manual invocation inside the service also fits D-17's `Result<T>` pattern better than a filter that runs before the service layer even executes. |
| `FluentValidation.AspNetCore` (deprecated automatic filter) | `SharpGrip.FluentValidation.AutoValidation` | A community package still offering automatic async validation for MVC/minimal APIs if the project ever wants a filter-based approach. Not needed here since D-17 already routes validation through the service layer. |
| EF Core `HasData()` seeding | EF Core 9's `UseSeeding`/`UseAsyncSeeding` | `UseSeeding`/`UseAsyncSeeding` only execute via `EnsureCreated()`, not `Migrate()` `[CITED: learn.microsoft.com EF Core 9.0 what's new / data-seeding.md via Context7]`. This project's `Program.cs` calls `Migrate()`, so `UseSeeding` would silently never run. `HasData()` is the only option that ships seed rows inside the generated migration. |
| Hand-written `lib/api.ts` extension for Services | OpenAPI-generated client (`openapi-typescript` + `openapi-fetch`) | Either satisfies "OpenAPI is the source of truth" per PROJECT.md — generated client removes drift risk but adds a build step; hand-written extension is faster to ship and matches the existing Bookings client style. Left to Claude's discretion per CONTEXT.md. |

**Installation:**
```bash
# API/ZachHairStudio.Shared
dotnet add package FluentValidation --version 12.1.1
dotnet add package FluentValidation.DependencyInjectionExtensions --version 12.1.1

# landing-page/
npm install zod@4.4.3
# only if choosing the OpenAPI-generated client path:
npm install --save-dev openapi-typescript@7.13.0
npm install openapi-fetch@0.17.0
```

**Version verification:** All four npm packages and both NuGet packages were checked against their live registries this session (`npm view` blocked for `fluentvalidation.aspnetcore` — expected, it's a .NET package not npm; NuGet flat-container API queried directly for FluentValidation packages). Package names for FluentValidation packages come from official FluentValidation documentation fetched via Context7, not training-data guesswork.

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| zod | npm | Long-established (v4.4.3 latest, published 2026-05-04) | 211,601,986/wk | github.com/colinhacks/zod | OK | Approved |
| openapi-typescript | npm | Long-established (v7.13.0, published 2026-02-11) | 4,487,128/wk | github.com/openapi-ts/openapi-typescript | OK | Approved (optional path) |
| openapi-fetch | npm | Long-established (v0.17.0, published 2026-02-11) | 5,372,044/wk | github.com/openapi-ts/openapi-typescript | OK | Approved (optional path) |
| FluentValidation | NuGet | Long-established (v12.1.1 latest of 100+ published versions) | N/A (NuGet API doesn't expose weekly downloads via flat-container) | github.com/FluentValidation/FluentValidation (per Context7 docs) | OK | Approved — name confirmed via official docs, not just registry lookup |
| FluentValidation.DependencyInjectionExtensions | NuGet | Long-established (v12.1.1 latest of 60+ published versions) | N/A | same as above | OK | Approved — name confirmed via official docs, not just registry lookup |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

*All packages above were discovered via official documentation (FluentValidation docs via Context7) or are already-established project dependencies (zod locked by CONTEXT.md D-18), then cross-checked against their live registries this session — they qualify for `[VERIFIED]` status per the package-name-provenance rule, not just registry existence.*

## Architecture Patterns

### System Architecture Diagram

```text
┌─────────────────────────────────────────────────────────────────┐
│ Browser                                                          │
│  - GET /services            (list page)                         │
│  - GET /services/precision-cut (detail page)                    │
│  - Click "Book" → /#contact?service=precision-cut                │
└───────────────────────────┬───────────────────────────────────────┘
                            │ HTTP (page request)
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│ Next.js App Router (Server Components) — landing-page/          │
│  app/services/page.tsx           app/services/[slug]/page.tsx    │
│    │                                  │                          │
│    ▼                                  ▼                          │
│  fetch(`${API}/api/services`,      fetch(`${API}/api/services/    │
│    { next: { revalidate: N } })      ${slug}`, { next: {...} })   │
│    │                                  │                          │
│    ▼                                  ▼                          │
│  ServiceListSchema.parse(json)     ServiceSchema.parse(json)     │
│    (Zod — D-18)                      (Zod — D-18)                │
│    │                                  │                          │
│    ▼                                  ▼                          │
│  render category-grouped cards     render detail + CTA link      │
└───────────────────────────┬───────────────────────────────────────┘
                            │ HTTP/JSON (fetch, server-side)
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│ ASP.NET Core API — API/ZachHairStudio.Api/                      │
│  ServicesController (HTTP entry point only)                     │
│    GetServices()        → ActionResult<IEnumerable<...Dto>>     │
│    GetService(slug)     → ActionResult<...Dto>                  │
│    CreateService(dto)   → ActionResult<...Dto>  (D-16, dev-only) │
│    UpdateService(id,dto)→ IActionResult          (D-16, dev-only)│
│    │  translates Result<T> → ActionResult (D-17)                │
│    ▼                                                             │
│  ServicesService (API/ZachHairStudio.Shared/Features/Services/) │
│    - injects IValidator<ServiceCreateDto> (FluentValidation)     │
│    - calls ValidateAsync manually before any write               │
│    - queries/writes via BookingDbContext                         │
│    - returns Result<T> (Success / ValidationError / NotFound)    │
│    │                                                              │
│    ▼                                                              │
│  BookingDbContext.Services (DbSet<Service>)                       │
│    - filters IsActive == true for public reads (D-09)            │
│    - orders by DisplayOrder (D-10)                                │
└───────────────────────────┬───────────────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│ SQL Server (LocalDB) — Services table                             │
│  Seeded via HasData() in OnModelCreating → baked into a migration │
│  (content migrated from landing-page/lib/data.ts per D-12)         │
└─────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure

```
API/ZachHairStudio.Shared/
├── Features/
│   └── Services/
│       ├── Service.cs                      # entity
│       ├── ServiceCreateDto.cs             # input (POST)
│       ├── ServiceUpdateDto.cs             # input (PUT) — new vs. Bookings; Bookings has no update-all endpoint
│       ├── ServiceResponseDto.cs           # output
│       ├── ServiceExtensions.cs            # ToDto() / ToEntity()
│       ├── ServiceCreateDtoValidator.cs    # FluentValidation
│       ├── ServiceUpdateDtoValidator.cs    # FluentValidation
│       └── ServicesService.cs              # NEW pattern: owns BookingDbContext access
├── Db/
│   └── BookingDbContext.cs                 # add DbSet<Service>, OnModelCreating config + HasData seed
└── Migrations/
    └── <timestamp>_AddServices.cs          # generated; includes seed InsertData

API/ZachHairStudio.Api/
├── Controllers/
│   └── ServicesController.cs               # thin — injects ServicesService, not DbContext
└── Program.cs                              # add AddValidatorsFromAssemblyContaining<...>() + AddScoped<ServicesService>()

landing-page/
├── app/
│   ├── services/
│   │   ├── page.tsx                        # list (RSC)
│   │   └── [slug]/
│   │       └── page.tsx                    # detail (RSC)
├── lib/
│   ├── services.ts                         # fetchServices()/fetchServiceBySlug() + Zod schemas
│   └── data.ts                             # services + serviceOptions REMOVED (D-14); team/reviews/branches remain
└── components/
    ├── Services.tsx                        # becomes API-backed homepage subset
    └── Contact.tsx                         # service dropdown reads from API; accepts ?service=slug preselect
```

### Pattern 1: ServicesService owns all DbContext access (PLAT-01)

**What:** A plain class in `Features/Services/`, constructor-injected with `BookingDbContext` and `IValidator<ServiceCreateDto>`/`IValidator<ServiceUpdateDto>`, exposing async methods that return `Result<T>`. The controller never sees `BookingDbContext`.

**When to use:** Every read/write path for the Services feature — this is the template PLAT-01 requires and every later phase's feature copies.

**Example:**
```csharp
// Source: pattern synthesized from FluentValidation official docs
// (github.com/fluentvalidation/fluentvalidation/blob/main/docs/aspnet.md — manual
// validation) + existing Result<T> shape in API/ZachHairStudio.Shared/Result.cs
namespace ZachHairStudio.Shared.Features.Services;

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
        var service = await _dbContext.Services
            .FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);

        return service is null
            ? Result<ServiceResponseDto>.NotFoundError($"Service '{slug}' not found")
            : Result<ServiceResponseDto>.Success(service.ToDto());
    }

    public async Task<Result<ServiceResponseDto>> CreateAsync(ServiceCreateDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return Result<ServiceResponseDto>.ValidationError(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var entity = dto.ToEntity();
        _dbContext.Services.Add(entity);
        await _dbContext.SaveChangesAsync();
        return Result<ServiceResponseDto>.Success(entity.ToDto());
    }
}
```

### Pattern 2: Controller translates Result<T> without inventing a new error shape (D-17)

**What:** `ServicesController` calls `ServicesService`, then maps `Result<T>` to `ActionResult`. For validation errors, it reuses the FluentValidation `ValidationResult` directly (not the pre-formed string in `Result<T>.Message`) so the response stays in ASP.NET's ModelState/ProblemDetails shape — the exact shape `landing-page/lib/api.ts`'s `extractErrorMessage()` already parses (`body.errors` as `Record<string, string[]>`, or `body.title`). No frontend changes are needed to read Services validation errors.

**When to use:** Every controller action that calls into a service returning `Result<T>`.

**Example:**
```csharp
// Source: pattern combining FluentValidation's ValidationResult.AddToModelState()
// (github.com/fluentvalidation/fluentvalidation/blob/main/docs/aspnet.md) with
// ASP.NET Core's built-in ControllerBase.ValidationProblem(ModelState)
[HttpPost]
public async Task<ActionResult<ServiceResponseDto>> CreateService([FromBody] ServiceCreateDto dto)
{
    var validation = await _createValidator.ValidateAsync(dto);
    if (!validation.IsValid)
    {
        validation.AddToModelState(ModelState);
        return ValidationProblem(ModelState); // same wire shape Contact.tsx already parses
    }

    var result = await _servicesService.CreateAsync(dto);
    return result.IsSuccess
        ? CreatedAtAction(nameof(GetService), new { slug = result.Data.Slug }, result.Data)
        : StatusCode(500, result.Message);
}
```
*Note: validating in the controller (to call `AddToModelState`) and again inside `ServicesService.CreateAsync` is intentional double-validation-surface — the service-layer check protects any future caller that isn't HTTP (e.g., a background job), while the controller-layer check produces the richer ModelState response. If this duplication feels wrong, an alternative is having `ServicesService` return the `ValidationResult` itself instead of a stringified `Result<T>.Message`; either is acceptable, but pick one and apply it consistently since every later phase copies this template.*

### Pattern 3: EF Core HasData seeding compatible with startup Migrate()

**What:** Seed rows are declared as part of the model in `OnModelCreating`, not as imperative startup code. `dotnet ef migrations add` diffs the declared seed data against the previous migration's seed data and emits `InsertData`/`UpdateData`/`DeleteData` operations into the migration file itself. Because these are ordinary migration operations, `db.Database.Migrate()` (already called in `Program.cs`) applies them like any schema change — no code changes to `Program.cs` are needed.

**When to use:** Any time initial/reference data must exist in every environment that runs migrations (exactly D-13's requirement).

**Example:**
```csharp
// Source: https://github.com/dotnet/entityframework.docs/blob/main/entity-framework/core/modeling/data-seeding.md
modelBuilder.Entity<Service>().HasData(
    new Service
    {
        Id = 1,
        Slug = "precision-cut",
        Name = "Precision Cut",
        ShortDescription = "Tailored haircuts designed to complement your face shape and lifestyle perfectly.",
        LongDescription = "…",
        Category = "Cuts",
        DurationMinutes = 45,
        Price = 35.00m,
        DisplayOrder = 1,
        IsActive = true,
        ImageUrl = null,
    }
    // ...remaining seeded services from landing-page/lib/data.ts (D-12)
);
```
**Do NOT use** `optionsBuilder.UseSeeding(...)`/`UseAsyncSeeding(...)` for this project — those EF Core 9 hooks only execute through `context.Database.EnsureCreated()`, not `Migrate()` `[CITED: github.com/dotnet/entityframework.docs — ef-core-9.0/whatsnew.md via Context7]`. Since `Program.cs` already calls `Migrate()`, `UseSeeding` would silently never run and the catalog would appear empty in every fresh environment.

### Pattern 4: RSC data fetching with ISR revalidate (D-03)

**What:** Server Components call `fetch()` with the `next: { revalidate: N }` option. Next.js caches the rendered result and the underlying fetch response for `N` seconds; requests within that window are served from cache with no client-side loading state, and the cache is transparently refreshed after expiry.

**When to use:** `/services` list page and `/services/[slug]` detail page — both are read-only content that changes infrequently (only via Phase 4 staff CRUD, later).

**Example:**
```tsx
// Source: https://github.com/vercel/next.js/blob/canary/docs/01-app/02-guides/migrating/app-router-migration.mdx
// app/services/page.tsx
async function getServices() {
  const res = await fetch(`${API_BASE_URL}/api/services`, {
    next: { revalidate: 60 }, // short window per D-03; exact seconds is Claude's discretion
  });
  const json = await res.json();
  return ServiceListSchema.parse(json); // Zod — D-18
}

export default async function ServicesPage() {
  const services = await getServices();
  // group by category, render — no useState/loading spinner needed
  return <CatalogGrid services={services} />;
}
```
For the `[slug]` detail route, the same `next: { revalidate }` fetch pattern applies per-request; `generateStaticParams()` is available for fully pre-rendering all known slugs at build time but is optional here — since Phase 4 will make services staff-editable, a per-request revalidate-only approach (no `generateStaticParams`) avoids needing a rebuild/redeploy to pick up new services, at the cost of a slightly slower first hit per slug after cache expiry. Recommend the per-request approach for this phase; note this in the plan as a discretionary call.

### Anti-Patterns to Avoid

- **Automatic `FluentValidation.AspNetCore` MVC filter integration:** Deprecated by the FluentValidation project since v11 `[CITED: fluentvalidation.net docs via Context7]`. Do not add this package; use `FluentValidation.DependencyInjectionExtensions` + manual `ValidateAsync` instead.
- **`UseSeeding`/`UseAsyncSeeding` in this project:** Silently no-ops because `Program.cs` uses `Migrate()`, not `EnsureCreated()`. Always seed via `HasData()` here.
- **Controllers injecting `BookingDbContext` for Services:** This is the exact anti-pattern PLAT-01/D-19 asks Phase 1 to avoid — `BookingsController` is grandfathered (D-19), `ServicesController` must not repeat it.
- **Inventing a new JSON error shape for Services validation:** `Contact.tsx`'s `extractErrorMessage()` already parses ASP.NET's ModelState/ProblemDetails shape (`{ errors: {...}, title: "..." }`). Reuse it via `ValidationProblem(ModelState)` rather than hand-rolling `{ success: false, error: "..." }` or similar.
- **Client-rendered loading spinners for the catalog pages:** D-03 explicitly rules this out — use Server Components with `fetch`/ISR, not `useEffect` + `useState` client fetching.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Multi-field, cross-property validation rules (price ≥ 0, slug format, string lengths) | Custom `if` chains in the controller or entity setters | FluentValidation validators (`ServiceCreateDtoValidator`) | Composable, testable in isolation (`TestValidate` extension bundled in the main FluentValidation package since v9 — no separate test package needed), and already the project's stated PLAT-02 layer |
| Converting internal error states to HTTP responses | Ad hoc `if/else` per controller action returning different shapes each time | `Result<T>` (already in the repo, unused) + a single translation pattern (Pattern 2 above) | Keeps every future feature's error handling consistent; this is precisely why D-17 activates it now |
| Runtime response-shape checking on the frontend | Manual `if (typeof data.name !== 'string')` guards scattered through components | Zod schema + `.parse()`/`.safeParse()` in the data-fetching layer (`lib/services.ts`) | Single source of truth for the expected shape; throws a clear error at the fetch boundary instead of a confusing downstream crash |
| Seed data change-tracking across migrations | A hand-written SQL script run manually per environment | EF Core `HasData()` | EF Core's migration scaffolder diffs seed data automatically and generates the correct Insert/Update/Delete operations — no manual SQL to maintain |

**Key insight:** Every "don't hand-roll" item above is really the same insight applied four times: this project has enumerated exactly which layer owns which concern (service layer owns business rules, `Result<T>` owns error classification, Zod owns response-shape trust boundary, EF Core owns seed-data lifecycle) — resist the urge to shortcut any of them for "just this one feature," because Phase 1's whole purpose is setting the template every later phase copies verbatim.

## Common Pitfalls

### Pitfall 1: FluentValidation "automatic" registration doesn't validate automatically anymore

**What goes wrong:** A developer (or an AI trained on pre-v11 tutorials) adds `FluentValidation.AspNetCore` and expects invalid POST bodies to auto-400 before the controller runs, the way DataAnnotations does with `[ApiController]`.

**Why it happens:** That was true through FluentValidation v10; the automatic ASP.NET MVC integration was removed from the officially documented path starting at v11 in favor of manual validation.

**How to avoid:** Install only `FluentValidation` + `FluentValidation.DependencyInjectionExtensions`; call `ValidateAsync` explicitly in the controller and/or service (Pattern 1/2 above).

**Warning signs:** Invalid POST bodies reach `ServicesService.CreateAsync` with `ModelState.IsValid == true` and no validator ever ran.

### Pitfall 2: EF Core seed data silently missing in fresh environments

**What goes wrong:** Seed data is added via `UseSeeding`/`UseAsyncSeeding` (the newer, more-discoverable-in-2026-training-data EF Core 9 API), migrations apply cleanly, but the `Services` table is empty on every fresh clone/CI run.

**Why it happens:** `UseSeeding`/`UseAsyncSeeding` only run as part of `EnsureCreated()`. This project's `Program.cs` calls `Migrate()`, which never invokes those hooks.

**How to avoid:** Seed exclusively via `HasData()` in `OnModelCreating` (Pattern 3 above); verify by dropping the local DB and re-running `dotnet ef database update` (or just `dotnet run`, since `Migrate()` runs at startup) and confirming rows appear.

**Warning signs:** `/api/services` returns `[]` right after a fresh migration on a clean database.

### Pitfall 3: Slug uniqueness only enforced by application logic

**What goes wrong:** Two services get the same slug (e.g., both named "Color") because the validator's uniqueness check races with a concurrent insert, or because a manual DB edit bypasses the API entirely.

**Why it happens:** FluentValidation runs in application code; without a DB-level unique constraint/index on `Slug`, nothing stops a duplicate at the storage layer.

**How to avoid:** Add `entity.HasIndex(e => e.Slug).IsUnique();` in `OnModelCreating` alongside the FluentValidation format check — defense in depth, mirroring how `BookingStatus` gets both an entity constraint (max length) and enum-level type safety.

**Warning signs:** `/services/precision-cut` renders the wrong service, or a `SaveChangesAsync()` throws a `DbUpdateException` with a unique-constraint violation that surfaces as an unhandled 500 instead of a friendly validation error.

### Pitfall 4: Homepage and `/services` page silently drift once both read from the API

**What goes wrong:** `Services.tsx` (homepage subset) and the new `/services` page each write their own fetch/filter/sort logic, and a change to `DisplayOrder` or `IsActive` semantics only gets applied to one of them.

**Why it happens:** D-01 keeps two rendering surfaces (homepage subset + full catalog) reading the same underlying data; without a shared data-fetching function, logic duplicates.

**How to avoid:** Put `fetchServices()`/`fetchServiceBySlug()` and the Zod schemas in one shared module (`lib/services.ts`); both `Services.tsx` and `app/services/page.tsx` import from it, differing only in how many results they render/how they're laid out.

**Warning signs:** Homepage shows a retired (`IsActive: false`) service that the `/services` page correctly hides, or the two pages show services in different order.

### Pitfall 5: Double-counting duration/price formatting logic between list and detail views

**What goes wrong:** `DurationMinutes` (D-07) and `Price` (D-06) are stored as raw `int`/`decimal`; if list-card and detail-page components each format them independently ("45 min" vs. "0h 45m" vs. "45m"), the catalog looks inconsistent.

**Why it happens:** D-07 explicitly pushes display formatting to the frontend, so it's easy to duplicate the formatter.

**How to avoid:** One shared formatting helper (e.g., `formatDuration(minutes: number): string`) used by both the list card and detail page components.

**Warning signs:** Visual inconsistency between `/services` and `/services/[slug]` for the same service's duration/price text.

## Code Examples

### FluentValidation validator with slug format + price bound rules

```csharp
// Source: pattern per FluentValidation official rule-builder API
// (github.com/fluentvalidation/fluentvalidation/blob/main/docs — general validator syntax)
using FluentValidation;

namespace ZachHairStudio.Shared.Features.Services;

public class ServiceCreateDtoValidator : AbstractValidator<ServiceCreateDto>
{
    public ServiceCreateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(150)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase kebab-case (e.g. 'precision-cut').");
        RuleFor(x => x.ShortDescription).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LongDescription).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DurationMinutes).GreaterThan(0).LessThanOrEqualTo(480);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
```

### Zod response-validation schema (frontend data layer)

```typescript
// Source: pattern per Zod's standard object-schema API (training-knowledge — Zod's
// public API is stable and well-documented; [ASSUMED] this exact schema shape,
// verify field names against the final ServiceResponseDto during implementation)
import { z } from "zod";

export const ServiceSchema = z.object({
  id: z.number(),
  slug: z.string(),
  name: z.string(),
  shortDescription: z.string(),
  longDescription: z.string(),
  category: z.string(),
  durationMinutes: z.number(),
  price: z.number(),
  imageUrl: z.string().nullable(),
  displayOrder: z.number(),
});

export const ServiceListSchema = z.array(ServiceSchema);

export type Service = z.infer<typeof ServiceSchema>;
```

### Reflection-based unit test enforcing PLAT-01 ("controllers never call DbContext directly")

```csharp
// Source: pattern using System.Reflection against project conventions — no external
// architecture-testing package required for this single, narrow check
[Fact]
public void ServicesController_DoesNotDependOnBookingDbContext()
{
    var ctorParams = typeof(ServicesController)
        .GetConstructors()
        .SelectMany(c => c.GetParameters());

    Assert.DoesNotContain(ctorParams, p => p.ParameterType == typeof(BookingDbContext));
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `FluentValidation.AspNetCore` automatic MVC validation filter | Manual `ValidateAsync()` calls (or the community `SharpGrip.FluentValidation.AutoValidation` package if automatic behavior is still wanted) | FluentValidation v11 (automatic ASP.NET integration removed from the maintained/documented path) | Any AI-generated code or older tutorial suggesting `services.AddFluentValidationAutoValidation()` from `FluentValidation.AspNetCore` is following a deprecated pattern for this project's FluentValidation 12.x |
| EF Core `.EnsureCreated()` + no seed strategy | `Migrate()` + `HasData()` in `OnModelCreating` | Project already made this switch (ef-migrations skill documents the one-time move away from `EnsureCreated()`) | Confirms `HasData()`, not `UseSeeding`, is correct for this codebase |
| Next.js Pages Router `getStaticProps`/`getServerSideProps` | App Router Server Components + `fetch(url, { next: { revalidate } })` | Next.js 13+ App Router (this project is already on 15.3.0, App Router) | No `getStaticProps`-era APIs apply; ISR is configured per-fetch-call or via route segment `export const revalidate` |

**Deprecated/outdated:**
- `FluentValidation.AspNetCore`: still installable and functional for MVC's older filter-based flow, but explicitly deprecated in official docs; do not add it to this project.
- EF Core `UseSeeding`/`UseAsyncSeeding`: not deprecated in general, but incompatible with this project's `Migrate()`-based startup — effectively "doesn't apply here."

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Exact Zod schema field names (`shortDescription`, `longDescription`, etc.) match the final `ServiceResponseDto` property names | Code Examples → Zod schema | Low — schema is trivial to adjust once the DTO is finalized during planning/implementation; caught immediately by a failing `.parse()` in dev |
| A2 | A short ISR `revalidate` window (e.g., 60s) is an acceptable default absent an explicit owner preference | Architecture Patterns → Pattern 4 | Low — CONTEXT.md D-03 only specifies "short," exact seconds is explicitly Claude's discretion per CONTEXT.md |
| A3 | Seed prices/durations (D-15) are placeholder business data, not verified against the real salon's actual pricing | User Constraints (carried from CONTEXT.md D-15) | Low-Medium — explicitly flagged as owner-reviewable in CONTEXT.md; no action needed beyond carrying the flag into the plan/summary |

**If this table is empty:** N/A — see rows above; all are low-risk and already anticipated by CONTEXT.md.

## Open Questions

1. **Should `ServicesService` validate again, or should validation live only at the controller boundary?**
   - What we know: FluentValidation supports either (controller-only via `ValidationProblem(ModelState)`, or service-layer via injected `IValidator<T>` returning `Result<T>.ValidationError`).
   - What's unclear: CONTEXT.md's D-17 says "`ServicesService` methods return `Result<T>`" and separately implies ValidationError originates from the service, while Pattern 2 above shows the controller doing the ModelState-shaping. Both can call the same validator; the plan should pick one canonical flow (recommend: controller calls `ValidateAsync` for the rich ModelState/ProblemDetails response, service layer also validates defensively for non-HTTP callers) and state it explicitly so every later phase's feature follows the same split.
   - Recommendation: Plan should specify exactly which layer owns the `ValidateAsync()` call that produces the HTTP-facing error, to avoid each later phase inventing its own answer.

2. **Exact ISR revalidate window and whether `generateStaticParams` is used for `/services/[slug]`.**
   - What we know: D-03 says "short revalidate window," Claude's discretion on specifics.
   - What's unclear: No explicit number; Pattern 4 recommends skipping `generateStaticParams` in favor of a uniform per-request revalidate approach, given Phase 4 will add live CRUD.
   - Recommendation: Plan should pick a concrete number (e.g., 60s) and state the `generateStaticParams` decision explicitly in a task, not leave it implicit in code.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 10 | Backend build/run, EF migrations | Assume ✓ (per CLAUDE.md platform requirements; not independently re-probed this session) | 10.x | — |
| Node.js 18+ | Frontend build/run | Assume ✓ (per CLAUDE.md platform requirements) | 18+ | — |
| SQL Server LocalDB | Local dev DB | Assume ✓ (per existing working Bookings feature and `dev` skill) | — | — |
| NuGet registry access | Installing FluentValidation packages | ✓ — confirmed reachable this session (`api.nuget.org` queried directly) | — | — |
| npm registry access | Installing zod/openapi-typescript/openapi-fetch | ✓ — confirmed reachable this session (`npm view` succeeded for zod) | — | — |

**Missing dependencies with no fallback:** none identified.

**Missing dependencies with fallback:** none — this phase's new dependencies (FluentValidation, zod) are additive packages with no environment prerequisites beyond what's already required to build/run the existing Bookings feature.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | None currently exists in the repo — `[VERIFIED: repo search]` no `.Tests.csproj`, no `pytest`/`jest`/`vitest` config, no test files under `landing-page/` or `API/` (only `node_modules` internals matched, which don't count) |
| Config file | none — see Wave 0 |
| Quick run command | `dotnet test API/ZachHairStudio.Api.Tests --filter Category=Unit` (once created) |
| Full suite command | `dotnet test API/ZachHairStudio.slnx` (once a test project exists and is added to the solution) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PLAT-01 | `ServicesController` never references `BookingDbContext` | unit (reflection) | `dotnet test --filter FullyQualifiedName~ServicesController_DoesNotDependOnBookingDbContext` | ❌ Wave 0 |
| PLAT-02 | Invalid `ServiceCreateDto` (missing name, negative price) fails validation before reaching the DB | unit | `dotnet test --filter FullyQualifiedName~ServiceCreateDtoValidatorTests` | ❌ Wave 0 |
| CAT-01 | `GET /api/services` returns active services with name/description/duration/price | integration (`WebApplicationFactory` + EF InMemory) | `dotnet test --filter FullyQualifiedName~ServicesControllerTests.GetServices_ReturnsActiveServices` | ❌ Wave 0 |
| CAT-02 | `GET /api/services/{slug}` returns one service; unknown slug returns 404 | integration | `dotnet test --filter FullyQualifiedName~ServicesControllerTests.GetService_BySlug` | ❌ Wave 0 |
| CAT-03 | `Service` entity persists via migration with seed data | integration (migration applies + row count check) | `dotnet ef database update --project API/ZachHairStudio.Shared --startup-project API/ZachHairStudio.Api` then a smoke query | ❌ Wave 0 |

Frontend (`/services`, `/services/[slug]` rendering, Zod parsing) has no automated test framework in the repo yet. Given Phase 1's emphasis is the backend architectural template (PLAT-01/PLAT-02), recommend deferring frontend automated tests to a later phase and relying on manual verification via the `dev` skill for this phase — flag this explicitly as a manual-only gap rather than silently skipping it.

### Sampling Rate

- **Per task commit:** `dotnet test API/ZachHairStudio.Api.Tests --filter Category=Unit` (fast, no DB)
- **Per wave merge:** `dotnet test API/ZachHairStudio.slnx` (full suite including integration tests against EF InMemory)
- **Phase gate:** Full suite green before `/gsd-verify-work`, plus a manual pass of `/services` and `/services/[slug]` via the `dev` skill

### Wave 0 Gaps

- [ ] Create `API/ZachHairStudio.Api.Tests` (or `ZachHairStudio.Shared.Tests`) xUnit project — no test project exists in the solution today
- [ ] Add test packages: `xunit` 2.9.3, `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 (matches installed EF Core/ASP.NET Core 10.0.9 pin), `Microsoft.EntityFrameworkCore.InMemory` 10.0.9 — all confirmed current on NuGet this session `[VERIFIED: nuget.org registry]`
- [ ] Reference the new test project from the solution (`ZachHairStudio.slnx` or equivalent `.sln`)
- [ ] `tests/conftest`-equivalent shared fixture: a `WebApplicationFactory<Program>` subclass swapping `BookingDbContext` to use `UseInMemoryDatabase` for integration tests
- [ ] Frontend: no test framework installed (no Vitest/Jest, no Playwright config file despite Playwright being a listed dependency) — explicitly out of scope for this phase's automated coverage per the recommendation above

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Deferred to Phase 3 per roadmap; Services write endpoints remain unauthenticated in Phase 1 by explicit decision (D-16) |
| V3 Session Management | no | No session state introduced by this phase |
| V4 Access Control | no | No ownership/role concept exists yet for Services; all endpoints are equally (un)protected, consistent with the existing Bookings API |
| V5 Input Validation | yes | FluentValidation on the API (`ServiceCreateDtoValidator`/`ServiceUpdateDtoValidator`); Zod on the frontend response boundary |
| V6 Cryptography | no | No secrets/crypto introduced by this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection via query parameters (slug, category filters) | Tampering | EF Core LINQ-to-SQL parameterizes all queries automatically — never build raw SQL string concatenation for the slug/category lookup |
| Mass assignment / over-posting (client sets `IsActive`, `DisplayOrder`, or `Id` on create) | Tampering | Explicit `ServiceCreateDto`/`ServiceUpdateDto` (not the entity itself) as the bound model — already the established DTO pattern in this repo; `Id` is never client-settable, `IsActive`/`DisplayOrder` should default server-side on create rather than trusting client input unless intentionally exposed |
| Stored XSS via service description fields rendered on the catalog pages | Tampering / Information Disclosure | React escapes string interpolation by default (`{service.longDescription}`); do not use `dangerouslySetInnerHTML` for description fields |
| Unauthenticated write endpoints (`POST`/`PUT /api/services`) reachable by anyone who finds the URL | Elevation of Privilege | Explicitly accepted risk for this phase per D-16 — same dev-only exposure as the existing `BookingsController`; documented in CONCERNS.md as a pre-existing, tracked gap closed by Phase 3's auth gate. No new mitigation required in Phase 1 beyond not making the exposure worse (e.g., don't expose delete-all or admin-only operations here). |
| Slug-based enumeration of inactive/retired services | Information Disclosure | Public `GetBySlugAsync` filters `IsActive == true` (D-09) — a retired service's slug returns 404, not its (possibly stale) data |

## Sources

### Primary (HIGH confidence)
- Context7 `/fluentvalidation/fluentvalidation` — ASP.NET Core manual validation pattern, DI registration (`AddValidatorsFromAssemblyContaining`), deprecation of automatic MVC integration
- Context7 `/dotnet/entityframework.docs` — `HasData()` vs. `UseSeeding`/`UseAsyncSeeding` compatibility with `Migrate()` vs. `EnsureCreated()`
- Context7 `/vercel/next.js` — App Router `fetch(url, { next: { revalidate } })` ISR pattern, `generateStaticParams` for dynamic routes
- NuGet flat-container API (`api.nuget.org`) — direct registry queries confirming FluentValidation 12.1.1, FluentValidation.DependencyInjectionExtensions 12.1.1, Microsoft.AspNetCore.Mvc.Testing 10.0.9, Microsoft.EntityFrameworkCore.InMemory 10.0.9, xunit 2.9.3
- `npm view` (npm registry) — confirmed zod 4.4.3
- Repo inspection — `Booking.cs`, `BookingCreateDto.cs`, `BookingResponseDto.cs`, `BookingExtensions.cs`, `BookingsController.cs`, `BookingDbContext.cs`, `Program.cs`, `Result.cs`, `lib/api.ts`, `lib/data.ts`, `Contact.tsx`, `Services.tsx`, `SectionHeading.tsx`, three project skills (`feature-scaffold`, `ef-migrations`, `openapi-client`)

### Secondary (MEDIUM confidence)
- `.planning/codebase/ARCHITECTURE.md`, `STRUCTURE.md`, `CONVENTIONS.md`, `CONCERNS.md` — prior codebase-mapping analysis, cross-checked against direct file reads this session

### Tertiary (LOW confidence)
- None — every substantive claim above was either fetched from official docs this session or directly verified against repo files/registries

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - FluentValidation and EF Core versions/patterns confirmed via official docs (Context7) and live registry queries; Zod already locked by CONTEXT.md D-18 and confirmed on npm
- Architecture: HIGH - patterns synthesized directly from the existing Bookings feature (read in full) plus official FluentValidation/Next.js/EF Core documentation, not inferred from memory alone
- Pitfalls: HIGH - the two highest-value pitfalls (FluentValidation automatic-integration deprecation, EF Core UseSeeding/Migrate incompatibility) were specifically verified against official docs this session because they contradict likely training-data defaults

**Research date:** 2026-07-07
**Valid until:** 30 days (stable stack: FluentValidation/EF Core/Next.js all move at a normal cadence; re-verify NuGet/npm versions if planning is delayed past early August 2026)
