---
phase: 06-cart-checkout
plan: 01
subsystem: api
tags: [cart, checkout, ef-core, fluentvalidation, session, shop-01]

requires:
  - phase: 05-product-catalog
    provides: Products catalog with Price/Stock and ProductsService pattern
provides:
  - Session-keyed guest Cart/CartItem API under X-Cart-Session-Id
  - Server-enriched cart responses (UnitPrice/LineTotal from Products.Price)
  - Message-only Result.ConflictError overload for stock 409 (Plan 03)
  - AddCarts migration with unique Cart.SessionKey
affects: [06-cart-checkout Plan 02 UI, Plan 03 checkout/stock]

tech-stack:
  added: []
  patterns:
    - "Price-less cart persistence — ProductId+Quantity only; enrich at read (D-05)"
    - "Guest cart session via X-Cart-Session-Id header (not cookie)"
    - "Message-only Result.ConflictError overload alongside AvailabilityConflictDto overload"

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Carts/Cart.cs
    - API/ZachHairStudio.Shared/Features/Carts/CartItem.cs
    - API/ZachHairStudio.Shared/Features/Carts/CartsService.cs
    - API/ZachHairStudio.Api/Controllers/CartsController.cs
    - API/ZachHairStudio.Shared/Migrations/20260810061814_AddCarts.cs
    - API/ZachHairStudio.Api.Tests/Features/Carts/CartsServiceTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Carts/CartsControllerTests.cs
  modified:
    - API/ZachHairStudio.Shared/Result.cs
    - API/ZachHairStudio.Shared/Db/BookingDbContext.cs
    - API/ZachHairStudio.Api/Program.cs

key-decisions:
  - "Cart upsert sets absolute Quantity (clamped to Products.Stock) keyed by ProductId"
  - "Unknown session GET returns empty items Success — never 404"
  - "X-Cart-Session-Id header (max 64) because AllowAnyOrigin blocks credentialed cookies"

patterns-established:
  - "CartsService owns all Cart/CartItem DbContext access; Controllers inject services only (PLAT-01)"
  - "Write DTOs never carry Price/Total; response DTOs expose server-computed money"

requirements-completed: [SHOP-01]

coverage:
  - id: D1
    description: Anonymous client can upsert/get cart lines under X-Cart-Session-Id with server-enriched UnitPrice/LineTotal from Products.Price
    requirement: SHOP-01
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Carts/CartsServiceTests.cs#UpsertThenGet_EnrichesUnitPriceAndLineTotalFromCatalog
        status: pass
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Carts/CartsControllerTests.cs#UpsertThenGet_ReturnsServerEnrichedLineFromSeededCatalog
        status: pass
    human_judgment: false
  - id: D2
    description: CartItem persists ProductId and Quantity only — no Price/Total columns
    requirement: SHOP-01
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Shared/Features/Carts/CartItem.cs (property surface)
        status: pass
    human_judgment: false
  - id: D3
    description: GET cart for unknown/empty session returns empty items list (not null, not 404)
    requirement: SHOP-01
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Carts/CartsServiceTests.cs#GetCartAsync_UnknownSession_ReturnsEmptyItemsList
        status: pass
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Carts/CartsControllerTests.cs#GetCart_UnknownSession_ReturnsEmptyItems
        status: pass
    human_judgment: false
  - id: D4
    description: CartsController does not depend on BookingDbContext (PLAT-01)
    requirement: SHOP-01
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Carts/CartsControllerTests.cs#CartsController_DoesNotDependOnBookingDbContext
        status: pass
    human_judgment: false
  - id: D5
    description: AddCarts migration creates Carts/CartItems with unique SessionKey; AppointmentSlot unique index remains unfiltered
    requirement: SHOP-01
    verification:
      - kind: other
        ref: API/ZachHairStudio.Shared/Migrations/20260810061814_AddCarts.cs + BookingDbContextModelSnapshot AppointmentSlot HasIndex without HasFilter
        status: pass
    human_judgment: false
  - id: D6
    description: Message-only Result.ConflictError overload compiles and IsConflict() is true
    verification:
      - kind: unit
        ref: API/ZachHairStudio.Api.Tests/Features/Carts/CartsServiceTests.cs#ConflictError_MessageOnly_IsConflictWithNullConflicts
        status: pass
    human_judgment: false

duration: 6min
completed: 2026-08-10
status: complete
---

# Phase 6 Plan 01: Guest Cart API Summary

**Session-keyed guest cart REST API with price-less CartItem rows and server-enriched catalog prices (SHOP-01).**

## Performance

- **Duration:** 6 min
- **Started:** 2026-08-10T06:14:01Z
- **Completed:** 2026-08-10T06:19:52Z
- **Tasks:** 2
- **Files modified:** 17

## Accomplishments

- Delivered `Features/Carts/` vertical: entities, price-less upsert DTO + FluentValidation, enrichment extensions, `CartsService`, thin `CartsController`
- Added message-only `Result.ConflictError(string)` overload (Pitfall 7) for later stock 409 mapping
- Shipped `AddCarts` migration with unique `Cart.SessionKey` and cascade Cart→Items; left AppointmentSlot `(StylistId, SlotStart)` unfiltered
- Green Carts filter: 9/9 tests passing (`dotnet test --filter FullyQualifiedName~Carts`)

## Task Commits

Each task was committed atomically:

1. **Task 1: ConflictError overload + Cart domain + RED Carts tests** - `e7ab451` (test)
2. **Task 2: GREEN — CartsService, CartsController, DbContext, AddCarts migration** - `07bdf21` (feat)

**Plan metadata:** _(pending docs commit)_

## Files Created/Modified

- `API/ZachHairStudio.Shared/Result.cs` — message-only ConflictError overload
- `API/ZachHairStudio.Shared/Features/Carts/*` — Cart domain, DTOs, validator, extensions, CartsService
- `API/ZachHairStudio.Api/Controllers/CartsController.cs` — GET/PUT|POST/DELETE under `api/carts` + `X-Cart-Session-Id`
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` — Carts/CartItems DbSets + config
- `API/ZachHairStudio.Shared/Migrations/20260810061814_AddCarts.*` — schema migration
- `API/ZachHairStudio.Api/Program.cs` — `AddScoped<CartsService>()`
- `API/ZachHairStudio.Api.Tests/Features/Carts/*` — service + controller tests

## Decisions Made

- Upsert sets absolute quantity (clamped to `Products.Stock`); inactive/missing product → NotFound; stock 0 → ValidationError
- Unknown session GET returns Success with empty `Items` (creates no row until first upsert)
- Session via `X-Cart-Session-Id` header (max 64 chars) — RESEARCH Pattern 4 / AllowAnyOrigin constraint

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Jwt:SigningKey missing in local test host**
- **Found during:** Task 2 (CartsControllerTests via CustomWebApplicationFactory)
- **Issue:** Host ValidateOnStart failed without `Jwt:SigningKey`, blocking integration tests
- **Fix:** Set local `dotnet user-secrets` Jwt:SigningKey on Api project (D-13 — not tracked)
- **Files modified:** none in repo (user-secrets store only)
- **Verification:** `dotnet test --filter FullyQualifiedName~Carts` → 9 passed
- **Committed in:** n/a (environment only)

---

**Total deviations:** 1 auto-fixed (Rule 3)
**Impact on plan:** Environment unblock only; no scope change.

## Issues Encountered

Controller tests initially failed with `OptionsValidationException` for Jwt until user-secrets were configured — same requirement already documented in CLAUDE.md for `dotnet test`.

## User Setup Required

None for cart endpoints. Existing Api user-secrets still required for test host boot:
- `Jwt:SigningKey` (32+ chars)
- `RESEND_API_KEY` (when exercising appointment email paths)

## Next Phase Readiness

Plan 02 can attach landing-page cart UI to `GET/PUT/DELETE /api/carts` with `X-Cart-Session-Id`. ConflictError overload is ready for Plan 03 stock 409.

## Self-Check: PASSED

- FOUND: CartsService.cs, CartsController.cs, AddCarts migration, Result.cs, Carts tests
- FOUND: commits e7ab451, 07bdf21
- No stubs (TODO/FIXME/placeholder) in Carts feature files

---
*Phase: 06-cart-checkout*
*Completed: 2026-08-10*
