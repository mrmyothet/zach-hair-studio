# Phase 2: Booking Core - Context

**Gathered:** 2026-07-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Real slot-based appointment booking. A client picks a service, sees genuinely open
slots for a stylist (derived from working hours minus time-off minus existing
bookings), and confirms an appointment that the **database** guarantees cannot be
double-booked. Times are stored timezone-aware against a configured salon timezone.
The client receives an on-screen confirmation and a confirmation email.

This phase replaces the legacy free-text booking request with a real reservation.

**Not in this phase:** staff-facing schedule UI or status management (Phase 3),
staff-editable services/availability CRUD (Phase 4), accounts/auth (Phase 7),
payments (Phase 6).

</domain>

<decisions>
## Implementation Decisions

### Slot model & double-booking guarantee

- **D-01:** Appointments sit on a **fixed 15-minute time grid** within a stylist's
  working hours. A booking occupies N consecutive grid cells derived from
  `Service.DurationMinutes`. Rejected: arbitrary start times with overlap detection
  (SQL Server has no exclusion constraint, so it would need SERIALIZABLE range locks
  — materially harder to prove correct, and Success Criterion 4 demands a
  *database-level* guarantee).
- **D-02:** Grid increment is **15 minutes**. Every seeded duration divides cleanly
  (45, 90, 45, 120, 210) except the 40-minute Scalp Treatment, which rounds up to 45
  — 5 minutes lost. A 30-minute grid was rejected because it wastes 15 minutes on
  both 45-minute services and 20 minutes on the Scalp Treatment.
- **D-03:** The guarantee is enforced by **occupancy rows plus an unfiltered unique
  index**. One `Appointment` row plus one `AppointmentSlot` child row per occupied
  15-minute cell, with `UNIQUE (StylistId, SlotStart)`. All cells are inserted in a
  single transaction; a colliding concurrent booking hits a duplicate-key violation
  (SQL Server error 2627), which is caught and translated to a clean **409 "slot
  taken"**. A unique index on `(StylistId, StartsAt)` alone is insufficient — a
  90-minute booking at 09:00 and a cut at 09:15 have different start times but
  overlap.
- **D-04:** Cancelling an appointment **deletes its `AppointmentSlot` rows** and
  keeps the `Appointment` row with `Status = Cancelled`. The slot becomes immediately
  bookable, the unique index needs **no filter predicate** (a filtered unique index is
  easy to get subtly wrong — precisely the failure mode SC4 exists to prevent), and
  booking history is preserved. Phase 3's no-show status behaves identically:
  terminal, slot released.

### Stylist & availability model

- **D-05:** A new **`Stylist` entity** (Id, Slug, Name, IsActive, DisplayOrder),
  seeded via EF `HasData` from the current static team members in
  `landing-page/lib/data.ts`. This mirrors Phase 1's catalog seeding pattern
  (D-12/D-13). The public Team marketing section **keeps rendering its static
  content** — retiring it is a separate, later concern, not Phase 2 scope.
- **D-06:** Availability is **recurring weekly hours plus exceptions**:
  `StylistWorkingHours` (StylistId, DayOfWeek, StartTime, EndTime) and
  `StylistTimeOff` (holidays, sick days, one-off closures). Open slots are computed
  on the fly as `hours − timeOff − bookedCells`. No generation job, no slot rows to
  regenerate. Phase 4's staff CRUD edits **these same two tables** — one system, not
  two, per the roadmap's explicit constraint.
- **D-07:** Stylist selection is **optional via an "Any stylist" option**, which is
  the default. Open slots are the union across all active stylists. On confirm, the
  server deterministically assigns one free stylist for that slot **inside the same
  transaction** (the unique index requires a concrete `StylistId` at write time). The
  confirmation names the concrete assigned stylist. Satisfies BOOK-06.
- **D-08:** **All stylists perform all services** in Phase 2. No `Service`↔`Stylist`
  capability join table; slot queries filter only on working hours, time-off, and
  existing bookings. A capability matrix belongs to Phase 4 staff management.

### Confirmation email delivery

- **D-09:** Ship a **real transactional email provider in Phase 2** — not a dev-only
  sink. There is currently no email infrastructure of any kind in `API/`.
- **D-10:** The provider is **Resend**, called via a single `HttpClient` POST to its
  REST API. No SDK dependency to keep current. Requires verifying a sending domain.
- **D-11:** **The booking commits first; email is best-effort.** The appointment and
  its occupancy rows are committed, and only then is the email sent. A send failure is
  logged and surfaced to staff but **never rolls back the appointment**. Consequence:
  the on-screen confirmation must carry every detail the client needs, because the
  email may not arrive. This directly honors the project's core value — *"If
  everything else fails, this must work"* — a Resend outage cannot cost a client their
  slot. Never hold a database transaction open across a third-party network call.
- **D-12:** ⚠️ **Real email sends in Development AND Testing.** The user was shown the
  conflicts and explicitly chose this anyway. **This is deliberate — do not silently
  reintroduce a fake sender for the test suite.**

  Accepted trade-offs, which the plan must account for:
  - `RESEND_API_KEY` becomes **required** to run the API and the test suite.
  - This knowingly **relaxes the dev-simplicity constraint** in `.claude/CLAUDE.md`
    ("SQL Server LocalDB + `next dev` + `dotnet run` must be enough to run the whole
    system locally"). That doc should be updated to match.
  - The test suite becomes network-dependent, burns Resend quota on every run, and
    can go flaky when Resend is slow. Phase 1's 49 tests currently run fully offline.
  - The recommended alternative (real sends in Development, fake sender in the
    `Testing` environment, reusing the existing `IsEnvironment("Testing")` branch in
    `Program.cs`) was presented and declined.
- **D-13:** The Resend API key lives in **`dotnet user-secrets` (dev)** and an
  **environment variable (prod)** — **never `appsettings.json`**. gitleaks runs on the
  pre-commit hook and in CI and would block such a commit anyway.

### Legacy booking migration

- **D-14:** **New `Appointment` entity; retire `Booking` wholesale.** Add
  `Appointment` (ServiceId FK, StylistId FK, `DateTimeOffset StartsAt`, Status)
  alongside `AppointmentSlot`, `Stylist`, `StylistWorkingHours`, and `StylistTimeOff`.
  **Drop** the `Booking` entity, the `Bookings` table, `BookingsController`, and the
  `BookingRequestForm` component in the same phase. This honors Phase 1's D-19
  ("Phase 2 rebuilds booking wholesale") and leaves no half-migrated free-text field
  behind. Existing `Booking` rows are dev/test data with no production users — nothing
  real is lost, so no backfill is required.
- **D-15:** The booking flow is a **single `/book` page with progressive reveal**:
  choosing a service reveals the stylist picker, which reveals a date + slot grid,
  which reveals the contact fields (name, email, phone — this is a **guest booking**;
  accounts arrive in Phase 7). This preserves the existing `?service={slug}` deep link
  that the Phase 1 service-detail CTA already points at (D-04 of Phase 1). Slot
  availability is fetched client-side as the selected date changes.
- **D-16:** The salon timezone is a **configured IANA id** (e.g.
  `"America/New_York"`) under a `Salon` section in `appsettings.json`. Slot grids and
  confirmations **always render in salon-local time with the zone explicitly labelled**
  ("Fri 10 Jul, 10:00 AM EDT"), never converted to the browser's timezone. A
  single-location salon has exactly one meaningful clock. Satisfies BOOK-05; SC5
  requires this be verified correct across a DST-transition date.

### Claude's Discretion

- Exact FluentValidation rules for appointment create DTOs (field lengths, lead-time
  bounds, email/phone format).
- The concrete SQL/LINQ shape of the open-slot query, and whether slot computation
  lives in a `SlotService` separate from an `AppointmentsService`.
- Tie-breaking rule for "Any stylist" assignment (lowest `StylistId`, least-booked
  that day, or round-robin) — any deterministic rule is acceptable.
- Entity/DTO naming, mapper extension placement, and feature-folder layout (must
  follow the `Features/Services` template established in Phase 1).
- How far ahead clients may book (booking horizon), minimum lead time, and whether
  same-day booking is permitted — **not discussed**; pick sensible salon defaults and
  flag them as owner-reviewable, exactly as Phase 1 did for seed prices (D-15).
- The `/book` page's visual design, empty states, and the recovery UX when a 409
  "slot taken" comes back after another client claims the slot mid-flow.
- Whether the frontend API client is OpenAPI-generated or hand-written (OpenAPI
  remains the source of truth either way).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` § "Phase 2: Booking Core" — goal, the five Success Criteria,
  the `Depends on` note, and the research flag
- `.planning/REQUIREMENTS.md` — BOOK-01 … BOOK-06 (lines 23–28); BOOK-04 and BOOK-05
  are the correctness-critical ones
- `.planning/STATE.md` § "Blockers/Concerns" — Phase 2 is flagged for a research pass
  before planning

### Prior-phase decisions that bind this phase
- `.planning/phases/01-service-catalog/01-CONTEXT.md` — **D-06** (single fixed
  `Price`), **D-07** (`DurationMinutes` as a plain int, consumed directly by slot
  math), **D-04** (service-detail CTA already targets `/book?service={slug}`),
  **D-19** (`BookingsController` deliberately not refactored — "Phase 2 rebuilds
  booking wholesale"), **D-12/D-13** (the `HasData` seeding pattern to mirror for
  `Stylist`), **D-17** (`Result<T>` + ProblemDetails translation), **D-18** (Zod
  response validation on the frontend)
- `.planning/phases/01-service-catalog/01-VERIFICATION.md` — confirms the
  `ServicesService` layer boundary (PLAT-01) that this phase must also honor

### Project constraints
- `.claude/CLAUDE.md` — core value ("Booking a salon appointment is effortless… If
  everything else fails, this must work"), feature-folder backend architecture,
  OpenAPI as source of truth, gitleaks secret-scanning, and the dev-simplicity
  constraint that **D-12 knowingly relaxes**
- `.planning/codebase/CONVENTIONS.md` — naming, DTO/mapper, and error-handling
  conventions
- `.planning/codebase/TESTING.md` — existing test harness shape (49 tests, currently
  fully offline)

### Existing code this phase replaces or extends
- `API/ZachHairStudio.Shared/Features/Bookings/Booking.cs` — the legacy free-text
  entity being retired (`Service` string, `PreferredDate` as `DateTime`)
- `API/ZachHairStudio.Api/Controllers/BookingsController.cs` — queries `DbContext`
  directly; being deleted
- `API/ZachHairStudio.Shared/Features/Services/` — **the template to follow** for the
  new feature folder (entity, DTOs, validators, mappers, service layer)
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` — `OnModelCreating` fluent config
  and `HasData` seeding to mirror
- `landing-page/app/book/page.tsx` — existing route that the new flow replaces
- `landing-page/lib/data.ts` — static `team[]` array that seeds `Stylist`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`Features/Services/` feature folder** — the exact entity → DTO → validator →
  mapper → service-layer template Phase 1 established and PLAT-01 verified. The new
  Appointments and Stylists features copy this shape.
- **`Result<T>`** (`API/ZachHairStudio.Shared/Result.cs`) — activated in Phase 1 via
  D-17. `AppointmentsService` returns it; the controller translates `ValidationError`
  → 400 ProblemDetails, `NotFound` → 404, and a new **conflict → 409** for "slot
  taken".
- **`fetchServices()` + Zod `ServiceSchema`** (`landing-page/lib/services.ts`) — the
  established frontend data-fetch + response-validation pattern (D-18) to copy for
  slots and appointments.
- **`Program.cs` `IsEnvironment("Testing")` branch** — an existing seam that already
  special-cases the Testing environment for the migration bootstrap.
- **`HasData` seeding in `OnModelCreating`** — proven in Phase 1 for 6 services;
  reused for `Stylist` and `StylistWorkingHours`.

### Established Patterns

- **Controllers never touch `BookingDbContext`** (PLAT-01, verified in Phase 1). All
  data access lives in the feature's service class.
- **FluentValidation** on write DTOs, surfaced as ProblemDetails (PLAT-02).
- **Feature folders, not technical layers** — group by feature per `CLAUDE.md`.
- **EF migrations own the schema.** `Program.cs` calls `db.Database.Migrate()` at
  startup (skipped in the Testing environment).
- **`decimal` money with `HasPrecision(18, 2)`**; enums stored as strings via
  `HasConversion<string>()` — `AppointmentStatus` follows suit.

### Integration Points

- **`Service.DurationMinutes`** drives how many 15-minute grid cells a booking
  occupies — the direct dependency ROADMAP declares in Phase 2's `Depends on`.
- **`/book?service={slug}`** — the Phase 1 CTA target; the deep link must keep working
  and preselect the service.
- **`BookingDbContext`** gains `Appointments`, `AppointmentSlots`, `Stylists`,
  `StylistWorkingHours`, and `StylistTimeOff` DbSets, and loses `Bookings`.
- **Phase 3** consumes `Appointment` + its status enum for the staff schedule; **Phase
  4** makes `StylistWorkingHours`/`StylistTimeOff` staff-editable. Both read the model
  this phase creates — design for that, but do not build it here.

</code_context>

<specifics>
## Specific Ideas

- The 409 path is a first-class, testable outcome, not an edge case: two
  near-simultaneous bookings for the same stylist and slot must yield **exactly one
  success and one clean "slot taken" rejection**, enforced by the unique index (SC4).
  A concurrency test that fires overlapping inserts is the proof.
- SC5 requires correctness across a **DST-transition date** specifically. A test that
  books across the salon timezone's spring-forward/fall-back boundary is the proof —
  not a generic timezone test.
- The on-screen confirmation carries **full appointment details** (service, stylist by
  name, salon-local date/time with the zone labelled, duration, price), because per
  D-11 the email is best-effort and may never arrive.
- Slot grid rendering shows the labelled zone alongside times ("10:00 AM EDT") so a
  client in another timezone is never misled.

</specifics>

<deferred>
## Deferred Ideas

- **Retiring the static `team[]` content** in favor of an API-backed Team section
  (mirroring what D-14 of Phase 1 did for services). The `Stylist` entity created here
  makes it possible; the UI work is out of Phase 2 scope.
- **`Service`↔`Stylist` capability matrix** — only qualified stylists offer a given
  service. Belongs with Phase 4 staff management.
- **Transactional-outbox retry for confirmation emails** — a durable outbox table plus
  a `BackgroundService` retrying with backoff. Considered and set aside for D-11's
  simpler best-effort send; there are no background jobs in this system yet. Revisit
  in Phase 8 hardening if undelivered confirmations prove to be a real problem.
- **Booking horizon, minimum lead time, and same-day booking rules** — surfaced but not
  discussed. Left as Claude's discretion with owner-reviewable defaults; may warrant an
  explicit owner decision later.
- **"Slot just got taken" recovery UX** on the `/book` page when a 409 returns
  mid-flow. Left to design; worth a `/gsd-ui-phase 2` pass since the phase carries a
  `UI hint: yes`.

</deferred>

---

*Phase: 02-booking-core*
*Context gathered: 2026-07-09*
