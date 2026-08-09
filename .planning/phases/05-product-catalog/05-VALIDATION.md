---
phase: 5
slug: product-catalog
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: 2026-08-09
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + `Microsoft.EntityFrameworkCore.InMemory` 10.0.9 (backend); no frontend test runner configured (matches CLAUDE.md: "no `test` script exists yet") |
| **Config file** | `API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` |
| **Quick run command** | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Products"` |
| **Full suite command** | `dotnet test API/ZachHairStudio.slnx` |
| **Estimated runtime** | ~30-60 seconds (full suite, per prior phases' precedent) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Products"`
- **After every plan wave:** Run `dotnet test API/ZachHairStudio.slnx`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 1 | PROD-01 | — | `GetProductsAsync()` returns only active products, ordered by Name; empty array when none active | unit | `dotnet test ZachHairStudio.Api.Tests --filter "FullyQualifiedName~ProductsServiceTests"` | ✅ | ✅ green |
| 05-01-02 | 01 | 1 | PROD-02 | T-05-01 | `GetBySlugAsync()` 404s for unknown/inactive slug, succeeds for active slug (enumeration-safe) | unit | `dotnet test ZachHairStudio.Api.Tests --filter "FullyQualifiedName~ProductsServiceTests.GetBySlugAsync"` | ✅ | ✅ green |
| 05-01-03 | 01 | 1 | PROD-03 | T-05-02 | `ServicesService.GetBySlugAsync` returns only active recommended products for a linked service; empty non-null list for unlinked; omitted from services list response | unit | `dotnet test ZachHairStudio.Api.Tests --filter "FullyQualifiedName~ServicesServiceTests.GetBySlugAsync_RecommendedProducts"` | ✅ | ✅ green |
| 05-01-04 | 01 | 1 | PROD-01/02 | — | `GET /api/products` and `/api/products/{slug}` return expected JSON shape end-to-end (exact seeded price round-trip) | integration | `dotnet test ZachHairStudio.Api.Tests --filter "FullyQualifiedName~ProductsControllerTests"` | ✅ | ✅ green |
| 05-01-05 | 01 | 1 | PROD-01 | — | Name-ascending ordering is stable across repeated requests with identical sort keys (backstop); `GET /api/products` items never carry a `recommendedProducts` key; `ProductCreateDto` has no client-settable `IsActive`/`Id` (T-05-03); `Product.ToEntity()` defaults `IsActive=true`; `ProductResponseDto` exposes no `IsActive`; validator length limits count UTF-16 units (multi-byte names accepted up to the limit) | unit | `dotnet test ZachHairStudio.Api.Tests --filter "FullyQualifiedName~ProductsEdgesTests"` | ✅ | ✅ green |
| 05-01-06 | 01 | 1 | PROD-03 | T-05-02 | `GET /api/services/{slug}` for a linked service includes a `recommendedProducts` array of active products in the JSON body; an unlinked service's empty array renders NO Recommended Products section in the UI (D-14) | integration | `dotnet test ZachHairStudio.Api.Tests --filter "FullyQualifiedName~ProductsEdgesTests"` | ✅ | ✅ green |
| 05-01-07 | 01 | 1 | PLAT-01 | — | `ProductsController` constructor does not inject/reference `BookingDbContext` | unit | `dotnet test ZachHairStudio.Api.Tests --filter "FullyQualifiedName~ProductsControllerTests.ProductsController_DoesNotDependOnBookingDbContext"` | ✅ | ✅ green |
| 05-01-08 | 01 | 1 | PROD-01/02/03 | — | After the AddProducts migration, `GET /api/products` and `GET /api/services/{slug}` return real seeded rows (unique `Product.Slug` DB index + `HasData` seed, verified in the applied migration) | integration | `dotnet test ZachHairStudio.Api.Tests --filter "FullyQualifiedName~SqlServerFixtureSmokeTests"` (needs LocalDB + RESEND_API_KEY/Jwt:SigningKey) | ✅ | ⚠️ caveat |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky — task IDs follow the `{phase}-{plan}-{task}` convention from each PLAN.md; the product-catalog backend tests are self-contained and run without the Resend key (integration factory boots in Testing), while the LocalDB migration smoke test needs the full-suite secrets.*

---

## Wave 0 Requirements

- [x] `API/ZachHairStudio.Api.Tests/Features/Products/ProductsServiceTests.cs` — mirrors `ServicesServiceTests.cs` structure exactly (in-memory `BookingDbContext`, `CreateProduct` helper, `IsActive` filtering assertions) — covers PROD-01/PROD-02
- [x] `API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs` — mirrors `ServicesControllerTests.cs`'s anonymous-GET assertions (no auth needed — no write endpoints exist yet) — covers PROD-01/PROD-02
- [x] `API/ZachHairStudio.Api.Tests/Features/Products/ProductsEdgesTests.cs` — Nyquist-added uncovered-edge coverage (ordering stability backstop, JSON wire-shape checks, mass-assignment guard, `IsActive` non-exposure, UTF-16 unit-count length limits) — covers PROD-01/PROD-02/PROD-03/PLAT-01
- [x] Extend `API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs` with `GetBySlugAsync_RecommendedProducts` cases (linked-active, linked-inactive-excluded, unlinked-empty) — covers PROD-03
- [x] No new test framework install needed — xUnit + EF InMemory already present and already used by the exact test shape this phase needs

*(No frontend Wave 0 gap — no test script exists for `landing-page` today, matching every prior phase's precedent; frontend correctness is verified via UAT per CLAUDE.md's existing workflow.)*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Product images render correctly, same-origin (not the cross-origin trap documented in `04-UAT.md`) | PROD-01/02 | Visual/network-tab check, not unit-testable | Open `/products` and a product detail page in a browser; confirm images load and Network tab shows same-origin (`landing-page/public/`) requests, not cross-origin API calls |
| "Recommended Products" section renders correctly on a service detail page and is cleanly omitted when a service has zero mapped products | PROD-03 | Visual verification of the UI-SPEC's "omit if empty" rule | Visit a service detail page with recommended products (confirm section + cards render per UI-SPEC) and one seeded with none (confirm section is absent, no empty box) |
| Out-of-stock badge displays correctly and product remains browsable | PROD-01 | Visual state check | Seed a product with `stock: 0`; confirm the neutral "Out of Stock" badge renders per UI-SPEC Color section and the product card/detail page remain fully browsable (not greyed out or hidden) |
| Multi-byte product name (e.g. a product named with 75 emoji) renders correctly end-to-end through the catalog and detail pages | PROD-01/02 | Visual rendering + DB round-trip | Add/seed a product with a surrogate-pair name and visit `/products` and `/products/{slug}`; confirm it renders and that the API accepted it (validator counts UTF-16 units: 150-unit limit verified by `ProductsEdgesTests.Validate_NameOverUnitLimit_HasValidationError`) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
