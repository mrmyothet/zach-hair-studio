# Roadmap: Zach Hair Studio

## Overview

Zach Hair Studio grows from the already-shipped Phase 0 foundation (API booting with EF Core + SQL Server, landing page wired to a free-text booking form) into the full services-led salon platform described in `specs/roadmap.md` and `.planning/REQUIREMENTS.md`. Phase 1 replaces the free-text booking form's backing model with a real service catalog and introduces the per-feature service layer that every later phase builds on. Phases 2-4 deliver the core value end to end — real slot-based booking with database-enforced double-booking prevention, a staff schedule dashboard, and staff-editable availability feeding the same slot logic Phase 2 reads from. Phases 5-6 layer in a curated, stylist-recommended product catalog and a trustworthy cart/checkout (server-authoritative pricing, atomic stock decrement, webhook-driven fulfillment). Phase 7 adds shared staff+client accounts on one Identity schema, self-service booking management, and loyalty groundwork, without blocking the guest checkout already shipped in Phase 6. Phase 8 hardens the whole system for production — responsive polish, restricted CORS, controlled migrations, structured logging, rate limiting — and retires the legacy `ZachHairStudio.Admin` scaffold in favor of `dashboard/`.

Each phase is a vertical slice (DB → API → UI) that is shippable and verifiable on its own before the next phase starts, per the user's explicit P1-8 scope and services-first sequencing.

## Phases

**Phase Numbering:**

- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Service Catalog** - Clients browse services (name, description, duration, price); API built on a real service layer from day one (completed 2026-07-09)
- [x] **Phase 2: Booking Core** - Clients pick a service, see real open slots, and confirm a double-booking-safe appointment (completed 2026-07-10)
- [x] **Phase 3: Staff Dashboard (Schedule)** - Staff view the day's/week's appointments and update status behind a staff-only auth gate (completed 2026-07-16)
- [x] **Phase 4: Staff Management (Services & Availability)** - Staff self-serve CRUD for services and stylist availability, conflict-checked against existing bookings (completed 2026-08-09)
- [x] **Phase 5: Product Catalog** - Clients browse a curated product catalog surfaced as stylist-recommended add-ons (completed 2026-08-09)
- [ ] **Phase 6: Cart & Checkout** - Clients buy recommended products through a trustworthy, server-authoritative checkout, as a guest or logged in
- [ ] **Phase 7: Accounts & Retention** - Clients get accounts (shared Identity with staff), booking/order history, self-service cancel/reschedule, and loyalty groundwork
- [ ] **Phase 8: Polish & Launch Readiness** - Responsive polish, production hardening, and retirement of the legacy Admin scaffold

## Phase Details

### Phase 1: Service Catalog

**Goal**: As a client, I want to browse the salon's services and see everything I need to know about them, so that I can decide what to book.
**Mode:** mvp
**Depends on**: Nothing new (builds on the shipped Phase 0 foundation)
**Requirements**: PLAT-01, PLAT-02, CAT-01, CAT-02, CAT-03
**Success Criteria** (what must be TRUE):

  1. Client can browse a list of services showing name, description, duration, and price
  2. Client can open a service detail page for a single service
  3. Submitting invalid service data (e.g., missing name, negative price) returns a clear validation error before it reaches the database
  4. Service catalog requests are handled by a dedicated `ServicesService` layer — controllers never query `BookingDbContext` directly (verified by code inspection)

**Plans**: 4 plans
Plans:
**Wave 1**

- [x] 01-01-PLAN.md — Backend test harness + Service domain model + FluentValidation validators (PLAT-02, CAT-03)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 01-02-PLAN.md — ServicesService + endpoints + DI + DbSet/unique-slug/seed migration (PLAT-01, CAT-03)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 01-03-PLAN.md — Public /services list + /services/[slug] detail pages (RSC + ISR + Zod) (CAT-01, CAT-02)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 01-04-PLAN.md — Single source of truth: API-backed homepage subset + Contact dropdown, retire lib/data.ts (D-14)

**UI hint**: yes

### Phase 2: Booking Core

**Goal**: As a client, I want to pick a service and book a real open slot with my chosen stylist, so that my appointment is confirmed and never double-booked.
**Mode:** mvp
**Depends on**: Phase 1 (needs `Service.DurationMinutes`/price for slot math and receipts)
**Requirements**: BOOK-01, BOOK-02, BOOK-03, BOOK-04, BOOK-05, BOOK-06
**Success Criteria** (what must be TRUE):

  1. Client can view real open slots for a chosen service that reflect stylist working hours and existing bookings (backed by a minimal/seeded availability model — the same model Phase 4 later makes staff-editable, not a second system)
  2. Client can complete a booking end-to-end on the public site — pick a service, pick a slot, confirm — and see an on-screen confirmation plus receive a confirmation email
  3. Client can optionally choose a preferred stylist during booking, with slots filtered to that stylist
  4. Two near-simultaneous booking attempts for the same stylist/slot result in exactly one success and one clear "slot taken" rejection, enforced by a database-level uniqueness/overlap guarantee, not just an app-level check
  5. Appointment and availability times are stored as `DateTimeOffset` against a configured salon IANA timezone, verified correct across a DST-transition date

**Plans**: 9/9 plans executed
Plans:
**Wave 1**

- [x] 02-01-PLAN.md — Booking domain foundation + Stylist read slice + [BLOCKING] AddBookingCore migration (unfiltered unique index) + retire legacy Booking wholesale, API+Admin (BOOK-04, BOOK-05, BOOK-06)

**Wave 2** *(blocked on Wave 1)*

- [x] 02-02-PLAN.md — Testability prerequisites: Resend account/domain/key human checkpoint + real SQL Server LocalDB test fixture (BOOK-03, BOOK-04, BOOK-05)
- [x] 02-03-PLAN.md — Open-slot query slice with DST-safe time math: SlotService + GET /api/appointments/slots (BOOK-01, BOOK-05, BOOK-06)

**Wave 3** *(blocked on Wave 2)*

- [x] 02-04-PLAN.md — Booking confirm slice: AppointmentsService retry loop + 409 guarantee + best-effort Resend email + SC4 concurrency & SC5 DST round-trip proofs (BOOK-02, BOOK-03, BOOK-04, BOOK-06)

**Wave 4** *(blocked on Wave 3)*

- [x] 02-05-PLAN.md — Public /book progressive-reveal UI + on-screen confirmation + 409 recovery + homepage repoint + frontend Booking teardown (BOOK-02, BOOK-03, BOOK-06)

**Wave 5** *(blocked on Wave 4)*

- [x] 02-06-PLAN.md — Human-verify: drive /book in a browser, confirm real email delivery + 409 recovery (BOOK-02..BOOK-06)

**Gap-closure (Wave 6)** *(from 02-VERIFICATION.md — added by `/gsd-plan-phase 2 --gaps`)*

- [x] 02-07-PLAN.md — De-date-bomb the booking test suite (relative-to-now helper), prove shipped create-path salon-offset on real SQL, record SC5 DST descope for Asia/Yangon (BOOK-03, BOOK-05)
- [x] 02-08-PLAN.md — [BLOCKING HUMAN] Fresh full-suite run + real booking email inspection for all five BOOK-03 fields (BOOK-03, BOOK-05)
- [x] 02-09-PLAN.md — Doc reconciliation only: reframe the owner-removed confirmation caption in 02-05/02-06 acceptance bars and refresh 02-VERIFICATION.md's stale `gaps_found` verdict with an evidence-cited reconciliation (commit ea8eb85 + UAT Tests 6/8/9); no source code touched (BOOK-03, BOOK-05)

**Research flag**: yes — highest-correctness-risk phase in the roadmap; run a focused research pass on DB-level uniqueness/overlap constraint design, DateTimeOffset/timezone strategy, and seeded-availability-model shape before planning (research complete — see 02-RESEARCH.md)
**UI hint**: yes

### Phase 3: Staff Dashboard (Schedule)

**Goal**: Staff have a private, authenticated schedule view where they can see what booking actually produced and manage appointment status, including a first-class no-show state. (Note: staff features build in `dashboard/` per the existing Key Decision — the scaffolded `ZachHairStudio.Admin` MVC project is legacy and should not receive new work; see Phase 8 for retirement.)
**Mode:** mvp
**Depends on**: Phase 2 (needs real appointments to display and act on)
**Requirements**: DASH-01, DASH-02, DASH-03, DASH-04, DASH-05
**Success Criteria** (what must be TRUE):

  1. Staff can view the day's and week's appointments in a schedule dashboard
  2. Staff can open an appointment to view its full details
  3. Staff can update an appointment's status to confirmed, completed, cancelled, or no-show
  4. "No-show" behaves as a distinct terminal status from "cancelled" — queryable and reportable separately, not folded into the same enum meaning
  5. Attempting to reach the dashboard or its API without staff authentication is rejected

**Plans**: 5/5 plans complete

**Wave 1**

- [x] 03-01-PLAN.md — Identity + JWT foundation + Owner seed + Appointment audit columns (DASH-05)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 03-02-PLAN.md — Auth login + Owner-only staff-user create (DASH-05)
- [x] 03-03-PLAN.md — Schedule API read + constrained status updates + no-show separability (DASH-01..04)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 03-04-PLAN.md — dashboard/ scaffold + OpenAPI client + staff login/guard (DASH-05 frontend)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 03-05-PLAN.md — Day/week schedule UI + detail + status actions + polling + Owner add-staff (DASH-01..04 frontend)

**UI hint**: yes

### Phase 4: Staff Management (Services & Availability)

**Goal**: As a salon staff member, I want to keep the service catalog and stylist availability accurate from the dashboard without a code deploy, so that clients always see and book real services and open slots, and no availability edit silently orphans a confirmed booking.
**Mode:** mvp
**Depends on**: Phase 1 (service schema), Phase 2 (availability model to make staff-editable), Phase 3 (dashboard app + staff auth boundary)
**Requirements**: MGMT-01, MGMT-02, MGMT-03
**Success Criteria** (what must be TRUE):

  1. Staff can create, edit, and retire a service (name, description, duration, price) from the dashboard
  2. Staff can manage a stylist's working hours, breaks, and time off from the dashboard, and Phase 2's open-slot query immediately reflects the change (same availability model, not a second one)
  3. Attempting to save an availability edit that conflicts with an existing confirmed booking surfaces the conflict instead of silently applying it

**Plans**: 7/7 plans executed
Plans:
**Wave 1**

- [x] 04-01-PLAN.md — Services backend: action-level Owner-gate on writes + image-upload endpoint + static-file serving (MGMT-01)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 04-02-PLAN.md — Services frontend: shared DashboardNav + /services CRUD page + ServiceForm + ImageUploadField + OpenAPI regen (MGMT-01)
- [x] 04-03-PLAN.md — Availability backend: working-hours replace + time-off write path feeding SlotService, any-staff gate (MGMT-02)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 04-04-PLAN.md — Availability frontend: StylistPicker + WeekStripEditor + TimeOffCalendar + single Save Changes (MGMT-02)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 04-05-PLAN.md — Conflict hard-block: SalonTimeZone.ToSalonLocal + full-final-state conflict scan + 409 + inline ConflictList (MGMT-03)

**Wave 5** *(gap closure from UAT — blocked on Wave 4 completion)*

- [x] 04-06-PLAN.md — Gap G-04-5: WeekStripEditor commits the drag range from pointerup via a ref (no setState during a sibling's render) + ESLint state-updater purity guard (MGMT-02)

**Wave 6** *(gap closure from UAT — blocked on Wave 5 completion)*

- [x] 04-07-PLAN.md — Gap G-04-6: WeekStripEditor edge-resize handle to shrink an existing working-hours segment, direct-replace commit bypassing mergeSegments (MGMT-02)

**UI hint**: yes

### Phase 5: Product Catalog

**Goal**: As a client, I want to browse a curated, stylist-recommended product catalog tied to the services I care about, so that I can find products my stylist actually recommends without wading through a general storefront.
**Mode:** mvp
**Depends on**: Phase 4 (sequenced after the service experience is complete, per the services-first priority — no functional dependency on Phase 4 itself)
**Requirements**: PROD-01, PROD-02, PROD-03
**Success Criteria** (what must be TRUE):

  1. Client can browse a list of products showing name, description, price, image, and stock
  2. Client can open a product detail page
  3. A service detail page surfaces a curated set of stylist-recommended products tied to that specific service

**Plans**: 2/2 plans executed
Plans:
**Wave 1**

- [x] 05-01-PLAN.md — Product/ServiceRecommendedProduct backend: entity, DTOs, ProductsService/Controller, AddProducts migration + seed, extended ServicesService recommendations (PROD-01, PROD-02, PROD-03)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 05-02-PLAN.md — /products catalog + detail pages, extended ServiceSchema, Recommended Products section on service detail (PROD-01, PROD-02, PROD-03)

**UI hint**: yes

### Phase 6: Cart & Checkout

**Goal**: As a client, I want to add recommended products to a cart and check out as a guest with trustworthy, server-verified pricing and stock, so that I can complete a real purchase without creating an account.
**Mode:** mvp
**Depends on**: Phase 5 (needs product price/stock to sell)
**Requirements**: SHOP-01, SHOP-02, SHOP-03, SHOP-04, SHOP-05, SHOP-06, SHOP-07
**Success Criteria** (what must be TRUE):

  1. Client can add products to a cart, review it on a cart page, and complete checkout through an integrated payment provider as a guest — no account required (`Order.ClientId` nullable)
  2. Order totals are always recomputed server-side from the product catalog; a tampered client-submitted price/total has no effect on the amount actually charged
  3. Concurrent checkout attempts against the last unit of a product result in exactly one successful order; stock never goes negative
  4. An order is marked fulfilled only after a verified payment-provider webhook fires, never from the client's post-payment redirect alone
  5. Stylist-recommended add-ons are surfaced both on the service detail page and again at checkout

**Plans**: 4/5 plans executed
Plans:
**Wave 1**

- [x] 06-01-PLAN.md — Guest cart API: ConflictError overload, Cart/CartItem, CartsService/Controller, AddCarts migration (SHOP-01)

**Wave 2** *(blocked on Wave 1)*

- [x] 06-02-PLAN.md — Guest cart UI: session header, Add to Cart, Navbar badge, /cart review (SHOP-01)
- [x] 06-03-PLAN.md — Checkout API + fake IPaymentProvider: server totals, atomic stock, guest ClientId null (SHOP-02, SHOP-03, SHOP-04, SHOP-06)

**Wave 3** *(blocked on Wave 2)*

- [x] 06-04-PLAN.md — Checkout UI + SHOP-07 chips + success/cancel display-only (SHOP-02, SHOP-05, SHOP-07)

**Wave 4** *(blocked on Wave 3)*

- [ ] 06-05-PLAN.md — Stripe.net + webhook fulfillment + StockConcurrencyTests + Stripe CLI human verify (SHOP-02, SHOP-04, SHOP-05)

**Research flag**: yes — highest external-integration risk after Phase 2; run a focused research pass on the payment provider integration (Stripe.net), webhook-verified fulfillment, idempotency, and atomic stock-decrement mechanics before planning
**UI hint**: yes

### Phase 7: Accounts & Retention

**Goal**: As a client, I want to create an account to see my booking and order history and manage my upcoming appointments myself, so that I do not have to call the salon for things I can handle on my own.
**Mode:** mvp
**Depends on**: Phase 2 (booking history source), Phase 3 (extends the lightweight staff auth scheme into full Identity — one schema, not two), Phase 6 (order history source; guest checkout already shipped independently so this phase is additive, not blocking)
**Requirements**: ACCT-01, ACCT-02, ACCT-03, ACCT-04, ACCT-05, ACCT-06, ACCT-07
**Success Criteria** (what must be TRUE):

  1. Client can create an account, log in, and view their booking and order history from an account page
  2. Client can cancel or reschedule their own upcoming appointment from their account (self-service)
  3. A client can only ever fetch their own bookings/orders — attempting to access another client's records by ID is rejected (no IDOR)
  4. Staff authentication and client accounts share a single ASP.NET Core Identity schema/migration — not two separate auth stores
  5. A client earns a loyalty point for each completed appointment, visible in their account and redeemable as a discount

**Plans:** 2/4 plans executed

Plans:
**Wave 1**

- [x] 07-01-PLAN.md — Client register/login E2E: Client role + JWT + landing /account auth + Navbar

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 07-02-PLAN.md — Ownership-gated bookings/orders history + claim-by-email + Bookings|Orders tabs

**Wave 3** *(blocked on Wave 2 completion)*

- [ ] 07-03-PLAN.md — Self-service cancel + transactional cancel-and-rebook reschedule

**Wave 4** *(blocked on Wave 3 completion)*

- [ ] 07-04-PLAN.md — LoyaltyLedger earn on Completed + server checkout redeem

**Research flag**: yes — auth provider/session strategy is an explicit open decision in PROJECT.md; re-verify the ASP.NET Core Identity vs. Auth.js/Better Auth landscape immediately before planning this phase
**UI hint**: yes

### Phase 8: Polish & Launch Readiness

**Goal**: As a salon owner, I want to launch on a responsive, secure-by-default, observable site running a properly migrated production database with the legacy Admin scaffold retired, so that I can go live with confidence and no lingering legacy risk.
**Mode:** mvp
**Depends on**: Phase 7 (and all prior phases — this is the final hardening/launch pass over the complete system)
**Requirements**: LAUNCH-01, LAUNCH-02, LAUNCH-03, LAUNCH-04, LAUNCH-05
**Success Criteria** (what must be TRUE):

  1. Public site and dashboard pass a responsive/mobile and visual-polish review across common breakpoints
  2. Production CORS accepts only known origins (no `AllowAnyOrigin`), and the legacy `ZachHairStudio.Admin` MVC project is removed/retired in favor of `dashboard/`
  3. Production SQL Server schema is applied via a controlled migration path (`dotnet ef database update` in a deploy step), not startup `db.Database.Migrate()`
  4. The API emits structured logs across requests and key operations (bookings, checkout, auth)
  5. Auth and checkout endpoints have basic rate limiting in place

**Plans**: TBD
**UI hint**: yes

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8

| Phase | Plans Complete | Status | Completed |
|-------|-----------------|--------|-----------|
| 1. Service Catalog | 4/4 | Complete    | 2026-07-09 |
| 2. Booking Core | 9/9 | Complete    | 2026-08-09 |
| 3. Staff Dashboard (Schedule) | 5/5 | Complete    | 2026-07-16 |
| 4. Staff Management (Services & Availability) | 7/7 | Complete    | 2026-08-09 |
| 5. Product Catalog | 2/2 | Complete    | 2026-08-09 |
| 6. Cart & Checkout | 4/5 | In Progress|  |
| 7. Accounts & Retention | 2/4 | In Progress|  |
| 8. Polish & Launch Readiness | 0/TBD | Not started | - |
