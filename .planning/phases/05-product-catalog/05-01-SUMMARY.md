---
phase: 05-product-catalog
plan: 01
subsystem: api
tags: [efcore, fluentvalidation, many-to-many, sqlserver, dotnet]

requires:
  - phase: 01-service-catalog
    provides: Features/Services/ template (service layer, Result<T>, FluentValidation, HasData seeding) cloned by this plan
provides:
  - Product entity + read-only GET /api/products, GET /api/products/{slug}
  - ServiceRecommendedProduct many-to-many join entity, explicit UsingEntity<T>() config
  - ServiceResponseDto.RecommendedProducts populated only by GetBySlugAsync
  - AddProducts migration (Product + join schema, unique Slug index, seed data)
affects: [05-02 (frontend catalog/detail pages consuming this API contract)]

tech-stack:
  added: []
  patterns:
    - "Explicit join POCO + UsingEntity<T>() for HasData-seedable many-to-many (first in this codebase)"

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Products/Product.cs
    - API/ZachHairStudio.Shared/Features/Products/ProductResponseDto.cs
    - API/ZachHairStudio.Shared/Features/Products/ProductCreateDto.cs
    - API/ZachHairStudio.Shared/Features/Products/ProductExtensions.cs
    - API/ZachHairStudio.Shared/Features/Products/ProductCreateDtoValidator.cs
    - API/ZachHairStudio.Shared/Features/Products/ServiceRecommendedProduct.cs
    - API/ZachHairStudio.Shared/Features/Products/ProductsService.cs
    - API/ZachHairStudio.Api/Controllers/ProductsController.cs
    - API/ZachHairStudio.Shared/Migrations/20260809095729_AddProducts.cs
  modified:
    - API/ZachHairStudio.Shared/Db/BookingDbContext.cs
    - API/ZachHairStudio.Shared/Features/Services/ServiceResponseDto.cs
    - API/ZachHairStudio.Shared/Features/Services/ServicesService.cs
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs

key-decisions:
  - "ServiceRecommendedProduct is an explicit join POCO configured via UsingEntity<T>() rather than EF's implicit shadow join table, so HasData seeding works with typed objects (RESEARCH Pattern 2/Pitfall 2)"
  - "Recommended-products query lives in ServicesService.GetBySlugAsync (not a separate ProductsService method) — both entities share BookingDbContext, no cross-service dependency needed (RESEARCH Open Question 1)"
  - "ServiceResponseDto.RecommendedProducts extends the existing DTO (not a dedicated endpoint), resolving D-16 discretion per RESEARCH recommendation"

patterns-established:
  - "Explicit many-to-many join entity + UsingEntity<T>() for any future HasData-seeded relationship"

requirements-completed: [PROD-01, PROD-02, PROD-03]

coverage:
  - id: D1
    description: "GET /api/products returns only active products ordered by Name; GET /api/products/{slug} returns 200 for active, 404 for unknown/inactive slugs"
    requirement: PROD-01
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Products/ProductsServiceTests.cs#GetProductsAsync_ReturnsOnlyActiveProductsOrderedByName"
        status: pass
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs#GetProducts_ReturnsOkWithSeededActiveProductsOrderedByName"
        status: pass
    human_judgment: false
  - id: D2
    description: "GET /api/products/{slug} product detail page contract — 200 for active slug, 404 for unknown/inactive slug (enumeration-safe)"
    requirement: PROD-02
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Products/ProductsServiceTests.cs#GetBySlugAsync_ReturnsNotFoundForInactiveSlug"
        status: pass
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs#GetProduct_WithInactiveSlug_ReturnsNotFound"
        status: pass
    human_judgment: false
  - id: D3
    description: "ServiceRecommendedProduct join surfaces only active linked products via ServiceResponseDto.RecommendedProducts on the service-detail path only; list endpoint stays byte-identical"
    requirement: PROD-03
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs#GetBySlugAsync_RecommendedProducts_ReturnsOnlyActiveLinkedProducts"
        status: pass
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs#GetBySlugAsync_RecommendedProducts_OmittedFromServicesListResponse"
        status: pass
    human_judgment: false
  - id: D4
    description: "ProductsController never touches BookingDbContext directly (PLAT-01)"
    requirement: PLAT-01
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs#ProductsController_DoesNotDependOnBookingDbContext"
        status: pass
    human_judgment: false

duration: 27min
completed: 2026-08-09
status: complete
---

# Phase 5 Plan 01: Product Catalog Backend Summary

**Product entity + ServiceRecommendedProduct curation join cloned from Features/Services, with GET /api/products, GET /api/products/{slug}, and an extended ServiceResponseDto.RecommendedProducts — migrated and seeded against LocalDB.**

## Performance

- **Duration:** 27 min
- **Started:** 2026-08-09T17:53:52+08:00
- **Completed:** 2026-08-09T18:00:23+08:00
- **Tasks:** 3
- **Files modified:** 14

## Accomplishments
- `Features/Products/` cloned from `Features/Services/` (Product entity, DTOs, mapper, FluentValidation validator) per D-05..D-10/D-15
- `ServiceRecommendedProduct` explicit join entity + `UsingEntity<T>()` config — HasData-seedable many-to-many, the first in this codebase
- `ProductsService`/`ProductsController` implement read-only `GET /api/products` and `GET /api/products/{slug}` (PLAT-01: no `BookingDbContext` in the controller)
- `ServicesService.GetBySlugAsync` extended to surface `RecommendedProducts` (active-only, filtered inside the join per RESEARCH Pitfall 3); `GetServicesAsync` untouched — list response stays byte-identical
- `AddProducts` migration generated and applied to `(localdb)\MSSQLLocalDB`: `Products` table, `ServiceRecommendedProduct` join table, unique `Products.Slug` index, seed data for both

## Task Commits

1. **Task 1: Domain model + RED** - `531f09c` (test)
2. **Task 2: GREEN — ProductsService/ProductsController/extended ServicesService** - `e908820` (feat)
3. **Task 3: DbSet + join config + AddProducts migration** - `e1e61bb` (feat)

_Note: Task 2's code required Task 3's `BookingDbContext.Products`/join wiring to compile, so Task 3's schema changes were authored alongside Task 2 and verified together before either was committed — each task's commit still contains only its own designated files per the plan's `files_modified` scoping._

## Files Created/Modified
- `API/ZachHairStudio.Shared/Features/Products/Product.cs` - entity (Slug/Name/ShortDescription/LongDescription/Category/Price/Stock/ImageUrl/IsActive)
- `API/ZachHairStudio.Shared/Features/Products/ProductResponseDto.cs`, `ProductCreateDto.cs`, `ProductExtensions.cs`, `ProductCreateDtoValidator.cs` - DTO/mapper/validator mirroring Service's shape
- `API/ZachHairStudio.Shared/Features/Products/ServiceRecommendedProduct.cs` - join POCO (ServiceId, ProductId)
- `API/ZachHairStudio.Shared/Features/Products/ProductsService.cs` - owns all Product `BookingDbContext` access
- `API/ZachHairStudio.Api/Controllers/ProductsController.cs` - anonymous GET list/detail actions
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` - `DbSet<Product>`, `Product` model config + 7-row seed, `ServiceRecommendedProduct` `UsingEntity<T>()` config + 6-link seed
- `API/ZachHairStudio.Shared/Features/Services/ServiceResponseDto.cs` - added `RecommendedProducts` (JsonIgnore WhenWritingNull)
- `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` - `GetBySlugAsync` joins to active recommended products
- `API/ZachHairStudio.Api/Program.cs` - `AddScoped<ProductsService>()`
- `API/ZachHairStudio.Shared/Migrations/20260809095729_AddProducts.cs` - schema + seed migration
- `API/ZachHairStudio.Api.Tests/Features/Products/ProductsServiceTests.cs`, `ProductsControllerTests.cs` - new test classes
- `API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs` - 3 new `RecommendedProducts` tests

## Seeded Data (D-17 — owner-reviewable placeholders)

**Products (7 rows, Id 1-7):**

| Id | Slug | Name | Category | Price | Stock | Active |
|----|------|------|----------|-------|-------|--------|
| 1 | leave-in-repair-serum | Leave-In Repair Serum | Hair Care | $24.00 | 40 | yes |
| 2 | color-safe-shampoo | Color-Safe Shampoo | Hair Care | $18.00 | 60 | yes |
| 3 | color-safe-conditioner | Color-Safe Conditioner | Hair Care | $19.00 | 55 | yes |
| 4 | texturizing-styling-cream | Texturizing Styling Cream | Styling | $22.00 | 0 (out-of-stock badge test) | yes |
| 5 | heat-protectant-spray | Heat Protectant Spray | Styling | $16.00 | 50 | yes |
| 6 | revitalizing-scalp-oil | Revitalizing Scalp Oil | Treatments | $28.00 | 30 | yes |
| 7 | discontinued-styling-wax | Discontinued Styling Wax | Styling | $15.00 | 0 | **no** (404 test path) |

All `ImageUrl = null` (D-07 — no seed images this phase; frontend falls back to placeholder, sidesteps RESEARCH Pitfall 1's cross-origin trap).

**Service → Product recommendation links (6 rows):**

| Service | Recommended Products |
|---------|----------------------|
| color-and-highlights | color-safe-shampoo, color-safe-conditioner |
| blowout-and-styling | texturizing-styling-cream, heat-protectant-spray |
| keratin-treatment | leave-in-repair-serum |
| scalp-treatment | revitalizing-scalp-oil |
| precision-cut | *(none — deliberately unlinked, exercises the empty-state path)* |
| full-glam-package | *(none — deliberately unlinked, exercises the empty-state path)* |

## Decisions Made
- `ServiceRecommendedProduct` uses an explicit join POCO + `UsingEntity<T>()`, not EF's implicit shadow join table — required for typed `HasData()` seeding (RESEARCH Pattern 2/Pitfall 2)
- The recommendation join query lives in `ServicesService.GetBySlugAsync` rather than a separate `ProductsService` method — both share `BookingDbContext`, avoiding a cross-feature service dependency (RESEARCH Open Question 1)
- `ServiceResponseDto` was extended in place (not a dedicated `/recommended-products` endpoint), resolving D-16's discretion per RESEARCH's recommendation

## Deviations from Plan

None — plan executed exactly as written. Task 2 and Task 3's code were necessarily authored together (Task 2's `ProductsService`/`ServicesService` code references `BookingDbContext.Products` and `ServiceRecommendedProduct`, which Task 3 adds to the DbContext) since C# requires the whole solution to compile before any test can run; task boundaries were preserved in the git history by staging and committing only each task's designated `files_modified` per commit.

## Issues Encountered
- `dotnet ef database update` initially failed against the Azure SQL connection string stored in user-secrets (`zachhairstudio.database.windows.net`) — the client IP wasn't allow-listed on the Azure SQL firewall (a known, pre-existing environment condition, see STATE.md Blockers). Resolved by overriding `ConnectionStrings__DefaultConnection` via an environment variable to point at `(localdb)\MSSQLLocalDB` for this migration run, matching the connection string documented in CLAUDE.md. No code change required.

## Next Phase Readiness
- Full backend contract for Product Catalog + Recommendations is real, migrated, and queryable — `GET /api/products`, `GET /api/products/{slug}`, and `GET /api/services/{slug}`'s `recommendedProducts` field all verified live against LocalDB with real seeded rows
- Ready for Plan 05-02 to build `landing-page/lib/products.ts` and the `/products` + `/products/[slug]` routes against this exact JSON contract (`id, slug, name, shortDescription, longDescription, category, price, stock, imageUrl`)
- Seeded product data is a Claude-authored placeholder (D-17) — flagged for owner review before launch, same as Phase 1's service seed data precedent

---
*Phase: 05-product-catalog*
*Completed: 2026-08-09*
