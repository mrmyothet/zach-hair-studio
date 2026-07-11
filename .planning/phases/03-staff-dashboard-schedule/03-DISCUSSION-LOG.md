# Phase 3: Staff Dashboard (Schedule) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-11
**Phase:** 03-staff-dashboard-schedule
**Areas discussed:** Staff auth scheme, Schedule view layout, Status update flow, Dashboard foundation

---

## Staff auth scheme

### Who logs into the dashboard?

| Option | Description | Selected |
|--------|-------------|----------|
| Shared salon login (Recommended) | One set of credentials for all staff; simplest DASH-05 gate; Phase 7 replaces it anyway | |
| Per-staff accounts | Individual username + password per staff member; per-person audit from day one | |
| Per-staff with roles | Per-staff accounts plus Owner-vs-Staff role distinction now | ✓ |

**User's choice:** Per-staff with roles (declined the shared-login recommendation).

### Identity now vs minimal StaffUser table

| Option | Description | Selected |
|--------|-------------|----------|
| Identity now (Recommended) | ASP.NET Core Identity in BookingDbContext with Staff/Owner roles; ACCT-05 "one schema" satisfied by construction | ✓ |
| Minimal StaffUser table | Hand-rolled entity with PasswordHasher; lighter now, migration burden in Phase 7 | |

**User's choice:** Identity now.

### Session mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| httpOnly cookie (Recommended) | Identity cookie auth; XSS-safe; requires credentialed CORS tightening | |
| JWT bearer tokens | API returns JWT; dashboard attaches Authorization headers; token JS-readable; expiry handling needed | ✓ |

**User's choice:** JWT bearer tokens (declined the cookie recommendation — deliberate choice, noted in CONTEXT.md).

### Account provisioning

| Option | Description | Selected |
|--------|-------------|----------|
| Seeded accounts (Recommended) | Seed Owner (+ optional stylists) at migration/startup; no registration UI this phase | |
| Owner-creates-staff UI | Seed only Owner; build an Owner-only "add staff user" screen this phase | ✓ |
| Registration + approval | Staff self-register, owner approves; overkill for one salon | |

**User's choice:** Owner-creates-staff UI (gives the Owner role a real job in Phase 3).

---

## Schedule view layout

### Day view shape

| Option | Description | Selected |
|--------|-------------|----------|
| Time-grid, stylist columns (Recommended) | Salon-book layout: stylist columns, time axis, duration-sized blocks | ✓ |
| Chronological list | Single ordered list of the day's appointments; simpler, no spatial gaps | |
| Grid with list fallback | Grid on desktop, list on small screens; most work | |

**User's choice:** Time-grid with stylist columns.

### Week view shape

| Option | Description | Selected |
|--------|-------------|----------|
| Compact 7-day columns (Recommended) | Seven day-columns of condensed chips; click into day view | ✓ |
| Agenda list by day | Scrolling list grouped by day headings | |
| Full week time-grid | Calendar-style week grid; cramped with multiple stylists | |

**User's choice:** Compact 7-day columns.

### Landing view & navigation

| Option | Description | Selected |
|--------|-------------|----------|
| Today's day view (Recommended) | Land on today; prev/next, Today button, date picker, Day/Week toggle; Monday weeks | ✓ |
| This week's overview | Land on week view, click into days | |

**User's choice:** Today's day view.

### Cancelled/no-show visualization

| Option | Description | Selected |
|--------|-------------|----------|
| Hidden, with a toggle (Recommended) | Grid shows live appointments only (slots genuinely free again); toggle reveals muted entries | ✓ |
| Greyed-out in place | Visible in grid, struck through; contradicts actual availability | |
| Separate list below the grid | Live-only grid + "Cancelled & no-shows today" section | |

**User's choice:** Hidden, with a toggle.

---

## Status update flow

### Update surface

| Option | Description | Selected |
|--------|-------------|----------|
| Both: quick + detail (Recommended) | Quick actions on schedule blocks + same controls in detail view | ✓ |
| Detail view only | Must open appointment to change status; deliberate but slower | |

**User's choice:** Both quick actions and detail view.

### Transition rules

| Option | Description | Selected |
|--------|-------------|----------|
| Constrained (Recommended) | Confirmed → Completed/Cancelled/NoShow; terminal final; server-enforced 400 | ✓ |
| Any-to-any | Free transitions incl. reviving cancelled; needs slot re-claim conflict flow | |
| Constrained + undo window | Constrained plus short undo re-claiming slots | |

**User's choice:** Constrained.

### Confirmation UX

| Option | Description | Selected |
|--------|-------------|----------|
| Confirm slot-releasing only (Recommended) | Cancel/No-show confirm; Completed is one click | ✓ |
| Confirm all transitions | Every change confirms; friction on the common action | |
| No confirmations | All one-click; accidental cancel is irreversible | |

**User's choice:** Confirm slot-releasing only.

### Audit trail

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, minimal audit (Recommended) | StatusChangedAt + StatusChangedBy on Appointment, shown in detail view | ✓ |
| Full history table | AppointmentStatusHistory recording every transition | |
| No audit this phase | Just update the field | |

**User's choice:** Minimal audit.

---

## Dashboard foundation

### Data freshness

| Option | Description | Selected |
|--------|-------------|----------|
| Polling + focus refetch (Recommended) | ~60s silent refetch + on-focus refetch + manual refresh button | ✓ |
| Manual refresh only | Load on navigation; refresh button | |
| Aggressive polling (~10s) | Near-live feel; more chatter than the volume warrants | |

**User's choice:** Polling + focus refetch.

### Visual identity

| Option | Description | Selected |
|--------|-------------|----------|
| Branded but utilitarian (Recommended) | Salon fonts/accents on a clean, dense, tool-like layout | ✓ |
| Full landing-page styling | Marketing look wholesale; fights information density | |
| Plain admin look | Neutral, zero branding | |

**User's choice:** Branded but utilitarian.

### API client

| Option | Description | Selected |
|--------|-------------|----------|
| OpenAPI-generated client (Recommended) | Typed TS client via the existing openapi-client skill | ✓ |
| Hand-written fetch + Zod | Mirror landing-page lib pattern; more hand-maintenance | |

**User's choice:** OpenAPI-generated client.

### Session length

| Option | Description | Selected |
|--------|-------------|----------|
| Workday token, ~12h (Recommended) | Login at open, valid through close; no refresh machinery | ✓ |
| Short token + refresh | 15–60 min tokens with refresh flow | |
| Long-lived, ~30 days | Convenient, weak revocation story | |

**User's choice:** Workday token, ~12h.

### Device targeting

| Option | Description | Selected |
|--------|-------------|----------|
| Desktop-first, phone-usable (Recommended) | Grid for front-desk; graceful phone degradation; Phase 8 does the polish pass | ✓ |
| Desktop only for now | No effort below tablet width | |
| Fully responsive now | Phone-first schedule as a Phase 3 deliverable | |

**User's choice:** Desktop-first, phone-usable.

---

## Claude's Discretion

- JWT storage/claims/signing-key management and 401-handling UX
- Login page design, empty/loading/error states, appointment block & detail contents
- Endpoint shapes and controller placement (PLAT-01 boundary preserved)
- Identity setup details and Owner seed provisioning mechanics
- Dashboard dev port and env conventions
- Polling implementation library choice
- CORS handling for the new dashboard origin (full tightening remains Phase 8)

## Deferred Ideas

- Refresh-token/session hardening → Phase 7/8
- Real-time push sync across staff views → v2 (DASH2-01)
- Full staff-user management (edit/deactivate/reset password) → alongside Phase 4
- Full AppointmentStatusHistory table → if reporting needs grow
- Undo window for terminal status changes → set aside (slot re-claim complexity)
- Phone-first schedule presentation → Phase 8 responsive polish
