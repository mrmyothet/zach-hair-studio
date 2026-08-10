# Phase 7: Accounts & Retention - Context

**Gathered:** 2026-08-10
**Status:** Ready for planning
**Mode:** Smart discuss (autonomous)

<domain>
## Phase Boundary

Clients create accounts and log in on the landing-page, view their own booking and order history, and cancel or reschedule upcoming appointments themselves. Staff and clients share one ASP.NET Core Identity schema (extend Phase 3). Ownership checks prevent IDOR. Loyalty groundwork: points earned on completed appointments, redeemable as a server-side checkout discount. Guest checkout from Phase 6 remains valid; accounts are additive.

</domain>

<decisions>
## Implementation Decisions

### Client auth & Identity
- **D-01:** Same ASP.NET Core Identity store as staff — add `Client` role to existing `ApplicationUser` / `BookingDbContext` Identity schema (ACCT-05; Phase 3 D-02).
- **D-02:** Client session transport is JWT in localStorage on landing-page, mirroring `dashboard/lib/auth.ts` (Phase 3 D-03 JWT lock — one pattern).
- **D-03:** Email + password register/login on landing-page `/account/*` routes; no OAuth this phase.
- **D-04:** On register, optionally claim guest bookings/orders by matching Email (confirm when ambiguous); history is not forced empty.

### Account surface & history
- **D-05:** Account UI lives on `landing-page/` (`/account`, `/account/bookings`, `/account/orders`) — not under `dashboard/`.
- **D-06:** History presented as two tabs: Bookings | Orders, date-desc lists with detail views.
- **D-07:** Navbar shows Account when JWT present; Login/Register when not (alongside cart).
- **D-08:** Server ownership only — filter/authorize by authenticated user id / linked ClientId; cross-client ID access rejected (ACCT-03/06). Never trust client-supplied owner IDs.

### Self-service cancel & reschedule
- **D-09:** Client cancel reuses `Confirmed → Cancelled` transition (releases `AppointmentSlot` rows) via ownership-gated client endpoint.
- **D-10:** Reschedule = cancel-and-rebook in one UX/transaction (new open slot → new appointment → cancel old) to preserve unique-index invariant.
- **D-11:** Cancel/reschedule allowed until appointment start (no deposit cutoff this phase).
- **D-12:** Only the owning client (Client role + ownership) may self-service; staff keep dashboard status path.

### Loyalty groundwork
- **D-13:** Earn +1 point when staff marks appointment `Completed`.
- **D-14:** `LoyaltyLedger` append-only rows (ClientUserId, Delta, Reason, AppointmentId?, CreatedAt); balance = sum of deltas.
- **D-15:** Redeem at product checkout as server-computed $ discount (Phase 6 price authority — never trust client discount $).
- **D-16:** MVP rates: 1 pt per completed appointment; 10 pts = $5 off (constants; Claude may tune). Not RETN-02 tiers.

### Claude's Discretion
- Exact JWT claim shape for Client role, register validation rules, claim-by-email confirmation UX, discount application order vs tax/shipping (no shipping yet), and ledger reason enum values — follow codebase conventions.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Features/Identity/` — `ApplicationUser`, `JwtTokenService`, `IdentitySeeder`, Owner/Staff roles (Phase 3).
- `AuthController` + `dashboard/lib/auth.ts` — JWT login pattern to mirror on landing-page.
- `Features/Appointments/` — status transitions, slot release on Cancel (Phase 2/3).
- `Features/Orders/` + guest Email on Order (Phase 6) — claim source for history.
- Landing-page Navbar, cart session patterns, Zod `lib/` fetch layers.

### Established Patterns
- Feature folders; services own DbContext (PLAT-01); FluentValidation (PLAT-02); Result → ProblemDetails.
- EF migrations via `ef-migrations` skill; Identity already on `BookingDbContext`.
- OpenAPI source of truth; landing-page hand-written fetch until generated client adopted.

### Integration Points
- `BookingDbContext` — Client role seed; LoyaltyLedger DbSet; optional Appointment/Order → ApplicationUser FKs.
- `AppointmentsService.UpdateStatusAsync` — hook earn on Completed.
- `OrdersService.CreateCheckoutAsync` — apply loyalty discount after catalog recompute.
- Landing-page Navbar + new `/account/*` routes.

</code_context>

<specifics>
## Specific Ideas

- ROADMAP research flag: re-verify Identity vs Auth.js/Better Auth before planning — discuss already locked Identity+JWT for consistency with Phase 3; research should confirm, not reopen, unless a blocking defect is found.
- Phase 6 deferred IDOR for guest cart/order lookup — this phase owns real ownership for authenticated history.

</specifics>

<deferred>
## Deferred Ideas

- OAuth / magic-link (out of scope this phase).
- RETN-02 tiered loyalty; RETN-01 deposits/no-show fees; RETN-03 book-again shortcut.
- Refresh-token hardening beyond Phase 3 workday JWT (optional later).
- Phase 6 Stripe human UAT (`verification_deferred_human`) — resume `/gsd-verify-work 6`.

</deferred>
