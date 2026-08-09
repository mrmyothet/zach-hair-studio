---
phase: 5
slug: product-catalog
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
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
| 05-01-TBD | TBD | 1 | PROD-01 | — | `GetProductsAsync()` returns only active products | unit | `dotnet test --filter "FullyQualifiedName~ProductsServiceTests.GetProductsAsync_ReturnsOnlyActiveProducts"` | ❌ W0 | ⬜ pending |
| 05-01-TBD | TBD | 1 | PROD-02 | — | `GetBySlugAsync()` 404s for unknown/inactive slug, succeeds for active slug | unit | `dotnet test --filter "FullyQualifiedName~ProductsServiceTests.GetBySlugAsync"` | ❌ W0 | ⬜ pending |
| 05-01-TBD | TBD | 1 | PROD-03 | — | `ServicesService.GetBySlugAsync` returns only active recommended products for a linked service; empty for unlinked | unit | `dotnet test --filter "FullyQualifiedName~ServicesServiceTests.GetBySlugAsync_RecommendedProducts"` | ❌ W0 | ⬜ pending |
| 05-01-TBD | TBD | 1 | PROD-01/02 | — | `GET /api/products` and `/api/products/{slug}` return expected JSON shape end-to-end | integration | `dotnet test --filter "FullyQualifiedName~ProductsControllerTests"` | ❌ W0 | ⬜ pending |

*Exact Task IDs assigned by the planner; this map is seeded from RESEARCH.md's Phase Requirements → Test Map and refined once PLAN.md files exist.*

---

## Wave 0 Requirements

- [ ] `API/ZachHairStudio.Api.Tests/Features/Products/ProductsServiceTests.cs` — mirrors `ServicesServiceTests.cs` structure exactly (in-memory `BookingDbContext`, `CreateProduct` helper, `IsActive` filtering assertions) — covers PROD-01/PROD-02
- [ ] `API/ZachHairStudio.Api.Tests/Features/Products/ProductsControllerTests.cs` — mirrors `ServicesControllerTests.cs`'s anonymous-GET assertions (no auth needed — no write endpoints exist yet) — covers PROD-01/PROD-02
- [ ] Extend `API/ZachHairStudio.Api.Tests/Features/Services/ServicesServiceTests.cs` with `GetBySlugAsync_RecommendedProducts` cases (linked-active, linked-inactive-excluded, unlinked-empty) — covers PROD-03
- [ ] No new test framework install needed — xUnit + EF InMemory already present and already used by the exact test shape this phase needs

*(No frontend Wave 0 gap — no test script exists for `landing-page` today, matching every prior phase's precedent; frontend correctness is verified via UAT per CLAUDE.md's existing workflow.)*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Product images render correctly, same-origin (not the cross-origin trap documented in `04-UAT.md`) | PROD-01/02 | Visual/network-tab check, not unit-testable | Open `/products` and a product detail page in a browser; confirm images load and Network tab shows same-origin (`landing-page/public/`) requests, not cross-origin API calls |
| "Recommended Products" section renders correctly on a service detail page and is cleanly omitted when a service has zero mapped products | PROD-03 | Visual verification of the UI-SPEC's "omit if empty" rule | Visit a service detail page with recommended products (confirm section + cards render per UI-SPEC) and one seeded with none (confirm section is absent, no empty box) |
| Out-of-stock badge displays correctly and product remains browsable | PROD-01 | Visual state check | Seed a product with `stock: 0`; confirm the neutral "Out of Stock" badge renders per UI-SPEC Color section and the product card/detail page remain fully browsable (not greyed out or hidden) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
