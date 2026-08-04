# Phase 4: Staff Management (Services & Availability) - Context

**Gathered:** 2026-07-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Staff keep the service catalog and stylist availability accurate from the
`dashboard/` app — without a code deploy — using the **same** `Service`,
`StylistWorkingHours`, and `StylistTimeOff` models Phase 1–2 already ship.
Availability edits that would leave Confirmed bookings outside working hours
(or under new time off) are **hard-blocked** with an inline conflict list.
Phase 2's open-slot query reflects changes immediately (one availability
system, not two).

Requirements: MGMT-01, MGMT-02, MGMT-03.

**Not in this phase:** stylist↔service capability matrix (deferred again),
stylist create/retire UI, client accounts, product catalog, payment, real-time
push, production hardening / full responsive polish (Phase 8). Legacy
`ZachHairStudio.Admin` receives no new work.

</domain>

<decisions>
## Implementation Decisions

### Service CRUD surface

- **D-01:** **Owner-only** service create/edit/retire. Staff role uses schedule
  and availability; catalog merchandising stays with Owner (same Owner-gated
  pattern as Phase 3 add-staff).
- **D-02:** **Soft-retire** via existing `Service.IsActive = false`. Public
  catalog and booking continue to filter active services. No hard delete.
- **D-03:** **File upload** for service images from the dashboard. Owner
  uploads an image; API stores it and sets `ImageUrl`. This delivers Phase 1's
  deferred “image management arrives with Phase 4 CRUD” promise (D-08 there).
- **D-04:** **Edit price/duration freely** with no warn/block when future
  appointments exist. Existing appointments keep their already-written
  `AppointmentSlot` cells (duration at book time). Response DTOs that join
  live `Service` will show updated name/price/duration on reads — accepted.

### Availability editor shape

- **D-05:** **Visual week strip** for recurring weekly hours — drag/select
  ranges per weekday on a compact week grid, persisted to
  `StylistWorkingHours` (same table Phase 2 `SlotService` reads).
- **D-06:** **No separate Break entity.** Recurring midday “breaks” (e.g.
  lunch) are modeled as **gaps in the week-strip hours** (split segments or
  shorter day span). Do not extend `StylistTimeOff` into a recurring weekly
  pattern this phase.
- **D-07:** **`StylistTimeOff` is one-off / date-range only** (vacation, sick,
  holiday, ad-hoc blocks). Entry UX is a **calendar overlay** where staff
  paint blocked ranges on a month/week calendar next to the hours strip.
- **D-08:** Slot math stays `hours − timeOff − bookedCells` — no second
  availability system (Phase 2 D-06 / roadmap constraint).

### Conflict handling on save

- **D-09:** **Hard block** conflicting availability saves. Refuse the write;
  staff must cancel/reschedule conflicting Confirmed appointments first. No
  Owner override, no partial apply.
- **D-10:** Conflicts are **Confirmed** appointments only (Cancelled/NoShow
  already release slots; Completed is historical and out of scope for the
  check).
- **D-11:** Surfaced as an **inline conflict list**: client name, service,
  stylist, salon-local time — enough to act without deep-linking into
  schedule (deep links are nice-to-have, not required).
- **D-12:** Conflict check runs on **both** shrinking/removing weekly hours
  **and** adding/extending time off.

### Who edits whose schedule / navigation

- **D-13:** **Any authenticated staff** may edit **any** stylist's
  availability (hours + time off). Services remain Owner-only (D-01).
- **D-14:** **Stylist picker only** — choose among existing active stylists.
  No create/edit/retire stylist UI this phase.
- **D-15:** **Defer stylist↔service capability matrix** — all stylists still
  perform all services (Phase 2 D-08 stays deferred; not in MGMT-*).
- **D-16:** Dashboard nav: **two items** — **Services** (Owner-only; hide or
  403 for Staff) and **Availability** (all staff), alongside existing
  Schedule / add-staff.

### Claude's Discretion

- Image storage backend (local `wwwroot`/static folder vs blob), allowed MIME
  types, max size, and whether `ImageUrl` stays a public path vs signed URL.
- Exact week-strip interaction (drag vs click-paint), closed-day affordance,
  and how multiple ranges per weekday map to `StylistWorkingHours` rows.
- Conflict API shape (400 vs 409 ProblemDetails), look-ahead window for
  scanning Confirmed appointments, and timezone labeling consistent with
  Asia/Yangon (Phase 2 D-16).
- Authz wiring: `[Authorize(Roles = Owner)]` on service write endpoints;
  `[Authorize]` on availability write endpoints; gate existing unauthenticated
  Service POST/PUT.
- OpenAPI client regen for new/extended endpoints; empty/loading/error states
  matching Phase 3 utilitarian dashboard chrome (D-15 there).
- Whether retiring a service should be blocked when it is the only remaining
  bookable service (edge case) — default: allow soft-retire.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning & requirements
- `.planning/ROADMAP.md` — Phase 4 goal, success criteria, MGMT-01..03, UI hint
- `.planning/REQUIREMENTS.md` — MGMT-01, MGMT-02, MGMT-03 exact wording
- `.planning/PROJECT.md` — stack constraints, OpenAPI source of truth, Key Decisions
- `.planning/STATE.md` — current milestone position (reconcile after this context)

### Prior phase decisions (must honor)
- `.planning/phases/01-service-catalog/01-CONTEXT.md` — Service model (`IsActive`,
  `DisplayOrder`, `ImageUrl`, dual descriptions); write endpoints exist; image
  management deferred to Phase 4
- `.planning/phases/02-booking-core/02-CONTEXT.md` — 15-min grid; occupancy +
  unique index; `StylistWorkingHours` + `StylistTimeOff` as the single
  availability model; capability matrix deferred; salon timezone Asia/Yangon
- `.planning/phases/03-staff-dashboard-schedule/03-CONTEXT.md` — Owner/Staff
  roles, JWT, OpenAPI client, utilitarian dashboard, schedule at `/schedule`

### Domain entities & slot logic
- `API/ZachHairStudio.Shared/Features/Services/` — Service entity, DTOs, validators
- `API/ZachHairStudio.Shared/Features/Availability/StylistWorkingHours.cs`
- `API/ZachHairStudio.Shared/Features/Availability/StylistTimeOff.cs`
- `API/ZachHairStudio.Shared/Features/Availability/SlotService.cs` — open-slot
  computation that must reflect staff edits immediately
- `API/ZachHairStudio.Shared/Features/Appointments/Appointment.cs` — Confirmed
  status; no price/duration snapshot on entity
- `API/ZachHairStudio.Api/Controllers/ServicesController.cs` — existing POST/PUT
  (currently ungated; Phase 4 must authorize)

### Dashboard integration
- `dashboard/app/schedule/page.tsx` — existing staff schedule surface
- `dashboard/app/staff/new/page.tsx` — Owner-only pattern to mirror for Services
- `.claude/skills/openapi-client/SKILL.md` — regenerate typed client after API changes

### Project constitution
- `specs/roadmap.md` — original P1–8 source
- `specs/tech-stack.md` — locked stack
- `specs/tooling.md` — project skills (`dev`, `ef-migrations`, `openapi-client`)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ServicesService` + FluentValidation + `Result<T>` — Phase 1 write path;
  dashboard forms call existing/extended endpoints behind Owner auth
- `StylistWorkingHours` / `StylistTimeOff` + `SlotService` — edit these tables;
  do not invent a parallel availability store
- Dashboard JWT auth, `requireAuth()`, Owner role checks — Phase 3 patterns
- OpenAPI-generated client in `dashboard/` — extend after new routes

### Established Patterns
- Feature folders under `API/ZachHairStudio.Shared/Features/{Feature}/`
- Soft flags (`IsActive`) over hard deletes for catalog entities
- ProblemDetails for validation/conflict responses
- Salon-local times via configured IANA zone (Asia/Yangon); label offsets in UI
- Utilitarian dashboard chrome (Phase 3 D-15): tool-like, brand accents, not marketing

### Integration Points
- Gate `ServicesController` write actions with Owner role
- New availability write API (hours replace/update + time-off CRUD) consumed by
  dashboard Availability page; public slot GET stays anonymous
- Nav shell in dashboard layout: add Services + Availability links with role
  visibility
- Public landing catalog already filters `IsActive` — retire shows up without
  frontend changes if API continues to filter

</code_context>

<specifics>
## Specific Ideas

- Week-strip + calendar-overlay pairing: hours on a visual week; time off
  painted on a calendar beside it — not a bare form list.
- Conflict list must be actionable enough without leaving the Availability
  page (inline details, no required deep link).

</specifics>

<deferred>
## Deferred Ideas

- **Stylist↔service capability matrix** — parked from Phase 2 D-08; discussed
  in Phase 4 and deferred again. Remains “all stylists perform all services”
  until a later phase/backlog item.
- **Stylist create / soft-retire UI** — picker-only this phase; roster CRUD
  is a future capability.
- **Owner override on conflicting availability** — rejected; hard block only.

</deferred>

---

*Phase: 4-Staff Management (Services & Availability)*
*Context gathered: 2026-07-24*
