# US2 — Booking Core (Phase 2)

> **User story:** As a client, I want to pick a service and book a real open slot with
> my chosen stylist, so that my appointment is confirmed and never double-booked.
>
> **Requirements:** BOOK-01, BOOK-02, BOOK-03, BOOK-04, BOOK-05, BOOK-06

## Prerequisites

- Complete the [one-time setup](./README.md#one-time-setup-do-this-before-any-guide).
- API at http://localhost:5236, landing page at http://localhost:3000.
- `RESEND_API_KEY` set in user-secrets (Scenario 4 sends a real email).
- The salon is **open every day, 09:00–18:00 (Asia/Yangon)**, and **all four stylists**
  work daily — any future date within the booking horizon should return slots.

> ⚠️ **Known gap (already recorded, expect this):** the confirmation **email** is missing
> the **zone label, duration, and price** (it shows service, stylist, and time only). The
> on-screen confirmation is complete. Scenario 4 calls this out explicitly.

---

## Scenario 1 — See real open slots for a service (BOOK-01)

**Steps**

1. Open http://localhost:3000/book.
2. Select a service (e.g. **Precision Cut**).
3. When prompted, choose any future date (try a **Sunday** and a **weekday**).
4. Look at the slot grid.

**Expected result**

- Real open time slots appear for that date, within **09:00–18:00** salon-local time.
- Slots reflect service duration and existing bookings (already-booked times are absent).
- **Every day returns slots**, including Sunday and Monday — the salon opens seven
  days a week.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 2 — Filter slots by preferred stylist (BOOK-06)

**Steps**

1. Continue from Scenario 1 (service + date selected).
2. Select a specific stylist — pick **May Yoon**.
3. Observe the slot grid refresh.
4. Now select **Thiri Cho**.

**Expected result**

- With **May Yoon** selected, the grid narrows to her availability only.
- Switching back to "any stylist" restores the union across stylists.
- **Thiri Cho** (and **Sai Min Htet**) also show slots — all four stylists work daily.
- Booking a slot for one stylist removes it from that stylist's grid, but the same
  time may still be open for a different stylist.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 3 — Complete a booking end-to-end + on-screen confirmation (BOOK-02, BOOK-03)

**Steps**

1. Select service **Precision Cut**, a valid weekday date, and **Zin Min**.
2. Click an open slot.
3. Fill in contact details (first name, last name, email, phone).
4. Submit / confirm the booking.

**Expected result**

- The booking succeeds and an **on-screen confirmation panel** appears immediately
  (no page refresh needed).
- The confirmation shows **all five fields**: service name, the concrete stylist
  (Zin Min), salon-local **date**, salon-local **time with zone label**,
  **duration**, and **price**.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 4 — Confirmation email (BOOK-03) — *expect the known gap*

**Steps**

1. Use a **real email address** you can check in Scenario 3.
2. After booking, check that inbox (and spam).

**Expected result (as currently shipped)**

- An email from the salon's verified Resend domain **arrives**.
- It contains: service, stylist, and appointment time.
- ⚠️ It is currently **missing** the zone label, duration, and price. Per
  `02-VALIDATION.md` the bar is all five fields — so this scenario is expected to be a
  **partial pass / known gap** until the fix lands.

**Result** (mark Pass only if the email arrives; note the missing fields)

- [ ] Email arrived / note which fields were present vs. missing: __________________

---

## Scenario 5 — Double-booking is prevented (BOOK-04)

**Goal:** two attempts at the same stylist + slot → exactly one success, one clear rejection.

**Option A — via the UI (two browser windows)**

1. Open http://localhost:3000/book in **two** browser windows side by side.
2. In both, select the **same** service, **same** date, **same stylist**, and the **same slot**.
3. Fill contact details in both.
4. Submit both as close together as you can.

**Option B — via the API (deterministic)**

1. Get an open slot: `GET http://localhost:5236/api/appointments/slots?serviceId=1&date=<YYYY-MM-DD>`
   (use a valid weekday). Note a `startsAt` value and a `stylistId`.
2. Fire two `POST http://localhost:5236/api/appointments` with the **same** stylist +
   `startsAt` back-to-back (same JSON body).

**Expected result**

- **Exactly one** attempt succeeds (201 Created / on-screen confirmation).
- The other is cleanly rejected as **"slot taken" (HTTP 409 Conflict)** — not a 500,
  not a silent double-book.
- In the UI, the losing attempt shows a recoverable "slot taken" message and keeps the
  contact details entered (you can pick another slot without re-typing).

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 6 — Times stored timezone-aware (BOOK-05) *(technical, optional)*

**Steps**

1. After booking, fetch the slots or appointment via API and inspect the time value, e.g.
   `GET http://localhost:5236/api/appointments/slots?serviceId=1&date=<YYYY-MM-DD>`.

**Expected result**

- Time values carry an **offset of `+06:30`** (Asia/Yangon), i.e. `DateTimeOffset`
  format like `2026-07-24T09:00:00+06:30`, not a naive/UTC-only string.
- Salon-local times shown in the UI match the `+06:30` offset.

> Note: Asia/Yangon has no daylight saving, so the DST-transition clause of BOOK-05 is
> deliberately descoped for this deployment (documented decision).

**Result**

- [ ] Pass / note: ____________________________________________

---

## Sign-off

| Requirement | Covered by | Pass? |
|-------------|-----------|-------|
| BOOK-01 (real open slots) | Scenario 1 | [ ] |
| BOOK-02 (end-to-end booking) | Scenario 3 | [ ] |
| BOOK-03 (on-screen + email) | Scenarios 3 & 4 | [ ] (email = known gap) |
| BOOK-04 (no double-booking) | Scenario 5 | [ ] |
| BOOK-05 (timezone-aware storage) | Scenario 6 | [ ] |
| BOOK-06 (preferred stylist filter) | Scenario 2 | [ ] |

**Overall US2:** ⬜ Pass  ⬜ Issues found (describe above)
