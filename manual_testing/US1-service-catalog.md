# US1 — Service Catalog (Phase 1)

> **User story:** As a client, I want to browse the salon's services and see everything
> I need to know about them, so that I can decide what to book.
>
> **Requirements:** CAT-01, CAT-02, CAT-03, PLAT-01, PLAT-02

## Prerequisites

- Complete the [one-time setup](./README.md#one-time-setup-do-this-before-any-guide).
- API running at http://localhost:5236, landing page at http://localhost:3000.
- Seeded services present (see [seed data reference](./README.md#services-catalog)).

---

## Scenario 1 — Browse the services list (CAT-01)

**Steps**

1. Open http://localhost:3000/services in a browser.
2. Wait for the page to load fully.
3. Look at the list/grid of services.

**Expected result**

- A list of services renders (the 6 seeded services above are present).
- Each service card shows **name, a short description, duration, and price**.
- Prices show as currency (e.g. `$35`) and durations in minutes/time (e.g. `45 min`).
- No error message, no empty state, no console errors (open DevTools → Console).

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 2 — Open a service detail page (CAT-02)

**Steps**

1. From http://localhost:3000/services, click one service card (e.g. **Color & Highlights**).
2. Confirm the URL becomes a per-service detail route, e.g. `/services/color-highlights`.
3. Read the detail page.

**Expected result**

- A dedicated detail page loads for that single service.
- It shows the service's **name, full description, duration, and price**.
- There is a clear **call-to-action to book** (e.g. a "Book" button/link) that points at
  the booking flow for this service (e.g. `/book?service=<slug>`).
- Refreshing the page (F5) still shows the correct service (deep link works).

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 3 — Booking CTA carries the service into the booking flow

**Steps**

1. On a service detail page, click the **Book** call-to-action.
2. Observe the booking page that opens.

**Expected result**

- You land on `/book` with the chosen service **pre-selected** (the service you came from).
- If you instead visit `/book?service=unknown-slug` manually, the form falls back to
  an empty/unselected service rather than crashing.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 4 — Homepage service subset

**Steps**

1. Open http://localhost:3000 (homepage).
2. Scroll to the services section.

**Expected result**

- The homepage shows a **subset** of services (first 6 by display order) with name,
  duration, and price — sourced from the same live API, not hardcoded.
- The booking form's service dropdown lists the real catalog services.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 5 — Catalog is API-backed (CAT-03) *(technical, optional)*

**Steps**

1. Open http://localhost:5236/api/services in a browser (or `curl`).
2. Open http://localhost:5236/api/services/precision-cut (or any seeded slug).

**Expected result**

- `/api/services` returns a JSON array of services with `name`, `description`,
  `durationMinutes`, `price`, `slug`.
- `/api/services/{slug}` returns the single matching service as JSON.
- An unknown slug (e.g. `/api/services/does-not-exist`) returns **404**, not 500.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 6 — Validation layer rejects bad service data (PLAT-02) *(technical, optional)*

> Only relevant if you exercise the write endpoint directly; there is no public
> client UI to create services yet (that's Phase 4).

**Steps**

1. POST an invalid service to `http://localhost:5236/api/services` — e.g. empty name
   and negative price:

   ```bash
   curl -i -X POST http://localhost:5236/api/services \
     -H "Content-Type: application/json" \
     -d '{"name":"","description":"x","durationMinutes":45,"price":-5}'
   ```

**Expected result**

- Response is **400 Bad Request** with a validation problem (RFC 7807 `ProblemDetails`)
  naming the invalid fields (name required, price must be positive).
- The bad record is **not** created (re-list `/api/services` to confirm no new row).

**Result**

- [ ] Pass / note: ____________________________________________

---

## Sign-off

| Requirement | Covered by | Pass? |
|-------------|-----------|-------|
| CAT-01 (browse list) | Scenario 1 | [ ] |
| CAT-02 (detail page) | Scenario 2 | [ ] |
| CAT-03 (list + detail API) | Scenario 5 | [ ] |
| PLAT-01 (service layer) | Scenarios 5–6 (behaviorally) | [ ] |
| PLAT-02 (validation layer) | Scenario 6 | [ ] |

**Overall US1:** ⬜ Pass  ⬜ Issues found (describe above)
