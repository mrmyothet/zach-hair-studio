# Phase 2: Booking Core - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-09
**Phase:** 02-booking-core
**Areas discussed:** Slot model & double-booking guarantee, Stylist & availability model, Confirmation email delivery, Legacy booking migration

---

## Slot model & double-booking guarantee

### Q1: How should appointment start times be modeled?

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed time grid | Starts on a fixed increment; booking occupies N consecutive cells from `Service.DurationMinutes`. Makes the DB guarantee a simple unique index. | ✓ |
| Arbitrary start + overlap check | Store `[StartsAt, EndsAt)`, reject overlaps. Tightest packing, but SQL Server has no exclusion constraint — needs SERIALIZABLE range locks. | |
| Materialized slot rows | Pre-generate a row per bookable slot; book by claiming a row. | |

**User's choice:** Fixed time grid
**Notes:** Chosen because SC4 demands a *database-level* guarantee, and a grid makes that a plain unique index rather than an isolation-level argument.

### Q2: What grid increment?

| Option | Description | Selected |
|--------|-------------|----------|
| 15 minutes | All seeded durations divide cleanly except 40-min Scalp (rounds to 45, 5 min lost). | ✓ |
| 30 minutes | Classic salon convention, but wastes 15 min on both 45-min services and 20 min on Scalp. | |
| 5 minutes | Zero waste, but 12 cells/hour/stylist and a fiddly schedule UI. | |

**User's choice:** 15 minutes
**Notes:** Existing seeded durations are 45, 90, 45, 120, 40, 210 minutes.

### Q3: How is the double-booking guarantee physically enforced?

| Option | Description | Selected |
|--------|-------------|----------|
| Occupancy rows + unique index | `Appointment` + one `AppointmentSlot` row per occupied cell, `UNIQUE (StylistId, SlotStart)`; dup-key violation → 409. | ✓ |
| Pre-generated slot rows, claimed by UPDATE | Conditional `UPDATE ... WHERE Status='open'`; rowcount mismatch → rollback. | |
| SERIALIZABLE transaction range lock | Overlap check inside a serializable transaction. Weakest fit for SC4's wording. | |

**User's choice:** Occupancy rows + unique index
**Notes:** Established during discussion that a unique index on `(StylistId, StartsAt)` alone is insufficient — a 90-min booking at 09:00 and a cut at 09:15 have distinct start times but overlap.

### Q4: When an appointment is cancelled, what happens to its occupancy rows?

| Option | Description | Selected |
|--------|-------------|----------|
| Delete occupancy rows, keep Appointment | Slot immediately rebookable; unique index needs no filter; history preserved. | ✓ |
| Keep rows, add status to the index | Exact audit trail, but requires a filtered unique index. | |

**User's choice:** Delete occupancy rows, keep Appointment
**Notes:** Raised because the unique index would otherwise permanently block rebooking of a cancelled slot. Phase 3's no-show status behaves identically.

---

## Stylist & availability model

### Q1: Where do stylists come from?

| Option | Description | Selected |
|--------|-------------|----------|
| New Stylist entity, seeded from team data | Seed via `HasData` from `lib/data.ts` team members; Team marketing section stays static. | ✓ |
| Stylist entity replaces static team data now | Also retire the static team array, mirroring D-14 for services. | |
| Single implicit stylist (no entity yet) | Defer stylists to Phase 4 — would fail BOOK-06 outright. | |

**User's choice:** New Stylist entity, seeded from team data
**Notes:** Keeps Team-section UI work out of a booking-focused phase, per the scope guardrail.

### Q2: How is stylist availability represented?

| Option | Description | Selected |
|--------|-------------|----------|
| Recurring weekly hours + exceptions | `StylistWorkingHours` + `StylistTimeOff`; open slots computed on the fly. Phase 4 edits the same tables. | ✓ |
| Recurring weekly hours only | Smallest surface, but no way to block a holiday — system would offer slots on Christmas. | |
| Explicit per-date availability rows | Flexible, but needs rows generated far into the future and bulk edits in Phase 4. | |

**User's choice:** Recurring weekly hours + exceptions
**Notes:** Satisfies the roadmap's "one system, not two" constraint between Phase 2 and Phase 4.

### Q3: How does booking handle a client with no stylist preference?

| Option | Description | Selected |
|--------|-------------|----------|
| "Any stylist" option, server assigns | Union of open slots; server deterministically assigns a free stylist in the same transaction. | ✓ |
| Stylist selection required | Simplest query, but forces a choice many clients don't care about. | |

**User's choice:** "Any stylist" option, server assigns
**Notes:** The unique index requires a concrete `StylistId` at write time, so "no preference" must resolve server-side.

### Q4: Can every stylist perform every service?

| Option | Description | Selected |
|--------|-------------|----------|
| All stylists perform all services | No capability join table; keeps the phase focused on the concurrency guarantee. | ✓ |
| Model Service↔Stylist capability now | More realistic, but unscoped for Phase 2 and needs invented seed data. | |

**User's choice:** All stylists perform all services

---

## Confirmation email delivery

### Q1: How should confirmation emails be delivered?

| Option | Description | Selected |
|--------|-------------|----------|
| `IEmailSender` abstraction + dev sink | Interface + file/console sink; real provider chosen in Phase 8. No API key in repo. | |
| Commit to a provider now | Real inbox delivery in Phase 2; needs an account and a managed API key. | ✓ |
| On-screen confirmation only, defer email | Would knowingly fail BOOK-03 and SC2. | |

**User's choice:** Commit to a provider now
**Notes:** Context established that the API has zero email infrastructure today — no SMTP, SendGrid, MailKit, or `IEmailSender` anywhere.

### Q2: Which transactional email provider?

| Option | Description | Selected |
|--------|-------------|----------|
| Resend | Single `HttpClient` POST to a REST API, no SDK. Generous free tier. Needs domain verification. | ✓ |
| SendGrid | Official .NET SDK, Azure-native — pairs with the existing Azure SQL setup. Heavier surface. | |
| SMTP (bring your own) | MailKit against any mailbox. No lock-in, but deliverability is on you. | |

**User's choice:** Resend

### Q3: If the confirmation email fails to send, what happens to the booking?

| Option | Description | Selected |
|--------|-------------|----------|
| Booking commits, email best-effort | Slot committed first; send failure logged, never rolls back. | ✓ |
| Email inside the transaction, rollback on failure | Guarantees written confirmation, but holds a DB transaction across a network call — Resend outage = booking outage. | |
| Commit, then queue for retry | Transactional outbox + background worker. Most robust, but new infrastructure. | |

**User's choice:** Booking commits, email is best-effort
**Notes:** Directly honors the CLAUDE.md core value — "If everything else fails, this must work." The transactional-outbox option was captured as a deferred idea for Phase 8.

### Q4: Does a local dev run or the test suite hit Resend for real?

| Option | Description | Selected |
|--------|-------------|----------|
| Dev sends real, tests always fake | Reuses the existing `IsEnvironment("Testing")` seam; suite stays offline and deterministic. | |
| Dev sends real, API boots without a key | Missing key degrades to file sink with a loud warning; preserves dev-simplicity. | |
| Keep it as I said — real everywhere | Dev *and* Testing hit Resend. | ✓ |

**User's choice:** Keep it as I said — real everywhere
**Notes:** ⚠️ Claude pushed back once, explicitly surfacing two conflicts: (1) the `CLAUDE.md` dev-simplicity constraint that "LocalDB + `next dev` + `dotnet run` must be enough" would be broken, since `RESEND_API_KEY` becomes required to boot; (2) Phase 1's 49 currently-offline tests would become network-dependent, quota-burning, and flaky. A middle option (dev real / tests fake) was offered and **declined**. The user reaffirmed the original answer. Recorded as deliberate in CONTEXT.md D-12 so downstream agents do not "helpfully" reintroduce a fake sender.

---

## Legacy booking migration

### Q1: What happens to the existing Booking entity, table, and rows?

| Option | Description | Selected |
|--------|-------------|----------|
| New Appointment entity; retire Booking | Drop `Booking`, `BookingsController`, `BookingRequestForm`. Existing rows are dev/test data. | ✓ |
| Evolve Booking in place | Add FKs, convert `PreferredDate` → `DateTimeOffset`, backfill. Fiddly migration; legacy baggage persists. | |
| Keep both side by side | Two booking concepts, two staff workflows — the "one system, not two" failure. | |

**User's choice:** New Appointment entity; retire Booking
**Notes:** Honors Phase 1's D-19, which deliberately left `BookingsController` unrefactored on the grounds that "Phase 2 rebuilds booking wholesale."

### Q2: What shape does the booking flow take at `/book`?

| Option | Description | Selected |
|--------|-------------|----------|
| Single page, progressive reveal | Service → stylist → date + slot grid → contact fields. Preserves the `?service={slug}` deep link. | ✓ |
| Multi-step wizard with routes | Proper back/forward per step, but four page loads on the primary path. | |
| Modal over the service detail page | Immediate, but breaks the deep link and cramps the slot grid on mobile. | |

**User's choice:** Single page, progressive reveal
**Notes:** Guest booking (name/email/phone) — accounts don't arrive until Phase 7.

### Q3: How is the salon timezone configured, and what times does the client see?

| Option | Description | Selected |
|--------|-------------|----------|
| Config IANA id; always render salon-local | `Salon:TimeZone` in appsettings; UI always shows salon-local with the zone labelled. | ✓ |
| Config IANA id; render in browser timezone | Correct but confusing — a California client sees 7:00 AM for a 10 AM New York appointment. | |
| Hardcode the timezone constant | Roadmap says "a configured salon IANA timezone"; hardcoding means a redeploy to change it. | |

**User's choice:** Config IANA id; always render salon-local

---

## Claude's Discretion

- Exact FluentValidation rules for appointment create DTOs.
- Concrete SQL/LINQ shape of the open-slot query; whether slot computation lives in a separate `SlotService`.
- Tie-breaking rule for "Any stylist" assignment (any deterministic rule acceptable).
- Entity/DTO naming, mapper placement, feature-folder layout (following the Phase 1 `Features/Services` template).
- Booking horizon, minimum lead time, same-day booking rules — pick sensible defaults, flag as owner-reviewable.
- `/book` visual design, empty states, and 409 "slot taken" recovery UX.
- Whether the frontend API client is OpenAPI-generated or hand-written.

## Deferred Ideas

- Retiring the static `team[]` content in favor of an API-backed Team section.
- `Service`↔`Stylist` capability matrix — Phase 4 staff management.
- Transactional-outbox retry for confirmation emails — revisit in Phase 8 hardening.
- Booking horizon / lead-time / same-day rules — may warrant an explicit owner decision later.
- "Slot just got taken" recovery UX — worth a `/gsd-ui-phase 2` pass (phase carries `UI hint: yes`).
