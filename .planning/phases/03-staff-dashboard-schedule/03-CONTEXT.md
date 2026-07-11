# Phase 3: Staff Dashboard (Schedule) - Context

**Gathered:** 2026-07-11
**Status:** Ready for planning

<domain>
## Phase Boundary

A private, authenticated staff schedule dashboard built in the (currently empty)
`dashboard/` Next.js app. Staff log in with individual accounts, see the day's and
week's appointments, open an appointment to view its full details, and update its
status — confirmed, completed, cancelled, or no-show — with no-show as a distinct,
separately reportable terminal status. The dashboard and every API endpoint it calls
are rejected without staff authentication (DASH-05).

This phase also introduces the staff auth foundation (ASP.NET Core Identity with
Owner/Staff roles) that Phase 7 later extends to client accounts — one schema, not
two (ACCT-05).

**Not in this phase:** staff CRUD for services/availability (Phase 4), client
accounts and self-service cancel/reschedule (Phase 7), real-time push sync
(v2, DASH2-01), production hardening/full responsive polish (Phase 8). The legacy
`ZachHairStudio.Admin` MVC scaffold receives no new work (retired in Phase 8).

</domain>

<decisions>
## Implementation Decisions

### Staff auth scheme

- **D-01:** **Per-staff accounts with roles.** Each staff member has their own
  login; roles distinguish **Owner** from **Staff**. Rejected: a single shared salon
  login (no per-person audit trail) and role-less per-staff accounts (the Owner-only
  add-staff screen in D-04 needs the role split anyway).
- **D-02:** **ASP.NET Core Identity now**, added to `BookingDbContext` in this phase
  with Owner/Staff roles. Users, roles, password hashing, and lockout come
  battle-tested instead of hand-rolled, and ACCT-05's "one Identity schema shared
  with client accounts" is satisfied by construction — Phase 7 adds client users to
  the same tables. Rejected: a hand-rolled minimal `StaffUser` table that Phase 7
  would have to migrate into Identity.
- **D-03:** **JWT bearer tokens** authenticate dashboard → API calls. The API issues
  a JWT at login; the dashboard attaches `Authorization: Bearer` headers. The user
  chose this over the recommended httpOnly-cookie approach — honor it; do not
  silently switch to cookie auth. Token lifetime is a **~12-hour workday token**
  (log in at open, valid through close, re-login tomorrow) with **no refresh-token
  machinery this phase**; Phase 7 can harden the session story.
- **D-04:** **Seed only the Owner account; staff accounts are created from the
  dashboard.** Phase 3 ships an Owner-only "add staff user" screen. The seeded
  Owner's initial credentials come from user-secrets/env — never a tracked file
  (gitleaks). No self-registration flow.

### Schedule view layout

- **D-05:** **Day view is a salon-book time-grid**: one column per active stylist, a
  vertical time axis spanning working hours, appointments rendered as blocks sized by
  duration. This maps directly onto Phase 2's 15-minute grid data and makes gaps and
  free time visible at a glance.
- **D-06:** **Week view is compact 7-day columns** of condensed appointment chips
  (time + client + service); clicking a day drills into the full day view. The day
  view is the working surface; the week view is the capacity overview.
- **D-07:** **The dashboard lands on today's day view.** Navigation: prev/next
  arrows, a "Today" button, a date picker, and a Day/Week toggle. Weeks start on
  Monday. All times render in salon-local time (Asia/Yangon) with the zone labelled,
  per Phase 2's D-16.
- **D-08:** **Cancelled and no-show appointments are hidden from the grid by
  default**, revealed by a "show cancelled/no-show" toggle as muted entries. Their
  slots are genuinely free again (Phase 2 D-04 deletes slot rows on cancel), so
  rendering them as grid blocks would misrepresent availability.

### Status update flow

- **D-09:** **Status changes happen in two places**: quick actions directly on a
  schedule block (Complete / Cancel / No-show) and the same controls inside the
  appointment detail view.
- **D-10:** **Transitions are constrained and server-enforced**: from `Confirmed` an
  appointment may move to `Completed`, `Cancelled`, or `NoShow`; terminal statuses
  are final. An invalid transition returns a 400 ProblemDetails. Rationale: reverting
  a cancel/no-show would require re-claiming slot rows that may already be booked by
  someone else — mistake recovery is "book a new appointment," not "un-cancel."
- **D-11:** **Slot-releasing changes confirm first; Completed is one click.** Cancel
  and No-show pop a confirmation dialog (irreversible, frees the slot); marking
  Completed — the routine end-of-appointment action — applies immediately.
- **D-12:** **Minimal status audit**: add `StatusChangedAt` and `StatusChangedBy`
  (the authenticated staff user) to `Appointment`, shown in the detail view. Per-staff
  accounts (D-01) make this meaningful; CONCERNS.md already flags the missing
  auditability. A full `AppointmentStatusHistory` table was considered and rejected
  as speculative at this volume.
- **D-13:** No-show is already a distinct member of `AppointmentStatus` (Phase 2
  shipped the enum with `NoShow`). DASH-04's "queryable and reportable separately"
  means the dashboard/API must treat it as its own filterable status — e.g., the list
  endpoint can filter by status and the UI distinguishes no-show from cancelled —
  never folding the two together.

### Dashboard foundation

- **D-14:** **Freshness = polling + focus refetch**: the schedule silently refetches
  roughly every 60 seconds and whenever the tab regains focus, plus a manual refresh
  button. Real-time push stays deferred (v2, DASH2-01).
- **D-15:** **Branded but utilitarian look**: carry the salon's fonts/accent colors
  from the landing page for brand familiarity, on a clean, dense, tool-like layout
  (light neutral surfaces, compact grid/tables). It is a work tool, not a marketing
  page.
- **D-16:** **OpenAPI-generated TypeScript client** via the existing
  `openapi-client` project skill (`.claude/skills/openapi-client/SKILL.md`). The
  dashboard touches many endpoints (auth, appointments, staff users); generation
  keeps types honest and OpenAPI remains the declared source of truth.
- **D-17:** **Desktop-first, phone-usable.** The time-grid targets the front-desk
  laptop/tablet; on phones the experience degrades gracefully (week chips, detail
  view, and status actions work; the grid may scroll). The full responsive polish
  pass remains Phase 8's job (LAUNCH-01).

### Claude's Discretion

- JWT storage location on the dashboard client, claims shape, signing key
  management (user-secrets in dev, env var in prod), and 401-handling/redirect-to-login
  UX — D-03 fixes the mechanism and lifetime; the rest is implementation.
- Login page design, empty states, loading/error states, and the exact content of
  appointment blocks and the detail view (client contact, service, price, duration,
  stylist, created-at, status audit line).
- Exact endpoint shapes for list-by-date-range/detail/status-update, and whether
  they live on the existing `AppointmentsController` or a dashboard-scoped
  controller — must keep the service-layer boundary (PLAT-01) and `Result<T>` →
  ProblemDetails translation.
- Identity setup details (table naming/schema, password policy, lockout settings)
  and how the Owner seed account is provisioned at startup/migration.
- Dashboard dev port and `next.config`/env conventions (mirror `landing-page/`
  where sensible; `NEXT_PUBLIC_API_URL` pattern exists).
- Polling implementation (SWR/React Query/hand-rolled) — pick what fits the
  generated client.
- CORS handling for the new dashboard origin — bearer tokens don't require
  credentialed CORS, but the API must accept the dashboard origin; tightening beyond
  that remains Phase 8 scope.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` § "Phase 3: Staff Dashboard (Schedule)" — goal, the five
  Success Criteria, the `dashboard/`-not-Admin note
- `.planning/REQUIREMENTS.md` — DASH-01 … DASH-05 (lines 32–36); DASH2-01 (real-time
  sync) is explicitly v2, not this phase

### Prior-phase decisions that bind this phase
- `.planning/phases/02-booking-core/02-CONTEXT.md` — **D-04** (cancel deletes
  `AppointmentSlot` rows, keeps the `Appointment` with terminal status; "Phase 3's
  no-show status behaves identically"), **D-16** (salon-local time rendering with
  labelled zone), **D-14** (Appointment/Stylist model this dashboard reads),
  **D-11/D-12** (email is best-effort; send failures are "surfaced to staff" — the
  detail view is a natural home if planning chooses to surface them)
- `.planning/phases/01-service-catalog/01-CONTEXT.md` — **D-17** (`Result<T>` +
  ProblemDetails translation), **D-18** (frontend response validation pattern)

### Project constraints
- `.claude/CLAUDE.md` — feature-folder backend architecture, OpenAPI as source of
  truth, gitleaks secret-scanning, RESEND_API_KEY requirement for running API/tests
- `.planning/codebase/CONCERNS.md` § "Missing Authentication & Authorization" and
  § "Open CORS Policy" — the known gaps DASH-05 starts closing
- `.claude/skills/openapi-client/SKILL.md` — the client-generation skill D-16 mandates
- `.claude/skills/feature-scaffold/SKILL.md` — backend feature scaffolding template

### Existing code this phase extends
- `API/ZachHairStudio.Shared/Features/Appointments/` — `Appointment`,
  `AppointmentStatus` (already includes `NoShow`), `AppointmentsService` (currently
  `CreateAsync` only — list/detail/status-update are new), `AppointmentResponseDto`
- `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs` — existing
  endpoints (slots + create) that the new staff endpoints sit alongside
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` — gains Identity tables and the
  status-audit columns (D-02, D-12) via migration
- `API/ZachHairStudio.Shared/Features/Stylists/` — stylist read slice powering the
  day-view columns
- `dashboard/` — empty scaffold (`.gitkeep` only); the new Next.js app lands here
- `landing-page/` — reference for Next.js 15/React 19/Tailwind 4 conventions, fonts,
  and brand colors (D-15)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`Features/Appointments/` + `Features/Services/` feature folders** — the entity →
  DTO → validator → mapper → service-layer template; new staff-facing queries and the
  status-update slice follow it.
- **`Result<T>`** (`API/ZachHairStudio.Shared/Result.cs`) — status-update returns it;
  controller translates invalid transition → 400, missing appointment → 404.
- **`AppointmentStatus` enum** — already contains `NoShow`; stored as string via
  `HasConversion<string>()`. No enum migration needed, only behavior.
- **Salon timezone config** (`Salon` section, Asia/Yangon) — reuse for all
  dashboard date-range queries and rendering.
- **`openapi-client` skill** — generates the dashboard's typed client (D-16).
- **Landing page brand tokens** (`landing-page/app/globals.css` @theme, layout
  fonts) — source for D-15's branded-but-utilitarian styling.

### Established Patterns

- **Controllers never touch `BookingDbContext`** (PLAT-01) — all new data access
  lives in feature services.
- **FluentValidation on write DTOs** surfaced as ProblemDetails (PLAT-02) — applies
  to the status-update DTO and staff-user creation DTO.
- **EF migrations own the schema**; `db.Database.Migrate()` runs at startup (skipped
  in Testing). Identity tables and audit columns arrive by migration.
- **Real Resend sends in Development AND Testing** (Phase 2 D-12) — any test that
  exercises booking creation needs `RESEND_API_KEY`; status-update tests should not
  send email.

### Integration Points

- **`GET` appointments by date range** (new) feeds both day and week views;
  slot-releasing status changes must delete `AppointmentSlot` rows exactly as
  Phase 2's cancel path does — same code path, not a copy.
- **Identity + JWT middleware** lands in `API/ZachHairStudio.Api/Program.cs`;
  `[Authorize]` (staff) guards all dashboard endpoints — public booking endpoints
  stay anonymous.
- **CORS** must admit the new dashboard origin alongside the landing page.
- **Phase 4** builds its staff management screens inside the dashboard app and auth
  boundary this phase creates; **Phase 7** extends the same Identity schema to
  client accounts. Design for that, but do not build it here.

</code_context>

<specifics>
## Specific Ideas

- The day view should read like a physical salon appointment book: stylist columns,
  time down the side, blocks sized by duration — gaps visible at a glance.
- DASH-05's proof is a rejected request: hitting the dashboard or any staff API
  endpoint without a valid staff JWT returns 401/redirects to login — worth an
  explicit test.
- DASH-04's proof is separability: querying/filtering no-shows returns only
  no-shows, never cancelled — the two terminal statuses must be independently
  reportable.
- The user deliberately chose JWT over the recommended httpOnly cookie, and
  owner-creates-staff over simple seeding — both are conscious choices; downstream
  agents should not "correct" them.

</specifics>

<deferred>
## Deferred Ideas

- **Refresh-token / session hardening** — the ~12h workday JWT is deliberately
  simple; revisit in Phase 7 when Identity expands to clients, or Phase 8 hardening.
- **Real-time push sync across staff views** — tracked as v2 DASH2-01; polling is
  the Phase 3 answer.
- **Full staff-user management** (edit/deactivate/reset password, beyond
  Owner-adds-staff) — natural fit alongside Phase 4's staff management screens.
- **Full `AppointmentStatusHistory` table** — rejected in favor of minimal
  StatusChangedAt/By audit (D-12); revisit if reporting needs grow.
- **Undo window for terminal status changes** — considered and set aside; would
  need slot re-claiming with conflict handling.
- **Phone-first schedule presentation** — deliberate Phase 8 responsive-polish
  scope (D-17).

</deferred>

---

*Phase: 03-staff-dashboard-schedule*
*Context gathered: 2026-07-11*
