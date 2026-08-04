# US3 — Staff Dashboard: Schedule (Phase 3)

> **User story:** As staff, I want a private, authenticated schedule where I can see the
> day's/week's appointments and manage their status — including a first-class no-show —
> so I can run the salon.
>
> **Requirements:** DASH-01, DASH-02, DASH-03, DASH-04, DASH-05

## Prerequisites

- Complete the [one-time setup](./README.md#one-time-setup-do-this-before-any-guide).
- API at http://localhost:5236, dashboard at http://localhost:3001.
- You know the **Owner login** (`Owner:Email` / `Owner:InitialPassword` from user-secrets —
  e.g. `owner@zachhairstudio.local`).
- **Create at least one appointment first** (run US2 Scenario 3) so the schedule has data.
  Note the date you booked — you'll navigate to it in the dashboard.

---

## Scenario 1 — Dashboard/API is gated behind auth (DASH-05)

**Steps**

1. Open a **fresh/incognito** browser (no existing login).
2. Go directly to http://localhost:3001/schedule (skip the login page).
3. Separately, call the API without a token:
   `GET http://localhost:5236/api/schedule`.

**Expected result**

- The dashboard `/schedule` route **redirects you to `/login`** (or blocks access) — you
  cannot see appointments unauthenticated.
- The API returns **401 Unauthorized** (JSON), **not** a data payload and **not** a
  cookie-login redirect.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 2 — Staff login (DASH-05)

**Steps**

1. Open http://localhost:3001/login.
2. Enter the Owner email and password.
3. Submit.
4. Also try a **wrong** password once.

**Expected result**

- Valid credentials log you in and land you on the schedule (or dashboard home).
- Invalid credentials show a clear error and **do not** log you in.
- (API check, optional) `POST http://localhost:5236/api/auth/login` with the correct
  body returns a **JWT token**; wrong credentials return **401**.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 3 — View the day's and week's appointments (DASH-01)

**Steps**

1. Logged in, open the schedule.
2. Navigate to the **date you booked** in the prerequisites.
3. Toggle between **day** and **week** views.

**Expected result**

- The appointment(s) you created appear on the correct day and time (salon-local,
  Asia/Yangon).
- Day view shows a single day; week view shows the surrounding week with your
  appointment placed correctly.
- Each appointment tile shows enough to identify it (client name, service, time).

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 4 — Open an appointment's details (DASH-02)

**Steps**

1. Click one appointment in the schedule.

**Expected result**

- A detail view/panel opens showing the appointment's full details: client name,
  contact, service, stylist, salon-local date & time, duration/price, and current status.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 5 — Update appointment status (DASH-03)

**Steps**

1. Open an appointment currently **Pending/Confirmed**.
2. Change its status to **Confirmed**, then **Completed** (use the status action).
3. Observe the schedule after each change.

**Expected result**

- The status update succeeds and the new status is reflected immediately (or after the
  next poll refresh) in both the detail view and the schedule tile.
- Allowed transitions behave sensibly; an invalid transition is rejected with a clear
  message rather than silently corrupting state.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 6 — No-show is a distinct terminal status (DASH-04)

**Steps**

1. Open an appointment.
2. Set its status to **No-show**.
3. Separately, set a different appointment to **Cancelled**.
4. Compare how the two are shown/queried.

**Expected result**

- **No-show** is a first-class status **distinct from Cancelled** — labeled separately,
  not folded into "cancelled".
- Both are terminal (you can't casually flip a no-show back to confirmed as if nothing
  happened, per the allowed-transition rules).
- (API check, optional) `PATCH http://localhost:5236/api/schedule/{id}/status` with
  `{"newStatus":"NoShow"}` returns the updated appointment with status `NoShow`; the two
  statuses are separately reportable.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Scenario 7 — Owner-only: add a staff user (DASH-05, Owner role) *(optional)*

**Steps**

1. Logged in as **Owner**, open http://localhost:3001/staff/new.
2. Create a new staff user (email, display name, password).
3. Submit.

**Expected result**

- The Owner can create the staff user (success / 201 Created).
- (API check, optional) `POST http://localhost:5236/api/staff-users` **without** an Owner
  token, or with a non-Owner token, is rejected (**401/403**) — only the Owner role may
  create staff.

**Result**

- [ ] Pass / note: ____________________________________________

---

## Sign-off

| Requirement | Covered by | Pass? |
|-------------|-----------|-------|
| DASH-01 (day/week schedule) | Scenario 3 | [ ] |
| DASH-02 (appointment details) | Scenario 4 | [ ] |
| DASH-03 (status updates) | Scenario 5 | [ ] |
| DASH-04 (no-show distinct) | Scenario 6 | [ ] |
| DASH-05 (auth gate) | Scenarios 1, 2, 7 | [ ] |

**Overall US3:** ⬜ Pass  ⬜ Issues found (describe above)
