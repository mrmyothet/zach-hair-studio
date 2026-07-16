# Zach Hair Studio

## What This Is

A services-led platform for a hair salon. The heart of the product is the salon
experience — clients discover styles and colors, then book styling and coloring
appointments online. Selling hair-related products is a supporting offering,
framed as stylist-recommended extensions of the service relationship. It serves
three audiences: clients (discover, book, optionally buy), staff (a dashboard to
run the salon), and the owner (a modern, attractive, maintainable site).

## Core Value

Booking a salon appointment is effortless — browsing services and reserving a
slot is the primary, friction-free path. If everything else fails, this must work.

## Business Context

- **Customer**: Salon clients (book services, buy recommended products); the salon owner operates it.
- **Revenue model**: Service appointments are the core; product sales are a supporting add-on to services.
- **Success metric**: A client can find a style/color and confirm an appointment in a few taps.
- **Strategy notes**: See `specs/mission.md` and `specs/roadmap.md` (services-first, small shippable phases).

## Requirements

### Validated

<!-- Inferred from existing code (see .planning/codebase/). Phase 0 foundation is shipped. -->

- ✓ Public landing page with salon marketing sections (hero, services, gallery, team, reviews, contact) — existing
- ✓ Client can submit a booking request via the public form (name, contact, service, preferred date, message) — existing
- ✓ Booking API: create booking, list bookings, get booking by id, update booking status — existing
- ✓ Bookings persist to SQL Server via EF Core migrations — existing
- ✓ Booking status lifecycle (Pending → Confirmed → Completed → Cancelled) — existing
- ✓ API surface documented via OpenAPI/Swagger — existing
- ✓ Frontend ↔ API round trip wired end-to-end (Phase 0 foundation) — existing
- ✓ **Service catalog (read-only)** — services modeled (slug, name, descriptions, category, duration, price, image, active, display order); list + detail API; public `/services` browse and `/services/[slug]` detail; homepage subset and booking dropdown both database-backed. Validated in Phase 1: Service Catalog (2026-07-09)
- ✓ **Per-feature service layer + validation layer** — `ServicesService` owns all `BookingDbContext` access; FluentValidation rejects invalid writes before they reach the database. Validated in Phase 1: Service Catalog (2026-07-09)
- ✓ **Booking core** — appointments + stylist availability; open-slot query; pick service → choose slot → confirm with DB-enforced double-booking prevention; confirmation email. Validated in Phase 2: Booking Core (2026-07-10; gap-closure 2026-07-16)
- ✓ **Staff dashboard (schedule)** — authenticated day/week schedule in `dashboard/`; appointment detail; Complete/Cancel/No-show with distinct no-show; staff auth gate. Validated in Phase 3: Staff Dashboard (Schedule) (2026-07-16)

### Active

<!-- Current milestone: full specs roadmap P1–8. Hypotheses until shipped and validated. -->

- [ ] **Staff management of services & availability** — dashboard CRUD for services; manage stylist availability feeding the slot logic
- [ ] **Product catalog (read-only)** — model products (name, description, price, image, stock); list + detail API; public browse surfaced as stylist-recommended add-ons
- [ ] **Cart & checkout** — cart on the public site; create order; decrement stock; integrate a payment provider and complete checkout
- [ ] **Accounts & retention** — client accounts and auth; booking & order history per client; loyalty/rewards groundwork
- [ ] **Polish & launch readiness** — responsive/mobile pass and visual polish; production SQL Server config, hosting/deploy, observability, basic hardening

### Out of Scope

<!-- From specs/mission.md "Out of scope (for now)". -->

- Marketplace / third-party sellers — not the business model; this is a single salon's own services and products
- Franchise / multi-location management — single-location focus for now
- Native mobile apps — a responsive web experience covers mobile; no native apps
- Client-facing product reviews/ratings, subscriptions — not part of the services-led v1 scope

## Context

- **Brownfield.** Phase 0 (foundation) is already shipped: the .NET API boots with EF Core + SQL Server, and the Next.js landing page calls it successfully via a working booking form. A `dashboard/` app is scaffolded but not built; a `mobile-app/` is only referenced.
- **Phases 1–3 shipped:** service catalog, slot-based booking with confirmation email, and an authenticated staff schedule dashboard (`dashboard/`) with status updates including distinct no-show. Next: staff self-serve services & availability (Phase 4).
- **Codebase map** lives in `.planning/codebase/` (STACK, ARCHITECTURE, STRUCTURE, CONVENTIONS, TESTING, INTEGRATIONS, CONCERNS).
- **Known concerns flagged during mapping** (see `.planning/codebase/CONCERNS.md`): open CORS (must be restricted before public deployment / Phase 8); staff JWT auth is in place for dashboard APIs (Phase 3); `db.Database.Migrate()` runs on startup.
- **Project skills exist** for this stack: `dev`, `ef-migrations`, `feature-scaffold`, `openapi-client` (see `specs/tooling.md`).

## Constraints

- **Tech stack**: Next.js 15 (App Router) + React 19 + Tailwind 4 for `landing-page/` (public) and `dashboard/` (staff); .NET 10 / ASP.NET Core + EF Core 10 / SQL Server for the API — matches the existing repo; new work aligns unless a deliberate decision updates `specs/tech-stack.md`.
- **Architecture**: Feature folders on the backend (group by feature, e.g. `Features/Bookings`), not by technical layer. TypeScript everywhere on the frontend. OpenAPI is the source of truth for API clients.
- **Dev simplicity**: SQL Server LocalDB + `next dev` + `dotnet run` must be enough to run the whole system locally.
- **Sequencing**: Services and the booking flow take priority at every step; product commerce is layered in only after the service experience is solid.
- **Security/Compliance**: gitleaks secret-scanning is wired via pre-commit hook and CI — keep secrets out of the repo.

## Key Decisions

<!-- Decisions that constrain future work. Add throughout project lifecycle. -->

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Services-led product (booking primary, products supporting) | Booking is the core value; products reinforce the service relationship | ✓ Good |
| Stack locked to Next.js + .NET / EF Core / SQL Server | Matches existing repo; maintainable, modern | ✓ Good |
| Separate `dashboard/` app for staff | Clear access boundaries and independent deployment | — Pending |
| Auth provider / session strategy (staff vs. client) | Deferred — decide when Phase 7 (accounts) / staff-dashboard access needs it | — Pending |
| Payment provider for product checkout | Deferred — decide when Phase 6 (cart & checkout) needs it | — Pending |
| Hosting / deployment targets | Deferred — decide in Phase 8 (launch readiness) | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Business Context check — customer, revenue model, success metric still accurate?
4. Audit Out of Scope — reasons still valid?
5. Update Context with current state

---
*Last updated: 2026-07-16 after Phase 3 (Staff Dashboard) completion*
