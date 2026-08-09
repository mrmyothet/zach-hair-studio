# Phase 5: Product Catalog - Research

**Researched:** 2026-08-09
**Domain:** Extending an already-established ASP.NET Core feature-layer pattern (service layer + FluentValidation + EF Core `HasData`) and Next.js 15 RSC/ISR read pattern to a second catalog entity, plus a many-to-many curation join table
**Confidence:** HIGH

## Summary

Phase 5 is not new-pattern research — it is a byte-for-byte structural clone of Phase 1 (Service Catalog), which already shipped and is running in production code today. Every architectural question this phase raises (service layer, `Result<T>`, FluentValidation, Zod, RSC + ISR, `HasData` seeding, slug uniqueness) was already answered and verified by Phase 1's `Features/Services/` slice, and CONTEXT.md's decisions explicitly point at that code as the template. The only genuinely new piece of technical work is the `ServiceRecommendedProduct` many-to-many join table (D-11) and its EF Core configuration — everything else is copy-adapt.

The join table has one real trap: EF Core's convention-based `HasMany().WithMany()` produces a *skip-navigation* with a hidden/shadow join entity, and seeding a hidden join entity via `HasData` requires an anonymous object with the shadow FK property names rather than a first-class C# type — this is a documented friction point. Given this phase has no plan to expose the join entity as an API resource of its own (D-11's `ServiceRecommendedProduct` is described as "a many-to-many join table," not queried as its own DTO), the lower-friction and more explicit path is to declare `ServiceRecommendedProduct` as its own POCO with `ServiceId`/`ProductId` and configure it explicitly via `UsingEntity<ServiceRecommendedProduct>` — this lets `HasData` seed it exactly like every other entity in `BookingDbContext` (`Service`, `Stylist`), with zero shadow-property gymnastics, and gives a natural place to add columns later (e.g. a curation note) without a breaking schema change.

Because this phase ships **zero new npm/NuGet packages** — every library, pattern, and DI registration it needs is already installed and already used by `Features/Services/` — there is no Package Legitimacy Audit to run and no new install commands to verify.

**Primary recommendation:** Clone `Features/Services/` file-for-file into `Features/Products/` (entity, `ProductResponseDto`, `ProductExtensions`, `ProductsService`, FluentValidation validators), add `ServiceRecommendedProduct` as an explicit join POCO configured via `UsingEntity<ServiceRecommendedProduct>()` on the existing `Service`↔`Product` skip-navigation, seed both `Product` and `ServiceRecommendedProduct` rows via `HasData()` in the same migration, and mirror `lib/services.ts` → `lib/products.ts` and the `/services` + `/services/[slug]` pages → `/products` + `/products/[slug]` pages on the frontend. Resolve D-16's "extended DTO vs. dedicated endpoint" choice by extending `ServiceResponseDto` with an optional `recommendedProducts` array populated only on the detail (`GetBySlugAsync`) path — this avoids a second controller/route pair for what is fundamentally "more data about the one service the client already asked for."

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Product list/detail query & filtering (`IsActive`) | API / Backend | Database / Storage | `ProductsService` owns the EF Core query, mirroring `ServicesService` exactly |
| Service→Product recommendation lookup | API / Backend | Database / Storage | Join-table query lives in `ProductsService` or `ServicesService` (whichever owns `ServiceResponseDto` population) — never in the controller |
| Write-path validation (name required, price ≥ 0, slug format, stock ≥ 0) | API / Backend | — | FluentValidation validators exist even though no write endpoint ships this phase (D-15) — same pattern as `ServiceCreateDtoValidator` |
| Response-shape validation (Zod parsing API JSON) | Frontend Server (SSR) | — | `lib/products.ts` Zod schemas run in the RSC data-fetching layer before JSX consumes the data |
| List/detail/recommended-products page rendering | Frontend Server (SSR) | CDN / Static | React Server Components render HTML; ISR (`next: { revalidate: 60 }`) caches at the edge, identical to `/services` |
| Static product images (`ImageUrl`) | CDN / Static | Frontend Server (SSR) | Files served from `landing-page/public/` this phase — no upload pipeline (D-07); see Pitfall 1 for the cross-origin trap this reveals in the existing Services pattern |
| Product/mapping persistence & seed data | Database / Storage | — | EF Core `HasData()` + a single new migration own schema and seed rows for both `Product` and `ServiceRecommendedProduct` |
| Stock display (badge only, no reservation) | Frontend Server (SSR) | API / Backend | `stock === 0` → "Out of Stock" badge is a pure render decision on data the API already returns; no locking logic anywhere (D-06) |

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Browse & detail page placement**
- **D-01:** Catalog lives at a dedicated `/products` route on `landing-page/`, mirroring Phase 1's `/services` pattern (D-01).
- **D-02:** Detail URLs are slug-based: `/products/[slug]`, mirroring Phase 1 D-02. `Product` carries a unique slug column.
- **D-03:** Catalog pages fetch in React Server Components with the same ISR revalidate window as `lib/services.ts` (60s). No client-side loading spinners for read-only content.
- **D-04:** Add a "Products" link to `navLinks` (`lib/data.ts`) pointing at `/products`. No homepage teaser section — the Core Value keeps the homepage focused on booking/services; products surface primarily through the service-detail recommendation (PROD-03), not a competing homepage section.

**Product model shape**
- **D-05:** `Category` is a simple string field on `Product`, mirroring Service D-05. No separate Category entity/FK.
- **D-06:** `Stock` is a plain `int` (0 = "out of stock" badge in the UI). No reservation/locking logic — that belongs to Phase 6's atomic-decrement checkout concern (SHOP-04), out of scope here.
- **D-07:** Nullable `ImageUrl` string pointing at static files in `landing-page/public/`, mirroring Service D-08. No upload pipeline — no staff CRUD UI ships for products in this milestone.
- **D-08:** `IsActive` bool (default true) included now, mirroring Service D-09, so public queries filter to active products and no later migration is needed if/when staff CRUD arrives. Public list/detail queries filter `IsActive == true`.
- **D-09:** Single fixed `decimal Price`, displayed as-is (mirrors Service D-06).
- **D-10:** Short + long description fields (mirrors Service D-11): a short teaser for list cards, a longer detail-page description.

**Service→Product recommendation mapping (PROD-03)**
- **D-11:** Mapping is a many-to-many join table `ServiceRecommendedProduct(ServiceId, ProductId)` — explicit curation, not a FK on `Product`. A product may reasonably support more than one service.
- **D-12:** The mapping is seeded via EF Core `HasData` at migration time (same pattern as D-13 Phase 1 service seeding). No staff UI to edit the mapping in this phase — PROD-03 says "curated," not "staff-editable"; a future CRUD phase can add management UI over the same table.
- **D-13:** On the service detail page, recommended products render in a new "Recommended Products" section below the existing description, reusing the `SectionHeading` component and card styling consistent with the products catalog page.
- **D-14:** The relationship is one-directional for this phase: service detail pages show recommended products; product detail pages do NOT show "used for which services" (matches ROADMAP SC3 wording exactly — can be added later without a schema change since the join table already carries both directions).

**API layer & validation scope**
- **D-15:** New `API/ZachHairStudio.Shared/Features/Products/` folder mirrors `Features/Services/` exactly: `Product` entity, `ProductResponseDto`, `ProductExtensions` mapper, `ProductsService` (all `BookingDbContext` access lives here, PLAT-01), FluentValidation validators (PLAT-02) even though no write endpoints ship yet — establishes the pattern for a future write phase without rework.
- **D-16:** Read-only public API only this phase: `GET /api/products`, `GET /api/products/{slug}`, and a way to fetch a service's recommended products (either `GET /api/services/{slug}` extended to include a `recommendedProducts` array, or a dedicated `GET /api/services/{slug}/recommended-products` endpoint — left to planner/implementer judgment based on what's cleanest against the existing `ServiceResponseDto` shape). No POST/PUT/DELETE for products in this phase — PROD-01/02/03 are all client-browse requirements.
- **D-17:** Seed data is Claude-authored plausible placeholder products (name, description, price, category, stock), explicitly flagged as owner-reviewable in the plan/summary — same precedent as Phase 1 D-15. Do not block on real product data or real images.
- **D-18:** Frontend response validation via Zod in a new `landing-page/lib/products.ts`, mirroring `lib/services.ts`'s `fetchServices`/`fetchServiceBySlug` pattern (schema, list schema, fetch functions with the same try/catch-to-empty-array and 404-to-null conventions).

### Claude's Discretion

- Exact FluentValidation rules per field (lengths, price/stock bounds, slug format) — mirror `ServiceCreateDtoValidator` conventions.
- Whether the recommended-products data flows through an extended `ServiceResponseDto` or a dedicated endpoint (D-16) — implementer's call based on what keeps the existing Services feature untouched vs. cleanest wiring. **Research recommendation: extend `ServiceResponseDto`** (see Summary and Pattern 3 below).
- Number and content of seeded placeholder products and categories.
- Visual details of the catalog/detail pages and the recommended-products section (consistent with existing Tailwind theme, card styling from `Services.tsx`/`/services` pages).
- Empty states (product with zero stock, service with no recommended products).

### Deferred Ideas (OUT OF SCOPE)

- Staff CRUD UI for products and the recommendation mapping — natural follow-up once Phase 5/6 prove out the model, but not requested by PROD-01/02/03. Not scheduled in the current 8-phase roadmap; flagged for a future milestone/phase if the owner wants it.
- Bidirectional recommendation display (showing "used for" services on a product detail page) — deferred per D-14, no schema change needed later since the join table already supports the reverse query.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROD-01 | Client can browse a list of products showing name, description, price, image, and stock | `Product` entity shape (Standard Stack), `/products` RSC page cloned from `app/services/page.tsx` (Pattern 1) |
| PROD-02 | Client can open a product detail page | `/products/[slug]` RSC page cloned from `app/services/[slug]/page.tsx` (Pattern 1) |
| PROD-03 | A service detail page surfaces stylist-recommended product add-ons via a curated service→product mapping | `ServiceRecommendedProduct` join entity + `UsingEntity` config (Pattern 2), extended `ServiceResponseDto.recommendedProducts` (Pattern 3) |
</phase_requirements>

## Standard Stack

### Core

No new packages — this phase reuses 100% of the stack Phase 1 already installed and verified.

| Library | Version (installed) | Purpose | Why Standard |
|---------|---------|---------|--------------|
| FluentValidation | 12.1.1 | `ProductCreateDtoValidator` rules (even though unused by any endpoint yet, per D-15) | Already the project's PLAT-02 validation layer `[VERIFIED: repo file — API/ZachHairStudio.Shared/ZachHairStudio.Shared.csproj]` |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | Auto-registers the new validators via the existing `AddValidatorsFromAssemblyContaining<ServiceCreateDtoValidator>()` assembly scan — **no `Program.cs` change needed for validator DI** | `[VERIFIED: repo file — Program.cs line 49 scans the whole assembly]` |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 | Persists `Product` and `ServiceRecommendedProduct` | Matches existing pinned version `[VERIFIED: repo file]` |
| zod | 4.4.3 | Parses `/api/products` and `/api/products/{slug}` responses in `lib/products.ts` | Locked by D-18; already installed `[VERIFIED: repo file — landing-page/package.json]` |

### Supporting

None new. `openapi-typescript`/`openapi-fetch` are not applicable — `landing-page` has no generated client (CLAUDE.md: "Hand-written fetch calls live in `lib/services.ts`... no generated client yet"); `lib/products.ts` follows the same hand-written convention.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Explicit `ServiceRecommendedProduct` join POCO + `UsingEntity<ServiceRecommendedProduct>()` | EF Core's fully-implicit skip-navigation (`HasMany().WithMany()` with no `UsingEntity` call at all) | The implicit path creates a *shadow* join entity with no C# type — seeding it via `HasData` requires an anonymous object matching internal shadow-property names (`ServiceId`, `ProductId` by convention, but undocumented/fragile if EF's naming convention ever changes) `[CITED: learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many]`. The explicit-POCO path used here is the documented, type-safe alternative and is directly seedable like `Service`/`Stylist` already are in this codebase. |
| Extending `ServiceResponseDto` with `recommendedProducts` (Claude's discretion, D-16) | Dedicated `GET /api/services/{slug}/recommended-products` endpoint | A dedicated endpoint is defensible if `ServiceResponseDto` needs to stay lean for the list endpoint — but D-16 only requires the data reachable "for a service detail page," and `GetBySlugAsync` already does a single-record fetch; one extra `Include`-style query there avoids a second controller action, second frontend fetch call, and second Zod schema for what is a 1:N sub-resource of a single detail view. |

**Installation:** None — no new packages this phase.

**Version verification:** All four items above are already-installed versions confirmed by reading the repo's own `.csproj`/`package.json` files directly, not registry lookups — this is a "clone the existing feature" phase, not a new-dependency phase.

## Package Legitimacy Audit

**Not applicable this phase.** Zero new external packages are introduced — `Features/Products/` reuses FluentValidation and EF Core (already audited and approved in Phase 1's research); `landing-page/lib/products.ts` reuses `zod` (already audited and approved in Phase 1's research, verdict OK, 211M+/wk downloads). No `npm install` / `dotnet add package` command is part of this phase's plan.

## Architecture Patterns

### System Architecture Diagram

```text
Client browser
   │
   ├──► GET /products ─────────────────────────────► landing-page RSC (app/products/page.tsx)
   │                                                        │
   │                                                        ▼
   │                                          fetchProducts() [lib/products.ts]
   │                                                        │  fetch + Zod parse, ISR 60s
   │                                                        ▼
   │                                          GET {API}/api/products ───────────► ProductsController
   │                                                                                    │
   │                                                                                    ▼
   │                                                                          ProductsService.GetProductsAsync()
   │                                                                                    │  WHERE IsActive
   │                                                                                    ▼
   │                                                                          BookingDbContext.Products
   │
   ├──► GET /products/[slug] ──────────────────────► app/products/[slug]/page.tsx
   │                                                        │
   │                                                        ▼
   │                                          fetchProductBySlug(slug) → GET {API}/api/products/{slug}
   │                                                        │  404/error → notFound()
   │                                                        ▼
   │                                          ProductsService.GetBySlugAsync(slug)
   │
   └──► GET /services/[slug] ──────────────────────► app/services/[slug]/page.tsx (existing)
                                                            │
                                                            ▼
                                              fetchServiceBySlug(slug) → GET {API}/api/services/{slug}
                                                            │  response now includes recommendedProducts[]
                                                            ▼
                                              ServicesService.GetBySlugAsync(slug)
                                                            │  LEFT JOIN ServiceRecommendedProduct
                                                            │            → Products (IsActive)
                                                            ▼
                                              BookingDbContext.Services / ServiceRecommendedProducts / Products
                                                            │
                                                            ▼
                                              "Recommended Products" section renders
                                              the same ProductCard grid as /products
```

### Recommended Project Structure

```
API/ZachHairStudio.Shared/Features/Products/
├── Product.cs                              # entity — mirrors Service.cs field-for-field shape
├── ProductResponseDto.cs                   # mirrors ServiceResponseDto
├── ProductCreateDto.cs                     # mirrors ServiceCreateDto (D-15 — no endpoint consumes it yet)
├── ProductCreateDtoValidator.cs            # mirrors ServiceCreateDtoValidator
├── ProductExtensions.cs                    # ToDto()/ToEntity() mirrors ServiceExtensions
└── ProductsService.cs                      # all Product DbContext access (PLAT-01)

API/ZachHairStudio.Shared/Features/ServiceRecommendations/   # or inline in Products/ — small enough either way
└── ServiceRecommendedProduct.cs            # join POCO: ServiceId, ProductId (+ nav props)

API/ZachHairStudio.Api/Controllers/
└── ProductsController.cs                   # GET /api/products, GET /api/products/{slug} — mirrors ServicesController's GET actions only

landing-page/lib/
└── products.ts                             # mirrors services.ts: ProductSchema, ProductListSchema, fetchProducts, fetchProductBySlug

landing-page/app/products/
├── page.tsx                                # mirrors app/services/page.tsx
└── [slug]/page.tsx                         # mirrors app/services/[slug]/page.tsx

landing-page/app/services/[slug]/page.tsx   # MODIFIED — add "Recommended Products" section
landing-page/lib/services.ts                # MODIFIED — extend ServiceSchema with recommendedProducts
landing-page/lib/data.ts                    # MODIFIED — add "Products" navLink (D-04)
```

### Pattern 1: Clone-the-feature (Service → Product)

**What:** `Features/Products/` is produced by copying every file in `Features/Services/` and doing a mechanical rename (`Service`→`Product`, `ServiceResponseDto`→`ProductResponseDto`, etc.), then adding the two Product-only fields (`Stock` int, no `DurationMinutes`/`DisplayOrder`).

**When to use:** Always, for this phase. Do not redesign the shape — CONTEXT.md D-05 through D-10 map 1:1 onto Service's already-shipped D-05/D-06/D-08/D-09/D-11 fields.

**Example — `Product.cs` (adapted from the real `Service.cs`):**
```csharp
// Source: API/ZachHairStudio.Shared/Features/Services/Service.cs (existing, verified)
using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Shared.Features.Products;

public class Product
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

    public decimal Price { get; set; }

    public int Stock { get; set; }                 // D-06 — display-only this phase

    [StringLength(500)]
    public string? ImageUrl { get; set; }           // D-07

    public bool IsActive { get; set; } = true;      // D-08
}
```
No `DisplayOrder`/`DurationMinutes` — CONTEXT.md's D-05..D-10 never mention product ordering or duration; catalog grouping only needs `Category` (D-05) which the frontend can group/sort by name within category, same simplification already applied by not requiring an explicit sort key for a small curated catalog. *(If a future plan wants merchandising order, add `DisplayOrder` then — do not speculatively add it now.)*

`ProductsService` mirrors `ServicesService` exactly: constructor takes `BookingDbContext` + `IValidator<ProductCreateDto>`, `GetProductsAsync()` filters `IsActive`, `GetBySlugAsync(slug)` returns `Result<ProductResponseDto>.NotFoundError` when absent/inactive.

### Pattern 2: Explicit many-to-many join entity, seeded via `HasData`

**What:** Rather than letting EF Core auto-generate a shadow join table for `Service`↔`Product`, declare the join type explicitly so it can be configured and seeded exactly like every other entity already in `BookingDbContext`.

**When to use:** Any many-to-many relationship in this codebase that needs `HasData` seeding (this is the first one) — the shadow-entity path works too, but requires anonymous-object seeding with internal shadow-property names, which is fragile and undocumented for exact naming.

**Example:**
```csharp
// Source: adapted from https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many
// (explicit join entity for HasData seeding) — pattern applied to this codebase's entities
namespace ZachHairStudio.Shared.Features.Products;

public class ServiceRecommendedProduct
{
    public int ServiceId { get; set; }
    public int ProductId { get; set; }
}
```

```csharp
// In BookingDbContext.OnModelCreating, alongside the existing Service/Stylist blocks:
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
This is a plain composite-key entity (no navigation properties required on `ServiceRecommendedProduct` itself) — `HasData` works on it exactly the way it already works on `Service` and `Stylist` in this file, with a `[ServiceId, ProductId]` seed pair instead of a single `Id`.

### Pattern 3: Extend `ServiceResponseDto` for recommended products (resolves D-16 discretion)

**What:** Add `IReadOnlyList<ProductResponseDto>? RecommendedProducts` to `ServiceResponseDto`, populated only by `GetBySlugAsync` (the list endpoint stays untouched — no behavior change for existing `GET /api/services` consumers).

**When to use:** This phase's PROD-03 — a service detail page needs its recommended products, and the client already calls `GetBySlugAsync` for that page.

**Example:**
```csharp
// Source: adapted from existing ServicesService.GetBySlugAsync (API/ZachHairStudio.Shared/Features/Services/ServicesService.cs)
public async Task<Result<ServiceResponseDto>> GetBySlugAsync(string slug)
{
    var service = await _dbContext.Services
        .FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);

    if (service is null)
    {
        return Result<ServiceResponseDto>.NotFoundError($"Service '{slug}' not found.");
    }

    var recommendedProducts = await _dbContext.Set<ServiceRecommendedProduct>()
        .Where(link => link.ServiceId == service.Id)
        .Join(_dbContext.Products.Where(p => p.IsActive),
              link => link.ProductId, product => product.Id, (link, product) => product)
        .Select(product => product.ToDto())
        .ToListAsync();

    var dto = service.ToDto();
    dto.RecommendedProducts = recommendedProducts;
    return Result<ServiceResponseDto>.Success(dto);
}
```
Keep `RecommendedProducts` nullable and use the same `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` convention `ServiceResponseDto.IsActive` already uses (see D-16's "byte-identical" precedent, DD-2 in STATE.md) so `GET /api/services` (the list endpoint, which never populates this field) keeps its existing wire shape.

### Anti-Patterns to Avoid

- **Adding `DisplayOrder`/`DurationMinutes` to `Product`:** Not requested by any Product decision (D-05..D-10) — `Service` has these because CONTEXT.md's Phase 1 decisions asked for them; don't cargo-cult every Service field onto Product.
- **Building the recommended-products query in the controller:** PLAT-01 requires all `BookingDbContext` access inside a `*Service` class — put the join query in `ServicesService` (or `ProductsService`, whichever ends up owning it), never in `ServicesController`/`ProductsController`.
- **Using `UseSeeding`/`UseAsyncSeeding` for the new seed rows:** Already established as a trap in Phase 1's research — this project's `Program.cs` calls `db.Database.Migrate()`, and those EF Core 9+ hooks only fire under `EnsureCreated()`. Use `HasData()` in `OnModelCreating`, same as every existing seeded entity.
- **A second Zod fetch/schema pair for recommended products:** If Pattern 3 (extended `ServiceResponseDto`) is chosen, `recommendedProducts` should be added as an optional array field on the *existing* `ServiceSchema` in `lib/services.ts`, not a new schema/fetch function in a separate file.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Many-to-many join persistence | A manual `ServiceProductMapping` table managed with raw SQL or ad-hoc LINQ inserts | EF Core `UsingEntity<T>()` explicit join configuration | EF Core's relationship API already handles FK constraints, cascade behavior, and query translation — a hand-rolled join loses migration tracking and `HasData` seeding support |
| Response/API contract validation | Custom manual `if (typeof x !== "string")` checks in `lib/products.ts` | Zod (`ProductSchema`, `ProductListSchema`) — already the project's D-18-mandated pattern | Consistent with `lib/services.ts`; one runtime-validation library across the whole frontend, not two |
| Input validation for a future write endpoint | DataAnnotations only | FluentValidation (`ProductCreateDtoValidator`) | PLAT-02 already establishes FluentValidation as the dedicated validation layer; DataAnnotations stay as defense-in-depth on the entity, matching `Service.cs`'s existing dual-layer approach |

**Key insight:** This entire phase is "don't hand-roll a new pattern when Phase 1 already built and shipped the correct one." The only net-new mechanism is the join table, and EF Core's own relationship API covers that too.

## Runtime State Inventory

Not applicable — this is a greenfield addition (new entity, new table, new routes), not a rename/refactor/migration phase.

## Common Pitfalls

### Pitfall 1: Product images will hit the same cross-origin trap Services already hit

**What goes wrong:** `ImageUrl` values are relative paths like `/uploads/products/foo.jpg`, served by the **API** origin (`:5236`), not the Next.js **landing-page** origin (`:3000`). If `<Image src={product.imageUrl}>` is used verbatim (as `app/services/[slug]/page.tsx` currently does for `service.imageUrl`), the browser resolves it relative to `:3000` and 404s.

**Why it happens:** Confirmed as a live, already-encountered bug in this exact codebase — `04-UAT.md` documents this precisely for the dashboard's `RowThumbnail` component: *"RowThumbnail resolves src as `${API_BASE_URL}${imageUrl}` (the API origin on :5236), and the landing page serves from its own separate public/ folder on :3000."* `landing-page`'s existing `app/services/[slug]/page.tsx` renders `service.imageUrl` **without** prefixing `API_BASE_URL` — this works today only because every seeded `Service.ImageUrl` currently begins with `/uploads/services/...` and, per the UAT doc, the images ARE served correctly for the dashboard via explicit `${API_BASE_URL}` prefixing, but landing-page's own detail page was never exercised against a non-null `ImageUrl` in Phase 1 UAT (all seed rows there were `ImageUrl = null` until a later quick-fix). This is worth verifying at execution time — **do not copy `app/services/[slug]/page.tsx`'s raw `src={service.imageUrl}` pattern into the Product detail page without prefixing `API_BASE_URL`,** since D-07 explicitly points ImageUrl at `landing-page/public/` (a same-origin path) OR (if seed data instead points at `/uploads/products/...` served by the API, mirroring Service's actual current seed convention) it needs the `${API_BASE_URL}` prefix to resolve cross-origin.

**How to avoid:** Decide explicitly at plan time whether seeded `Product.ImageUrl` values point at (a) files placed in `landing-page/public/` (same-origin, no prefix needed — this is what D-07's wording literally says) or (b) the API's `/uploads/products/` static-file root (cross-origin, needs `${API_BASE_URL}` prefix, mirrors what Service actually does today). Given D-07 says "static files in `landing-page/public/`," prefer (a) for placeholder seed images and skip the cross-origin question entirely this phase.

**Warning signs:** A product image renders as a broken `<img>` icon in dev even though the network tab shows the API returning a non-null `imageUrl` string.

### Pitfall 2: Seeding a many-to-many without an explicit join entity

**What goes wrong:** Calling `.HasMany(s => s.Products).WithMany(p => p.RecommendingServices)` (or an unnamed variant) without an explicit `UsingEntity<T>()` produces a shadow join table with a generated name and shadow FK properties. `HasData()` on that shadow entity requires an anonymous object shaped like `new { ServicesId = 1, ProductsId = 1 }` — the exact property names depend on EF Core's pluralization/ordering convention and are easy to get wrong, producing a confusing runtime or migration-generation error rather than a compile error.

**Why it happens:** EF Core's many-to-many convention is optimized for the "I don't need the join row for anything" case; this phase specifically needs to `HasData()`-seed the join row, which pushes toward the explicit-entity path documented by Microsoft `[CITED: learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many]`.

**How to avoid:** Declare `ServiceRecommendedProduct` as a real class (Pattern 2 above) and seed it with `HasData(new ServiceRecommendedProduct { ServiceId = ..., ProductId = ... })` — a normal typed object, not an anonymous shadow-property guess.

**Warning signs:** `dotnet ef migrations add` throws a model-validation error about a "shared-type entity" or the generated migration's `InsertData` call has unexpected column names.

### Pitfall 3: Forgetting `IsActive` filtering inside the join query

**What goes wrong:** A recommended-products query that joins `ServiceRecommendedProduct` → `Product` without also filtering `Product.IsActive == true` will surface a retired/inactive product on a service detail page, even though the standalone `/products` catalog correctly hides it.

**Why it happens:** It's easy to write `.Join(_dbContext.Products, ...)` and forget the `Where` clause that every other Product query path (list, detail) already applies — there's no foreign-key constraint that enforces "only active products may be linked."

**How to avoid:** Apply `.Where(p => p.IsActive)` inside the join, exactly as shown in Pattern 3's code example. Add a unit test asserting an inactive product is excluded from `recommendedProducts` even when a `ServiceRecommendedProduct` row links it (mirrors the existing `ServicesServiceTests.GetBySlugAsync_ReturnsNotFoundForInactiveSlug` test shape).

### Pitfall 4: `AddValidatorsFromAssemblyContaining<T>` needs no new call, but the Product validator class must still exist

**What goes wrong:** Assuming `ProductCreateDtoValidator` needs a separate `builder.Services.AddValidator<ProductCreateDtoValidator>()` registration line in `Program.cs`.

**Why it happens:** The existing call, `AddValidatorsFromAssemblyContaining<ServiceCreateDtoValidator>()`, scans the **entire assembly** for `AbstractValidator<T>` subclasses — any new validator class anywhere in `ZachHairStudio.Shared` is auto-registered. Adding an explicit second registration line is harmless but redundant; forgetting it is fine (no bug) — the risk is the reverse: over-thinking a change to `Program.cs` that isn't needed at all.

**How to avoid:** Confirm the existing assembly-scan registration by reading `Program.cs` line 49 before touching it — no edit needed there for validator DI.

## Code Examples

### Frontend fetch layer (`lib/products.ts`) — direct clone of the verified `lib/services.ts`

```typescript
// Source: landing-page/lib/services.ts (existing, verified) — Product field set adapted per D-05..D-10
import { z } from "zod";

const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236"
).replace(/\/$/, "");

const PRODUCT_REVALIDATE_SECONDS = 60; // D-03 — same window as services

export const ProductSchema = z.object({
  id: z.number(),
  slug: z.string(),
  name: z.string(),
  shortDescription: z.string(),
  longDescription: z.string(),
  category: z.string(),
  price: z.number(),
  stock: z.number(),
  imageUrl: z.string().nullable(),
});

export const ProductListSchema = z.array(ProductSchema);
export type Product = z.infer<typeof ProductSchema>;

export async function fetchProducts(): Promise<Product[]> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/products`, {
      next: { revalidate: PRODUCT_REVALIDATE_SECONDS },
    });
    if (!response.ok) throw new Error(`Products request failed with ${response.status}`);
    return ProductListSchema.parse(await response.json());
  } catch {
    return [];
  }
}

export async function fetchProductBySlug(slug: string): Promise<Product | null> {
  let response: Response;
  try {
    response = await fetch(
      `${API_BASE_URL}/api/products/${encodeURIComponent(slug)}`,
      { next: { revalidate: PRODUCT_REVALIDATE_SECONDS } }
    );
  } catch {
    return null;
  }
  if (response.status === 404 || !response.ok) return null;
  return ProductSchema.parse(await response.json());
}
```

### Extending `ServiceSchema` for `recommendedProducts` (Pattern 3's frontend half)

```typescript
// Source: landing-page/lib/services.ts (existing) — add one optional field
import { ProductSchema } from "@/lib/products";

export const ServiceSchema = z.object({
  // ...existing fields unchanged...
  recommendedProducts: z.array(ProductSchema).optional(),
});
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| N/A — this phase clones an already-current pattern | N/A | N/A | No stack drift since Phase 1's research (2026-07-07); FluentValidation 12.1.1, EF Core 10.0.9, zod 4.4.3 all remain the pinned versions in the repo today |

**Deprecated/outdated:** None encountered — nothing in this phase's scope touches a deprecated API.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | Extending `ServiceResponseDto` (rather than a dedicated endpoint) is the cleaner resolution of D-16's discretion point | Summary, Pattern 3 | Low — D-16 explicitly leaves this to planner/implementer judgment; if the planner disagrees, the dedicated-endpoint alternative is fully documented in Alternatives Considered and requires no rework of the `Product` entity/DTOs, only a different controller wiring |
| A2 | Product seed images should live in `landing-page/public/` (same-origin) rather than the API's `/uploads/` static-file root | Pitfall 1 | Medium — if the plan instead points seeded `ImageUrl` at `/uploads/products/...` (mirroring what `Service` seed data actually does today), the frontend fetch/render code needs an explicit `${API_BASE_URL}` prefix that D-07's literal wording doesn't call out; flagged so the planner makes this choice deliberately rather than by accident |

## Open Questions

1. **Does the recommended-products query belong in `ServicesService` or `ProductsService`?**
   - What we know: `ServiceResponseDto` is the DTO being extended (Pattern 3), and it's currently mapped/populated inside `ServicesService`.
   - What's unclear: Whether `ServicesService` should take a dependency on `Product`/`ServiceRecommendedProduct` types (owned by the new `Features/Products/` folder) or whether the query should live in `ProductsService` with `ServicesService` calling into it.
   - Recommendation: Keep it simple — `ServicesService.GetBySlugAsync` can query `_dbContext.Set<ServiceRecommendedProduct>()` and `_dbContext.Products` directly (both live on the shared `BookingDbContext`, so no new project reference is needed); this avoids a cross-service dependency for a single query. If the planner prefers stricter feature isolation, a `ProductsService.GetRecommendedForServiceAsync(serviceId)` method is the alternative — either is fine.

2. **Where does `ServiceRecommendedProduct.cs` physically live?**
   - What we know: D-11 calls it "a many-to-many join table," and it references both `Service` and `Product`.
   - What's unclear: Whether it belongs inside `Features/Products/` (since Products is the new feature this phase adds) or its own small `Features/ServiceRecommendations/` folder.
   - Recommendation: `Features/Products/ServiceRecommendedProduct.cs` is fine — it's a small POCO, doesn't need its own folder, and keeps this phase's file footprint minimal (YAGNI on folder structure for a single-file concept).

## Environment Availability

Skipped — this phase has no new external dependencies (tools, services, runtimes) beyond what Phase 1–4 already require and have verified working (dotnet/SQL Server LocalDB, Node/npm). No new CLI tools, databases, or services are introduced.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + `Microsoft.EntityFrameworkCore.InMemory` 10.0.9 (backend); no frontend test runner configured (matches CLAUDE.md: "no `test` script exists yet") |
| Config file | `API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` |
| Quick run command | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Products"` |
| Full suite command | `dotnet test API/ZachHairStudio.slnx` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|---------------------|--------------|
| PROD-01 | `GetProductsAsync()` returns only active products | unit | `dotnet test --filter "FullyQualifiedName~ProductsServiceTests.GetProductsAsync_ReturnsOnlyActiveProducts"` | ❌ Wave 0 |
| PROD-02 | `GetBySlugAsync()` 404s for unknown/inactive slug, succeeds for active slug | unit | `dotnet test --filter "FullyQualifiedName~ProductsServiceTests.GetBySlugAsync"` | ❌ Wave 0 |
| PROD-03 | `ServicesService.GetBySlugAsync` returns only active recommended products for a linked service; returns none/empty for an unlinked service | unit | `dotnet test --filter "FullyQualifiedName~ServicesServiceTests.GetBySlugAsync_RecommendedProducts"` | ❌ Wave 0 |
| PROD-01/02 | `GET /api/products` and `/api/products/{slug}` return expected JSON shape end-to-end | integration | `dotnet test --filter "FullyQualifiedName~ProductsControllerTests"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Products"`
- **Per wave merge:** `dotnet test API/ZachHairStudio.slnx`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `API/ZachHairStudio.Api.Tests/Features/Products/ProductsServiceTests.cs` — mirrors `ServicesServiceTests.cs` structure exactly (in-memory `BookingDbContext`, `CreateProduct` helper, `IsActive` filtering assertions) — covers PROD-01/PROD-02
- [ ] `API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs` — mirrors `ServicesControllerTests.cs`'s anonymous-GET assertions (no auth needed — no write endpoints exist yet) — covers PROD-01/PROD-02
- [ ] Extend `API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs` with `GetBySlugAsync_RecommendedProducts` cases (linked-active, linked-inactive-excluded, unlinked-empty) — covers PROD-03
- [ ] No new test framework install needed — xUnit + EF InMemory already present and already used by the exact test shape this phase needs

*(No frontend Wave 0 gap — no test script exists for `landing-page` today, matching every prior phase's precedent; frontend correctness is verified via UAT per CLAUDE.md's existing workflow.)*

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|--------------------|
| V2 Authentication | No | No new auth surface — all Product endpoints this phase are anonymous GETs, same as `GetServices`/`GetService` |
| V3 Session Management | No | N/A — no session state introduced |
| V4 Access Control | No | No write endpoints ship this phase (D-16); nothing to gate behind `[Authorize(Roles = StaffRoles.Owner)]` yet — future CRUD phase inherits the exact pattern `ServicesController`'s POST/PUT actions already establish |
| V5 Input Validation | Yes | FluentValidation `ProductCreateDtoValidator` (unused by any endpoint this phase, per D-15, but present) + DataAnnotations on `Product.cs` as defense-in-depth, mirroring `Service.cs` |
| V6 Cryptography | No | No secrets, tokens, or crypto touched by this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|------------------------|
| Mass assignment via a future `ProductCreateDto` accepting `Id`/`IsActive` from the client | Tampering | `ProductCreateDto` excludes `Id` (server-owned) and `IsActive` (defaults `true` in `ToEntity()`), mirroring `ServiceCreateDto`'s existing T-01-01 mitigation — even though no endpoint uses it yet, get the DTO shape right now so a future write phase inherits the safe default |
| Reflected XSS via unescaped product description in JSX | Tampering / Info Disclosure | React's default auto-escaping (already the pattern in `app/services/[slug]/page.tsx` — no `dangerouslySetInnerHTML` anywhere in this phase's scope) |
| Enumeration of inactive/retired products via slug guessing | Info Disclosure | `GetBySlugAsync` already filters `IsActive == true` before returning — an inactive product's slug 404s exactly like a nonexistent one, giving no signal that it once existed (mirrors `Service`'s existing behavior, verified by `ServicesServiceTests.GetBySlugAsync_ReturnsNotFoundForInactiveSlug`) |

## Sources

### Primary (HIGH confidence)
- `API/ZachHairStudio.Shared/Features/Services/*.cs` (repo, direct read) — the structural template this entire phase clones
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` (repo, direct read) — existing `HasData` seeding pattern for `Service`/`Stylist`/`StylistWorkingHours`
- `API/ZachHairStudio.Api/Controllers/ServicesController.cs` (repo, direct read) — anonymous-GET / Owner-gated-write controller pattern
- `landing-page/lib/services.ts`, `landing-page/app/services/page.tsx`, `landing-page/app/services/[slug]/page.tsx` (repo, direct read) — frontend RSC/ISR/Zod pattern
- `API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs` (repo, direct read) — existing test shape to mirror for Wave 0
- `.planning/phases/04-staff-management-services-availability/04-UAT.md` (repo, direct read) — documented cross-origin `imageUrl` bug precedent (Pitfall 1)
- `.planning/phases/01-service-catalog/01-RESEARCH.md` (repo, direct read) — Phase 1's own verified findings on `HasData` vs. `UseSeeding`, FluentValidation DI, package versions

### Secondary (MEDIUM confidence)
- [EF Core many-to-many relationships — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many) — explicit join-entity `UsingEntity<T>()` pattern for seedable many-to-many `[CITED: learn.microsoft.com]`
- [EF Core Data Seeding — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding) — `HasData()` vs. `UseSeeding`/`UseAsyncSeeding` distinction, confirming Phase 1 research's existing finding still holds

### Tertiary (LOW confidence)
- None used — this phase's scope was fully answerable from the repo's own already-shipped code plus two official Microsoft Learn pages.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new packages, all versions read directly from repo config files
- Architecture: HIGH — direct clone of a shipped, tested, UAT-verified Phase 1 pattern; the one new element (join table) is backed by official EF Core docs
- Pitfalls: HIGH — Pitfall 1 is a documented, already-encountered bug in this exact codebase (04-UAT.md), not a hypothetical

**Research date:** 2026-08-09
**Valid until:** 2026-09-08 (30 days — stable, no fast-moving dependencies; re-check only if FluentValidation/EF Core major versions bump before planning executes)
