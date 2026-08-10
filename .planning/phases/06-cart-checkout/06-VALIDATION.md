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
> Seeded from RESEARCH.md ## Validation Architecture. Task IDs will be refined when PLAN.md exists.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + `Microsoft.AspNetCore.Mvc.Testing` 10.0.9; EF InMemory 10.0.9 for service/unit tests; real SQL via `SqlServerWebApplicationFactory` for stock concurrency (SHOP-04) — mirrors `ConcurrencyTests` `[VERIFIED: repo — ZachHairStudio.Api.Tests.csproj]`
| **Config file** | `API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` (+ factories `CustomWebApplicationFactory.cs`, `SqlServerWebApplicationFactory.cs`)
| **Quick run command** | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Carts\|FullyQualifiedName~Orders\|FullyQualifiedName~Stripe\|FullyQualifiedName~StockConcurrency\|FullyQualifiedName~Checkout"` |
| **Full suite command** | `dotnet test API/ZachHairStudio.slnx` |
| **Estimated runtime** | ~60-180 seconds (SHOP-04 concurrency longer) |

---

## Sampling Rate

- **Per task commit:** Quick filter above (Carts/Orders/Stripe/StockConcurrency/Checkout subset) — prefer InMemory-backed tests when iterating; run `StockConcurrencyTests` only when touching the decrement transaction
- **Per wave merge:** `dotnet test API/ZachHairStudio.slnx` (includes SqlServer-backed concurrency when SQL is reachable)
- **Phase gate:** Full suite green before `/gsd-verify-work`; SHOP-05 also needs human Stripe CLI UAT when CLI is available (`stripe listen` + test card) — synthetic signature tests cover the automated gate
---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 06-00-01 | TBD | TBD | SHOP-01 | — | Add/review cart lines keyed by session (`X-Cart-Session-Id`); cart DTOs carry Pr | unit + integration | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Carts"` | ❌ W0 | ⬜ pending |
| 06-00-02 | TBD | TBD | SHOP-02 | — | Checkout creates Pending order + returns Stripe Checkout URL via `IPaymentProvid | integration | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Orders\` | ❌ W0 | ⬜ pending |
| 06-00-03 | TBD | TBD | SHOP-03 | — | Server recomputes line/total from catalog by ProductId; client-submitted price/t | unit (+ integration assert charged total) | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~PriceAuthority\` | ❌ W0 | ⬜ pending |
| 06-00-04 | TBD | TBD | SHOP-04 | — | Concurrent checkout on last unit → exactly one success and one 409; final Stock  | integration (**SqlServerWebApplicationFactory**, not InMemory) | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~StockConcurrencyTests"` | ❌ W0 | ⬜ pending |
| 06-00-05 | TBD | TBD | SHOP-05 | — | Bad/missing Stripe-Signature → 400; verified `checkout.session.completed` + `pay | unit + integration (synthetic signed payload; CLI optional for UAT) | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~StripeWebhook\` | ❌ W0 | ⬜ pending |
| 06-00-06 | TBD | TBD | SHOP-06 | — | Guest checkout: `Order.ClientId` is null; no auth required on cart/checkout | unit + integration | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~GuestCheckout\` | ❌ W0 | ⬜ pending |
| 06-00-07 | TBD | TBD | SHOP-07 | — | Recommended add-ons for checkout reuse `ServiceRecommendedProduct`; excludes in- | unit (+ optional API) | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~RecommendedForCheckout\` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `API/ZachHairStudio.Api.Tests/Features/Carts/CartsServiceTests.cs` (+ controller tests as needed) — SHOP-01 session-keyed cart CRUD
- [ ] `API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs` — SHOP-02/03/06 (fake `IPaymentProvider`, price-authority cases, null `ClientId`)
- [ ] `API/ZachHairStudio.Api.Tests/Features/Orders/StockConcurrencyTests.cs` — SHOP-04; `IClassFixture<SqlServerWebApplicationFactory>`; mirror `ConcurrencyTests` two-parallel-POST shape
- [ ] `API/ZachHairStudio.Api.Tests/Features/Payments/StripeWebhookTests.cs` (or `Orders/MarkFulfilledTests.cs`) — SHOP-05 signature reject + fulfill-once + no fulfill from redirect helper
- [ ] Recommended-for-checkout tests under Products/Services — SHOP-07 join reuse
- [ ] Message-only `Result<T>.ConflictError` overload (or DuplicateRecord→409 mapping) so stock 409 compiles — Pitfall 7
- [ ] Linux/CI: `SqlServerWebApplicationFactory` connection override (Azure SQL / Docker) — LocalDB unavailable in this codespace; without it SHOP-04 cannot run here
- [ ] Secrets for host boot: `RESEND_API_KEY`, `Jwt:SigningKey`, plus test `Stripe:WebhookSecret` for ConstructEvent cases (user-secrets/env — never tracked)
- [ ] No new test framework install — xUnit + Mvc.Testing + SqlServer/InMemory already present
- [ ] No frontend Wave 0 gap — `landing-page` has no test script (prior-phase precedent); cart/checkout UX via UAT / UI-SPEC
---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| End-to-end Stripe Checkout redirect + webhook with Stripe CLI | SHOP-02, SHOP-05 | Requires Stripe CLI + live test keys | stripe listen --forward-to localhost:5236/api/webhooks/stripe; complete test Checkout |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 180s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
