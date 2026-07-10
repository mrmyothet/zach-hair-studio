# Requirements: Zach Hair Studio

**Defined:** 2026-07-07
**Core Value:** Booking a salon appointment is effortless — browsing services and reserving a slot is the primary, friction-free path.

## v1 Requirements

Requirements for the current milestone (full services-led platform, specs roadmap P1–8). Each maps to a roadmap phase. Categories follow the specs' phase order (services first, commerce and accounts after).

### Platform (cross-cutting)

- [x] **PLAT-01**: API features are served through a per-feature service layer; controllers do not call `DbContext` directly
- [x] **PLAT-02**: Input validation runs through a dedicated validation layer (FluentValidation on the API; Zod on the frontend), not only DataAnnotations

### Service Catalog (P1)

- [x] **CAT-01**: Client can browse a list of services showing name, description, duration, and price
- [x] **CAT-02**: Client can open a service detail page for a single service
- [x] **CAT-03**: Services are backed by a `Service` entity with list + detail API endpoints

### Booking Core (P2)

- [x] **BOOK-01**: Client can see real open appointment slots for a chosen service, reflecting stylist working hours and existing bookings
- [x] **BOOK-02**: Client can book an appointment by picking a service, then an open slot, then confirming
- [x] **BOOK-03**: Client receives an on-screen and email confirmation for a booked appointment
- [x] **BOOK-04**: The system prevents double-booking a stylist for the same slot, enforced server-side with a database-level guarantee
- [x] **BOOK-05**: Appointment and availability times are stored with timezone-aware types (`DateTimeOffset`) against a configured salon timezone
- [x] **BOOK-06**: Client can choose a preferred stylist during booking (slots filtered by stylist)

### Staff Dashboard — Schedule (P3)

- [ ] **DASH-01**: Staff can view the day's and week's appointments in a schedule dashboard
- [ ] **DASH-02**: Staff can open an appointment to view its details
- [ ] **DASH-03**: Staff can update an appointment's status (confirmed, completed, cancelled, no-show)
- [ ] **DASH-04**: "No-show" is a distinct terminal status, separate from "cancelled"
- [ ] **DASH-05**: The staff dashboard and its API are behind an authentication gate (staff-only; not publicly accessible)

### Staff Management — Services & Availability (P4)

- [ ] **MGMT-01**: Staff can create, edit, and retire services (name, description, duration, price)
- [ ] **MGMT-02**: Staff can manage stylist availability (working hours, breaks, time off) feeding the P2 slot logic
- [ ] **MGMT-03**: Availability edits are checked against existing confirmed bookings and surface conflicts

### Product Catalog (P5)

- [ ] **PROD-01**: Client can browse a list of products showing name, description, price, image, and stock
- [ ] **PROD-02**: Client can open a product detail page
- [ ] **PROD-03**: A service detail page surfaces stylist-recommended product add-ons via a curated service→product mapping

### Cart & Checkout (P6)

- [ ] **SHOP-01**: Client can add products to a cart and review it
- [ ] **SHOP-02**: Client can check out and pay through an integrated payment provider
- [ ] **SHOP-03**: Order total is computed server-side from the catalog; client-supplied prices are never trusted
- [ ] **SHOP-04**: Product stock is decremented atomically on order creation, with no overselling under concurrent checkout
- [ ] **SHOP-05**: Order fulfillment is confirmed only via a verified payment webhook, not the client redirect
- [ ] **SHOP-06**: Guest checkout works without an account (`Order.ClientId` nullable)
- [ ] **SHOP-07**: Stylist-recommended add-ons are surfaced at checkout

### Accounts & Retention (P7)

- [ ] **ACCT-01**: Client can create an account and log in
- [ ] **ACCT-02**: Client can view their booking history
- [ ] **ACCT-03**: Client can view their order history
- [ ] **ACCT-04**: Client can cancel or reschedule their own upcoming appointment (self-service)
- [ ] **ACCT-05**: Client accounts and staff authentication share a single ASP.NET Core Identity setup (one schema/migration)
- [ ] **ACCT-06**: A client can access only their own bookings and orders (ownership checks prevent IDOR)
- [ ] **ACCT-07**: Loyalty groundwork — client earns points per completed appointment, redeemable as a discount

### Polish & Launch Readiness (P8)

- [ ] **LAUNCH-01**: Public site and dashboard pass a responsive/mobile and visual-polish review
- [ ] **LAUNCH-02**: CORS is restricted to known origins in production (no `AllowAnyOrigin`)
- [ ] **LAUNCH-03**: Production SQL Server is configured and schema is applied via a controlled migration path (not startup `db.Database.Migrate()`)
- [ ] **LAUNCH-04**: The API emits structured logs across requests and key operations
- [ ] **LAUNCH-05**: Sensitive endpoints (auth, checkout) have basic hardening (rate limiting)

## v2 Requirements

Deferred to a future release. Tracked but not in the current roadmap.

### Notifications

- **NOTF-01**: Automated appointment reminders (email/SMS) ahead of the appointment
- **NOTF-02**: SMS booking confirmations (in addition to email)

### Retention & Revenue

- **RETN-01**: Deposit / no-show-fee capture at booking time (depends on payment provider from P6)
- **RETN-02**: Tiered loyalty program beyond simple points
- **RETN-03**: "Book again" / rebook-last-service shortcut from booking history

### Dashboard

- **DASH2-01**: Real-time (push) dashboard sync across staff/front-desk views

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Marketplace / third-party seller listings | Single-salon storefront, not a multi-tenant marketplace; discovery is the salon's own marketing responsibility |
| Multi-location / franchise management | Single-location schema assumption; adds a location dimension to every model for no benefit to one salon |
| Native mobile apps (iOS/Android) | Responsive web covers mobile; native roughly doubles the maintenance surface |
| Client-facing product reviews / ratings | Moderation and spam burden for a secondary catalog; staff recommend products in person |
| Subscription / membership billing | Recurring-billing complexity on top of a deferred one-time payment decision |
| General-purpose product recommendation engine | Overkill for a small curated catalog; the curated service→product mapping (PROD-03) is the on-brand alternative |

## Traceability

Which phases cover which requirements. Populated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| PLAT-01 | Phase 1 | Complete |
| PLAT-02 | Phase 1 | Complete |
| CAT-01 | Phase 1 | Complete |
| CAT-02 | Phase 1 | Complete |
| CAT-03 | Phase 1 | Complete |
| BOOK-01 | Phase 2 | Complete |
| BOOK-02 | Phase 2 | Complete |
| BOOK-03 | Phase 2 | Complete |
| BOOK-04 | Phase 2 | Complete |
| BOOK-05 | Phase 2 | Complete |
| BOOK-06 | Phase 2 | Complete |
| DASH-01 | Phase 3 | Pending |
| DASH-02 | Phase 3 | Pending |
| DASH-03 | Phase 3 | Pending |
| DASH-04 | Phase 3 | Pending |
| DASH-05 | Phase 3 | Pending |
| MGMT-01 | Phase 4 | Pending |
| MGMT-02 | Phase 4 | Pending |
| MGMT-03 | Phase 4 | Pending |
| PROD-01 | Phase 5 | Pending |
| PROD-02 | Phase 5 | Pending |
| PROD-03 | Phase 5 | Pending |
| SHOP-01 | Phase 6 | Pending |
| SHOP-02 | Phase 6 | Pending |
| SHOP-03 | Phase 6 | Pending |
| SHOP-04 | Phase 6 | Pending |
| SHOP-05 | Phase 6 | Pending |
| SHOP-06 | Phase 6 | Pending |
| SHOP-07 | Phase 6 | Pending |
| ACCT-01 | Phase 7 | Pending |
| ACCT-02 | Phase 7 | Pending |
| ACCT-03 | Phase 7 | Pending |
| ACCT-04 | Phase 7 | Pending |
| ACCT-05 | Phase 7 | Pending |
| ACCT-06 | Phase 7 | Pending |
| ACCT-07 | Phase 7 | Pending |
| LAUNCH-01 | Phase 8 | Pending |
| LAUNCH-02 | Phase 8 | Pending |
| LAUNCH-03 | Phase 8 | Pending |
| LAUNCH-04 | Phase 8 | Pending |
| LAUNCH-05 | Phase 8 | Pending |

**Coverage:**

- v1 requirements: 41 total (corrected during roadmap creation — the actual v1 list above totals 41 requirements, not the 34 noted when this file was first drafted)
- Mapped to phases: 41
- Unmapped: 0 ✓

---
*Requirements defined: 2026-07-07*
*Last updated: 2026-07-08 after Phase 1 Plan 02 execution (PLAT-01, PLAT-02, CAT-03 marked complete)*
