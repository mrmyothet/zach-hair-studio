# Manual Testing Guides — Zach Hair Studio

Step-by-step manual test guides for every **user story implemented so far** (Phases 1–3).
Each guide is self-contained: prerequisites, exact steps, expected results, and a
pass/fail checkbox per scenario.

> Scope: this covers what is actually built and shippable today — the **client-facing
> service catalog**, the **client booking flow**, and the **staff schedule dashboard**.
> Phase 4+ (staff self-service management, products, cart, accounts) is not built yet
> and has no guide here.

## User stories covered

| # | Guide | User story | Requirements |
|---|-------|------------|--------------|
| US1 | [US1-service-catalog.md](./US1-service-catalog.md) | As a client, I want to browse the salon's services and see everything I need to know about them, so I can decide what to book. | CAT-01, CAT-02, CAT-03, PLAT-01, PLAT-02 |
| US2 | [US2-booking-core.md](./US2-booking-core.md) | As a client, I want to pick a service and book a real open slot with my chosen stylist, so my appointment is confirmed and never double-booked. | BOOK-01…BOOK-06 |
| US3 | [US3-staff-dashboard.md](./US3-staff-dashboard.md) | As staff, I want a private, authenticated schedule where I can see appointments and manage their status, including no-show. | DASH-01…DASH-05 |

---

## One-time setup (do this before any guide)

### Prerequisites

- .NET SDK 10, Node.js 18+, and SQL Server LocalDB (`(localdb)\MSSQLLocalDB`).
- `RESEND_API_KEY` set in the API's user-secrets (required to run the API — real
  emails are sent in Development). US2's email test needs a real key.
- Staff auth secrets set in the API's user-secrets (already provisioned on the dev
  machine): `Jwt:SigningKey`, `Owner:Email`, `Owner:InitialPassword`.

Verify the secrets from `API/ZachHairStudio.Api`:

```powershell
cd API/ZachHairStudio.Api
dotnet user-secrets list
```

You should see `Jwt:SigningKey`, `Owner:Email`, `Owner:InitialPassword`, and
`Resend:ApiKey` (or `RESEND_API_KEY`). Note the `Owner:Email` and
`Owner:InitialPassword` values — you'll log into the dashboard with them in US3.

### Start the stack (3 terminals)

| # | Service | From | Command | URL |
|---|---------|------|---------|-----|
| 1 | API (.NET 10) | `API/ZachHairStudio.Api` | `dotnet run` | http://localhost:5236 |
| 2 | Landing page | `landing-page` | `npm install` (first time) then `npm run dev` | http://localhost:3000 |
| 3 | Dashboard | `dashboard` | `npm install` (first time) then `npm run dev -- -p 3001` | http://localhost:3001 |

The API applies EF Core migrations on startup and seeds the catalog, stylists,
working hours, and the Owner account. Wait until each server prints "ready" before testing.

### Quick smoke check (confirms the stack is live)

- Open http://localhost:5236/openapi/v1.json → returns the OpenAPI JSON spec.
- Open http://localhost:3000 → landing page renders.
- Open http://localhost:3001/login → dashboard login form renders.

If any of these fail, fix the stack before running the guides below.

---

## Seed data reference

These guides assume the seeded data below (created automatically on first API run).

### Services (catalog)

| Service | Duration | Price |
|---------|----------|-------|
| Precision Cut | 45 min | $35 |
| Color & Highlights | 90 min | $80 |
| Blowout & Styling | 45 min | $55 |
| Keratin Treatment | 120 min | $120 |
| Scalp Treatment | 40 min | $65 |
| Full Glam Package | 210 min | $199 |

### Stylists & availability

| Stylist | Working hours | Notes |
|---------|---------------|-------|
| Mr. Zachary | ✅ Every day, 9:00–18:00 | Shows open slots |
| Aria Chen | ✅ Every day, 9:00–18:00 | Shows open slots |
| Marcus Lee | ✅ Every day, 9:00–18:00 | Shows open slots |
| Sofia Reyes | ✅ Every day, 9:00–18:00 | Shows open slots |

- Salon timezone: **Asia/Yangon (UTC+06:30, no daylight saving)**. All displayed
  times are salon-local.
- The salon is **open seven days a week**, 09:00–18:00, for all four stylists —
  any future date should return slots (owner-directed, migration
  `OpenSalonEveryDay`).

---

## How to record results

For each scenario, mark the checkbox and note anything unexpected:

- `[x]` Pass — reality matched the expected result.
- `[ ]` + a note — describe exactly what differed (screenshot/console error helps).

When done, report results back (or feed them into `/gsd-verify-work 2` / `3`).
