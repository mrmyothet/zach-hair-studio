# Roadmap

High-level implementation order, **services first**, in very small phases.
Each phase should be shippable on its own. Don't start the next phase until the
current one works end to end.

## Phase 0 — Foundation

- Confirm repo layout: `landing-page/`, `dashboard/`, `API/`.
- API boots with EF Core + SQLite; one trivial endpoint reachable.
- Frontend can call the API (one wired-up request round trip).

## Phase 1 — Service catalog (read-only)

- Model services (styles & colors): name, description, duration, price.
- API: list services + service detail.
- Public site: browse services and view a service detail page.

## Phase 2 — Booking core

- Model appointments and stylist availability.
- API: query open slots, create a booking.
- Public site: pick a service → choose a slot → confirm an appointment.

## Phase 3 — Staff dashboard (schedule)

- Dashboard lists the day's/week's appointments.
- Staff can view booking details and update status (confirmed, completed,
  cancelled, no-show).

## Phase 4 — Staff management of services & availability

- Dashboard CRUD for services.
- Dashboard manages stylist availability that feeds Phase 2 slot logic.

## Phase 5 — Product catalog (read-only)

- Model products: name, description, price, image, stock.
- API: list products + product detail.
- Public site: browse products; surface them as stylist-recommended add-ons.

## Phase 6 — Cart & checkout

- Cart on the public site.
- API: create order; decrement stock.
- Add a payment provider (decision required) and complete checkout.

## Phase 7 — Accounts & retention

- Client accounts and auth (decision required).
- Booking & order history per client.
- Loyalty / rewards groundwork.

## Phase 8 — Polish & launch readiness

- Responsive/mobile pass and visual polish.
- Production database decision and migration off SQLite.
- Hosting/deploy, observability, and basic hardening.

---

**Guiding rule:** services and the booking flow take priority at every step;
product commerce is layered in only after the service experience is solid.
