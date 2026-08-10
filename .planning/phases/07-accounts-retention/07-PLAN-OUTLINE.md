# Phase 7 — Plan Outline (chunked, outline-only)

**Goal:** As a client, I want to create an account to see my booking and order history and manage my upcoming appointments myself, so that I do not have to call the salon for things I can handle on my own.

**Mode:** MVP vertical slices · **No Auth.js** (D-01/D-02/ACCT-05) · Guest checkout stays valid

| Plan ID | Objective | Wave | Depends On | Requirements |
|---------|-----------|------|------------|--------------|
| 07-01 | Client register/login E2E: seed `Client` role on existing Identity; `POST /api/auth/register` + JWT Client claim; landing `/account/login|register` + Navbar JWT localStorage (mirror dashboard) | 1 | — | ACCT-01, ACCT-05 |
| 07-02 | Account history: ownership-gated bookings/orders APIs; claim guest by email on register (D-04); `/account` Bookings\|Orders tabs + detail; cross-client ID → reject | 2 | 07-01 | ACCT-02, ACCT-03, ACCT-06 |
| 07-03 | Self-service cancel (`Confirmed→Cancelled`, slot release) + transactional cancel-and-rebook reschedule; Client-role + ownership only (D-09–D-12) | 3 | 07-02 | ACCT-04 |
| 07-04 | LoyaltyLedger append-only; +1 on staff `Completed`; account balance; redeem server-computed $ at checkout (10 pts = $5; D-13–D-16) | 4 | 07-02 | ACCT-07 |

## Slice notes (for single-plan writers)

- **07-01:** `StaffRoles.Client` + IdentitySeeder; AuthController register; `landing-page/lib/auth.ts`; `/account/*` auth pages; Navbar Account vs Login/Register. Tests: `ClientAuthTests` (+ IdentitySeeder role assert).
- **07-02:** `AccountController` + `ClientUserId`/claim; never trust client-supplied owner IDs. UI: history tabs only. Tests: `AccountBookingsTests`, `AccountOrdersTests` (IDOR cases).
- **07-03:** Ownership-gated client cancel/reschedule endpoints; book-new→cancel-old one transaction (unique index). UI on account booking detail. Tests: `ClientRescheduleTests`.
- **07-04:** `Features/Loyalty/*` + DbSet/migration; hook `UpdateStatusAsync`→Completed; `CreateCheckoutAsync` discount after catalog recompute; CheckoutForm redeem + account balance. Tests: `LoyaltyTests`. Wave after 07-02 (FK/claim + account surface); after 07-03 if sharing `AppointmentsService` edits.

**Deferred (do not plan):** OAuth, RETN-01/02/03, refresh-token hardening.

## OUTLINE COMPLETE

**Phase:** 07-accounts-retention
**Plans:** 4 plan(s) in 4 wave(s)

plan count: 4
