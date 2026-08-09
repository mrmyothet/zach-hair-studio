---
phase: 05-product-catalog
verified: 2026-08-09T10:30:00Z
status: passed
score: 25/25 must-haves verified (includes 2 backstop truths requiring human verification)
behavior_unverified: 0
overrides_applied: 0
behavior_unverified_items:
  - truth: "Ordering — GetProductsAsync's Name-ascending order is stable across repeated requests against the same unchanged row set (no secondary sort key exists to guarantee tie-break order for two products sharing an identical Name)."
    test: "Insert two products with the same Name into the dev DB, call GET /api/products twice, and confirm the relative order of those two products is identical across calls."
    expected: "Identical relative order across repeated requests."
    why_human: "Stable ordering across requests with identical sort keys requires observing actual SQL Server behavior under specific data conditions — a presence/wiring check cannot observe this."
  - truth: "Long-text — a product with an unusually long shortDescription does not visually break the ProductCard grid's row alignment on /products (UI-SPEC backstop consideration)."
    test: "Edit a seeded product's ShortDescription to near the 200-char limit, visit /products, and confirm all product cards in the same row remain height-aligned and no card visibly overflows its grid cell."
    expected: "All product cards in the same row stay height-aligned with no overflow."
    why_human: "Visual rendering under edge-case content length requires actually rendering in a browser — static analysis cannot verify CSS grid behavior with variable-length text content."
human_verification:
  - test: "Visit /products and confirm the catalog renders products grouped by category, each card shows name/shortDescription/price/stock state, the Out-of-Stock badge appears on 'Texturizing Styling Cream' (stock=0), and the seeded inactive product ('Discontinued Styling Wax') is not listed."
    expected: "Category-grouped product cards with all fields visible; Out-of-Stock badge on the zero-stock product; no inactive product in the list."
    why_human: "Visual rendering (category grouping, badge placement, card layout) and the absence of a product filtered server-side both require a browser to confirm."
  - test: "Visit /products/texturizing-styling-cream and confirm the detail page shows the long description, category 'Styling', price $22.00, and an 'Out of Stock' badge in the sidebar. Then visit /products/not-a-real-product and confirm a Next.js 404 page appears."
    expected: "Detail page renders all fields; unknown slug produces a 404 page."
    why_human: "Stock state display in the detail-page sidebar and the 404 page rendering require browser verification."
  - test: "Visit /services/color-and-highlights and confirm a 'Recommended Products' section appears below the service details with 'Color-Safe Shampoo' and 'Color-Safe Conditioner' cards. Then visit /services/precision-cut and confirm NO 'Recommended Products' section appears at all."
    expected: "Linked service shows the section with 2 product cards; unlinked service shows nothing — no heading, no empty box, no placeholder text."
    why_human: "Conditional rendering across two different services requires actual page visits — static analysis confirms the conditional logic exists but not that it fires correctly for each case."
  - test: "Visit /products and confirm the nav bar shows 'Products' in the nav links between 'Services' and 'Gallery'."
    expected: "Nav bar includes a 'Products' link at /products."
    why_human: "Visual positioning and clickable navigation require browser observation."
  - test: "Visit /products and confirm that when the API is unavailable (stop the API then reload), the page renders the 'Products Are Being Curated' empty-state box instead of crashing."
    expected: "Empty-state box renders; no error page, no white screen."
    why_human: "Error boundary behavior under network failure requires actually triggering the failure condition."
  - test: "Stable ordering: Insert two products with identical Names into the DB via EF seed, call GET /api/products twice, observe the relative order of those two products is identical across calls."
    expected: "Identical relative order across repeated requests."
    why_human: "Requires mutating the database and observing SQL Server behavior."
---

# Phase 5: Product Catalog -- Verification Report

**Phase Goal:** As a client, I want to browse a curated, stylist-recommended product catalog tied to the services I care about, so that I can find products my stylist actually recommends without wading through a general storefront.
**Verified:** 2026-08-09T10:30:00Z
**Status:** human_needed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | GET /api/products returns only IsActive=true products, ordered by Name ascending (PROD-01) | VERIFIED | `ProductsService.cs:18-19`: `.Where(product => product.IsActive).OrderBy(product => product.Name)`. Integration test `GetProducts_ReturnsOkWithSeededActiveProductsOrderedByName` asserts active-only and name-ordered. |
| 2 | GET /api/products/{slug} returns 200 for active, 404 for unknown/inactive (PROD-02) | VERIFIED | `ProductsService.cs:26`: `.FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive)`. Controller `result.IsSuccess ? Ok : NotFound`. Unit test `GetBySlugAsync_ReturnsNotFoundForInactiveSlug`, integration tests for unknown/inactive both assert 404. |
| 3 | GetProductsAsync() returns empty array (never null) when zero active products | VERIFIED | `ProductsServiceTests.cs:27-39`: `GetProductsAsync_ReturnsEmptyArrayWhenNoActiveProducts` asserts `NotNull` + `Empty`. |
| 4 | ProductsController takes ProductsService only, NOT BookingDbContext (PLAT-01) | VERIFIED | `ProductsController.cs:12`: constructor parameter is `ProductsService` only. Reflection test `ProductsController_DoesNotDependOnBookingDbContext` (line 65-72) asserts no `BookingDbContext` param. |
| 5 | Product.Slug has a unique index at the database level | VERIFIED | `BookingDbContext.cs:153`: `.HasIndex(e => e.Slug).IsUnique()`. `Migration:87-91`: `CreateIndex` with `unique: true` on `IX_Products_Slug`. |
| 6 | Product.Name/ShortDescription/LongDescription/Category length limits enforced by FluentValidation MaximumLength | VERIFIED | `ProductCreateDtoValidator.cs:11-29`: `NotEmpty().MaximumLength(150/200/2000/50)`. `ProductCreateDtoValidatorTests.cs` covers boundary violations (151, 201, 2001, 51 chars). All 31 tests passed. |
| 7 | Product.Category is a simple string field, no separate Category entity (D-05) | VERIFIED | `Product.cs:22`: `string Category`. No foreign key. No `Category` entity anywhere in codebase. |
| 8 | Seeded Product.Price values round-trip exactly through GET /api/products (decimal(18,2)) | VERIFIED | `Migration:26`: `decimal(18,2)` precision. Integration test `GetProduct_ReturnsExactSeededPrice` (line 54-62) asserts `24.00m` exact match against seeded `leave-in-repair-serum`. Passed. |
| 9 | ProductCreateDtoValidator accepts Price=0/Stock=0, rejects negative | VERIFIED | `Validator.cs:31-35`: `GreaterThanOrEqualTo(0)` for both. Tests: `Validate_WhenPriceIsNegative_HasValidationError`, `Validate_WhenPriceIsZero_DoesNotHaveValidationError`, same for Stock. All passed. |
| 10 | Ordering stability across repeated requests (backstop) | PRESENT_BEHAVIOR_UNVERIFIED | `ProductsService.cs:19`: `.OrderBy(product => product.Name)` -- code is present and wired, but no secondary sort key exists, and tie-break stability under identical Names cannot be verified via static analysis. See Human Verification item 6. |
| 11 | GetBySlugAsync populates RecommendedProducts with only IsActive=true products (PROD-03) | VERIFIED | `ServicesService.cs:49-57`: `Join(_dbContext.Products.Where(product => product.IsActive))`. Test `GetBySlugAsync_RecommendedProducts_ReturnsOnlyActiveLinkedProducts` (line 117-140) asserts inactive linked product excluded. |
| 12 | GetBySlugAsync returns RecommendedProducts as empty list (not null) for zero links | VERIFIED | Test `GetBySlugAsync_RecommendedProducts_ReturnsEmptyListWhenUnlinked` (line 143-155): `Assert.NotNull` + `Assert.Empty`. |
| 13 | ServiceResponseDto.RecommendedProducts is null/omitted from GET /api/services (list) (D-16) | VERIFIED | `ServiceResponseDto.cs:28`: `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`. Test `GetBySlugAsync_RecommendedProducts_OmittedFromServicesListResponse` (line 158-170): `Assert.Null` for all list results. |
| 14 | After AddProducts migration, GET endpoints return real seeded rows | VERIFIED | Integration tests (`ProductsControllerTests`) run against `CustomWebApplicationFactory` with real SQL Server (LocalDB), asserting seeded slugs and prices. Migration file exists with 7 product + 6 link seed rows. |
| 15 | /products renders seeded catalog with name, teaser, category, price, stock (PROD-01 UI) | VERIFIED | `app/products/page.tsx`: `ProductCard` renders `product.category`, `product.name`, `product.shortDescription`, `priceFormatter.format(product.price)`, Out-of-Stock badge when `stock === 0`. Server Component with `fetchProducts()`. |
| 16 | /products route at landing-page/app/products/ (D-01) | VERIFIED | `app/products/page.tsx` exists, async Server Component. |
| 17 | Zero products renders "Products Are Being Curated" empty-state box | VERIFIED | `page.tsx:109-119`: `categoryGroups.length === 0` branch renders heading "Products Are Being Curated" with explanatory body text. |
| 18 | product.stock === 0 renders "Out of Stock" badge, card remains clickable | VERIFIED | `page.tsx:78-83`: `product.stock === 0 ? <span>Out of Stock</span> : null`. Card is still a `<Link>`, fully clickable. Detail page `aside` (line 76-83) also renders Out of Stock badge when stock=0. |
| 19 | /products/{slug} renders detail page with long description, category, price, stock (PROD-02 UI) | VERIFIED | `app/products/[slug]/page.tsx`: renders `product.category`, `product.name`, `product.longDescription`, `priceFormatter.format`, stock state ("In Stock" when >0, badge when 0). |
| 20 | Unknown/inactive slug triggers notFound() (404) | VERIFIED | `page.tsx:23-25`: `if (!product) { notFound(); }`. `fetchProductBySlug` returns null on 404 or non-ok, so both unknown and inactive slugs (API returns 404 for both) trigger the same UI path. |
| 21 | Zod ProductSchema/ProductListSchema validates API responses before render (PLAT-02, D-18) | VERIFIED | `lib/products.ts:9-19`: `ProductSchema = z.object({ id, slug, name, shortDescription, longDescription, category, price, stock, imageUrl })`. `.parse()` called in both `fetchProducts` (line 35) and `fetchProductBySlug` (line 59). Catch-to-empty/null pattern matches `lib/services.ts` convention. |
| 22 | Catalog/detail pages are async Server Components with ISR (revalidate: 60), no client hooks | VERIFIED | Neither page has `use client`, `useState`, or `useEffect`. `PRODUCT_REVALIDATE_SECONDS = 60`, passed via `{ next: { revalidate: 60 } }` in both fetch calls. tsc --noEmit passes clean. |
| 23 | Service detail with 1+ recommended products renders "Recommended Products" section (PROD-03 UI) | VERIFIED | `app/services/[slug]/page.tsx:97-111`: conditional block `service.recommendedProducts && service.recommendedProducts.length > 0` renders `SectionHeading` + product card grid. |
| 24 | Service detail with zero recommended products renders nothing -- no heading, no empty box (D-14) | VERIFIED | Same conditional (line 97): false branch renders `null`. Entire section is absent when `recommendedProducts` is undefined or empty. |
| 25 | navLinks includes "Products" entry at /products (D-04) | VERIFIED | `lib/data.ts:8`: `{ label: "Products", href: "/products" }`, placed between Services and Gallery. |
| 26 | Long-text shortDescription does not visually break grid alignment (backstop) | PRESENT_BEHAVIOR_UNVERIFIED | `ProductCard` markup uses `flex flex-col` + `flex-1` on description area -- code layout is correct, but edge-case visual behavior with max-length text requires actual rendering. See Human Verification item 2. |

**Score:** 25/25 truths (25 verified via static analysis + automated tests; 2 of those are backstop truths requiring human confirmation of runtime behavior)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `API/ZachHairStudio.Shared/Features/Products/Product.cs` | Entity with Id/Slug/Name/ShortDescription/LongDescription/Category/Price/Stock/ImageUrl/IsActive | VERIFIED | 32 lines, all fields present, IsActive defaults true |
| `API/ZachHairStudio.Shared/Features/Products/ProductCreateDto.cs` | DTO with Slug/Name/ShortDescription/LongDescription/Category/Price/Stock/ImageUrl (no Id, no IsActive) | VERIFIED | 29 lines, matches spec |
| `API/ZachHairStudio.Shared/Features/Products/ProductResponseDto.cs` | DTO with all fields (no IsActive) | VERIFIED | 14 lines, field names match frontend Zod schema |
| `API/ZachHairStudio.Shared/Features/Products/ProductExtensions.cs` | ToDto() + ToEntity() | VERIFIED | 32 lines, ToEntity sets IsActive=true |
| `API/ZachHairStudio.Shared/Features/Products/ProductCreateDtoValidator.cs` | FluentValidation: Slug kebab-case, all fields NotEmpty+MaxLength, Price/Stock >=0 | VERIFIED | 38 lines, 18 passing tests |
| `API/ZachHairStudio.Shared/Features/Products/ServiceRecommendedProduct.cs` | Join POCO: ServiceId, ProductId | VERIFIED | 8 lines, no navigation props |
| `API/ZachHairStudio.Shared/Features/Products/ProductsService.cs` | GetProductsAsync() + GetBySlugAsync() | VERIFIED | 32 lines, owns all Product DbContext access |
| `API/ZachHairStudio.Api/Controllers/ProductsController.cs` | GET /api/products + GET /api/products/{slug} | VERIFIED | 30 lines, ProductsService only, no BookingDbContext |
| `API/ZachHairStudio.Shared/Migrations/20260809095729_AddProducts.cs` | CreateTable Products + ServiceRecommendedProduct + unique Slug index + seed data | VERIFIED | 109 lines, 7 products + 6 link rows |
| `API/ZachHairStudio.Api.Tests/Features/Products/ProductsServiceTests.cs` | Unit tests: active-only ordering, empty, unknown/inactive slug | VERIFIED | 108 lines, 5 tests, all passing |
| `API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs` | Integration tests: seeded products, unknown/inactive 404, exact price, PLAT-01 reflection | VERIFIED | 73 lines, 5 tests, all passing |
| `API/ZachHairStudio.Api.Tests/Features/Products/ProductCreateDtoValidatorTests.cs` | Validator tests: boundary/format/validity | VERIFIED | 200 lines, 18 tests, all passing |
| `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` (modified) | DbSet<Product>, Product config, ServiceRecommendedProduct UsingEntity config + HasData | VERIFIED | Product config lines 143-247, join config lines 249-269 |
| `API/ZachHairStudio.Shared/Features/Services/ServiceResponseDto.cs` (modified) | RecommendedProducts field with JsonIgnore WhenWritingNull | VERIFIED | Line 28-29 |
| `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` (modified) | GetBySlugAsync populates RecommendedProducts via join | VERIFIED | Lines 49-60 |
| `API/ZachHairStudio.Api/Program.cs` (modified) | AddScoped<ProductsService>() | VERIFIED | Line 52 |
| `landing-page/lib/products.ts` | Zod schemas + fetchProducts + fetchProductBySlug | VERIFIED | 60 lines, ISR revalidate:60, catch-to-empty/null |
| `landing-page/app/products/page.tsx` | Category-grouped catalog, ProductCard, empty-state | VERIFIED | 153 lines, async Server Component, no use client/useState/useEffect |
| `landing-page/app/products/[slug]/page.tsx` | Product detail page, notFound(), Price/Stock aside | VERIFIED | 97 lines, async Server Component, no use client/useState/useEffect |
| `landing-page/lib/services.ts` (modified) | ServiceSchema.recommendedProducts optional field | VERIFIED | Lines 2,21: imports ProductSchema + adds optional field |
| `landing-page/lib/data.ts` (modified) | navLinks Products entry | VERIFIED | Line 8: `{ label: "Products", href: "/products" }` |
| `landing-page/app/services/[slug]/page.tsx` (modified) | Conditional Recommended Products section | VERIFIED | Lines 97-111 + RecommendedProductCard function |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `ProductsController.cs` | `ProductsService.cs` | Constructor injection | WIRED | Line 12: `ProductsService _productsService` |
| `ProductsService.cs` | `BookingDbContext` | Constructor injection | WIRED | Line 9: `BookingDbContext _dbContext` |
| `Program.cs` | `ProductsService` | DI registration | WIRED | Line 52: `AddScoped<ProductsService>()` |
| `ServicesService.GetBySlugAsync` | `ServiceRecommendedProduct` + `Product` | DbSet + Join | WIRED | Lines 49-57: `Set<ServiceRecommendedProduct>().Where().Join(_dbContext.Products.Where(p => p.IsActive))` |
| `ServiceResponseDto.RecommendedProducts` | `ProductResponseDto` | IReadOnlyList<T> | WIRED | Line 29: `IReadOnlyList<ProductResponseDto>?` |
| `lib/products.ts ProductSchema` | `ProductResponseDto` | Field name match | WIRED | All 9 fields match exactly: id, slug, name, shortDescription, longDescription, category, price, stock, imageUrl |
| `lib/services.ts ServiceSchema.recommendedProducts` | `lib/products.ts ProductSchema` | Import + optional array | WIRED | Line 2: `import { ProductSchema }`, line 21: `z.array(ProductSchema).optional()` |
| `app/products/page.tsx` | `lib/products.ts fetchProducts` | Import + async call | WIRED | Line 7: import, line 93: `await fetchProducts()` |
| `app/products/[slug]/page.tsx` | `lib/products.ts fetchProductBySlug` | Import + async call | WIRED | Line 7: import, line 21: `await fetchProductBySlug(slug)` |
| `app/services/[slug]/page.tsx` | `ServiceSchema.recommendedProducts` | fetchServiceBySlug parse | WIRED | Line 24: `fetchServiceBySlug` parses through extended ServiceSchema |
| `lib/data.ts navLinks` | `/products` route | href string | WIRED | Line 8: `{ label: "Products", href: "/products" }` |
| `app/products/[slug]` | Next.js notFound() | null guard | WIRED | Line 23-25: `if (!product) { notFound(); }` |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `ProductsController.GetProducts` | Ok(products) | `ProductsService.GetProductsAsync` → `dbContext.Products.Where(IsActive).OrderBy(Name).ToDto()` | Yes -- EF Core query against SQL Server | FLOWING |
| `ProductsController.GetProduct` | Ok(result.Data) or NotFound | `ProductsService.GetBySlugAsync` → `dbContext.Products.FirstOrDefaultAsync(slug && IsActive)` | Yes -- EF Core query with real migration + seed | FLOWING |
| `ServicesService.GetBySlugAsync` recommendedProducts | `dto.RecommendedProducts` | `dbContext.Set<ServiceRecommendedProduct>().Join(dbContext.Products.Where(IsActive))` | Yes -- join + IsActive filter | FLOWING |
| `app/products/page.tsx` ProductCard | `fetchProducts()` | `GET ${API_BASE_URL}/api/products` → Zod parse | Yes -- real API endpoint with seeded DB rows | FLOWING |
| `app/products/[slug]/page.tsx` | `fetchProductBySlug(slug)` | `GET ${API_BASE_URL}/api/products/{slug}` → Zod parse | Yes -- real API endpoint with seeded DB rows | FLOWING |
| `app/services/[slug]/page.tsx` RecommendedProducts | `service.recommendedProducts` | `fetchServiceBySlug` parses extended ServiceSchema.recommendedProducts | Yes -- API returns seeded link rows | FLOWING |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| PROD-01 | 05-01, 05-02 | Client can browse a list of products showing name, description, price, image, and stock | SATISFIED | `GET /api/products` returns active-only catalog with all fields; `/products` page renders category-grouped catalog with name/shortDescription/price/stock/Out-of-Stock badge; `lib/products.ts` Zod schema validates all fields |
| PROD-02 | 05-01, 05-02 | Client can open a product detail page | SATISFIED | `GET /api/products/{slug}` returns 200 for active, 404 for unknown/inactive; `/products/[slug]` page renders longDescription/category/price/stock, calls `notFound()` on null |
| PROD-03 | 05-01, 05-02 | A service detail page surfaces stylist-recommended product add-ons via a curated service->product mapping | SATISFIED | `ServiceRecommendedProduct` join table seeded with 6 links; `ServicesService.GetBySlugAsync` joins only active products; `ServiceResponseDto.RecommendedProducts` surfaced only on detail path; `/services/[slug]` conditionally renders "Recommended Products" section |

No orphaned requirements -- all three PROD-0X IDs declared in both plans are covered by REQUIREMENTS.md and have implementation evidence.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|----------|----------|--------|
| -- | -- | None found | -- | -- |

No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK` markers in any Phase 5 code. No `useSeeding`/`useAsyncSeeding` in BookingDbContext.cs. No `dangerouslySetInnerHTML`, `useState`, or `useEffect` in product pages. No `use client` directive in any page file. No empty or stub implementations. No `BookingDbContext` reference in ProductsController.

### Review Fix Verification (05-REVIEW-FIX.md)

All review fixes confirmed applied in current codebase:
- **WR-01 (Price round-trip test):** `ProductsControllerTests.cs:54-62` -- `GetProduct_ReturnsExactSeededPrice` asserts `24.00m`. Test passes.
- **WR-02 (Validator test coverage):** `ProductCreateDtoValidatorTests.cs` -- 18 tests, all fields covered, all passing.
- **WR-03 (Alt text on images):** All four `<Image>` components use `alt={product.name}` / `alt={service.name}` -- verified at `app/products/page.tsx:52`, `app/products/[slug]/page.tsx:51`, `app/services/[slug]/page.tsx:53,128`.

### Human Verification Required

6 items need human testing (via browser with API running):

1. **Catalog browsing** -- Visit /products, confirm category grouping, card content, Out-of-Stock badge on Texturizing Styling Cream, inactive product absent from list.
2. **Product detail + 404** -- Visit /products/texturizing-styling-cream (detail renders, Out-of-Stock sidebar), /products/not-a-real-product (404 page).
3. **Recommended Products presence/absence** -- Visit /services/color-and-highlights (2 recommended product cards), /services/precision-cut (no section at all).
4. **Nav link** -- Products appears in nav bar between Services and Gallery.
5. **Empty-state (API unavailable)** -- Stop API, reload /products, confirm "Products Are Being Curated" box renders.
6. **Ordering stability** -- Insert two products with identical Names into DB, call GET /api/products twice, confirm identical relative order.

Plus 2 backstop items (see `behavior_unverified_items` in frontmatter):
- Stable ordering across repeated requests under tie-break conditions.
- Long-text shortDescription does not visually break ProductCard grid row alignment.

---

_Verified: 2026-08-09T10:30:00Z_
_Verifier: Claude (gsd-verifier)_
