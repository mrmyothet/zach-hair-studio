# Phase 3: Staff Dashboard (Schedule) - Research

**Researched:** 2026-07-11
**Domain:** ASP.NET Core Identity + JWT staff auth; staff scheduling dashboard (Next.js 15)
**Confidence:** MEDIUM

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Per-staff accounts with roles. Each staff member has their own login; roles distinguish **Owner** from **Staff**. Rejected: shared salon login, role-less accounts.
- **D-02:** ASP.NET Core Identity now, added to `BookingDbContext` in this phase with Owner/Staff roles. Rejected: hand-rolled `StaffUser` table.
- **D-03:** JWT bearer tokens authenticate dashboard → API calls (not httpOnly cookies — a deliberate user choice, do not silently switch). ~12-hour workday token lifetime, no refresh-token machinery this phase.
- **D-04:** Seed only the Owner account; staff accounts are created from the dashboard via an Owner-only "add staff user" screen. Owner's initial credentials come from user-secrets/env — never a tracked file. No self-registration.
- **D-05:** Day view is a salon-book time-grid: one column per active stylist, a vertical time axis spanning working hours, appointments rendered as blocks sized by duration.
- **D-06:** Week view is compact 7-day columns of condensed appointment chips (time + client + service); clicking a day drills into the full day view.
- **D-07:** Dashboard lands on today's day view. Navigation: prev/next arrows, "Today" button, date picker, Day/Week toggle. Weeks start Monday. All times render in salon-local time (Asia/Yangon) with the zone labelled.
- **D-08:** Cancelled and no-show appointments are hidden from the grid by default, revealed by a "show cancelled/no-show" toggle as muted entries.
- **D-09:** Status changes happen in two places: quick actions on a schedule block (Complete/Cancel/No-show) and the same controls inside the appointment detail view.
- **D-10:** Transitions are constrained and server-enforced: from `Confirmed` an appointment may move to `Completed`, `Cancelled`, or `NoShow`; terminal statuses are final. An invalid transition returns 400 ProblemDetails.
- **D-11:** Slot-releasing changes (Cancel, No-show) confirm first (irreversible, frees the slot); Completed applies immediately (one click).
- **D-12:** Minimal status audit: add `StatusChangedAt` and `StatusChangedBy` (authenticated staff user) to `Appointment`, shown in the detail view. No full `AppointmentStatusHistory` table.
- **D-13:** No-show is already a distinct `AppointmentStatus` member (Phase 2 shipped it). DASH-04 means the API/UI must treat it as its own filterable status — never folded together with Cancelled.
- **D-14:** Freshness = polling (~60s) + focus refetch + manual refresh button. Real-time push deferred (v2, DASH2-01).
- **D-15:** Branded but utilitarian look — landing page fonts/accent colors, clean dense tool-like layout (light neutral surfaces, compact grid/tables).
- **D-16:** OpenAPI-generated TypeScript client via the `openapi-client` project skill.
- **D-17:** Desktop-first, phone-usable. Full responsive polish is Phase 8 (LAUNCH-01).

### Claude's Discretion

- JWT storage location on the dashboard client, claims shape, signing key management (user-secrets in dev, env var in prod), 401-handling/redirect-to-login UX.
- Login page design, empty states, loading/error states, exact content of appointment blocks and detail view.
- Exact endpoint shapes for list-by-date-range/detail/status-update, and whether they live on `AppointmentsController` or a dashboard-scoped controller — must keep the service-layer boundary (PLAT-01) and `Result<T>` → ProblemDetails translation.
- Identity setup details (table naming/schema, password policy, lockout settings) and Owner seed provisioning at startup/migration.
- Dashboard dev port and `next.config`/env conventions (mirror `landing-page/`).
- Polling implementation (SWR/React Query/hand-rolled) — pick what fits the generated client.
- CORS handling for the new dashboard origin (bearer tokens don't require credentialed CORS, but the API must accept the dashboard origin).

### Deferred Ideas (OUT OF SCOPE)

- Refresh-token / session hardening — Phase 7/8.
- Real-time push sync across staff views — v2 DASH2-01; polling is the Phase 3 answer.
- Full staff-user management (edit/deactivate/reset password) — Phase 4 territory.
- Full `AppointmentStatusHistory` table — rejected in favor of D-12's minimal audit.
- Undo window for terminal status changes — set aside (would need slot re-claiming).
- Phone-first schedule presentation — Phase 8 (D-17).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DASH-01 | Staff can view the day's and week's appointments in a schedule dashboard | Architecture Patterns (day/week views, GET-by-date-range endpoint); Don't Hand-Roll (calendar UI build-vs-library finding) |
| DASH-02 | Staff can open an appointment to view its details | Architecture Patterns (detail endpoint/modal); Code Examples (response DTO shape) |
| DASH-03 | Staff can update an appointment's status (confirmed, completed, cancelled, no-show) | Architecture Patterns (status-update endpoint + transition guard); Common Pitfalls (transition validation, slot release reuse) |
| DASH-04 | "No-show" is a distinct terminal status, separate from "cancelled" | Standard Stack (existing `AppointmentStatus` enum — no migration needed); Common Pitfalls (query/filter separability) |
| DASH-05 | The staff dashboard and its API are behind an authentication gate (staff-only) | Standard Stack (Identity + JwtBearer); Architecture Patterns (auth middleware, `[Authorize]`); Common Pitfalls (CORS + bearer, InMemory test host auth) |
</phase_requirements>

## Summary

This phase has two intertwined domains: (1) standing up **ASP.NET Core Identity + JWT bearer auth** inside the existing `BookingDbContext`/API, and (2) building a **staff scheduling dashboard** in a brand-new `dashboard/` Next.js 15 app that reads and mutates appointment status. Both are additive to a codebase whose conventions (feature folders, `Result<T>`, FluentValidation, OpenAPI-as-source-of-truth) are already well established from Phases 1–2 — the job here is to extend those conventions to auth and a second frontend, not invent new ones.

On the backend, `AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<BookingDbContext>()` is the standard, non-hand-rolled path (confirmed via ASP.NET Core official docs) [CITED: learn.microsoft.com via Context7 /dotnet/aspnetcore.docs]. `BookingDbContext` needs to inherit `IdentityDbContext<ApplicationUser, IdentityRole, int>` (or call the Identity model-builder extension) to get the Identity tables; JWT issuance/validation is a separate concern layered on with `Microsoft.AspNetCore.Authentication.JwtBearer`, configured via `TokenValidationParameters` and a symmetric signing key [CITED: learn.microsoft.com via Context7]. `AppointmentStatus` already has `NoShow` (Phase 2) — DASH-04 requires no enum change, only server-enforced transition rules (D-10) and a status-audit pair of columns (D-12).

On the frontend, `dashboard/` is empty and needs the same Next.js 15 / React 19 / Tailwind 4 scaffold as `landing-page/`, plus the OpenAPI-generated client (D-16, already an established skill). Since D-03 locks in JWT-over-bearer (not cookies), Next.js's built-in middleware/cookie-session patterns (NextAuth, iron-session) do not apply — the dashboard is really a bearer-token SPA-style client that happens to run inside Next.js, not a Next-native-auth app. The day-view time-grid (D-05) is a bespoke, single-day, stylist-column layout with quick-action buttons on blocks; it does not map cleanly onto FullCalendar (whose resource/stylist-column view is a paid Premium plugin) or react-big-calendar (MIT but thin resource API, mostly hand-built anyway) — a hand-rolled CSS Grid over Phase 2's 15-minute slot data is the lower-total-complexity path for this specific layout, matching the mode:mvp scope.

**Primary recommendation:** Add ASP.NET Core Identity (`AddIdentity<ApplicationUser, IdentityRole>` + `AddEntityFrameworkStores<BookingDbContext>`) and `Microsoft.AspNetCore.Authentication.JwtBearer` (both 10.0.9, matching the repo's existing EF Core 10.0.9 pin) to guard a new dashboard-scoped set of endpoints; scaffold `dashboard/` mirroring `landing-page/`'s Next.js 15/React 19/Tailwind 4 setup with SWR for the 60s-poll + focus-refetch data layer (D-14) over the OpenAPI-generated client (D-16); hand-roll the day-view time-grid rather than adopting a calendar library.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Staff login / credential check | API / Backend | — | ASP.NET Core Identity owns password hashing, lockout, user store — never re-implemented client-side |
| JWT issuance & validation | API / Backend | — | The API is the token issuer and the only party holding the signing key; JwtBearer middleware validates on every request |
| JWT storage & attach-to-request | Browser / Client (dashboard) | — | The Next.js dashboard is a client-rendered SPA-style consumer of an external API; it holds the token and sets `Authorization: Bearer` |
| Route/page gating (redirect to login) | Frontend Server / Client boundary (dashboard) | — | Client-side check on mount/route change; the token is opaque to Next.js's own session system since D-03 rejects cookies |
| Day/week schedule query (date-range read) | API / Backend | Database | New read endpoint(s) on the Appointments feature slice; DB does the date filtering via EF Core LINQ, not the client |
| Time-grid rendering (stylist columns, block sizing) | Browser / Client (dashboard) | — | Pure presentation over data already fetched; no server involvement beyond returning the raw appointment list |
| Status transition validation | API / Backend | — | D-10's constrained transitions are server-enforced; the UI's confirm dialogs (D-11) are a UX nicety, not the authority |
| Status audit (`StatusChangedAt/By`) | API / Backend | Database | Written inside the same service-layer transaction that performs the status update |
| Freshness (polling / focus refetch) | Browser / Client (dashboard) | — | SWR/React Query interval + `visibilitychange`/focus event, entirely client-side |
| CORS admission of dashboard origin | API / Backend | — | `AddCors` policy change in `Program.cs`; must list the dashboard's origin explicitly once policy is no longer `AllowAnyOrigin` |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.0.9 [VERIFIED: nuget.org registry] | User/role store, password hashing, lockout | The battle-tested Identity EF Core store; matches D-02 exactly and the repo's existing EF Core 10.0.9 pin |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.9 [VERIFIED: nuget.org registry] | JWT bearer token validation middleware | Standard ASP.NET Core package for accepting `Authorization: Bearer` tokens; pairs with Identity for role claims |
| `System.IdentityModel.Tokens.Jwt` | ships with .NET 10 SDK (part of `Microsoft.IdentityModel.*`) [CITED: learn.microsoft.com via Context7] | Mint JWTs (`JwtSecurityTokenHandler`) | Used in the official ASP.NET Core JWT sample for issuing tokens with `SigningCredentials` |
| `swr` | 2.4.2 [ASSUMED — package name/version from WebSearch/training data, not yet gated by package-legitimacy check; see audit] | Client polling + focus-revalidation data fetching in `dashboard/` | Smallest-surface fetch/cache hook with built-in `refreshInterval` + revalidate-on-focus, matching D-14 exactly without extra config |
| `openapi-typescript` + `openapi-fetch` | 7.13.0 / 0.17.0 [VERIFIED: nuget/npm registry via `npm view`; package identity itself is `[ASSUMED]` per provenance rule — see audit] | Generates + consumes the typed API client (D-16) | Already the project's declared `openapi-client` skill tooling |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `next-themes` or plain Tailwind tokens | n/a | Reuse landing-page's `@theme` gold/charcoal palette (D-15) | Copy `--color-gold*`/`--font-*` tokens into `dashboard/app/globals.css`, don't reinvent a second palette |
| `date-fns` or `Intl.DateTimeFormat` (built-in) | n/a | Format salon-local (Asia/Yangon) times with labelled zone (D-07) | Prefer built-in `Intl` with an explicit `timeZone: "Asia/Yangon"` option — avoids a new date library dependency the backend already solves server-side |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| SWR | `@tanstack/react-query` | Richer mutation/cache primitives and devtools, but ~3x the bundle size and more config for the same 60s-poll + focus-refetch behavior D-14 asks for; overkill for this phase's read-mostly + few-mutations shape |
| Hand-rolled CSS Grid time-axis | `react-big-calendar` (MIT) | Free, but its resource/day-column view is thin, poorly documented, and still requires hand-building event blocks, quick-action buttons, and duration-based sizing — little complexity saved for a very bespoke layout |
| Hand-rolled CSS Grid time-axis | FullCalendar + `@fullcalendar/resource-timegrid` | The exact intended layout (stylist columns) ships as a **Premium** plugin requiring a paid commercial license for for-profit use [CITED: fullcalendar.io/license] — not viable without a purchase |
| `IdentityDbContext<ApplicationUser, IdentityRole, int>` inheritance | Hand-rolled `StaffUser`/`StaffRole` tables | Rejected by D-02 explicitly — Phase 7's ACCT-05 needs one shared Identity schema, not a custom one to migrate later |

**Installation:**
```bash
# API (from API/ZachHairStudio.Shared or ZachHairStudio.Api as appropriate)
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 10.0.9
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.9

# dashboard/ (after scaffolding with create-next-app, mirroring landing-page/ versions)
npm install swr
npx -y openapi-typescript http://localhost:5236/openapi/v1.json -o lib/api/schema.d.ts
npm install openapi-fetch
```

**Version verification:** Both NuGet packages confirmed current-stable via the NuGet v3 flat-container index (`10.0.9`, matching the existing `Microsoft.EntityFrameworkCore*`/`Microsoft.AspNetCore.OpenApi` 10.0.9 pins in the repo) [VERIFIED: nuget.org registry]. `swr` (2.4.2), `openapi-fetch` (0.17.0), `openapi-typescript` (7.13.0), `react-big-calendar` (1.20.0), `jwt-decode` (4.0.0) confirmed present on the npm registry via `npm view <pkg> version` [VERIFIED: npm registry] — package-name legitimacy itself is a separate check, see the audit below.

## Package Legitimacy Audit

| Package | Registry | Age (latest publish) | Downloads/wk | Source Repo | Verdict | Disposition |
|---------|----------|----------------------|--------------|--------------|---------|-------------|
| `swr` | npm | 2026-06-22 (recent point release) | 12,417,262 | github.com/vercel/swr | [SUS] (tool reason: "too-new") | **Kept** — the "too-new" signal fires on the latest *point-release* date, not package age; 12.4M weekly downloads and the `vercel/swr` org-owned repo make this a false positive. Planner should still add a light `checkpoint:human-verify` before `npm install swr` per protocol, but no safer alternative exists for this specific need. |
| `@tanstack/react-query` | npm | 2026-06-27 (recent point release) | 59,380,417 | github.com/TanStack/query | [SUS] (tool reason: "too-new") | **Not selected** (SWR chosen instead) — same false-positive pattern; noted only because it appears in the Alternatives Considered row above. If the planner prefers React Query over SWR, apply the same `checkpoint:human-verify` treatment. |
| `openapi-fetch` | npm | 2026-02-11 | 5,648,493 | github.com/openapi-ts/openapi-typescript | [OK] | Approved |
| `openapi-typescript` | npm | 2026-02-11 | 4,670,917 | github.com/openapi-ts/openapi-typescript | [OK] | Approved |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | NuGet | 10.0.9 current | n/a (NuGet doesn't expose weekly downloads the same way) | github.com/dotnet/aspnetcore | Not run through the npm/pypi/crates seam (NuGet unsupported by this tool) — verified directly against nuget.org's flat-container index and official docs | Approved [VERIFIED: nuget.org + Context7] |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | NuGet | 10.0.9 current | n/a | github.com/dotnet/aspnetcore | Same as above | Approved [VERIFIED: nuget.org + Context7] |

**Packages removed due to [SLOP] verdict:** none.
**Packages flagged as suspicious [SUS]:** `swr`, `@tanstack/react-query` — both are false positives from a "recently published" heuristic reacting to routine point releases of extremely well-established, high-download libraries; the planner should still gate the actual `npm install` behind a `checkpoint:human-verify` per protocol, noting this rationale so the checkpoint isn't a surprise.

*Package names in this research were discovered via WebSearch/training data and are tagged `[ASSUMED]` regardless of registry existence, per the provenance rule — the planner must gate each first-time install behind `checkpoint:human-verify`.*

## Architecture Patterns

### System Architecture Diagram

```
Staff browser
   │
   │  1. POST /api/auth/login {email, password}
   ▼
dashboard/ (Next.js 15, client-rendered pages)
   │
   │  2. 200 OK { token, expiresAt, role }  →  stored client-side (memory/localStorage)
   ▼
Staff browser (holds JWT)
   │
   │  3. GET /api/appointments?from=...&to=...   Authorization: Bearer <jwt>
   │  4. PATCH /api/appointments/{id}/status      Authorization: Bearer <jwt>
   ▼
ASP.NET Core API (JwtBearer middleware)
   │
   │  5. [Authorize] validates signature+expiry+role claim
   │     → 401 if missing/invalid/expired  (DASH-05 proof point)
   ▼
AppointmentsService (feature-layer, PLAT-01)
   │
   │  6. Query Appointments by date range (salon-local day/week window)
   │  7. On status update: validate transition (D-10) → update Status,
   │     StatusChangedAt, StatusChangedBy → if slot-releasing (Cancel/NoShow),
   │     delete AppointmentSlot rows (same code path as Phase 2 cancel, D-04)
   ▼
BookingDbContext (SQL Server)
   │  Appointments, AppointmentSlots, AspNetUsers, AspNetRoles, AspNetUserRoles
   ▼
Response DTOs ──► dashboard renders day-view time-grid / week-view chips
   │
   └─ 8. Polling: every ~60s + on window focus + manual refresh button (D-14)
        re-issues step 3, diffing into the same grid state
```

### Recommended Project Structure

Backend additions (feature-folder convention, mirroring `Features/Appointments/`):
```
API/ZachHairStudio.Shared/
├── Features/
│   ├── Identity/
│   │   ├── ApplicationUser.cs          # : IdentityUser<int> (or default string key — Claude's discretion)
│   │   ├── StaffRoles.cs               # const strings "Owner", "Staff"
│   │   ├── StaffUserCreateDto.cs       # Owner-only "add staff" payload
│   │   ├── StaffUserResponseDto.cs
│   │   ├── LoginRequestDto.cs
│   │   ├── LoginResponseDto.cs         # { token, expiresAt, displayName, role }
│   │   ├── JwtOptions.cs               # signing key ref (user-secrets/env), issuer, lifetime
│   │   ├── JwtTokenService.cs          # mints tokens from ApplicationUser + roles
│   │   └── IdentitySeeder.cs           # startup: ensure Owner/Staff roles + seeded Owner exist
│   └── Appointments/
│       ├── AppointmentStatusUpdateDto.cs
│       ├── AppointmentStatusUpdateDtoValidator.cs
│       └── AppointmentsService.cs      # extended: ListByDateRangeAsync, GetByIdAsync, UpdateStatusAsync
API/ZachHairStudio.Api/Controllers/
├── AuthController.cs                   # POST /api/auth/login
├── StaffUsersController.cs             # [Authorize(Roles="Owner")] add-staff screen backing endpoint
└── AppointmentsController.cs           # extended with [Authorize] GET (range/detail) + PATCH status
```

Frontend (`dashboard/`, mirroring `landing-page/` conventions):
```
dashboard/
├── app/
│   ├── login/page.tsx                 # public
│   ├── schedule/page.tsx               # protected, default landing route (D-07)
│   ├── schedule/[date]/page.tsx        # optional — or query-param driven, Claude's discretion
│   └── layout.tsx                      # brand tokens, auth provider/context
├── components/
│   ├── DayGrid.tsx                     # stylist columns × time axis (D-05)
│   ├── WeekChips.tsx                   # 7-day condensed view (D-06)
│   ├── AppointmentBlock.tsx            # block + quick actions (D-09)
│   ├── AppointmentDetailPanel.tsx      # detail view + status controls (D-09)
│   └── ScheduleToolbar.tsx             # prev/next, Today, date picker, Day/Week toggle (D-07)
├── lib/
│   ├── api/                            # generated schema.d.ts + openapi-fetch client (D-16)
│   ├── auth.ts                         # token storage, attach header, 401 handling
│   └── useSchedule.ts                  # SWR hook: polling + focus-refetch (D-14)
└── app/globals.css                     # brand tokens copied from landing-page (D-15)
```

### Pattern 1: Identity attached to the existing DbContext

**What:** `BookingDbContext` inherits `IdentityDbContext<ApplicationUser, IdentityRole, int>` (or the model gets Identity's tables via the Identity model-builder call inside `OnModelCreating`), so Identity's `AspNetUsers`/`AspNetRoles`/etc. tables live in the same database and migration history as `Appointments`.
**When to use:** Exactly this phase's D-02 requirement — one schema now, extended (not migrated) by Phase 7 for client accounts (ACCT-05).
**Example:**
```csharp
// Source: ASP.NET Core docs pattern (Context7 /dotnet/aspnetcore.docs), adapted to this repo's naming
public void ConfigureServices(IServiceCollection services)
{
    services.AddDbContext<BookingDbContext>(options =>
        options.UseSqlServer(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure(...)));

    services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // password policy, lockout settings — Claude's discretion, keep ASP.NET defaults
            // unless the phase discussion calls for something stricter
        })
        .AddEntityFrameworkStores<BookingDbContext>()
        .AddDefaultTokenProviders();
}
```

### Pattern 2: JWT issuance + validation

**What:** `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` validates incoming tokens; a small `JwtTokenService` mints them at login using the same signing key.
**When to use:** D-03's mechanism, exactly.
**Example:**
```csharp
// Source: ASP.NET Core gRPC ticketer sample (Context7 /dotnet/aspnetcore.docs) — pattern adapted,
// not the literal gRPC sample code. Use a real secret from user-secrets/env (D-13-style), never Guid.NewGuid().
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        };
    });

builder.Services.AddAuthorization(); // enables [Authorize] / [Authorize(Roles = "Owner")]
```
Token minting (login endpoint):
```csharp
var claims = new List<Claim>
{
    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new(ClaimTypes.Name, user.UserName!),
};
claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
var token = new JwtSecurityToken(
    issuer: jwtOptions.Issuer,
    audience: jwtOptions.Audience,
    claims: claims,
    expires: DateTime.UtcNow.AddHours(12), // D-03's ~12h workday token
    signingCredentials: credentials);

return new JwtSecurityTokenHandler().WriteToken(token);
```

### Pattern 3: Constrained status transitions (D-10)

**What:** A small allow-list map checked server-side before any status write; anything outside it returns `Result<T>.ValidationError` → 400 ProblemDetails (mirroring the existing `IsValidationError()` translation in `AppointmentsController`).
**When to use:** Every status-update request, regardless of which UI surface triggered it (D-09's two entry points funnel into one endpoint).
**Example:**
```csharp
private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitions = new()
{
    [AppointmentStatus.Confirmed] = [AppointmentStatus.Completed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow],
    // Completed, Cancelled, NoShow are terminal — no entries, no outbound transitions.
};

public async Task<Result<AppointmentResponseDto>> UpdateStatusAsync(int id, AppointmentStatus newStatus, string staffUserId)
{
    var appointment = await _dbContext.Appointments.Include(a => a.Slots).FirstOrDefaultAsync(a => a.Id == id);
    if (appointment is null) return Result<AppointmentResponseDto>.NotFoundError("Appointment not found.");

    if (!AllowedTransitions.TryGetValue(appointment.Status, out var allowed) || !allowed.Contains(newStatus))
    {
        return Result<AppointmentResponseDto>.ValidationError(
            $"Cannot move from {appointment.Status} to {newStatus}.");
    }

    if (newStatus is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
    {
        _dbContext.AppointmentSlots.RemoveRange(appointment.Slots); // reuse Phase 2 D-04's cancel path
    }

    appointment.Status = newStatus;
    appointment.StatusChangedAt = DateTimeOffset.UtcNow;
    appointment.StatusChangedBy = staffUserId;

    await _dbContext.SaveChangesAsync();
    // ... map to dto, return Success
}
```

### Pattern 4: SWR polling + focus refetch (D-14)

**What:** A single hook wraps the generated OpenAPI client call with SWR's `refreshInterval` and default focus-revalidation.
**Example:**
```typescript
// Source: SWR docs pattern (general knowledge, [ASSUMED] — verify against swr.vercel.app before implementing)
import useSWR from "swr";
import { client } from "@/lib/api/client";

export function useSchedule(from: string, to: string) {
  return useSWR(
    ["appointments", from, to],
    () => client.GET("/api/appointments", { params: { query: { from, to } } }),
    {
      refreshInterval: 60_000,       // D-14: ~60s poll
      revalidateOnFocus: true,       // D-14: tab-focus refetch (SWR default, stated explicitly)
    }
  );
}
```

### Anti-Patterns to Avoid

- **Hand-rolled password hashing / token store:** D-02 explicitly rejects this — always go through Identity's `UserManager`/`PasswordHasher`.
- **Trusting the client-echoed status in a PATCH body without re-validating current DB state:** the transition check must read the *current* `Status` from the database inside the same request, not trust a stale client-side copy — otherwise a second stale tab could apply an already-superseded transition.
- **Copy-pasting the slot-release logic instead of reusing Phase 2's cancel path:** D-04 (Phase 2) and the no-show/cancel status update must delete `AppointmentSlot` rows through the same code path, not two independently-maintained copies that could drift.
- **Using cookies/NextAuth for the dashboard session:** D-03 is explicit — bearer tokens attached manually, not a cookie session. Introducing NextAuth or iron-session here would silently contradict a locked user decision.
- **Reaching for FullCalendar's resource view without checking licensing first:** the stylist-column layout is exactly FullCalendar Premium's `resource-timegrid`, which is not free for commercial use — confirm this before any spike time is sunk into it.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Password hashing, lockout, token providers | A custom `StaffUser` table + manual bcrypt/argon2 calls | ASP.NET Core Identity (`AddIdentity` + `AddEntityFrameworkStores`) | Battle-tested, handles lockout/reset-token edge cases, and is exactly what D-02 mandates for Phase 7 schema reuse |
| JWT signing/validation | Hand-parsed base64 tokens with custom HMAC checks | `Microsoft.AspNetCore.Authentication.JwtBearer` + `System.IdentityModel.Tokens.Jwt` | Handles clock-skew, expiry, issuer/audience validation, and integrates with `[Authorize]`/claims out of the box |
| Typed API client for the dashboard | Hand-written `fetch()` wrappers per endpoint | `openapi-typescript` + `openapi-fetch` (D-16, existing skill) | Already the project's declared convention; keeps OpenAPI as the single source of truth as new staff endpoints are added |
| Polling/focus-refetch data layer | A manual `setInterval` + `document.visibilitychange` listener with hand-rolled dedup | SWR (`refreshInterval`, `revalidateOnFocus`) | A few lines of config replace error-prone manual interval/cleanup logic and dedupe concurrent requests automatically |

**Key insight:** Everything auth-related in this phase (hashing, tokens, lockout, claims) has a mature, first-party ASP.NET Core answer — the only genuinely custom piece is the *status-transition state machine* (D-10), which is intentionally small and domain-specific enough that a library would be overkill.

## Common Pitfalls

### Pitfall 1: InMemory EF Core provider + ASP.NET Core Identity in tests

**What goes wrong:** The existing `CustomWebApplicationFactory` swaps `BookingDbContext` to `UseInMemoryDatabase` for fast tests. Some Identity operations (e.g., certain `UserManager` calls that rely on relational features or explicit transactions) can behave subtly differently against the InMemory provider than against SQL Server.
**Why it happens:** InMemory is not a relational provider; unique-index and transaction semantics differ (the codebase already hit this exact class of gap with the SQL Server 2601/2627 duplicate-key case in Phase 2, which is precisely why `SqlServerWebApplicationFactory`/`SqlServerFixtureSmokeTests` exist alongside the InMemory factory).
**How to avoid:** Keep new Identity-touching tests (login, role assignment) on the same `SqlServerWebApplicationFactory` pattern already established for concurrency-sensitive tests, and use the InMemory factory only for tests that don't depend on Identity's relational specifics.
**Warning signs:** A login/seed test passes against InMemory but fails against a real LocalDB/SQL Server run.

### Pitfall 2: CORS + bearer tokens still needs an explicit origin allow-list

**What goes wrong:** Because bearer tokens (unlike cookies) don't require `AllowCredentials()`, it's tempting to assume the current `AllowAnyOrigin()` policy is "fine" for bearer auth and skip updating CORS at all.
**Why it happens:** `AllowAnyOrigin()` already works today because Phase 2's endpoints are public; adding `[Authorize]` doesn't change the CORS policy on its own — the browser still needs the dashboard's origin permitted to even receive the response.
**How to avoid:** Explicitly confirm (or extend) the CORS policy to include the dashboard's dev/prod origins now, even though `AllowAnyOrigin()` remains until Phase 8 (LAUNCH-02) tightens it. Don't conflate "bearer tokens don't need credentialed CORS" with "no CORS changes needed."
**Warning signs:** Dashboard fetches fail with an opaque CORS error in the browser console despite the API returning 200 when hit directly (e.g., via curl/Postman).

### Pitfall 3: Status-update endpoint bypassing the transition guard via a different route

**What goes wrong:** If both the schedule-block quick actions (D-09) and the detail-view controls end up calling *different* backend endpoints (e.g., one PATCHes status directly, another goes through a generic PUT), the transition rule (D-10) could be enforced in one path and missed in the other.
**Why it happens:** Two UI entry points naturally invite two client-side call sites; without discipline they can drift to two server endpoints.
**How to avoid:** Route both UI surfaces through the *same* single status-update endpoint/service method (`UpdateStatusAsync`), never duplicate the transition table.
**Warning signs:** A code review finds two different places computing "is this transition allowed."

### Pitfall 4: No-show and cancelled queried together by accident

**What goes wrong:** A "hide inactive appointments" filter implemented as `Status != Confirmed` (or similar) would accidentally lump `NoShow` and `Cancelled` together whenever a report or the "show cancelled/no-show" toggle (D-08) tries to isolate one from the other.
**Why it happens:** Both are terminal/slot-releasing statuses that "feel" similar, tempting a shared boolean flag (e.g., `IsInactive`) instead of keeping `Status` as the single source of truth.
**How to avoid:** Always filter/query by the explicit `AppointmentStatus` enum value, never introduce a derived boolean that conflates the two (this is literally DASH-04's proof condition).
**Warning signs:** A "no-show report" or count also includes cancelled appointments, or vice versa.

### Pitfall 5: Signing key stored insecurely or rotated without invalidating tokens

**What goes wrong:** Committing a JWT signing key to `appsettings.json` (tracked file) would trip gitleaks and violate the project's secret-scanning constraint; conversely, forgetting the key must stay *stable* across deploys would silently invalidate all outstanding ~12h tokens on every restart if regenerated randomly (e.g., `Guid.NewGuid()` per process start, as seen in the Context7 gRPC sample — fine for a toy sample, wrong for this phase).
**Why it happens:** The quickest sample code online generates the key in-memory at startup for demo purposes.
**How to avoid:** Store the signing key via `dotnet user-secrets` in dev and an environment variable in production (mirroring the existing `RESEND_API_KEY` pattern, D-13-style), read once at startup, never regenerate per-process.
**Warning signs:** All staff get logged out simultaneously after any API restart/deploy.

## Code Examples

### Extending `AppointmentResponseDto` for the status audit (D-12)

```csharp
// Source: this repo, AppointmentResponseDto.cs — extend with the two D-12 audit fields
public class AppointmentResponseDto
{
    // ...existing fields...
    public DateTimeOffset? StatusChangedAt { get; set; }
    public string? StatusChangedBy { get; set; } // display name or username of the staff user
}
```

### `[Authorize]` gating on the controller (DASH-05)

```csharp
// Source: ASP.NET Core authorization docs pattern (Context7 /dotnet/aspnetcore.docs)
[ApiController]
[Route("api/[controller]")]
[Authorize] // staff-only by default; public booking endpoints (slots, create) stay [AllowAnonymous]
public class AppointmentsController : ControllerBase
{
    [HttpPost]
    [AllowAnonymous] // Phase 2's public booking create endpoint remains anonymous
    public async Task<ActionResult<AppointmentResponseDto>> CreateAppointment(...) { ... }

    [HttpGet("slots")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<OpenSlotDto>>> GetSlots(...) { ... }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponseDto>>> GetByDateRange(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to) { ... } // staff-only, new

    [HttpGet("{id}")]
    public async Task<ActionResult<AppointmentResponseDto>> GetById(int id) { ... } // staff-only, new

    [HttpPatch("{id}/status")]
    public async Task<ActionResult<AppointmentResponseDto>> UpdateStatus(
        int id, [FromBody] AppointmentStatusUpdateDto request) { ... } // staff-only, new

    [HttpPost("owner-only-add-staff")] // or a separate StaffUsersController — Claude's discretion
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<StaffUserResponseDto>> AddStaffUser(...) { ... }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Cookie-based ASP.NET Core Identity UI scaffolding (Razor Pages `Identity.UI`) | Custom JWT-issuing login endpoint over `AddIdentity` + `AddJwtBearer`, no Identity UI pages | N/A — this project never adopts Identity's Razor UI scaffolding; D-03 explicitly wants JWT, not cookies | The `Identity.UI` NuGet package and its Razor Pages are not needed at all in this phase — don't install `Microsoft.AspNetCore.Identity.UI` |
| `IdentityServer`/`AddApiAuthorization` (seen in some ASP.NET Core SPA templates, e.g., the "SpaWithAuth" Context7 sample) | Plain `AddJwtBearer` with a symmetric key issued by your own login endpoint | This project doesn't need IdentityServer/Duende's SPA-authorization template — that's built for a Next.js/React SPA hosted *by* the same app with OIDC, which is heavier than a single-salon internal dashboard needs | Skip `AddIdentityServer()`/`AddIdentityServerJwt()` entirely; they add OIDC/IdentityServer complexity this phase's D-03 doesn't call for |

**Deprecated/outdated:**
- `Microsoft.AspNetCore.Identity.UI` Razor scaffolding: not applicable — this is an API-only Identity setup with a Next.js frontend, no server-rendered login pages from the API itself.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `swr` is the right polling library for D-14 (vs. `@tanstack/react-query` or hand-rolled) | Standard Stack / Alternatives Considered | Low — both work; switching later is a contained refactor of `lib/useSchedule.ts` only |
| A2 | Hand-rolled CSS Grid time-axis beats `react-big-calendar`/FullCalendar for D-05 | Architecture Patterns, Don't Hand-Roll | Medium — if the day-grid interactions grow complex (drag-to-reschedule, resize) later phases, a library might have been cheaper long-term; revisit if Phase 4+ adds drag/drop |
| A3 | `BookingDbContext : IdentityDbContext<ApplicationUser, IdentityRole, int>` (int keys) is the right Identity key type, vs. the default `string`/GUID key | Architecture Patterns Pattern 1 | Low-medium — an int PK is smaller/simpler and matches this repo's other entities' `int Id`, but switching Identity's key type after seeding is a real migration; confirm during planning, not mid-implementation |
| A4 | A symmetric HMAC signing key (vs. asymmetric RSA/ECDSA) is sufficient for JWT signing in this single-API, single-audience setup | Architecture Patterns Pattern 2 | Low — asymmetric keys matter when multiple services must validate tokens without holding the signing secret; this phase has one API issuing and validating, so symmetric is standard and sufficient |
| A5 | Owner-only "add staff" endpoint lives on `AppointmentsController`/a new `StaffUsersController` rather than folding into `AuthController` | Recommended Project Structure | Low — purely an organizational choice left to planning; no functional risk |
| A6 | `swr`/`@tanstack/react-query` package-name legitimacy: both are genuine, high-download, org-owned packages despite the automated "too-new" SUS flag | Package Legitimacy Audit | Low — corroborated by 12M+/59M+ weekly downloads and matching GitHub org ownership (`vercel/swr`, `TanStack/query`), but flagged here per protocol since the verdict came back SUS |

## Open Questions

1. **Identity primary-key type for `ApplicationUser`/`IdentityRole`**
   - What we know: The rest of the schema uses `int` PKs (`Service.Id`, `Appointment.Id`, etc.).
   - What's unclear: Whether to key Identity as `IdentityUser<int>`/`IdentityRole<int>` (consistent with the rest of the schema) or accept Identity's default `string` (GUID) key, which is what most ASP.NET Core samples show out of the box.
   - Recommendation: Default to `int` keys for consistency with the existing schema unless Phase 7's client-account needs (not yet researched) push toward GUIDs; flag this as a planning decision, not something to leave implicit in code.

2. **Where does the day/week date-range endpoint live — `AppointmentsController` or a new `ScheduleController`?**
   - What we know: CONTEXT.md explicitly leaves this to Claude's discretion, with the one hard constraint being PLAT-01 (service layer boundary) and `Result<T>` → ProblemDetails translation.
   - What's unclear: Whether bundling staff-only endpoints into the existing public `AppointmentsController` (with mixed `[AllowAnonymous]`/`[Authorize]` attributes per action) is cleaner than a separate staff-scoped controller.
   - Recommendation: Lean toward keeping `AppointmentsController` as-is for the public create/slots actions and adding a new `[Authorize]`-by-default controller (e.g., `ScheduleController` or `StaffAppointmentsController`) for the day/week/detail/status-update actions — reduces the risk of an `[AllowAnonymous]` forgotten on a new public action or an `[Authorize]` forgotten on a new staff action. Final call belongs to planning.

3. **Exact JWT claims shape beyond `NameIdentifier`/`Name`/`Role`**
   - What we know: `StatusChangedBy` (D-12) needs to record "the authenticated staff user" — likely a display name or username.
   - What's unclear: Whether `StatusChangedBy` should store the Identity user's `Id`, `UserName`, or a friendlier `DisplayName` claim not yet modeled on `ApplicationUser`.
   - Recommendation: Add a `DisplayName` property to `ApplicationUser` (distinct from the Identity `UserName`/login) so the detail view can show "Cancelled by Aria Chen" rather than a raw username/email; confirm this doesn't conflict with any Phase 4 staff-management assumptions.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 10 | Identity/JwtBearer packages, API build | ✓ (per CLAUDE.md platform requirements) | 10.x | — |
| Node.js 18+ / npm | `dashboard/` scaffold, SWR, OpenAPI client generation | ✓ (per CLAUDE.md) | 18+ | — |
| SQL Server LocalDB | Identity tables via migration | ✓ (per STATE.md — `(localdb)\MSSQLLocalDB` resolved 2026-07-09) | v17.0.4025.3 | Azure SQL override already in use as a documented fallback |
| Running API instance (for OpenAPI doc generation) | `openapi-client` skill / `dashboard/` client generation | Not yet verified in this session — requires the **dev** skill to start the API before generating | — | Run the **dev** skill first; do not hand-write the client if generation is blocked |

**Missing dependencies with no fallback:** none identified.
**Missing dependencies with fallback:** none blocking — the only pre-condition (API must be running to hit `/openapi/v1.json`) is already solved by the existing **dev** project skill.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 (existing `ZachHairStudio.Api.Tests` project) |
| Config file | `API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` |
| Quick run command | `dotnet test API/ZachHairStudio.Api.Tests --filter FullyQualifiedName~Appointments` (or `~Identity`/`~Auth` once new test folders exist) |
| Full suite command | `dotnet test API/ZachHairStudio.slnx` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DASH-01 | GET date-range returns appointments for a given day/week window | integration | `dotnet test --filter FullyQualifiedName~ScheduleControllerTests` | ❌ Wave 0 |
| DASH-02 | GET by id returns full appointment detail | integration | `dotnet test --filter FullyQualifiedName~ScheduleControllerTests` | ❌ Wave 0 |
| DASH-03 | PATCH status succeeds for allowed transitions | integration | `dotnet test --filter FullyQualifiedName~StatusUpdateTests` | ❌ Wave 0 |
| DASH-03 | PATCH status rejects invalid transitions with 400 ProblemDetails | integration | `dotnet test --filter FullyQualifiedName~StatusUpdateTests` | ❌ Wave 0 |
| DASH-04 | Filtering by status returns no-show separately from cancelled | unit/integration | `dotnet test --filter FullyQualifiedName~StatusUpdateTests` | ❌ Wave 0 |
| DASH-05 | Unauthenticated request to any staff endpoint returns 401 | integration | `dotnet test --filter FullyQualifiedName~AuthGateTests` | ❌ Wave 0 |
| DASH-05 | Valid staff JWT is accepted; Owner-only endpoint rejects Staff-role tokens | integration | `dotnet test --filter FullyQualifiedName~AuthGateTests` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test API/ZachHairStudio.Api.Tests --filter <relevant-namespace>` (fast, targeted)
- **Per wave merge:** `dotnet test API/ZachHairStudio.slnx` (full suite, both InMemory and SQL Server-backed tests)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `API/ZachHairStudio.Api.Tests/Features/Identity/AuthGateTests.cs` — covers DASH-05 (401 without token, 403/401 on role mismatch, 200 with valid staff token)
- [ ] `API/ZachHairStudio.Api.Tests/Features/Identity/IdentitySeederTests.cs` — covers D-04's seeded-Owner-only guarantee
- [ ] `API/ZachHairStudio.Api.Tests/Features/Appointments/ScheduleControllerTests.cs` — covers DASH-01/DASH-02 (date-range + detail)
- [ ] `API/ZachHairStudio.Api.Tests/Features/Appointments/StatusUpdateTests.cs` — covers DASH-03/DASH-04 (transition matrix + no-show/cancelled separability)
- [ ] A `SqlServerWebApplicationFactory`-based (not InMemory) auth test, per Pitfall 1 above, to catch any Identity/relational-provider drift
- [ ] Framework install: none — xUnit/Mvc.Testing already present; only new test files are needed

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | ASP.NET Core Identity (`PasswordHasher`, lockout policy) — never hand-rolled hashing |
| V3 Session Management | yes | JWT bearer with ~12h expiry (D-03), `ValidateLifetime = true`; no refresh tokens this phase (deferred, documented risk) |
| V4 Access Control | yes | `[Authorize]` by default on staff endpoints, `[Authorize(Roles = "Owner")]` on the add-staff action; role claims validated server-side per request |
| V5 Input Validation | yes | FluentValidation on `AppointmentStatusUpdateDto`, `StaffUserCreateDto`, `LoginRequestDto` — matches PLAT-02 |
| V6 Cryptography | yes | JWT signing key via `SymmetricSecurityKey` sourced from user-secrets/env (never hardcoded, never committed); HMAC-SHA256 signing — do not hand-roll a custom token format |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Forged/tampered JWT (signature stripped or algorithm confusion, e.g. `alg: none`) | Tampering, Elevation of Privilege | `JwtBearer`'s `TokenValidationParameters` with `ValidateIssuerSigningKey = true` and an explicit algorithm allow-list rejects `alg: none` and mismatched signatures by default |
| Stale/replayed token after a staff account is deactivated mid-shift | Elevation of Privilege | Documented, accepted risk this phase (no refresh/blacklist machinery, D-03) — short ~12h expiry bounds the exposure window; Phase 7/8 hardening can add revocation if needed |
| CORS misconfiguration exposing staff endpoints to arbitrary origins | Information Disclosure, Tampering | Explicit origin allow-list update for the dashboard origin (see Pitfall 2); full lockdown to production-only origins is LAUNCH-02 (Phase 8) |
| Login endpoint brute-forcing the Owner/staff password | Information Disclosure | Identity's built-in lockout (`options.Lockout`) after N failed attempts; rate limiting on `/api/auth/login` is explicitly Phase 8 (LAUNCH-05) — acceptable gap for this phase given mode:mvp, but should be called out to the user if not addressed |
| IDOR on `GET /api/appointments/{id}` or the status-update endpoint | Tampering, Information Disclosure | Low residual risk this phase since all staff share full read/write over all appointments by design (no per-stylist row-level restriction requested); revisit only if the phase discussion later wants stylist-scoped visibility |

## Sources

### Primary (HIGH confidence)
- None obtained at HIGH tier this session — `npm view`/NuGet registry lookups are tagged [VERIFIED: registry] for version facts, but package-name legitimacy and architectural guidance below are MEDIUM/LOW per the source hierarchy.

### Secondary (MEDIUM confidence)
- Context7 `/dotnet/aspnetcore.docs` — ASP.NET Core Identity + EF Core configuration, JWT bearer middleware configuration, role-based `[Authorize]` samples [CITED: github.com/dotnet/aspnetcore.docs via Context7]
- nuget.org v3 flat-container registry — `Microsoft.AspNetCore.Identity.EntityFrameworkCore` and `Microsoft.AspNetCore.Authentication.JwtBearer` current version `10.0.9` [VERIFIED: nuget.org registry]
- npm registry (`npm view`) — `swr` 2.4.2, `@tanstack/react-query` 5.101.2, `openapi-fetch` 0.17.0, `openapi-typescript` 7.13.0, `react-big-calendar` 1.20.0, `jwt-decode` 4.0.0 [VERIFIED: npm registry — version facts only, not package-identity legitimacy]
- fullcalendar.io/license — Premium plugin (including resource-timegrid) commercial licensing terms [CITED: fullcalendar.io/license]

### Tertiary (LOW confidence)
- WebSearch: Identity role/admin seeding best practices (multiple blog posts, no single canonical source) [ASSUMED — pattern corroborated across several independent sources but not an official doc]
- WebSearch: Next.js 15 + external-API bearer-token dashboard patterns [ASSUMED — general community guidance, no official Next.js doc addresses this exact "external API bearer token, not Next-native session" shape]
- WebSearch: SWR vs TanStack Query bundle size/feature comparison [ASSUMED — comparison articles, not official benchmarks from either project]
- WebSearch: react-big-calendar vs FullCalendar tradeoffs [ASSUMED — comparison articles; FullCalendar licensing claim corroborated directly against fullcalendar.io/license, upgraded to CITED for that specific fact]

## Metadata

**Confidence breakdown:**
- Standard stack: MEDIUM — Identity/JwtBearer package choice and versions are CITED/VERIFIED against official docs and the registry; the frontend polling/calendar library choices are LOW-sourced comparisons resolved into a MEDIUM recommendation via first-party licensing facts (FullCalendar) and clear architectural reasoning (bespoke D-05 layout).
- Architecture: MEDIUM — the auth wiring patterns come from official ASP.NET Core doc snippets (via Context7); the status-transition state machine and project structure are original design work grounded in the existing codebase's established conventions (Result<T>, feature folders), not fetched from an external source.
- Pitfalls: MEDIUM — Pitfalls 1–4 are derived directly from this repo's existing test infrastructure and Phase 2 decisions (traceable, not speculative); Pitfall 5 is general JWT operational-security knowledge [ASSUMED] corroborated by the project's own existing secret-handling pattern (RESEND_API_KEY/D-13).

**Research date:** 2026-07-11
**Valid until:** 2026-08-10 (30 days — stable, first-party ASP.NET Core/EF Core APIs; re-verify npm package versions if planning is delayed past this window, per the fast-moving JS ecosystem)
