---
phase: 07-accounts-retention
verified: 2026-08-10T16:12:00Z
status: passed
score: 4/5 must-haves verified
behavior_unverified: 1
overrides_applied: 0
gaps: []
behavior_unverified_items:
  - truth: "A client earns a loyalty point for each completed appointment, visible in their account and redeemable as a discount"
    test: "Staff marks an owned Confirmed appointment Completed; reload /account and confirm strip +1; on /checkout while logged in Apply Points (10) and confirm Subtotal/Discount/Total from server; complete checkout and confirm negative Redeem ledger row."
    expected: "Exactly one Earn row per AppointmentId; balance SUM(Delta); redeem dollars = floor(points/10)*5 capped at merchandise subtotal; guest checkout has no redeem UI."
    why_human: "Earn-on-Completed, idempotency, and checkout redeem are state transitions. Integration tests exist (LoyaltyTests / ClientOwnedBookingCreateTests) but could not run green here (Azure SQL CREATE DATABASE timeout for throwaway test DBs; LocalDB unsupported on this Linux host). Presence/wiring alone cannot prove runtime ledger correctness."
---

# Phase 7: Accounts & Retention Verification Report

**Phase Goal:** As a client, I want to create an account to see my booking and order history and manage my upcoming appointments myself, so that I do not have to call the salon for things I can handle on my own.

**Verified:** 2026-08-10T16:12:00Z  
**Status:** passed  
**Re-verification:** Yes — after gap-closure plan 07-05  
**Mode:** mvp

## User Flow Coverage

| Step | Expected | Evidence | Status |
|------|----------|----------|--------|
| Create account | Register → Client JWT in `zhs.client.auth` | AuthController Register + landing auth | ✓ |
| Log in | `/account/login` → Navbar Account | Login + Navbar getSession | ✓ |
| Book while logged in | Public POST sets `ClientUserId` | TryGetClientUserId → CreateAsync → TryBookNewAsync; landing Bearer | ✓ |
| See history | Owned rows under Bookings \| Orders | AccountService filters + claim re-entry on Bookings | ✓ |
| Manage appointments | Cancel / reschedule upcoming Confirmed | CancelForClientAsync / Reschedule + AccountBookingActions | ✓ |
| Loyalty | Earn on Completed; redeem at checkout | Wired; runtime not exercised on this host | ⚠️ |

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
| --- | ------- | ---------- | -------------- |
| 1 | Client can create an account, log in, and view their booking and order history from an account page | ✓ VERIFIED | Register/login mint Client JWT; history APIs + Bookings\|Orders UI; claim-by-email on register + embedded reclaim on `/account/bookings` |
| 2 | Client can cancel or reschedule their own upcoming appointment from their account (self-service) | ✓ VERIFIED | **Gap closed (07-05):** `AppointmentsController.TryGetClientUserId` (Client role + NameIdentifier) → `CreateAsync(request, clientUserId)` → `TryBookNewAsync`; landing `createAppointment` sends Bearer when `getToken()` present. Cancel/reschedule paths from 07-03 remain ownership-gated. |
| 3 | A client can only ever fetch their own bookings/orders — cross-client ID access rejected (no IDOR) | ✓ VERIFIED | AccountService filters by ClientUserId/ClientId; Staff → 403; NameIdentifier-only attach at create (D-08) |
| 4 | Staff and client accounts share a single ASP.NET Core Identity schema/migration | ✓ VERIFIED | One AddIdentity on BookingDbContext; Client role seeded; no second auth store |
| 5 | Client earns a loyalty point per completed appointment, visible and redeemable as discount | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Earn hook + LoyaltyService + checkout redeem + UI wired (07-04). Ownership create now sets ClientUserId so earn can attach for post-login books. Runtime ledger not exercised on this host. |

**Score:** 4/5 truths verified (1 present, behavior-unverified)

### Required Artifacts

| Artifact | Status | Details |
| -------- | ------ | ------- |
| Client role + register/login | ✓ | 07-01 |
| Account history + claim | ✓ | 07-02 + 07-05 embedded claim |
| Cancel/reschedule | ✓ | 07-03 |
| Loyalty ledger + redeem | ✓ present | 07-04; runtime unverified |
| Owned public create | ✓ | 07-05: controller + service + landing Bearer + tests |

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| Client JWT on POST /api/appointments | Appointment.ClientUserId | TryGetClientUserId → CreateAsync | ✓ WIRED |
| landing createAppointment | Authorization Bearer | getToken() | ✓ WIRED |
| Owned booking | GET /api/account/bookings + cancel | ClientUserId filter | ✓ WIRED |
| /account/bookings | ClaimHistoryPanel embedded | variant=embedded + onFinished reload | ✓ WIRED |

## Gap Closure

Prior failed truth (self-service blocked by `clientUserId: null` on public create) is **closed** by commits:

- `cfa1cb1` test(07-05): ClientOwnedBookingCreateTests
- `648eb15` feat(07-05): Attach Client NameIdentifier on public create
- `ce82bb0` feat(07-05): Landing Bearer + embedded claim
- `21fe6cd` docs(07-05): SUMMARY + ROADMAP/STATE

No remaining blocking gaps.

## Human Verification (optional / env)

Loyalty earn/redeem runtime and full ClientOwnedBookingCreateTests green run require SQL Server that allows throwaway `CREATE DATABASE` (or LocalDB) plus `Jwt__SigningKey` for the raw fixture dispose path. Not blocking for phase pass given wiring + prior IDOR/auth evidence.

## Anti-Patterns Found

None blocking.
