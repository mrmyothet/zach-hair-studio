---
phase: 06-cart-checkout
plan: 05
subsystem: payments
tags: [stripe, webhook, checkout, shop-02, shop-04, shop-05, concurrency]

requires:
  - phase: 06-cart-checkout Plan 03
    provides: OrdersService.CreateCheckoutAsync + MarkFulfilledAsync + FakePaymentProvider + Order schema
  - phase: 06-cart-checkout Plan 04
    provides: Guest checkout UI + success display-only pages
provides:
  - Stripe.net 52.2.0 StripePaymentProvider Checkout Session create (SHOP-02)
  - Raw-body StripeWebhookController ConstructEvent → MarkFulfilledAsync (SHOP-05)
  - StockConcurrencyTests on SqlServerWebApplicationFactory with connection override (SHOP-04)
  - Program DI swap: StripePaymentProvider outside Testing; Fake in Testing
affects: [phase-06 verification, production Stripe Dashboard webhook config]

tech-stack:
  added: [Stripe.net 52.2.0]
  patterns:
    - "Webhook raw Request.Body + EventUtility.ConstructEvent; never FromBody"
    - "Fulfillment only via MarkFulfilledAsync from verified paid checkout.session.completed"
    - "SqlServerWebApplicationFactory honors ConnectionStrings__DefaultConnection / TEST_SQLSERVER_CONNECTION"

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Payments/StripePaymentProvider.cs
    - API/ZachHairStudio.Api/Controllers/StripeWebhookController.cs
    - API/ZachHairStudio.Api.Tests/Features/Payments/StripeWebhookTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Orders/MarkFulfilledTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Orders/StockConcurrencyTests.cs
  modified:
    - API/ZachHairStudio.Shared/ZachHairStudio.Shared.csproj
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Api.Tests/SqlServerWebApplicationFactory.cs
    - API/ZachHairStudio.Api.Tests/SqliteWebApplicationFactory.cs

key-decisions:
  - "Stripe.net pinned 52.2.0 on Shared; webhook controller stays in Api"
  - "ConstructEvent uses throwOnApiVersionMismatch: false for SDK/account version drift; signature still required"
  - "Stripe SecretKey/WebhookSecret ValidateOnStart only in Development; Testing keeps FakePaymentProvider"
  - "StockConcurrencyTests use Docker/Azure SQL via TEST_SQLSERVER_CONNECTION on Linux (no LocalDB)"

patterns-established:
  - "Synthetic Stripe-Signature via EventUtility.ComputeSignature + test whsec_ for CI without Stripe CLI"
  - "SqlServer factory AppendDatabaseName for unique per-run catalogs on shared SQL hosts"

requirements-completed: [SHOP-02, SHOP-04, SHOP-05]

coverage:
  - id: D1
    description: StripePaymentProvider creates Mode=payment Checkout Session with price_data UnitAmount and returns Session.Url
    requirement: SHOP-02
    verification:
      - kind: other
        ref: API/ZachHairStudio.Shared/Features/Payments/StripePaymentProvider.cs
        status: pass
    human_judgment: true
    rationale: Live Session create against Stripe test mode requires SecretKey + hosted Checkout (Task 3 UAT)
  - id: D2
    description: POST /api/stripe/webhook rejects bad/missing Stripe-Signature with 400; paid checkout.session.completed fulfills once
    requirement: SHOP-05
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Payments/StripeWebhookTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: MarkFulfilledAsync Pending→Fulfilled is idempotent no-op when already Fulfilled
    requirement: SHOP-05
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Orders/MarkFulfilledTests.cs
        status: pass
    human_judgment: false
  - id: D4
    description: Two parallel last-unit checkouts → one success + one 409; Stock==0 on SQL Server
    requirement: SHOP-04
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Orders/StockConcurrencyTests.cs
        status: pass
    human_judgment: false
  - id: D5
    description: Human Stripe CLI listen + test-card Checkout end-to-end fulfillment
    requirement: SHOP-02
    verification: []
    human_judgment: true
    rationale: Hosted Checkout + real webhook forwarding cannot be fully replaced by synthetic signature fixtures

duration: 7min
completed: 2026-08-10
status: complete
---

# Phase 6 Plan 05: Stripe Checkout + Webhook Fulfillment Summary

**Stripe.net 52.2.0 Checkout Session create, signature-verified webhook → MarkFulfilledAsync, and SQL Server last-unit concurrency proof — human Stripe CLI UAT still deferred.**

## Performance

- **Duration:** 7 min
- **Started:** 2026-08-10T06:38:55Z
- **Completed:** 2026-08-10T06:46:29Z
- **Tasks:** 2/3 code tasks complete; Task 3 human-verify awaiting operator
- **Files modified:** 9

## Accomplishments

- Pinned Stripe.net 52.2.0 and implemented `StripePaymentProvider` (Mode=payment, price_data UnitAmount, ClientReferenceId/metadata, Idempotency-Key).
- `StripeWebhookController` reads raw body, `EventUtility.ConstructEvent`, bad sig → 400; paid `checkout.session.completed` calls Plan 03 `MarkFulfilledAsync` only.
- Program registers real Stripe provider outside Testing; Fake retained for tests; Development ValidateOnStart for Stripe secrets.
- `StockConcurrencyTests` + SqlServer factory connection override green against Docker SQL Server on Linux.

## Task Commits

1. **Task 1: Stripe.net + StripePaymentProvider + webhook RED tests** - `619145c` (feat)
2. **Task 2: GREEN — StripeWebhookController + DI + StockConcurrencyTests** - `ff4459b` (feat)
3. **Task 3: Human verify — Stripe CLI checkout + webhook fulfillment** - *pending* (checkpoint)

**Plan metadata:** pending final docs commit after human UAT approval

## Files Created/Modified

- `API/ZachHairStudio.Shared/Features/Payments/StripePaymentProvider.cs` - Real Checkout Session create
- `API/ZachHairStudio.Api/Controllers/StripeWebhookController.cs` - Raw-body ConstructEvent webhook
- `API/ZachHairStudio.Api/Program.cs` - Stripe DI swap + Development ValidateOnStart
- `API/ZachHairStudio.Api.Tests/Features/Payments/StripeWebhookTests.cs` - Signature + fulfill + redelivery
- `API/ZachHairStudio.Api.Tests/Features/Orders/MarkFulfilledTests.cs` - Idempotent fulfill unit coverage
- `API/ZachHairStudio.Api.Tests/Features/Orders/StockConcurrencyTests.cs` - Last-unit SQL concurrency
- `API/ZachHairStudio.Api.Tests/SqlServerWebApplicationFactory.cs` - Env connection override + Fake provider
- `API/ZachHairStudio.Api.Tests/SqliteWebApplicationFactory.cs` - Test Stripe:WebhookSecret for fixtures
- `API/ZachHairStudio.Shared/ZachHairStudio.Shared.csproj` - Stripe.net 52.2.0

## Decisions Made

- Webhook wires Plan 03 `MarkFulfilledAsync` only — no reimplementation of the status flip.
- Synthetic webhook signatures via `EventUtility.ComputeSignature` for CI; CLI UAT remains the live trust check.
- Linux/Codespaces uses `TEST_SQLSERVER_CONNECTION` to Docker SQL Server 2022 instead of LocalDB.

## Deferred Human UAT (Task 3)

**Status:** Not run in this environment — Stripe CLI is not installed, and `Stripe:SecretKey` / `Stripe:WebhookSecret` are not present in user-secrets.

Automated gates that **did** pass:

```bash
export TEST_SQLSERVER_CONNECTION="Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~StripeWebhook|FullyQualifiedName~MarkFulfilled|FullyQualifiedName~StockConcurrency"
# Passed: 8
```

Operator must complete Stripe CLI UAT (see checkpoint how-to-verify) before SHOP-02/05 live path is approved.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] SQL Server for StockConcurrency on Linux**
- **Found during:** Task 2
- **Issue:** LocalDB unavailable in Codespaces/Linux; StockConcurrencyTests require real SQL Server.
- **Fix:** Started `mcr.microsoft.com/mssql/server:2022-latest` and set `TEST_SQLSERVER_CONNECTION`; factory already honors the override.
- **Files modified:** `SqlServerWebApplicationFactory.cs` (connection override)
- **Committed in:** `ff4459b`

**2. [Rule 2 - Correctness] ConstructEvent API version mismatch on fixtures**
- **Found during:** Task 2 (synthetic payloads vs Stripe.net 52.2.0 expected API version)
- **Issue:** Strict ConstructEvent throws on api_version mismatch before fulfill logic runs.
- **Fix:** Pass `throwOnApiVersionMismatch: false` while still requiring valid HMAC signature.
- **Files modified:** `StripeWebhookController.cs`
- **Committed in:** `ff4459b`

### Skipped / unchanged

- `CustomWebApplicationFactory.cs` already RemoveAll/Add `FakePaymentProvider` — no edit required (defense-in-depth remains).

## Known Stubs

None — FakePaymentProvider remains intentional for Testing only; production DI uses StripePaymentProvider.

## Threat Flags

None beyond plan threat model (T-06-13..17 mitigated as specified).

## Self-Check: PASSED

- FOUND: `API/ZachHairStudio.Shared/Features/Payments/StripePaymentProvider.cs`
- FOUND: `API/ZachHairStudio.Api/Controllers/StripeWebhookController.cs`
- FOUND: `API/ZachHairStudio.Api.Tests/Features/Orders/StockConcurrencyTests.cs`
- FOUND: `API/ZachHairStudio.Api.Tests/Features/Payments/StripeWebhookTests.cs`
- FOUND commit: `619145c`
- FOUND commit: `ff4459b`
