---
phase: 05-product-catalog
fixed_at: 2026-08-09T11:30:00Z
review_path: .planning/phases/05-product-catalog/05-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 5: Code Review Fix Report

**Fixed at:** 2026-08-09T11:30:00Z
**Source review:** .planning/phases/05-product-catalog/05-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 3 (WR-01, WR-02, WR-03 — IN-01/IN-02 out of scope per fix_scope: critical_warning)
- Fixed: 3
- Skipped: 0

## Fixed Issues

### WR-01: Coverage claim for Price round-trip precision is unverified — no test asserts it

**Files modified:** `API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs`
**Commit:** a7a2cd3
**Applied fix:** Added `GetProduct_ReturnsExactSeededPrice`, asserting `product.Price == 24.00m` against the seeded `leave-in-repair-serum` product via a live `/api/products/{slug}` round-trip. Matches the fix suggestion verbatim; confirmed the seeded price (24.00m) via `BookingDbContext.cs`/migration seed data before writing the assertion. Verified with `dotnet test --filter FullyQualifiedName~GetProduct_ReturnsExactSeededPrice` (passed).

### WR-02: ProductCreateDtoValidator has zero test coverage despite an explicit must_have

**Files modified:** `API/ZachHairStudio.Api.Tests/Features/Products/ProductCreateDtoValidatorTests.cs` (new file)
**Commit:** 4a7068b
**Applied fix:** Created `ProductCreateDtoValidatorTests.cs` mirroring `ServiceCreateDtoValidatorTests.cs`'s structure, adapted to `ProductCreateDto`'s fields (Slug format/length, Name/ShortDescription/LongDescription/Category NotEmpty+MaxLength, Price/Stock boundary at 0 and negative). 18 tests total, all passing (`dotnet test --filter FullyQualifiedName~ProductCreateDtoValidatorTests`).

### WR-03: Product/service images render with empty `alt=""`, losing meaningful alt text

**Files modified:** `landing-page/app/products/page.tsx`, `landing-page/app/products/[slug]/page.tsx`, `landing-page/app/services/[slug]/page.tsx`
**Commit:** eeebdbe
**Applied fix:** Replaced `alt=""` with `alt={product.name}` / `alt={service.name}` on all four `<Image>` usages (catalog card, product detail, service detail, `RecommendedProductCard`). No `node_modules` present in the isolated worktree, so verification used Tier 1 (re-read) per verification_strategy's fallback rule for this file type; changes are single-prop JSX edits, low structural risk.

## Skipped Issues

None — all in-scope findings were fixed.

---

_Fixed: 2026-08-09T11:30:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
