---
phase: 06-cart-checkout
plan: 03
subsystem: api
tags: [orders, checkout, payments, ef-core, execute-update, shop-02, shop-03, shop-04, shop-06]

requires:
  - phase: 06-cart-checkout Plan 01
    provides: Products catalog + message-only Result.ConflictError + X-Cart-Session-Id cart session
provides:
  - Guest checkout POST /api/orders/checkout with FakePaymentProvider checkoutUrl
  - Server-authoritative Order/OrderItem snapshots and totals from Products.Price
  - Atomic stock decrement via ExecuteUpdateAsync in CreateExecutionStrategy + transaction
  - Thin idempotent MarkFulfilledAsync (Pending→Fulfilled) ready for Plan 05 webhook
  - AddOrders migration with filtered unique StripeSessionId index
affects: [06-cart-checkout Plan 04 checkout UI redirect, Plan 05 Stripe + webhook + stock concurrency]

tech-stack:
  added: [Microsoft.EntityFrameworkCore.Sqlite 10.0.9 (test-only, ExecuteUpdateAsync proofs)]
  patterns:
    - "IPaymentProvider seam with FakePaymentProvider (StripePaymentProvider deferred to Plan 05)"
    - "CreateCheckoutAsync: strategy + BeginTransactionAsync + ExecuteUpdateAsync stock; compensate on provider failure"
    - "Required X-Cart-Session-Id header; optional body SessionKey must match when present"

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Orders/Order.cs
    - API/ZachHairStudio.Shared/Features/Orders/OrderItem.cs
    - API/ZachHairStudio.Shared/Features/Orders/OrdersService.cs
    - API/ZachHairStudio.Shared/Features/Payments/IPaymentProvider.cs
    - API/ZachHairStudio.Shared/Features/Payments/FakePaymentProvider.cs
    - API/ZachHairStudio.Api/Controllers/OrdersController.cs
    - API/ZachHairStudio.Shared/Migrations/20260810063022_AddOrders.cs
    - API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs
    - API/ZachHairStudio.Api.Tests/SqliteWebApplicationFactory.cs
  modified:
    - API/ZachHairStudio.Shared/Db/BookingDbContext.cs
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Api.Tests/CustomWebApplicationFactory.cs

key-decisions:
  - "Checkout money recomputed only from Products.Price; CheckoutRequestDto has no price/total fields (D-05)"
  - "Order.ClientId nullable for guest checkout (D-06); Status starts Pending and is never Fulfilled from checkout POST"
  - "SqliteWebApplicationFactory for checkout integration tests because ExecuteUpdateAsync is relational-only"

patterns-established:
  - "OrdersController injects OrdersService only (PLAT-01) — no BookingDbContext"
  - "Payment create after order commit; provider exception → stock restore + Status=Failed"
  - "MarkFulfilledAsync is real thin idempotent flip; Plan 05 only wires the webhook caller"

requirements-completed: [SHOP-02, SHOP-03, SHOP-04, SHOP-06]

coverage:
  - id: D1
    description: Anonymous POST /api/orders/checkout with X-Cart-Session-Id creates Pending guest order and returns FakePaymentProvider checkoutUrl
    requirement: SHOP-02
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Orders/OrdersControllerTests.cs#Checkout_AnonymousWithSessionHeader_ReturnsCheckoutUrl
        status: pass
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs#GuestCheckout_CreateCheckoutAsync_SetsClientIdNullAndStatusPending
        status: pass
    human_judgment: false
  - id: D2
    description: Order totals and OrderItem UnitPrice/LineTotal come from catalog Price (DTO has no money fields)
    requirement: SHOP-03
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs#PriceAuthority_CreateCheckoutAsync_UsesCatalogPriceIgnoringClientMoneyAbsence
        status: pass
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs#CheckoutRequestDto_HasNoPriceOrTotalProperties
        status: pass
    human_judgment: false
  - id: D3
    description: Insufficient stock returns Conflict and leaves Stock unchanged (atomic UPDATE path; full concurrency proof in Plan 05)
    requirement: SHOP-04
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs#CreateCheckoutAsync_InsufficientStock_IsConflictAndStockUnchanged
        status: pass
    human_judgment: false
  - id: D4
    description: Guest Order.ClientId is null on successful checkout
    requirement: SHOP-06
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs#GuestCheckout_CreateCheckoutAsync_SetsClientIdNullAndStatusPending
        status: pass
    human_judgment: false
  - id: D5
    description: MarkFulfilledAsync is thin idempotent Pending→Fulfilled (not a stub); already Fulfilled is success no-op
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs#MarkFulfilledAsync_PendingToFulfilled_IsIdempotent
        status: pass
    human_judgment: false
  - id: D6
    description: Payment provider failure after commit restores stock and marks Order Failed
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs#CreateCheckoutAsync_PaymentProviderFailure_RestoresStockAndMarksFailed
        status: pass
    human_judgment: false

duration: 5min
completed: 2026-08-10
status: complete
---

# Phase 6 Plan 03: Checkout API + Fake Payment Summary

**Guest checkout creates Pending null-ClientId orders with server-recomputed totals, atomic stock decrement, and FakePaymentProvider checkoutUrl (SHOP-02/03/04/06).**

## Performance

- **Duration:** 5 min
- **Started:** 2026-08-10T06:25:50Z
- **Completed:** 2026-08-10T06:31:13Z
- **Tasks:** 2
- **Files modified:** 25

## Accomplishments

- Orders + Payments seam: Order/OrderItem snapshots, price-less CheckoutRequestDto, IPaymentProvider + FakePaymentProvider
- CreateCheckoutAsync wraps ExecuteUpdateAsync stock decrement in CreateExecutionStrategy + transaction; compensates stock and sets Failed on provider errors
- Thin idempotent MarkFulfilledAsync shipped for Plan 05 webhook wire-up only (never fulfilled from checkout POST)
- AddOrders migration with filtered unique index on StripeSessionId; AppointmentSlot unique index remains unfiltered
- 12 Orders/PriceAuthority/GuestCheckout/Checkout tests green on Sqlite

## Task Commits

1. **Task 1: Order domain + IPaymentProvider + RED checkout tests** - `033d453` (test)
2. **Task 2: GREEN — OrdersService CreateCheckoutAsync + OrdersController + AddOrders migration** - `9d22d04` (feat)

**Plan metadata:** `653e848` (docs: complete plan)

## Files Created/Modified

- `API/ZachHairStudio.Shared/Features/Orders/*` — entities, DTOs, validator, extensions, OrdersService
- `API/ZachHairStudio.Shared/Features/Payments/*` — IPaymentProvider, FakePaymentProvider, StripeOptions
- `API/ZachHairStudio.Api/Controllers/OrdersController.cs` — POST checkout + GET by id; required X-Cart-Session-Id
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` — Orders/OrderItems + money precision + filtered StripeSessionId index
- `API/ZachHairStudio.Shared/Migrations/20260810063022_AddOrders.cs` — schema migration
- `API/ZachHairStudio.Api/Program.cs` — StripeOptions + FakePaymentProvider + OrdersService DI
- `API/ZachHairStudio.Api.Tests/SqliteWebApplicationFactory.cs` — relational host for ExecuteUpdateAsync checkout tests
- `API/ZachHairStudio.Api.Tests/Features/Orders/*` — service + controller coverage

## Decisions Made

- Kept FakePaymentProvider as the sole IPaymentProvider registration until Plan 05 swaps StripePaymentProvider
- Used Sqlite (not InMemory) for Orders checkout tests so ExecuteUpdateAsync runs; default CustomWebApplicationFactory stays InMemory for unrelated suites
- Optional CheckoutRequestDto.SessionKey is a body mirror of X-Cart-Session-Id; mismatch → 400 ValidationProblem

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Sqlite test host for ExecuteUpdateAsync**
- **Found during:** Task 2 (GREEN)
- **Issue:** InMemory cannot run ExecuteUpdateAsync; controller checkout would 500 on CustomWebApplicationFactory
- **Fix:** Added Microsoft.EntityFrameworkCore.Sqlite 10.0.9 to the test project, SqliteWebApplicationFactory, and Sqlite harness in OrdersServiceTests (explicitly allowed by plan action)
- **Files modified:** `ZachHairStudio.Api.Tests.csproj`, `SqliteWebApplicationFactory.cs`, Orders tests
- **Verification:** Orders filter — 12 passed
- **Committed in:** `9d22d04`

**2. [Rule 1 - Bug] Seeded catalog Id collision in Sqlite EnsureCreated**
- **Found during:** Task 2 (GREEN)
- **Issue:** EnsureCreated seeds Products 1–7; inserting Product Id=1 in tests conflicts
- **Fix:** Mutate seeded product 1 price/stock/name via SetCatalogAsync before checkout asserts
- **Files modified:** `OrdersServiceTests.cs`
- **Verification:** PriceAuthority + GuestCheckout tests pass
- **Committed in:** `9d22d04`

**3. [Rule 3 - Blocking] LocalDB migrate apply unavailable on Linux codespace**
- **Found during:** Task 2 (GREEN)
- **Issue:** `dotnet ef database update` fails — LocalDB not supported on this platform
- **Fix:** Migration artifacts committed; runtime `Migrate()` applies when SQL Server is available (same environment constraint as prior phases)
- **Files modified:** none beyond migration files
- **Verification:** migration file contains Orders/OrderItems + filtered StripeSessionId index
- **Committed in:** `9d22d04`

## TDD Gate Compliance

- RED: `033d453` test(06-03): Order domain + IPaymentProvider + RED tests
- GREEN: `9d22d04` feat(06-03): checkout API implementation

## Known Stubs

None — MarkFulfilledAsync is a real Pending→Fulfilled flip with Fulfilled no-op; FakePaymentProvider is an intentional Plan 05 stand-in (threat T-06-09 accept), not a NotImplemented stub.

## Self-Check: PASSED

- FOUND: OrdersService.cs, IPaymentProvider.cs, OrdersController.cs, AddOrders migration, OrdersServiceTests.cs
- FOUND commits: `033d453`, `9d22d04`
