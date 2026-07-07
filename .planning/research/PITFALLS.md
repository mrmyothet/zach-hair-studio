# Pitfalls Research

**Domain:** Salon appointment booking + light product commerce (slot-based scheduling, staff dashboard, cart/checkout, accounts)
**Researched:** 2026-07-07
**Confidence:** MEDIUM (web-sourced industry patterns + MEDIUM-confidence official EF Core / ASP.NET Core docs via Context7; no project-specific incident data)

This file is scoped to **domain-specific** mistakes for booking + commerce, building on top of what `.planning/codebase/CONCERNS.md` already flagged (open CORS, no auth, `db.Database.Migrate()` at startup, untested business logic, naive `CreatedAt`/date handling). Those items are referenced here only where a domain pitfall directly depends on fixing them — they are not re-explained.

## Critical Pitfalls

### Pitfall 1: Double-booking via read-then-write slot reservation

**What goes wrong:**
Two clients (or a client and staff) both query "is 2:00pm open for Stylist X" at nearly the same time, both see it's free, and both write a booking for that slot. The second write silently succeeds because nothing at the database layer enforces "one booking per stylist per slot." The result: two clients show up for the same chair.

**Why it happens:**
The current codebase pattern (`BookingsController`) queries, then inserts, with no uniqueness constraint and no transaction isolation tying the availability check to the insert. This is the single most common bug class in every booking-system post-mortem: app-level checks ("check then insert") look correct in manual testing (one browser tab, no concurrency) and only fail under real concurrent load — which won't show up in dev or even light staging traffic. Teams also sometimes add a distributed lock or in-memory mutex and believe that's sufficient, but a lock protects only processes that respect it; it does not stop a second code path, a retried request, or a second app instance from writing a conflicting row unless the database itself enforces the invariant.

**How to avoid:**
- Enforce the invariant at the database, not just in application code: a unique index on `(StylistId, SlotStart)` (or `(ResourceId, SlotStart)` if multiple chairs/stations) so a conflicting INSERT fails with a DB constraint violation, not a silent double-write.
- Wrap "check slot open → create booking" in a single transaction with an appropriate isolation level, or rely on the unique constraint plus a catch-and-report-"slot taken"-to-user pattern (optimistic: attempt the insert, catch the constraint violation, return a friendly "someone just booked this slot" error).
- If appointments have duration (not just a start instant), the unique index on exact start time isn't enough for overlap prevention (e.g., a 90-minute color booked at 2:00 collides with a 2:30 start) — model slots as fixed-size buckets generated from availability (so overlap reduces to exact-match) rather than open-ended time ranges, or add an explicit overlap check inside the same transaction.
- Concurrency tokens (`[Timestamp]`/rowversion in EF Core) protect against conflicting *updates* to an existing row (e.g., two staff members changing status simultaneously) but do **not** by themselves prevent two *new* bookings for the same slot — that needs the unique constraint on insert.

**Warning signs:**
- Load-testing the booking-confirmation endpoint with concurrent requests for the same slot succeeds more than once.
- No unique index exists on the appointment/slot table beyond the primary key.
- "Check availability" and "create booking" are two separate API calls/DB round-trips rather than one atomic operation.

**Phase to address:**
Phase 2 — Booking core (this is the exact moment appointments and availability are modeled; must be solved before Phase 3's dashboard trusts booking data, and before Phase 4 lets staff edit availability concurrently with a client booking).

---

### Pitfall 2: Timezone/DST bugs corrupting availability and confirmations

**What goes wrong:**
A slot shown to the client as "2:00 PM" doesn't match what's stored, checked, or displayed to staff — appointments appear off by an hour around DST changes, or a slot generated for a nonexistent local time during the spring-forward gap either silently shifts, errors, or gets stored and later fails to send its reminder.

**Why it happens:**
`.planning/codebase/CONCERNS.md` already flags that `PreferredDate` is a plain `DateTime` with no timezone info — that's the free-text-booking-era symptom. The deeper issue for Phase 2 is that slot generation, availability windows, and appointment storage all need one **consistent** time representation, and mixing "naive local time" (what staff picks when setting availability) with "the server's `DateTime.UtcNow`" is exactly how these bugs get introduced silently — everything works in local dev (server and browser share a timezone) and breaks only in production or the first DST transition after launch.

**How to avoid:**
- Store all appointment/slot timestamps as UTC (`DateTimeOffset` in EF Core / SQL Server, not bare `DateTime`) — never store "local wall-clock time" as if it were absolute.
- Store the salon's IANA timezone (e.g. `America/New_York`) as configuration (single-location business per PROJECT.md, so this can be one value, not per-user) and do the local↔UTC conversion only at the display/input boundary (staff sets "9am-5pm" in salon-local time → converted to UTC when persisted; client sees slots converted back to salon-local time for display, since the client is booking an in-person appointment at the salon's location, not their own timezone).
- When generating available slots from a recurring weekly schedule, generate them with explicit awareness of DST transition dates (skip/adjust for the nonexistent hour on spring-forward, don't double-generate on fall-back) rather than doing naive fixed-offset arithmetic.
- Use .NET's `TimeZoneInfo` conversions (not manual UTC offset math) for any local-time display or slot generation logic.

**Warning signs:**
- Any `DateTime` (not `DateTimeOffset`) field on the appointment/availability model.
- Slot-generation logic that adds/subtracts fixed hour offsets instead of calling `TimeZoneInfo.ConvertTimeToUtc`/`ConvertTimeFromUtc`.
- No test covering a booking made across a DST boundary date.

**Phase to address:**
Phase 2 — Booking core (slot/availability modeling is where the representation is chosen; retrofitting `DateTime` → `DateTimeOffset` later means a data migration across every booking).

---

### Pitfall 3: No-show and late-cancellation revenue leakage with no enforcement mechanism

**What goes wrong:**
The roadmap already includes a `no-show` status (Phase 3), but a status field alone doesn't *prevent* no-shows or recover the lost revenue — it only records that it happened after the fact. Salons that don't attach any commitment (deposit, card-on-file, or a clear cancellation-fee policy) to a booking see no-show rates in the 10-15% range; ones that require some form of payment/commitment at booking time see it drop to under 5%.

**Why it happens:**
It's tempting to treat "no-show" as just another value in the booking status enum (cheap to build) and defer the actual policy (deposits, fees, cutoff windows) as a "later" concern — but by the time Phase 6 (checkout/payments) exists, the booking flow (Phase 2) and the payment integration are separate subsystems, so retrofitting "charge a no-show fee" means bridging booking and payment after both were built independently.

**How to avoid:**
- In Phase 2/3, at minimum model a cancellation policy as data (e.g., "cancellations within X hours of appointment are marked late-cancel"), even if no payment is collected yet — this gives staff and the dashboard something enforceable rather than just a status label.
- Decide explicitly (and record in `specs/tech-stack.md` / Key Decisions) whether v1 collects a deposit/card-on-file at booking time (requires Phase 6's payment provider pulled earlier) or defers monetary enforcement to a later milestone — don't let it default silently to "no enforcement, ever" by omission.
- Communicate the cancellation/no-show policy at the point of booking confirmation (client-facing), not just internally in the staff dashboard — ambiguous policies drive both no-shows and client disputes.

**Warning signs:**
- No-show/cancellation logic exists only as a `BookingStatus` enum value with no linked policy, fee, or cutoff-window concept.
- No field capturing "when was this cancelled relative to the appointment time" (needed to distinguish late-cancel from early-cancel).

**Phase to address:**
Phase 3 — Staff dashboard (schedule) for the policy data model and status handling; Phase 6 — Cart & checkout if/when monetary enforcement (deposits) is decided, since that's when a payment provider exists.

---

### Pitfall 4: Overselling product stock at checkout (read-then-write race, same root cause as Pitfall 1)

**What goes wrong:**
Two clients add the last unit of a product to their cart and both complete checkout; stock goes to -1 (or the second sale is accepted with nothing left to fulfill). This is the commerce-side twin of double-booking: `stock=1`, two concurrent reads both see 1, both decrement, both "succeed."

**Why it happens:**
Cart/checkout flows often decrement stock as a side effect of order creation in application code (read stock, check `> 0`, write `stock - 1`) rather than as a single atomic, constrained database operation — this works fine at low traffic and silently breaks the first time two people check out the last unit near-simultaneously (e.g., a promoted product).

**How to avoid:**
- Decrement stock as a conditional, atomic update in the same transaction as order creation: `UPDATE Products SET Stock = Stock - @qty WHERE Id = @id AND Stock >= @qty`, and check rows-affected — if 0 rows affected, the order fails with "out of stock," not a negative stock value.
- Alternatively, use EF Core optimistic concurrency (rowversion/concurrency token on `Product.Stock`) and catch `DbUpdateConcurrencyException` to retry or reject — but the conditional-UPDATE-with-WHERE-guard approach is simpler and sufficient for this project's expected scale (single salon, not flash-sale volume) and avoids a retry loop.
- Never split "check stock" and "decrement stock" into two separate round-trips/requests — the checkout flow must treat this as one atomic step.
- Stock decrement must happen in the same DB transaction as order-row creation, so a failed payment doesn't leave stock decremented with no corresponding order.

**Warning signs:**
- Cart/checkout code path has a distinct "check stock" step followed later by a separate "create order" step against a different call.
- No `WHERE Stock >= @qty` guard on the decrement statement — a plain `Stock = Stock - 1` can go negative.
- Product stock and order creation are not in the same transaction scope.

**Phase to address:**
Phase 6 — Cart & checkout (this is precisely the phase that introduces stock decrement; get the atomic-decrement pattern right here, since Phase 5's read-only catalog has no state to protect yet).

---

### Pitfall 5: Trusting client-submitted prices/totals at checkout

**What goes wrong:**
The cart on the public site computes a total client-side (for display) and — if the backend naively accepts that total, or accepts a client-passed amount when creating the payment — a manipulated request (modified via browser dev tools or a replayed/edited API call) can pay less than the real price, or nothing at all.

**Why it happens:**
It's the path of least resistance to pass "amount" from the frontend cart state straight through to the payment-provider call, especially when the same cart total is already computed for display purposes — reusing it for the charge feels like avoiding duplicate logic. But the client is an untrusted environment; only the server's own database of authoritative prices (Phase 5's product catalog) can be trusted for the actual charge amount.

**How to avoid:**
- On `POST /checkout` (or wherever the order/PaymentIntent is created), the API must re-look-up each cart line's product price from the database by product ID — never accept a client-supplied unit price or total. The client should only ever send product IDs and quantities.
- Compute the authoritative order total server-side from those looked-up prices before creating the payment provider's charge/PaymentIntent.
- Treat the client-side cart total purely as a UI convenience/preview; it must never be the number that reaches the payment provider.
- Trigger order fulfillment (marking the order paid, decrementing stock if not already done atomically at order creation, sending confirmation) from the payment provider's **server-to-server webhook** (e.g., Stripe's `payment_intent.succeeded`), not from the client's post-payment redirect/callback — a client redirect can be spoofed, skipped (user closes tab), or simply never reached if the network drops.
- Use idempotency keys on the order-creation/charge call so a retried request (double-click, network retry) doesn't create two orders or charge twice.

**Warning signs:**
- Any DTO for creating an order/checkout session that accepts a `total` or per-item `price` field from the client rather than only `productId` + `quantity`.
- Order status (`Paid`) set directly by a client-facing endpoint rather than by a webhook handler.
- No webhook endpoint registered/verified for the payment provider at all.

**Phase to address:**
Phase 6 — Cart & checkout (this is where the payment provider decision from Key Decisions gets implemented — get server-side price authority and webhook-driven fulfillment right from the first commit, since retrofitting it after a "trust the client" version ships means auditing every historical order for tampering).

---

### Pitfall 6: Auth boundary confusion between the public site and the staff dashboard

**What goes wrong:**
`landing-page/` (public, unauthenticated) and `dashboard/` (staff-only) are separate Next.js apps calling one shared API. Without a deliberate boundary, staff-only endpoints (update booking status, CRUD services/availability, view all clients' orders) end up reachable by any caller of the API — exactly what `.planning/codebase/CONCERNS.md` already flags for the *current* `BookingsController.UpdateStatus`. The domain-specific risk as more surfaces get added (Phase 3 dashboard, Phase 4 CRUD, Phase 7 accounts) is that "no auth yet" quietly becomes "wrong auth" — e.g., a client's own account session accidentally satisfying a staff-only `[Authorize]` check because both use the same generic "authenticated user" policy instead of a role/scheme distinction.

**Why it happens:**
Since one API backs two frontends with very different trust levels (anonymous public browsing/booking vs. authenticated staff operations vs., later, authenticated client accounts), it's tempting to add a single blanket `[Authorize]` once auth is "turned on," which either locks out legitimate public traffic (booking creation must stay anonymous) or under-protects staff endpoints if the same policy is reused everywhere.

**How to avoid:**
- Design the auth boundary as three distinct trust levels, not two: **anonymous** (browse services/products, submit a booking, add to cart), **client account** (Phase 7 — own booking/order history only, scoped to the caller's own client ID), and **staff** (dashboard — full read/write on bookings, services, availability, orders).
- Register distinct authentication schemes for staff vs. client (e.g., cookie auth for the dashboard session, a separate scheme/policy for client accounts) and use explicit named authorization policies (`[Authorize(Policy = "StaffOnly")]`, `[Authorize(Policy = "ClientOwner")]`) rather than a single default `[Authorize]` — ASP.NET Core supports multiple simultaneous schemes precisely for this "different frontends, different trust levels" shape.
- For client-account endpoints (Phase 7), authorization must also check *ownership* (the authenticated client can only see their own bookings/orders), not just "is authenticated" — a missing ownership check (IDOR) is a distinct, common bug from missing authentication entirely.
- Apply this at Phase 3 (dashboard ships) even though full auth is deferred per Key Decisions — at minimum, scope `CORS` for the dashboard origin separately from the public origin now, so the eventual auth cutover doesn't also require an emergency CORS rewrite.

**Warning signs:**
- A single `[Authorize]` (no policy/role) used on both a staff-only action and a client-owned-resource action.
- Any staff-dashboard endpoint reachable without a session/token in a quick manual `curl` test.
- Client-account endpoints (Phase 7) that accept a `clientId` from the request body/query instead of deriving it from the authenticated principal.

**Phase to address:**
Phase 3 — Staff dashboard (schedule) for the staff/public boundary (even if "real" auth lands later, don't let the dashboard ship assuming the existing open-CORS/no-auth API is acceptable); Phase 7 — Accounts & retention for the client-ownership boundary once client accounts exist. This directly extends `.planning/codebase/CONCERNS.md`'s "Missing Authentication & Authorization" item — that item flags *that* auth is missing; this pitfall is about getting the *shape* of auth right (three trust levels, not one) once it's added.

---

### Pitfall 7: Availability model and booking model drift out of sync

**What goes wrong:**
Phase 2 builds "query open slots" from stylist availability; Phase 4 lets staff edit that availability via dashboard CRUD. If a stylist shortens their hours or blocks time off *after* slots were already shown/booked, existing confirmed bookings can end up outside the new availability window with no reconciliation — the dashboard shows a booking at a time the stylist is no longer marked available, and nothing flags the conflict.

**Why it happens:**
Availability and bookings are naturally modeled as two separate concerns (Phase 2 vs Phase 4), and "editing availability" is usually implemented as pure CRUD on the availability table without checking existing bookings against the new window — it's not obviously "wrong" in isolation, only in combination.

**How to avoid:**
- When staff edit availability (Phase 4), check for existing confirmed bookings that would fall outside the new window and surface them explicitly (block the edit, or require staff to acknowledge/reassign/cancel the conflicting bookings) rather than letting availability and bookings silently diverge.
- Keep the "generate open slots" query (Phase 2) always deriving from current availability + existing bookings at read time, rather than caching/pre-computing a slot list that can go stale when either changes.

**Warning signs:**
- Dashboard availability edit succeeds with no check against existing bookings.
- Slot-query logic and availability-edit logic live in unrelated code paths with no shared validation.

**Phase to address:**
Phase 4 — Staff management of services & availability.

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| App-level "check then insert" for slot booking (no DB unique constraint) | Faster to build, works in manual testing | Double-bookings under real concurrent traffic — reputationally costly for a salon (two clients in one chair) | Never for the booking table itself; acceptable only for genuinely non-conflicting resources |
| Storing appointment times as bare `DateTime` instead of `DateTimeOffset` + salon timezone | Simpler model initially, mirrors existing `PreferredDate` pattern | DST-boundary bugs, off-by-one-hour confirmations, broken reminders; expensive to migrate later | Never once Phase 2 introduces real slot-based scheduling |
| Client-computed cart total passed to checkout/payment call | Reuses display logic, less server code | Price-tampering vulnerability; direct revenue loss | Never — always recompute server-side from catalog prices |
| Single blanket `[Authorize]` (no policy separation) once auth is added | Fastest way to "turn on" auth | Either locks out public booking flow or under-protects staff/account boundaries (wrong trust level satisfies the check) | Never past Phase 3; acceptable only as a placeholder during initial Phase 7 auth spike before wiring real policies |
| No-show status with no linked policy/fee/cutoff data | Cheap to ship (Phase 3) | Retrofitting enforcement later requires bridging booking (Phase 2/3) and payment (Phase 6) subsystems after both are built independently | Acceptable temporarily if the cutoff/policy *fields* exist even before monetary enforcement is wired up |
| Stock decrement as a separate step after order-row creation (not same transaction, no `WHERE stock >= qty` guard) | Simpler order-creation code | Overselling under concurrent checkout of low-stock items | Never |

## Integration Gotchas

Common mistakes when connecting to external services.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|-------------------|
| Payment provider (Stripe or similar, Phase 6 decision) | Trusting client-supplied amount/total for the charge | Server recomputes total from DB prices; server creates the PaymentIntent/charge with that authoritative amount |
| Payment provider webhooks | Marking orders "paid"/fulfilling from the client-side success redirect | Verify and act on the provider's signed server-to-server webhook event (e.g. `payment_intent.succeeded`) as the source of truth |
| Payment provider retries | No idempotency key on order/charge creation → duplicate orders or double charges on network retry | Pass a unique idempotency key per checkout attempt |
| EF Core + SQL Server concurrency tokens | Assuming a rowversion/concurrency token on the appointment row prevents double-booking | Concurrency tokens guard conflicting *updates* to an existing row; double-booking is a conflicting *insert* — needs a unique index instead/also |
| ASP.NET Core multi-scheme auth | Reusing one default `[Authorize]` across staff, client-account, and (implicitly) public routes | Named authorization policies per trust level, explicit `AddAuthenticationSchemes(...)` on the default policy |

## Performance Traps

Patterns that work at small scale but fail as usage grows.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Read-then-write slot/stock checks with no DB constraint | Works fine in dev/staging with one tester; fails only under real concurrent traffic | DB-level unique constraint / conditional atomic UPDATE | First time two real users hit the same slot/last-unit simultaneously — can happen even at low volume (single salon, popular time slot), not just "at scale" |
| Pre-computed/cached slot lists that don't re-derive from live availability+bookings | Slots shown as "open" that are actually booked, or vice versa, as availability edits accumulate | Always query slots live from current availability + bookings | As soon as Phase 4 availability editing is used regularly alongside live bookings |

## Security Mistakes

Domain-specific security issues beyond general web security.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Accepting client-supplied price/amount at checkout | Direct revenue loss via tampered requests | Always recompute total server-side from catalog |
| Fulfilling orders from client redirect instead of payment webhook | Orders marked paid without payment actually completing (spoofed/dropped callback) | Fulfill only from verified server-to-server webhook |
| Blanket `[Authorize]` without role/ownership checks once auth exists | Client account can reach staff endpoints, or one client can view another's bookings/orders (IDOR) | Named policies per trust level; explicit ownership check on client-account endpoints, not just "is authenticated" |
| No rate limiting on booking creation (already flagged in CONCERNS.md) combined with new checkout endpoint in Phase 6 | Same spam/DoS risk now applies to a payment-triggering endpoint — worse, could trigger repeated real charges attempts | Extend the rate-limiting fix from CONCERNS.md to the checkout/order-creation endpoint specifically, not just bookings |

## UX Pitfalls

Common user experience mistakes in this domain.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-------------------|
| Silent double-booking rejection with a generic error | Client picks a slot, submits, gets an unexplained failure, has to start over | On unique-constraint conflict, return a specific "this slot was just taken" message and re-fetch fresh availability inline |
| No visible cancellation/no-show policy at booking confirmation | Client is surprised by a fee or blocked from cancelling for free later | Surface the policy text at the point of confirming a slot, not buried in T&Cs |
| Displaying appointment times without timezone context assumption made explicit | Client unsure if "2:00 PM" is their local time or the salon's (matters for a single-location business too, if client travels) | Always label times as the salon's local time explicitly in the UI copy |

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **Slot booking (Phase 2):** Looks done once "create booking" returns 201 — verify a unique DB constraint actually rejects a concurrent duplicate insert (test with two near-simultaneous requests for the same slot, not just sequential manual testing).
- [ ] **No-show status (Phase 3):** Looks done once the enum value exists and the dashboard can set it — verify there's a linked cutoff-window/policy concept, not just a label with no enforcement.
- [ ] **Stock decrement (Phase 6):** Looks done once checkout reduces `Stock` by the ordered quantity — verify the decrement is a guarded, atomic, same-transaction update (`WHERE Stock >= @qty`), not a plain read-modify-write.
- [ ] **Checkout total (Phase 6):** Looks done once the payment provider returns success — verify the charge amount was computed server-side from the DB, not passed through from the client cart.
- [ ] **Staff dashboard auth (Phase 3/7):** Looks done once a login screen exists — verify staff-only API endpoints actually reject unauthenticated/wrong-role requests (test with `curl`, not just "the button is hidden in the UI").
- [ ] **Availability editing (Phase 4):** Looks done once CRUD works — verify editing availability that conflicts with existing confirmed bookings is flagged, not silently allowed.

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|-----------------|
| Double-booking already occurred in production | MEDIUM | Identify via a query for duplicate `(StylistId, SlotStart)` rows; manually contact one affected client to reschedule; retroactively add the unique constraint (requires resolving existing duplicates first via a data-cleanup migration) |
| `DateTime` used instead of `DateTimeOffset` for appointments, discovered post-launch | HIGH | Requires a data migration: backfill a timezone assumption for all historical rows, change column type, audit every read/write path (slot generation, display, reminders) for correctness — budget this as its own mini-phase, don't attempt inline with feature work |
| Client price-tampering exploited before server-side recomputation was added | MEDIUM-HIGH | Audit orders for price mismatches against current/historical catalog prices; refund/adjust affected orders; ship the server-side recompute fix same-day, treat as a security incident (credentials/keys don't need rotation, but the checkout endpoint must be patched before any further traffic) |
| Overselling occurred (stock went negative) | LOW-MEDIUM | Contact affected customers proactively (out of stock, offer refund/backorder/substitute); backfill the atomic-decrement fix; add a check constraint (`Stock >= 0`) to catch future regressions at the DB level |

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|-------------------|----------------|
| Double-booking (read-then-write slot race) | Phase 2 — Booking core | Concurrent-request test against the same slot returns exactly one success and one clear "slot taken" rejection |
| Timezone/DST corruption of slots | Phase 2 — Booking core | Test a slot generated/booked across a DST transition date; confirm stored value is UTC and display converts correctly |
| No-show/cancellation policy has no enforcement mechanism | Phase 3 — Staff dashboard (data model); Phase 6 — Cart & checkout (monetary enforcement, if decided) | Dashboard can distinguish late-cancel from early-cancel via a cutoff-window field, not just a status label |
| Overselling stock at checkout | Phase 6 — Cart & checkout | Concurrent checkout test against last-unit stock returns exactly one success |
| Trusting client-side price at checkout | Phase 6 — Cart & checkout | Manually tamper a client request's price/total field; confirm server ignores it and recomputes from DB |
| Auth boundary confusion (public/staff/client) | Phase 3 (staff/public boundary); Phase 7 (client-ownership boundary) | `curl` staff-only endpoints unauthenticated → rejected; one client account cannot fetch another's bookings/orders by ID |
| Availability/booking drift | Phase 4 — Staff management of services & availability | Editing availability that conflicts with an existing confirmed booking is flagged/blocked, not silently applied |

## Sources

- [Handling Concurrency Conflicts — EF Core docs](https://github.com/dotnet/entityframework.docs/blob/main/entity-framework/core/saving/concurrency.md) — MEDIUM confidence (Context7-curated official docs)
- [Limiting identity by scheme — ASP.NET Core docs](https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/security/authorization/limitingidentitybyscheme.md) — MEDIUM confidence (Context7-curated official docs)
- [What is the best way to store an appointment time? — Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/1194364/what-is-the-best-way-to-store-an-appointment-time) — MEDIUM confidence (cross-checked web)
- [10 best practices for timestamps and time zones in databases — Tinybird](https://www.tinybird.co/blog/database-timestamps-timezones) — MEDIUM confidence (cross-checked web)
- [How to Handle Date and Time Correctly to Avoid Timezone Bugs — DEV Community](https://dev.to/kcsujeet/how-to-handle-date-and-time-correctly-to-avoid-timezone-bugs-4o03) — MEDIUM confidence
- [Five Online Booking Mistakes That Are Costing You Clients — Calendar](https://www.calendar.com/blog/five-online-booking-mistakes-that-are-costing-you-clients/) — MEDIUM confidence
- [Cancellation, deposit, and prepayment policies — Booking.com for Partners](https://partner.booking.com/en-us/solutions/cancellation-deposit-and-prepayment-policies) — MEDIUM confidence (adjacent hospitality domain, same no-show economics)
- [How I Eliminated Inventory Race Conditions in a Production E-Commerce System — Medium](https://medium.com/@chaturvediinitin/how-i-eliminated-inventory-race-conditions-in-a-production-e-commerce-system-2302ba81846b) — MEDIUM confidence
- [Race conditions in inventory tracking, order, payment status — Sylius GitHub issue #2776](https://github.com/Sylius/Sylius/issues/2776) — MEDIUM confidence (real project issue tracker)
- [Distributed Locking and Race Condition Prevention — DZone](https://dzone.com/articles/distributed-locking-and-race-condition-prevention) — MEDIUM confidence
- [Finalize payments on the server — Stripe Documentation](https://docs.stripe.com/payments/finalize-payments-on-the-server) — MEDIUM confidence (official vendor docs, cross-checked via web search)
- [Integration security guide — Stripe Documentation](https://docs.stripe.com/security/guide) — MEDIUM confidence
- [Server-side integration — Stripe Documentation](https://docs.stripe.com/plan-integration/get-started/server-side-integration) — MEDIUM confidence
- [Solving Double Booking at Scale: System Design Patterns from Top Tech Companies — ITNEXT](https://itnext.io/solving-double-booking-at-scale-system-design-patterns-from-top-tech-companies-4c5a3311d8ea) — MEDIUM confidence
- [The Double-Booking Trap in Distributed Systems: Why Locks Alone Fail to Guarantee Correctness — Medium](https://medium.com/@umeshcapg/the-double-booking-trap-in-distributed-systems-why-locks-alone-fail-to-guarantee-correctness-96ea87bb550c) — MEDIUM confidence
- [Race Conditions in Hotel Booking Systems — amitavroy.com](https://amitavroy.com/articles/race-conditions-in-hotel-booking-systems-why-your-technology-choice-matters-more-than-you-think) — MEDIUM confidence
- `.planning/codebase/CONCERNS.md` (2026-07-07) — project-specific baseline this file extends

---
*Pitfalls research for: salon appointment-booking + light commerce platform*
*Researched: 2026-07-07*
