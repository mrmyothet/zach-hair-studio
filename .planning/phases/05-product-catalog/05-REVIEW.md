---
phase: 05-product-catalog
reviewed: 2026-08-09T10:48:49Z
depth: standard
files_reviewed: 20
files_reviewed_list:
  - API/ZachHairStudio.Shared/Features/Products/Product.cs
  - API/ZachHairStudio.Shared/Features/Products/ProductCreateDto.cs
  - API/ZachHairStudio.Shared/Features/Products/ProductResponseDto.cs
  - API/ZachHairStudio.Shared/Features/Products/ProductExtensions.cs
  - API/ZachHairStudio.Shared/Features/Products/ProductCreateDtoValidator.cs
  - API/ZachHairStudio.Shared/Features/Products/ServiceRecommendedProduct.cs
  - API/ZachHairStudio.Shared/Features/Products/ProductsService.cs
  - API/ZachHairStudio.Shared/Features/Services/ServiceResponseDto.cs
  - API/ZachHairStudio.Shared/Features/Services/ServicesService.cs
  - API/ZachHairStudio.Shared/Db/BookingDbContext.cs
  - API/ZachHairStudio.Shared/Migrations/20260809095729_AddProducts.cs
  - API/ZachHairStudio.Api/Controllers/ProductsController.cs
  - API/ZachHairStudio.Api/Program.cs
  - API/ZachHairStudio.Api.Tests/Features/Products/ProductsServiceTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs
  - landing-page/lib/products.ts
  - landing-page/lib/services.ts
  - landing-page/lib/data.ts
  - landing-page/app/products/page.tsx
  - landing-page/app/products/[slug]/page.tsx
  - landing-page/app/services/[slug]/page.tsx
findings:
  critical: 0
  warning: 3
  info: 2
  total: 5
status: issues_found
---

# Phase 5: Code Review Report

**Reviewed:** 2026-08-09T10:48:49Z
**Depth:** standard
**Files Reviewed:** 20 (plus the 05-01/05-02 plan+summary docs read for context; migration Designer/Snapshot files excluded as generated)
**Status:** issues_found

## Summary

Backend (`ProductsService`/`ProductsController`, `ServiceRecommendedProduct` join, extended `ServicesService`) and frontend (`lib/products.ts`, `/products`, `/products/[slug]`, service-detail recommendations section) are straightforward clones of the existing `Services` pattern. `dotnet build` is clean (0 warnings/errors), the 12 Products/RecommendedProducts tests pass, the EF model snapshot matches the applied migration, and `ProductsController` never touches `BookingDbContext` (PLAT-01 verified by reflection test). No security issues found — all queries are LINQ-parameterized, no `dangerouslySetInnerHTML`, XSS mitigated by React's default escaping, mass-assignment guarded by DTO shape.

Issues found are all Warning/Info tier: a verification-claim gap in the plan's coverage table (Price precision "verified by" a test that doesn't actually assert it), a completely untested `ProductCreateDtoValidator`, and an accessibility regression (empty `alt=""` on content-bearing product/service images) inherited from the pattern this phase cloned.

## Warnings

### WR-01: Coverage claim for Price round-trip precision is unverified — no test asserts it

**File:** `API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs` (whole file); claim made in `.planning/phases/05-product-catalog/05-01-PLAN.md:34`
**Issue:** The plan's `must_haves.truths` states: *"Seeded Product.Price values round-trip exactly through GET /api/products (decimal(18,2) column, no floating-point drift) — verified by ProductsControllerTests asserting an exact decimal match against a seeded price (precision edge)"*. No such assertion exists — `ProductsControllerTests.cs` never reads or asserts a `Price` value anywhere (confirmed via `grep -n "Price" ProductsControllerTests.cs` → zero matches), and `ProductsServiceTests.cs` only ever sets `Price = 25` (a whole integer, incapable of exposing floating-point/precision drift) and never asserts `.Price` on the result either. The decimal-precision behavior is real (the migration correctly uses `decimal(18,2)`), but the specific verification the plan claims to exist does not — this is a documentation/traceability defect, and means a regression in price serialization (e.g. an accidental `double` cast somewhere upstream) would not be caught by this phase's test suite.
**Fix:** Add an assertion to an existing test, e.g. in `GetProduct_WithInactiveSlug_ReturnsNotFound`'s sibling or a new test:
```csharp
[Fact]
public async Task GetProduct_ReturnsExactSeededPrice()
{
    var client = _factory.CreateClient();
    var response = await client.GetAsync("/api/products/leave-in-repair-serum");
    var product = await response.Content.ReadFromJsonAsync<ProductResponseDto>();
    Assert.Equal(24.00m, product!.Price);
}
```

### WR-02: ProductCreateDtoValidator has zero test coverage despite an explicit must_have

**File:** `API/ZachHairStudio.Shared/Features/Products/ProductCreateDtoValidator.cs`
**Issue:** The plan's must_have states: *"ProductCreateDtoValidator accepts Price=0 and Stock=0 as valid (boundary edge, mirrors ServiceCreateDtoValidator's Price=0-valid precedent) and rejects a negative Price or Stock"*. `ServiceCreateDtoValidator` has a companion `ServiceCreateDtoValidatorTests.cs` (12+ tests, e.g. `Validate_WhenPriceIsNegative_HasValidationError`), but no equivalent `ProductCreateDtoValidatorTests.cs` was created — confirmed via `find API/ZachHairStudio.Api.Tests -iname "*ProductCreateDtoValidator*"` (no results). The validator is unreferenced by any registered endpoint this phase (by design, D-16 — no write endpoint ships), so a bug in it (e.g. a typo in the kebab-case regex, or a missing `GreaterThanOrEqualTo`) would go completely unnoticed until a future staff-CRUD phase wires it up.
**Fix:** Add `API/ZachHairStudio.Api.Tests/Features/Products/ProductCreateDtoValidatorTests.cs` mirroring `ServiceCreateDtoValidatorTests.cs`'s structure (Slug format/length, Name/ShortDescription/LongDescription/Category NotEmpty+MaxLength, Price/Stock boundary at 0 and negative). Small, low-risk addition since `AbstractValidator` testing needs no DB fixture.

### WR-03: Product/service images render with empty `alt=""`, losing meaningful alt text

**File:** `landing-page/app/products/page.tsx:53`, `landing-page/app/products/[slug]/page.tsx:51`, `landing-page/app/services/[slug]/page.tsx:54,130`
**Issue:** All four `<Image>` usages pass `alt=""`, which tells assistive technology to skip the image entirely (treat it as decorative). These images are the primary visual for a specific named product/service (e.g. "Leave-In Repair Serum"), not decoration — a screen-reader user gets no indication of what's pictured. This pattern is inherited unchanged from the pre-existing `ServiceCard`/service-detail code (not introduced by this phase), but this phase both propagates it into two new files (`app/products/page.tsx`, `app/products/[slug]/page.tsx`) and adds a third occurrence (`RecommendedProductCard` in `app/services/[slug]/page.tsx`), so it's in scope for this review as new/duplicated code.
**Fix:** Use the product/service name as alt text, e.g. `alt={product.name}` (catalog card, detail page) — the surrounding text already duplicates the name visually, but screen readers announcing "image: Leave-In Repair Serum" beside "Leave-In Repair Serum, $24.00" is standard, low-cost practice. If deliberately treating the image as redundant-with-adjacent-text decoration, that's a defensible call, but it wasn't made explicitly (05-UI-SPEC.md has no alt-text consideration) — worth a one-line note either way.

## Info

### IN-01: Category ordering on `/products` is non-deterministic across the seed set (`Map` insertion order after global name-sort), unlike `/services`'s explicit `displayOrder`

**File:** `landing-page/app/products/page.tsx:26-42`
**Issue:** `groupProductsByCategory` sorts the flat product list by `name` first, then builds category groups via `Map` insertion order — so the resulting category *section* order depends on which category's alphabetically-first product happens to come first (currently: Hair Care, Styling, Treatments, by coincidence of "Color-Safe..." < "Heat..." < "Revitalizing..."). `groupServicesByCategory` (the pattern this was cloned from) has the same structural shape, but `Service.DisplayOrder` gives it a stable, owner-controlled category order regardless of product-name changes. `Product` deliberately has no `DisplayOrder` (05-RESEARCH.md explicitly defers it), so this is a known, accepted simplification — flagging only because a future seed-data edit (e.g. renaming "Leave-In Repair Serum" to "A-New Serum") would silently reorder the whole `/products` page's category sections with no code change, which could surprise whoever edits the seed data next.
**Fix:** No action required this phase (matches 05-RESEARCH.md's explicit decision not to add `DisplayOrder` yet). `ponytail: category section order is incidental (alphabetical-by-first-product), add explicit Product.DisplayOrder when merchandising order actually matters (RESEARCH already flags the upgrade path).`

### IN-02: `ProductCreateDto`/`ToEntity()`/validator are fully unused dead code this phase (by design, but worth a visibility note)

**File:** `API/ZachHairStudio.Shared/Features/Products/ProductCreateDto.cs`, `ProductCreateDtoValidator.cs`, `ProductExtensions.cs:19-31`
**Issue:** `ProductCreateDto`, `ProductExtensions.ToEntity()`, and `ProductCreateDtoValidator` have no caller anywhere in `API/` outside their own declarations (confirmed via grep — no controller action, no service method references them). This is explicitly called out and justified in both the plan (D-15/D-16 — "present even though unused by any endpoint yet") and the summary, so it is not a defect, but it does mean these three files currently have no way to be exercised by CI at all (not even indirectly) until a future write-endpoint phase adds one.
**Fix:** No action required — deliberate per D-15/D-16. Noted alongside WR-02 since the validator specifically should still get direct unit tests now (cheap, decoupled from the missing endpoint) even while the endpoint itself waits.

---

_Reviewed: 2026-08-09T10:48:49Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
