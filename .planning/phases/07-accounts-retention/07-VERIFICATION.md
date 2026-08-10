---
phase: 07-accounts-retention
verified: 2026-08-10T10:37:06Z
status: gaps_found
score: 3/5 must-haves verified
behavior_unverified: 1
overrides_applied: 0
gaps:
  - truth: "Client can cancel or reschedule their own upcoming appointment from their account (self-service)"
    status: failed
    reason: "Self-service and account booking lists require Appointment.ClientUserId. Public POST /api/appointments CreateAsync always books with clientUserId:null; landing book does not send Client JWT. Claim-by-email UI exists only on /account/register after signup — not on the account shell. A client who registers first then books never owns that appointment, so it never appears under /account/bookings and cannot be cancelled, rescheduled, or earn loyalty."
    artifacts:
      - path: "API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs"
        issue: "CreateAsync calls TryBookNewAsync(request, clientUserId: null) — logged-in clients never get ownership on new bookings"
      - path: "API/ZachHairStudio.Api/Controllers/AppointmentsController.cs"
        issue: "CreateAppointment does not resolve ClaimTypes.NameIdentifier / Client role into CreateAsync"
      - path: "landing-page/lib/appointments.ts"
        issue: "Public book path has no Bearer / ownership attach (contrast OrdersController TryGetClientUserId)"
      - path: "landing-page/app/account/register/page.tsx"
        issue: "ClaimHistoryPanel only after register — no re-claim entry on /account/bookings"
    missing:
      - "Pass authenticated Client NameIdentifier into AppointmentsService.CreateAsync / TryBookNewAsync when Bearer Client JWT present (mirror OrdersController)"
      - "Or expose claim-confirm on the account shell so post-register guest-email bookings can be attached later"
      - "Regression test: Client JWT books via POST /api/appointments → ClientUserId set → appears in GET /api/account/bookings → cancel works"
behavior_unverified_items:
  - truth: "A client earns a loyalty point for each completed appointment, visible in their account and redeemable as a discount"
    test: "Staff marks an owned Confirmed appointment Completed; reload /account and confirm strip +1; on /checkout while logged in Apply Points (10) and confirm Subtotal/Discount/Total from server; complete checkout and confirm negative Redeem ledger row."
    expected: "Exactly one Earn row per AppointmentId; balance SUM(Delta); redeem dollars = floor(points/10)*5 capped at merchandise subtotal; guest checkout has no redeem UI."
    why_human: "Earn-on-Completed, idempotency, and checkout redeem are state transitions. Integration tests exist (LoyaltyTests) but could not run here (LocalDB unsupported on this Linux host). Presence/wiring alone cannot prove runtime ledger correctness."
---

# Phase 7: Accounts & Retention Verification Report

**Phase Goal:** As a client, I want to create an account to see my booking and order history and manage my upcoming appointments myself, so that I do not have to call the salon for things I can handle on my own.

**Verified:** 2026-08-10T10:37:06Z  
**Status:** gaps_found  
**Re-verification:** No — initial verification  
**Mode:** mvp

## User Flow Coverage

User story: «As a client, I want to create an account to see my booking and order history and manage my upcoming appointments myself, so that I do not have to call the salon for things I can handle on my own.»

| Step | Expected | Evidence | Status |
|------|----------|----------|--------|
| Create account | Register at `/account/register` with email+password → Client JWT in `zhs.client.auth` | `AuthController.Register` + `StaffRoles.Client`; `landing-page/lib/auth.ts` STORAGE_KEY; register page | ✓ |
| Log in | `/account/login` → session → Navbar **Account** | Login uses `/api/auth/login`; Navbar `getSession()` toggles Account vs Log In | ✓ |
| See history | Bookings \| Orders tabs with owned rows (date-desc) | `AccountController` + `AccountService` filters; `/account/bookings`, `/account/orders` + `AccountShell` | ⚠️ Claim path only — see Gaps |
| Manage appointments | Cancel / reschedule upcoming Confirmed from account | `CancelForClientAsync` / `RescheduleForClientAsync` + `AccountBookingActions` | ✗ Failed for post-login bookings (no `ClientUserId`) |
| Outcome | Handle own history/schedule without calling salon | Claim-then-manage works for prior guest rows; register-then-book does not | ✗ Gap blocks full outcome |

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
| --- | ------- | ---------- | -------------- |
| 1 | Client can create an account, log in, and view their booking and order history from an account page | ✓ VERIFIED | Register/login mint Client JWT; history APIs + Bookings\|Orders UI wired; claim-by-email attaches guest rows. **Caveat:** new public bookings after login are not owned (see gap). |
| 2 | Client can cancel or reschedule their own upcoming appointment from their account (self-service) | ✗ FAILED | APIs/UI exist and ownership-gate correctly, but public create never sets `ClientUserId`, so the primary register→book→manage path cannot self-serve. |
| 3 | A client can only ever fetch their own bookings/orders — cross-client ID access rejected (no IDOR) | ✓ VERIFIED | `AccountService` filters `ClientUserId`/`ClientId == userId`; cross-client → NotFound; controller scopes from `ClaimTypes.NameIdentifier` only; Staff → 403 via `[Authorize(Roles = Client)]`. IDOR tests enumerated. |
| 4 | Staff and client accounts share a single ASP.NET Core Identity schema/migration | ✓ VERIFIED | One `AddIdentity` on `BookingDbContext`; `IdentitySeeder` seeds Owner/Staff/**Client**; no Auth.js/Better Auth; Client users not seeded. |
| 5 | Client earns a loyalty point per completed appointment, visible and redeemable as discount | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Earn hook on `UpdateStatusAsync` Completed + `LoyaltyService` + filtered unique Earn index + checkout redeem after catalog recompute + `LoyaltyBalanceStrip`/`CheckoutForm` wired. Runtime not exercised here (LocalDB). Also depends on ownership for earn. |

**Score:** 3/5 truths verified (1 present, behavior-unverified; 1 failed)

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | ----------- | ------ | ------- |
| `StaffRoles.cs` + `IdentitySeeder` | Client role on shared Identity | ✓ VERIFIED | `StaffRoles.Client`; seeder creates role, no Client users |
| `AuthController` Register | Client JWT register | ✓ VERIFIED | CreateAsync → AddToRoleAsync(Client) → JWT |
| `AccountController` + `AccountService` | Ownership history + claim + cancel/reschedule + loyalty | ✓ VERIFIED | Substantive; Client-role authorize |
| `AppointmentsService` cancel/reschedule | Client self-service + txn reschedule | ✓ VERIFIED | Book-new then cancel-old in execution strategy |
| `LoyaltyLedger` + `LoyaltyService` + migration | Earn/redeem ledger | ✓ VERIFIED | Filtered unique Earn `AppointmentId` index |
| `OrdersService` / `OrdersController` | Server redeem + ClientId from JWT | ✓ VERIFIED | `RedeemPoints` only; dollars server-side |
| Landing `/account/*` + Navbar + actions | Auth + history + cancel/reschedule + loyalty UI | ✓ VERIFIED | Shell strip, bookings actions, checkout Apply Points |
| Phase 7 integration tests | Contract coverage | ✓ EXISTS | Listed; **could not execute** (LocalDB unsupported on Linux) |

### Key Link Verification

| From | To | Via | Status | Details |
| ---- | -- | --- | ------ | ------- |
| Register | Client role JWT | `AddToRoleAsync(StaffRoles.Client)` + `JwtTokenService` | ✓ WIRED | |
| Landing auth | `zhs.client.auth` | `STORAGE_KEY` + Bearer on `accountFetch` | ✓ WIRED | Distinct from dashboard staff key |
| Account lists | Ownership | `NameIdentifier` → `ClientUserId`/`ClientId` filter | ✓ WIRED | |
| Cancel/reschedule UI | Account API | `cancelBooking` / `rescheduleBooking` Bearer | ✓ WIRED | Only for rows already owned |
| Completed status | Loyalty earn | `UpdateStatusAsync` → `EarnForCompletedAsync` when `ClientUserId` set | ✓ WIRED | |
| Checkout redeem | Ledger | `QuoteRedeemAsync` / `AppendRedeem` inside order txn | ✓ WIRED | |
| **Public book** | **ClientUserId** | Create path | ✗ NOT_WIRED | `CreateAsync(..., null)` — **gap root cause** |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| -------- | ------------- | ------ | ------------------ | ------ |
| Bookings page | `bookings` | `GET /api/account/bookings` → EF `ClientUserId == userId` | Yes when FK set | ✓ FLOWING / ⚠️ empty if never claimed |
| Orders page | `orders` | `GET /api/account/orders` → `ClientId == userId` | Yes; checkout sets `ClientId` when Client JWT present | ✓ FLOWING |
| Loyalty strip | `balance` | `GET /api/account/loyalty` → `SUM(Delta)` | Yes from ledger | ✓ FLOWING |
| Checkout redeem | `quote.loyaltyDiscount` | `POST /api/orders/checkout/quote` server dollars | Yes; client sends points only | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Enumerate Phase 7 tests | `dotnet test --list-tests` filter ClientAuth/Account*/Loyalty/ClientReschedule | 40+ named tests present | ✓ PASS |
| Run IDOR / reschedule / loyalty tests | `dotnet test --filter …` | All failed: `LocalDB is not supported on this platform` | ? SKIP |
| Artifact existence | `test -f` on plan artifacts | All present, non-stub line counts | ✓ PASS |

### Probe Execution

| Probe | Command | Result | Status |
| ----- | ------- | ------ | ------ |
| — | — | No phase-declared or conventional `scripts/*/tests/probe-*.sh` | SKIP |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| ----------- | ---------- | ----------- | ------ | -------- |
| ACCT-01 | 07-01 | Create account + log in | ✓ SATISFIED | Register/login + landing auth |
| ACCT-02 | 07-02 | Booking history | ⚠️ PARTIAL | Works after claim; not for post-login public bookings |
| ACCT-03 | 07-02 | Order history | ✓ SATISFIED | Orders attach `ClientId` on authenticated checkout |
| ACCT-04 | 07-03 | Cancel/reschedule self-service | ✗ BLOCKED | Blocked by missing ownership on public create |
| ACCT-05 | 07-01 | Single Identity schema | ✓ SATISFIED | Client role on same AspNet* tables |
| ACCT-06 | 07-02/03 | IDOR prevention | ✓ SATISFIED | NameIdentifier-only scope + 404 |
| ACCT-07 | 07-04 | Loyalty earn + redeem | ⚠️ PARTIAL | Earn/redeem implemented; earn needs ownership; runtime unproven here |

No orphaned Phase 7 requirements outside plans.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| ---- | ---- | ------- | -------- | ------ |
| — | — | No TBD/FIXME/XXX in Phase 7 account/loyalty/landing account files | — | — |
| `AppointmentsService.CreateAsync` | ~72 | Always `clientUserId: null` | 🛑 Blocker | Breaks account ownership for logged-in bookings |

### Human Verification Required

*(Status is `gaps_found` — close the ownership gap first, then run these UAT steps harvested from plan `<human-check>` blocks.)*

1. **Register / Navbar session** — Open `/account/register`, create account, confirm Navbar shows Account; log out; confirm Log In.
2. **Claim + history tabs** — Guest-book with email, register same email, confirm claim, see Bookings|Orders.
3. **Cancel / reschedule** — On owned upcoming Confirmed: cancel confirm frees slots; reschedule chips → Confirm New Time; forced 409 shows recovery without cancelling original.
4. **Loyalty** — Staff Complete owned appt → strip +1; logged-in checkout Apply Points updates totals from server; logged-out checkout omits redeem.

### Gaps Summary

**Root cause:** Appointment ownership is only established via claim-at-register (or reschedule book-new). `CreateAsync` hard-codes `clientUserId: null` and the public appointments controller does not pass a Client JWT id — unlike orders checkout. Therefore register→book→manage (the natural account path) leaves appointments invisible to `/api/account/*`, blocks ACCT-04 self-service, and prevents ACCT-07 earn on those visits.

**What works:** Shared Identity + Client role; register/login; IDOR-safe history APIs; claim-by-email for prior guest rows; cancel/reschedule/loyalty **when `ClientUserId` is set**; authenticated order history + server-side loyalty redeem UI.

**Not deferred:** Phase 8 success criteria do not cover attaching `ClientUserId` on create.

---

_Verified: 2026-08-10T10:37:06Z_  
_Verifier: Claude (gsd-verifier)_
