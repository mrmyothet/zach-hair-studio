# Phase 5: Product Catalog - Pattern Map

**Mapped:** 2026-08-09
**Files analyzed:** 14
**Analogs found:** 14 / 14 (this phase is a byte-for-byte clone of Phase 1's Services feature)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `API/ZachHairStudio.Shared/Features/Products/Product.cs` | model | CRUD | `API/ZachHairStudio.Shared/Features/Services/Service.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Products/ProductCreateDto.cs` | model (DTO) | CRUD | `Features/Services/ServiceCreateDto.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Products/ProductCreateDtoValidator.cs` | validation | request-response | `Features/Services/ServiceCreateDtoValidator.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Products/ProductResponseDto.cs` | model (DTO) | CRUD | `Features/Services/ServiceResponseDto.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Products/ProductExtensions.cs` | utility (mapper) | transform | `Features/Services/ServiceExtensions.cs` | exact |
| `API/ZachHairStudio.Shared/Features/Products/ProductsService.cs` | service | CRUD | `Features/Services/ServicesService.cs` | exact (use only the read-only slice: `GetServicesAsync`/`GetBySlugAsync`) |
| `API/ZachHairStudio.Shared/Features/Products/ServiceRecommendedProduct.cs` | model (join entity) | CRUD | none (net-new — see EF Core docs pattern in RESEARCH Pattern 2) | no analog |
| `API/ZachHairStudio.Api/Controllers/ProductsController.cs` | controller | request-response | `API/ZachHairStudio.Api/Controllers/ServicesController.cs` | role-match (only clone the two anonymous `GET` actions, not image upload/write actions) |
| `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` (MODIFIED) | config | CRUD | itself — existing `Service`/`Stylist` `OnModelCreating`/`HasData` blocks | exact |
| `API/ZachHairStudio.Shared/Features/Services/ServiceResponseDto.cs` (MODIFIED) | model (DTO) | CRUD | itself — add nullable `RecommendedProducts` field | exact |
| `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` (MODIFIED) | service | CRUD | itself — extend `GetBySlugAsync` | exact |
| `landing-page/lib/products.ts` | utility (fetch layer) | request-response | `landing-page/lib/services.ts` | exact |
| `landing-page/lib/services.ts` (MODIFIED) | utility (fetch layer) | request-response | itself — add `recommendedProducts` to `ServiceSchema` | exact |
| `landing-page/app/products/page.tsx` | component (RSC page) | request-response | `landing-page/app/services/page.tsx` | exact |
| `landing-page/app/products/[slug]/page.tsx` | component (RSC page) | request-response | `landing-page/app/services/[slug]/page.tsx` | exact |
| `landing-page/app/services/[slug]/page.tsx` (MODIFIED) | component (RSC page) | request-response | itself — add "Recommended Products" section | exact |
| `landing-page/lib/data.ts` (MODIFIED) | config | — | itself — add one `navLinks` entry | exact |
| `API/ZachHairStudio.Api.Tests/Features/Products/ProductsServiceTests.cs` | test | — | `API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs` | exact |
| `API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs` | test | — | `API/ZachHairStudio.Api.Tests/Features/Services/ServicesControllerTests.cs` | exact |

## Pattern Assignments

### `Product.cs` (model, CRUD)

**Analog:** `API/ZachHairStudio.Shared/Features/Services/Service.cs` (full file, 34 lines)

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

    [Required, StringLength(200)]
    public string ShortDescription { get; set; } = null!;

    [Required, StringLength(2000)]
    public string LongDescription { get; set; } = null!;

    [Required, StringLength(50)]
    public string Category { get; set; } = null!;

    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
```

**Adaptation for `Product`:** drop `DurationMinutes` and `DisplayOrder` (D-05..D-10 never mention them — do not cargo-cult), add `public int Stock { get; set; }` (D-06). Everything else — `Id`, `Slug`, `Name`, `ShortDescription`, `LongDescription`, `Category`, `Price`, `ImageUrl`, `IsActive` — copies field-for-field with the same DataAnnotations lengths.

---

### `ProductResponseDto.cs` (model, CRUD)

**Analog:** `API/ZachHairStudio.Shared/Features/Services/ServiceResponseDto.cs` (full file, 24 lines)

```csharp
using System.Text.Json.Serialization;

namespace ZachHairStudio.Shared.Features.Services;

public class ServiceResponseDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string ShortDescription { get; set; } = null!;
    public string LongDescription { get; set; } = null!;
    public string Category { get; set; } = null!;
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsActive { get; set; }

    public int DisplayOrder { get; set; }
}
```

**Adaptation:** `ProductResponseDto` drops `DurationMinutes`/`DisplayOrder`, adds `Stock` (int, always shown — display-only per D-06, no `IsActive`-style conditional hiding needed since there's no "includeInactive" staff view for products yet). Keep `IsActive` off `ProductResponseDto` entirely (no write/staff endpoint reads it this phase) unless the planner wants forward parity — simplest is to omit it since D-16 ships no `includeInactive` query param for products.

---

### `ProductExtensions.cs` (utility/mapper, transform)

**Analog:** `API/ZachHairStudio.Shared/Features/Services/ServiceExtensions.cs` (full file, 49 lines) — see `ToDto()`/`ToEntity()` shown above in RESEARCH.md. Copy the `ToDto()` and `ToEntity()` shape 1:1, substituting `Stock` for `DurationMinutes`/`DisplayOrder`. Skip `ApplyTo()` unless the planner keeps `ProductUpdateDto` for parity (not required by D-16 — no write endpoints ship).

---

### `ProductCreateDtoValidator.cs` (validation, request-response)

**Analog:** `API/ZachHairStudio.Shared/Features/Services/ServiceCreateDtoValidator.cs` (full file, 41 lines)

```csharp
using FluentValidation;

namespace ZachHairStudio.Shared.Features.Services;

public class ServiceCreateDtoValidator : AbstractValidator<ServiceCreateDto>
{
    public ServiceCreateDtoValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(150)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase kebab-case.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ShortDescription).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LongDescription).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}
```

**Adaptation:** drop the `DurationMinutes`/`DisplayOrder` rules, add `RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);`. Same slug-kebab-case regex.

---

### `ProductsService.cs` (service, CRUD)

**Analog:** `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` (full file, 108 lines — shown in full above). Only the read-only slice is needed this phase:

```csharp
public async Task<IEnumerable<ServiceResponseDto>> GetServicesAsync(bool includeInactive = false)
{
    IQueryable<Service> query = _dbContext.Services;
    if (!includeInactive) query = query.Where(service => service.IsActive);
    return await query.OrderBy(s => s.DisplayOrder).Select(s => s.ToDto(includeInactive)).ToListAsync();
}

public async Task<Result<ServiceResponseDto>> GetBySlugAsync(string slug)
{
    var service = await _dbContext.Services
        .FirstOrDefaultAsync(service => service.Slug == slug && service.IsActive);
    return service is null
        ? Result<ServiceResponseDto>.NotFoundError($"Service '{slug}' not found.")
        : Result<ServiceResponseDto>.Success(service.ToDto());
}
```

**Adaptation for `ProductsService`:** rename to `GetProductsAsync()`/`GetBySlugAsync()`, drop `.OrderBy(DisplayOrder)` (no `DisplayOrder` field — order by `Name` or leave unordered per Claude's discretion), keep the `IsActive` filter and `Result<T>.NotFoundError` pattern verbatim. `ProductsService` still takes `IValidator<ProductCreateDto>` in its constructor per D-15 even though no controller action calls `CreateAsync` yet — keep constructor shape consistent with `ServicesService` for forward-compatibility, or omit `CreateAsync`/`UpdateAsync` methods entirely if the planner wants a leaner read-only class (YAGNI — either choice is defensible; D-15 only requires the validator class to exist, not that `ProductsService` exposes unused write methods).

---

### `ProductsController.cs` (controller, request-response)

**Analog:** `API/ZachHairStudio.Api/Controllers/ServicesController.cs` lines 52-65 (the two anonymous GET actions only — ignore lines 70-184 covering POST/PUT/image upload, none of which ship this phase):

```csharp
[HttpGet]
public async Task<ActionResult<IEnumerable<ServiceResponseDto>>> GetServices([FromQuery] bool includeInactive = false)
{
    var effectiveIncludeInactive = includeInactive && User.IsInRole(StaffRoles.Owner);
    var services = await _servicesService.GetServicesAsync(effectiveIncludeInactive);
    return Ok(services);
}

[HttpGet("{slug}", Name = nameof(GetService))]
public async Task<ActionResult<ServiceResponseDto>> GetService(string slug)
{
    var result = await _servicesService.GetBySlugAsync(slug);
    return result.IsSuccess ? Ok(result.Data) : NotFound();
}
```

**Adaptation:** `ProductsController` needs only `GetProducts()` (no `includeInactive` query param — no staff/owner view exists for products this phase, so drop the `User.IsInRole` branch entirely) and `GetProduct(string slug)`. Class-level `[ApiController]`/`[Route("api/[controller]")]` attributes copy verbatim. No `[Authorize]` anywhere — both actions stay fully anonymous.

---

### `ServiceRecommendedProduct.cs` (join entity) + `BookingDbContext` wiring — no in-repo analog

**Source:** RESEARCH.md Pattern 2 (Microsoft Learn `UsingEntity<T>()` explicit join pattern), applied alongside the existing `Service`/`Stylist` `HasData` blocks already in `BookingDbContext.OnModelCreating` (lines 39-150, confirmed present):

```csharp
public class ServiceRecommendedProduct
{
    public int ServiceId { get; set; }
    public int ProductId { get; set; }
}
```

```csharp
// In BookingDbContext.OnModelCreating, alongside the existing Service (line 39) / Stylist (line 138) blocks:
modelBuilder.Entity<Service>()
    .HasMany<Product>()
    .WithMany()
    .UsingEntity<ServiceRecommendedProduct>(
        j => j.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId),
        j => j.HasOne<Service>().WithMany().HasForeignKey(x => x.ServiceId),
        j =>
        {
            j.HasKey(x => new { x.ServiceId, x.ProductId });
            j.HasData(
                new ServiceRecommendedProduct { ServiceId = 1, ProductId = 1 },
                new ServiceRecommendedProduct { ServiceId = 1, ProductId = 2 });
        });
```

Also add `DbSet<Product> Products => Set<Product>();` next to the existing `DbSet<Service> Services => Set<Service>();` (line 21) and `DbSet<Stylist> Stylists => Set<Stylist>();` (line 23) — no separate `DbSet` needed for the join entity (it is reachable only via the skip-navigation config above, consistent with how EF Core's `UsingEntity` pattern is normally wired).

---

### `landing-page/lib/products.ts` (fetch layer, request-response)

**Analog:** `landing-page/lib/services.ts` (full file, 61 lines — shown in full above). Copy verbatim, renaming `Service`→`Product`, `/api/services`→`/api/products`, and adjusting the Zod schema fields to `{ id, slug, name, shortDescription, longDescription, category, price, stock, imageUrl }` (no `durationMinutes`/`displayOrder`). Keep the identical try/catch-to-`[]` (list) and 404-to-`null` (detail) conventions and the `PRODUCT_REVALIDATE_SECONDS = 60` constant (D-03).

---

### `landing-page/lib/services.ts` (MODIFIED — add `recommendedProducts`)

**Pattern (RESEARCH.md, Pitfall/Code Examples section):**
```typescript
import { ProductSchema } from "@/lib/products";

export const ServiceSchema = z.object({
  // ...existing fields (lines 9-20) unchanged...
  recommendedProducts: z.array(ProductSchema).optional(),
});
```
Add as an optional field on the existing schema — do not create a second schema/fetch pair (RESEARCH anti-pattern).

---

### `landing-page/app/products/page.tsx` (component, request-response)

**Analog:** `landing-page/app/services/page.tsx` (full file, 137 lines, shown in full above). Copy the `groupServicesByCategory` → `groupProductsByCategory` grouping helper, the card component (`ServiceCard` → `ProductCard`, replacing duration with a stock badge: `{product.stock === 0 ? "Out of Stock" : ...}`), `SectionHeading` usage, and the empty-state block (lines 94-103) verbatim in tone/structure.

### `landing-page/app/products/[slug]/page.tsx` (component, request-response)

**Analog:** `landing-page/app/services/[slug]/page.tsx` (full file, 101 lines, shown in full above). Copy the `notFound()` guard (lines 24-26), the `Image` usage (lines 49-57 — **caution: use a same-origin `/` path per D-07 and RESEARCH Pitfall 1, do not prefix with `API_BASE_URL`** since seed `ImageUrl`s should point at `landing-page/public/`), and the `<aside>` details-card structure (lines 63-92), substituting price/stock for duration/price.

### `landing-page/app/services/[slug]/page.tsx` (MODIFIED — add "Recommended Products" section)

Insert a new `<section>` below the existing `<article>`/`<aside>` grid (after line 94, before `</section>` line 95), reusing `SectionHeading` (already imported by `app/services/page.tsx`, not yet by this file — add the import) and the same card grid markup style as `ProductCard` in `app/products/page.tsx`. Render nothing (per D-14/empty-state discretion) or a small "No recommended products yet" note if `service.recommendedProducts` is empty/undefined.

### `landing-page/lib/data.ts` (MODIFIED — navLink)

**Analog:** itself, lines 5-12:
```typescript
export const navLinks: NavLink[] = [
  { label: "Home", href: "/#home" },
  { label: "Services", href: "/#services" },
  ...
];
```
Add `{ label: "Products", href: "/products" }` per D-04.

## Shared Patterns

### Result<T> + FluentValidation service pattern
**Source:** `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs`
**Apply to:** `ProductsService.cs` — `Result<T>.NotFoundError(...)` / `Result<T>.Success(...)` for every service method returning to a controller; FluentValidation `IValidator<T>.ValidateAsync()` even where the resulting validator is currently unused by any endpoint (D-15).

### Anonymous read-only GET controller pattern
**Source:** `API/ZachHairStudio.Api/Controllers/ServicesController.cs` lines 52-65
**Apply to:** `ProductsController.cs` — `[ApiController]`/`[Route("api/[controller]")]`, `Ok(result.Data)` / `NotFound()` translation from `Result<T>`, zero `[Authorize]` attributes (products endpoints are 100% anonymous this phase — no owner/includeInactive branch needed).

### RSC + ISR + Zod fetch pattern
**Source:** `landing-page/lib/services.ts`
**Apply to:** `landing-page/lib/products.ts` and any page needing product data — `fetch(..., { next: { revalidate: N } })`, Zod `.parse()` on the response body, try/catch → `[]` for lists, `response.status === 404 || !response.ok` → `null` for detail lookups. No client-side spinners (D-03).

### EF Core `HasData` seeding (not `UseSeeding`)
**Source:** `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` `OnModelCreating` (Service block ~line 39-64, Stylist block ~line 138-150)
**Apply to:** `Product` and `ServiceRecommendedProduct` seed rows — always via `entity.HasData(...)` inside `OnModelCreating`, never `UseSeeding`/`UseAsyncSeeding` (those only fire under `EnsureCreated()`, and this project calls `db.Database.Migrate()` on boot).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `API/ZachHairStudio.Shared/Features/Products/ServiceRecommendedProduct.cs` | model (join entity) | CRUD | First many-to-many relationship in this codebase — no existing join-entity precedent; follow RESEARCH.md's documented EF Core `UsingEntity<T>()` pattern (Microsoft Learn) instead of an in-repo analog. |

## Metadata

**Analog search scope:** `API/ZachHairStudio.Shared/Features/Services/`, `API/ZachHairStudio.Api/Controllers/`, `API/ZachHairStudio.Shared/Db/`, `API/ZachHairStudio.Api.Tests/Features/Services/`, `landing-page/lib/`, `landing-page/app/services/`
**Files scanned:** 10 Services-feature files, `BookingDbContext.cs`, `ServicesController.cs`, `services.ts`, `app/services/page.tsx`, `app/services/[slug]/page.tsx`, `lib/data.ts`
**Pattern extraction date:** 2026-08-09
