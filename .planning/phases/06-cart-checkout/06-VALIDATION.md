---
phase: 6
slug: cart-checkout
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-10
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Seeded from RESEARCH.md ## Validation Architecture. Task IDs refined to match PLAN.md.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + `Microsoft.AspNetCore.Mvc.Testing` 10.0.9; EF InMemory 10.0.9 for service/unit tests; real SQL via `SqlServerWebApplicationFactory` for stock concurrency (SHOP-04) — mirrors `ConcurrencyTests` `[VERIFIED: repo — ZachHairStudio.Api.Tests.csproj]` |
| **Config file** | `API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` (+ factories `CustomWebApplicationFactory.cs`, `SqlServerWebApplicationFactory.cs`) |
| **Quick run command (InMemory)** | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Carts\|FullyQualifiedName~Orders\|FullyQualifiedName~Stripe\|FullyQualifiedName~Checkout\|FullyQualifiedName~RecommendedForCheckout\|FullyQualifiedName~PriceAuthority\|FullyQualifiedName~GuestCheckout\|FullyQualifiedName~MarkFulfilled"` — keep filters narrow per task (e.g. `~Carts` only while on Plan 01); exclude `StockConcurrency` from the default quick path |
| **SQL-only concurrency** | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~StockConcurrency"` — requires reachable SQL Server (`SqlServerWebApplicationFactory`); run only when touching the SHOP-04 decrement transaction (Plan 05) |
| **Full suite command** | `dotnet test API/ZachHairStudio.slnx` |
| **Estimated runtime** | InMemory quick filters typically well under 30s when scoped to one feature name; full suite ~60–180s; **SQL-only `StockConcurrency` may exceed 30s Nyquist latency** (real SQL + contention) — acceptable for that gate; do not fold concurrency into every per-task sample |

---

## Sampling Rate

- **Per task commit:** Prefer a single narrow InMemory filter matching the task (`FullyQualifiedName~Carts`, `~Orders`, `~RecommendedForCheckout`, `~StripeWebhook`, etc.) — not the full combined OR list
- **When touching stock decrement / Plan 05 concurrency:** Run the SQL-only `StockConcurrency` filter separately; expect &gt;30s possible; do not block ordinary InMemory iteration on it
- **Per wave merge:** `dotnet test API/ZachHairStudio.slnx` (includes SqlServer-backed concurrency when SQL is reachable)
- **Phase gate:** Full suite green before `/gsd-verify-work`; SHOP-05 also needs human Stripe CLI UAT when CLI is available (`stripe listen` + test card) — synthetic signature tests cover the automated gate

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 06-01-01 | 01 | 1 | SHOP-01 | T-06-01 | Price-less cart DTOs; ConflictError overload | unit | `dotnet test … --filter "FullyQualifiedName~Carts"` (after 06-01-02) | ❌ W0 | ⬜ pending |
| 06-01-02 | 01 | 1 | SHOP-01 | T-06-01 | Session cart CRUD + server-enriched prices | unit + integration | `dotnet test … --filter "FullyQualifiedName~Carts"` | ❌ W0 | ⬜ pending |
| 06-02-01 | 02 | 2 | SHOP-01 | T-06-04 | X-Cart-Session-Id client; no price on upsert body | file/grep | grep header + upsert fields in `lib/cart.ts` | ❌ | ⬜ pending |
| 06-02-02 | 02 | 2 | SHOP-01 | T-06-04 | Add to Cart + Navbar badge | file/grep | AddToCartPanel + Navbar /cart link | ❌ | ⬜ pending |
| 06-02-03 | 02 | 2 | SHOP-01 | — | /cart empty/error/loading + review | file/grep | cart page copy strings | ❌ | ⬜ pending |
| 06-03-01 | 03 | 2 | SHOP-02,03,06 | T-06-06 | Checkout DTO no money; guest ClientId null tests RED | unit | OrdersServiceTests exist | ❌ W0 | ⬜ pending |
| 06-03-02 | 03 | 2 | SHOP-02,03,04,06 | T-06-06,T-06-07 | CreateCheckoutAsync strategy+ExecuteUpdateAsync + fake provider | unit + integration | `dotnet test … --filter "FullyQualifiedName~Orders\|PriceAuthority\|GuestCheckout\|Checkout"` | ❌ W0 | ⬜ pending |
| 06-04-01 | 04 | 3 | SHOP-07 | T-06-12 | ServiceRecommendedProduct join recommendations | unit | `dotnet test … --filter "FullyQualifiedName~RecommendedForCheckout"` | ❌ W0 | ⬜ pending |
| 06-04-02 | 04 | 3 | SHOP-07 | — | Cart chips + createCheckout helper | file/grep | Complete Your Routine + createCheckout | ❌ | ⬜ pending |
| 06-04-03 | 04 | 3 | SHOP-02,05 | T-06-10 | Checkout/success/cancel UI; success never fulfills | file/grep | success pages; no MarkFulfilled in success | ❌ | ⬜ pending |
| 06-05-01 | 05 | 4 | SHOP-02,05 | T-06-13 | Stripe.net + provider + webhook tests RED | unit | StripeWebhookTests file + package pin | ❌ W0 | ⬜ pending |
| 06-05-02 | 05 | 4 | SHOP-04,05 | T-06-13..16 | Webhook ConstructEvent + StockConcurrencyTests | integration | `dotnet test … --filter "FullyQualifiedName~StripeWebhook\|MarkFulfilled\|StockConcurrency"` | ❌ W0 | ⬜ pending |
| 06-05-03 | 05 | 4 | SHOP-02,05 | T-06-15 | Stripe CLI end-to-end human verify | manual | checkpoint:human-verify | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `API/ZachHairStudio.Api.Tests/Features/Carts/CartsServiceTests.cs` (+ controller tests) — SHOP-01 (Plan 01)
- [ ] `API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs` — SHOP-02/03/06 (Plan 03)
- [ ] `API/ZachHairStudio.Api.Tests/Features/Orders/StockConcurrencyTests.cs` — SHOP-04 (Plan 05)
- [ ] `API/ZachHairStudio.Api.Tests/Features/Payments/StripeWebhookTests.cs` (+ MarkFulfilledTests) — SHOP-05 (Plan 05)
- [ ] `API/ZachHairStudio.Api.Tests/Features/Products/RecommendedForCheckoutTests.cs` — SHOP-07 (Plan 04)
- [ ] Message-only `Result<T>.ConflictError` overload — Plan 01 Task 1
- [ ] Linux/CI: `SqlServerWebApplicationFactory` connection override — Plan 05 Task 2
- [ ] Secrets: `RESEND_API_KEY`, `Jwt:SigningKey`, `Stripe:SecretKey`, `Stripe:WebhookSecret` via user-secrets/env
- [ ] No new test framework install — xUnit + Mvc.Testing + SqlServer/InMemory already present
- [ ] No frontend Wave 0 gap — cart/checkout UX via UAT / UI-SPEC

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| End-to-end Stripe Checkout redirect + webhook with Stripe CLI | SHOP-02, SHOP-05 | Requires Stripe CLI + live test keys | `stripe listen --forward-to localhost:5236/api/stripe/webhook`; complete test Checkout with 4242…; confirm order Fulfilled via webhook |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 180s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
