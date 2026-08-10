---
phase: 07-accounts-retention
plan: 04
subsystem: api
tags: [loyalty, ledger, checkout, redeem, appointments, jwt, landing-page, ef-migrations]

requires:
  - phase: 07-accounts-retention
    provides: Appointment.ClientUserId ownership, AccountController Client surface, landing /account shell
provides:
  - LoyaltyLedger append-only earn/redeem with filtered unique Earn AppointmentId
  - Completed → +1 point (idempotent); checkout RedeemPoints → server $ after catalog recompute
  - GET /api/account/loyalty + POST /api/orders/checkout/quote
  - LoyaltyBalanceStrip + CheckoutForm Apply Points UI
affects: [phase-08 polish, RETN-02 tiers deferred]

tech-stack:
  added: []
  patterns:
    - LoyaltyRates constants (1 pt / Completed; 10 pts = $5); never trust client discount dollars
    - Redeem inside OrdersService CreateExecutionStrategy transaction after catalog TotalAmount
    - Quote recomputes catalog + LoyaltyService; Continue to Payment recomputes again

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Loyalty/LoyaltyLedger.cs
    - API/ZachHairStudio.Shared/Features/Loyalty/LoyaltyReasons.cs
    - API/ZachHairStudio.Shared/Features/Loyalty/LoyaltyRates.cs
    - API/ZachHairStudio.Shared/Features/Loyalty/LoyaltyService.cs
    - API/ZachHairStudio.Shared/Features/Loyalty/LoyaltyBalanceDto.cs
    - API/ZachHairStudio.Shared/Features/Loyalty/LoyaltyQuoteDto.cs
    - API/ZachHairStudio.Shared/Migrations/20260810103054_AddLoyaltyLedger.cs
    - API/ZachHairStudio.Api.Tests/Features/Loyalty/LoyaltyTests.cs
    - landing-page/components/LoyaltyBalanceStrip.tsx
  modified:
    - API/ZachHairStudio.Shared/Db/BookingDbContext.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs
    - API/ZachHairStudio.Shared/Features/Orders/OrdersService.cs
    - API/ZachHairStudio.Shared/Features/Orders/CheckoutRequestDto.cs
    - API/ZachHairStudio.Shared/Features/Orders/CheckoutRequestDtoValidator.cs
    - API/ZachHairStudio.Shared/Features/Orders/CheckoutResponseDto.cs
    - API/ZachHairStudio.Api/Controllers/OrdersController.cs
    - API/ZachHairStudio.Api/Controllers/AccountController.cs
    - API/ZachHairStudio.Api/Program.cs
    - landing-page/lib/account.ts
    - landing-page/lib/cart.ts
    - landing-page/components/CheckoutForm.tsx
    - landing-page/components/AccountShell.tsx
    - landing-page/app/account/page.tsx

key-decisions:
  - "LoyaltyReasons stored as Earn/Redeem strings; filtered unique index on AppointmentId where Reason=Earn"
  - "Payment-failure compensation appends positive Redeem delta (append-only; no ledger deletes)"
  - "Optional Client JWT on checkout/quote via NameIdentifier; RedeemPoints-only money authority"

patterns-established:
  - "Earn hook after UpdateStatusAsync SaveChanges when Completed + ClientUserId"
  - "CheckoutForm sends RedeemPoints only; totals from server quote/checkout DTO fields"

requirements-completed: [ACCT-07]

coverage:
  - id: D1
    description: Staff Complete on owned appointment earns +1 idempotent per AppointmentId
    requirement: ACCT-07
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Loyalty/LoyaltyTests.cs#Complete_OwnedAppointment_EarnsOnePoint_IdempotentPerAppointmentId
        status: pass
    human_judgment: false
  - id: D2
    description: Complete with null ClientUserId does not earn
    requirement: ACCT-07
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Loyalty/LoyaltyTests.cs#Complete_NullClientUserId_DoesNotEarn
        status: pass
    human_judgment: false
  - id: D3
    description: Checkout RedeemPoints=10 applies server $5 after catalog recompute; forged $ ignored
    requirement: ACCT-07
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Loyalty/LoyaltyTests.cs#Checkout_RedeemPoints10_AppliesServerFiveDollarDiscount
        status: pass
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Loyalty/LoyaltyTests.cs#Checkout_ClientSuppliedDollarOff_DoesNotChangeTotal_OnlyRedeemPoints
        status: pass
    human_judgment: false
  - id: D4
    description: GET /api/account/loyalty Client-only; invalid redeem → 400 without spending points
    requirement: ACCT-07
    verification:
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Loyalty/LoyaltyTests.cs#GetLoyalty_ClientJwt_ReturnsBalanceMatchingSumDelta
        status: pass
      - kind: integration
        ref: API/ZachHairStudio.Api.Tests/Features/Loyalty/LoyaltyTests.cs#Checkout_RedeemPointsExceedingBalanceOrNotMultipleOf10_Returns400
        status: pass
    human_judgment: false
  - id: D5
    description: Account loyalty strip + logged-in checkout Apply Points; guest omits redeem
    requirement: ACCT-07
    verification: []
    human_judgment: true
    rationale: Visual confirmation of strip copy, Apply Points totals, and guest omission needs a browser

duration: 8min
completed: 2026-08-10
status: complete
---

# Phase 7 Plan 04: Loyalty Earn & Redeem Summary

**LoyaltyLedger earns +1 on staff Completed (idempotent per AppointmentId) and redeems at checkout as server-computed dollars (10 pts = $5) after catalog recompute — client discount $ never trusted.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-08-10T10:26:47Z
- **Completed:** 2026-08-10T10:34:13Z
- **Tasks:** 3
- **Files modified:** 28

## Accomplishments

- Append-only `LoyaltyLedger` + filtered unique Earn index; `LoyaltyService` balance/earn/quote/redeem
- Staff Completed hook and checkout/quote redeem with `RedeemPoints` only (D-13–D-16)
- Account `LoyaltyBalanceStrip` + CheckoutForm Apply Points from server quote responses
- Nine green `LoyaltyTests` on SqlServerWebApplicationFactory / Docker SQL

## Task Commits

1. **Task 1: RED — LoyaltyTests** - `d7bd73d` (test)
2. **Task 2: GREEN — Ledger, earn, redeem** - `065787b` (feat)
3. **Task 3: Account strip + checkout redeem UI** - `a1441b2` (feat)

**Plan metadata:** (pending docs commit)

## Files Created/Modified

- `API/ZachHairStudio.Shared/Features/Loyalty/*` — entity, rates, service, DTOs
- `API/ZachHairStudio.Shared/Migrations/20260810103054_AddLoyaltyLedger.cs` — schema
- `AppointmentsService` / `OrdersService` / controllers — earn + redeem hooks
- `landing-page` — LoyaltyBalanceStrip, CheckoutForm redeem, cart/account helpers

## Decisions Made

- Earn/Redeem reason strings + SQL filtered unique on Earn AppointmentId (Pitfall 3)
- Payment failure restores points via positive compensating Redeem row (append-only)
- Optional Client Bearer on checkout/quote; Staff JWT cannot redeem

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Payment-failure loyalty compensation**
- **Found during:** Task 2 (OrdersService redeem inside stock txn before Stripe)
- **Issue:** If payment session creation fails after a Redeem ledger row was committed, points would stay spent while stock is restored
- **Fix:** `CompensateFailedPaymentAsync` appends a positive compensating Redeem delta for the same OrderId
- **Files modified:** `API/ZachHairStudio.Shared/Features/Orders/OrdersService.cs`
- **Committed in:** `065787b`

**Total deviations:** 1 auto-fixed (Rule 2)
**Impact on plan:** Correctness for failed payment path; no scope creep.

## Issues Encountered

None blocking. Docker SQL via `ConnectionStrings__DefaultConnection` used for LoyaltyTests on Linux.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Phase 07 plans complete for ACCT-01–07 MVP slice. Ready for phase verification / milestone close. RETN-02 tiers remain deferred.

## Self-Check: PASSED

- FOUND: LoyaltyLedger.cs, LoyaltyService.cs, LoyaltyRates.cs, LoyaltyTests.cs, LoyaltyBalanceStrip.tsx, CheckoutForm.tsx, AddLoyaltyLedger migration
- FOUND commits: d7bd73d, 065787b, a1441b2

---
*Phase: 07-accounts-retention*
*Completed: 2026-08-10*
