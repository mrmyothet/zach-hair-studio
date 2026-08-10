# Phase 7: Accounts & Retention - Research

**Researched:** 2026-08-10
**Domain:** ASP.NET Core Identity + JWT client accounts, ownership-gated history, self-service cancel/reschedule, loyalty ledger discount
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Same ASP.NET Core Identity store as staff — add `Client` role to existing `ApplicationUser` / `BookingDbContext` Identity schema (ACCT-05; Phase 3 D-02).
- **D-02:** Client session transport is JWT in localStorage on landing-page, mirroring `dashboard/lib/auth.ts` (Phase 3 D-03 JWT lock — one pattern).
- **D-03:** Email + password register/login on landing-page `/account/*` routes; no OAuth this phase.
- **D-04:** On register, optionally claim guest bookings/orders by matching Email (confirm when ambiguous); history is not forced empty.
- **D-05:** Account UI lives on `landing-page/` (`/account`, `/account/bookings`, `/account/orders`) — not under `dashboard/`.
- **D-06:** History presented as two tabs: Bookings | Orders, date-desc lists with detail views.
- **D-07:** Navbar shows Account when JWT present; Login/Register when not (alongside cart).
- **D-08:** Server ownership only — filter/authorize by authenticated user id / linked ClientId; cross-client ID access rejected (ACCT-03/06). Never trust client-supplied owner IDs.
- **D-09:** Client cancel reuses `Confirmed → Cancelled` transition (releases `AppointmentSlot` rows) via ownership-gated client endpoint.
- **D-10:** Reschedule = cancel-and-rebook in one UX/transaction (new open slot → new appointment → cancel old) to preserve unique-index invariant.
- **D-11:** Cancel/reschedule allowed until appointment start (no deposit cutoff this phase).
- **D-12:** Only the owning client (Client role + ownership) may self-service; staff keep dashboard status path.
- **D-13:** Earn +1 point when staff marks appointment `Completed`.
- **D-14:** `LoyaltyLedger` append-only rows (ClientUserId, Delta, Reason, AppointmentId?, CreatedAt); balance = sum of deltas.
- **D-15:** Redeem at product checkout as server-computed $ discount (Phase 6 price authority — never trust client discount $).
- **D-16:** MVP rates: 1 pt per completed appointment; 10 pts = $5 off (constants; Claude may tune). Not RETN-02 tiers.

### Claude's Discretion
- Exact JWT claim shape for Client role, register validation rules, claim-by-email confirmation UX, discount application order vs tax/shipping (no shipping yet), and ledger reason enum values — follow codebase conventions.

### Deferred Ideas (OUT OF SCOPE)
- OAuth / magic-link (out of scope this phase).
- RETN-02 tiered loyalty; RETN-01 deposits/no-show fees; RETN-03 book-again shortcut.
- Refresh-token hardening beyond Phase 3 workday JWT (optional later).
- Phase 6 Stripe human UAT (`verification_deferred_human`) — resume `/gsd-verify-work 6`.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| ACCT-01 | Client can create an account and log in | Extend Identity with `Client` role; `POST /api/auth/register` + reuse login; landing-page `/account/*` JWT localStorage mirror |
| ACCT-02 | Client can view their booking history | Ownership-filtered appointments API; optional `ClientUserId` FK + email claim on register |
| ACCT-03 | Client can view their order history | Ownership-filtered orders via `Order.ClientId` (already nullable); claim guest orders by email |
| ACCT-04 | Cancel or reschedule own upcoming appointment | Ownership-gated cancel reusing `Confirmed→Cancelled`; reschedule = transactional cancel-and-rebook |
| ACCT-05 | Single ASP.NET Core Identity schema | **CONFIRMED** — Identity+JWT already in repo; add `Client` role only; **no Auth.js** |
| ACCT-06 | Ownership checks prevent IDOR | Server filters by `NameIdentifier` / linked ClientId; never trust client-supplied owner IDs |
| ACCT-07 | Loyalty points + checkout discount | `LoyaltyLedger` append-only; earn on Completed; redeem server-side $ at checkout |
</phase_requirements>

## Summary

Phase 7 adds **client accounts on the existing ASP.NET Core Identity + JWT stack** (Phase 3), not a second auth system. `ApplicationUser : IdentityUser<int>`, `JwtTokenService`, `AuthController` login, and `StaffRoles` (Owner/Staff) already ship; this phase seeds a **`Client` role**, adds **register**, and mirrors `dashboard/lib/auth.ts` on `landing-page` under `/account/*`. Guest checkout and guest booking remain valid; accounts are additive.

History and self-service are **server ownership only**: filter by authenticated user id / linked `ClientId` (and claimed email links). Cancel reuses the existing `Confirmed → Cancelled` path (slot release). Reschedule must be a **single transaction** (book new → cancel old) so the unfiltered `(StylistId, SlotStart)` unique index never double-books. Loyalty is an append-only `LoyaltyLedger`; earn +1 on staff `Completed`; redeem as a **server-computed** checkout discount (never trust client $).

**Primary recommendation:** Extend Identity in-place (Client role + register + JWT claims), add ownership-gated account APIs, transactional reschedule, and LoyaltyLedger hooks on Completed + checkout — mirror staff JWT pattern on landing-page; do not introduce Auth.js/Better Auth.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Register / login (email+password) | API / Backend | Browser / Client | Identity `UserManager` + JWT mint; UI posts credentials |
| JWT session storage | Browser / Client | — | localStorage mirror of dashboard (D-02) |
| Ownership / IDOR prevention | API / Backend | Database / Storage | Claims + server filters; never client-supplied owner IDs |
| Booking/order history | API / Backend | Browser / Client | Query by ClientUserId/ClientId; UI lists only |
| Cancel / reschedule | API / Backend | Database / Storage | Status transitions + slot unique index in one transaction |
| Loyalty earn (Completed) | API / Backend | Database / Storage | Hook inside `UpdateStatusAsync` when → Completed |
| Loyalty redeem (checkout $) | API / Backend | — | Recompute discount after catalog prices (Phase 6 authority) |
| Account UI (`/account/*`) | Browser / Client (landing-page) | Frontend Server (SSR) | App Router pages; auth state client-side like dashboard |

## Project Constraints (from CLAUDE.md)

- Stack: Next.js 15 + React 19 + Tailwind 4 (`landing-page/`, `dashboard/`); .NET 10 / ASP.NET Core + EF Core 10 / SQL Server.
- Feature folders on backend; OpenAPI source of truth; landing-page hand-written fetch until generated client adopted.
- Secrets via user-secrets/env only (`Jwt:SigningKey`, `RESEND_API_KEY`); gitleaks in CI.
- AppointmentSlot unique index on `(StylistId, SlotStart)` — **never** add `HasFilter()`.
- Tests: `dotnet test API/ZachHairStudio.Api.Tests` over real SQL Server (`SqlServerWebApplicationFactory`).

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core Identity | .NET 10 (in-repo) | Users/roles/password hash | Already on `BookingDbContext`; ACCT-05 |
| `JwtBearer` + `JwtTokenService` | in-repo | Mint/validate bearer JWT | Phase 3 lock; D-02 |
| FluentValidation | in-repo | Register/login/DTO rules | PLAT-02 |
| EF Core 10 + SQL Server | in-repo | Schema + migrations | Identity + LoyaltyLedger |
| Next.js 15 App Router | landing-page | `/account/*` UI | D-05 |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `SqlServerWebApplicationFactory` | test project | Integration tests | All Identity/ownership/status tests |
| Result → ProblemDetails | in-repo | API errors | Controllers |
| Zod + hand-written fetch | landing-page `lib/` | Client validation/fetch | Until OpenAPI client adopted |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Identity + JWT | Auth.js / Better Auth | **Rejected** — would fork auth from staff dashboard; ACCT-05 + Phase 3 lock forbid |
| Cookie session | JWT localStorage | Cookie would diverge from dashboard pattern (D-02) |
| Separate Client entity table | `ApplicationUser` + Client role | Extra join table unnecessary; int Id already shared |

**Installation:** No new NuGet/npm packages required for MVP. Extend existing Identity/JWT/FluentValidation.

**Version verification:** Identity/`AddIdentity<ApplicationUser, IdentityRole<int>>` and `JwtTokenService` confirmed in `Program.cs` / Shared Features — [VERIFIED: codebase].

## Package Legitimacy Audit

> No new external packages recommended for this phase.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| — | — | — | — | — | — | N/A — reuse in-repo Identity/JWT |

**Packages removed due to [SLOP] verdict:** none  
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```text
[Landing /account Register|Login]
        │  email+password
        ▼
[AuthController] ──UserManager──► [AspNetUsers + Client role]
        │ CreateToken(roles)
        ▼
[JWT localStorage] ──Authorization: Bearer──► [Account APIs]
        │                                         │
        │                    ┌────────────────────┼────────────────────┐
        │                    ▼                    ▼                    ▼
        │         [My Appointments]      [My Orders]         [Cancel/Reschedule]
        │          filter ClientUserId    filter ClientId     ownership + Confirmed
        │                    │                    │                    │
        │                    └──────────┬─────────┘                    │
        │                               ▼                              ▼
        │                      [BookingDbContext]          [Txn: book new → cancel old]
        │                                                      (unique slot index)
        │
[Staff dashboard Complete] ──UpdateStatusAsync──► [LoyaltyLedger +1]
[Checkout CreateCheckoutAsync] ──points redeem──► [server $ discount on TotalAmount]
```

### Recommended Project Structure

```
API/ZachHairStudio.Shared/Features/
├── Identity/           # Client role constant, register DTOs/validators, seeder update
├── Loyalty/            # LoyaltyLedger entity, service (balance, earn, redeem)
├── Appointments/       # ClientUserId FK; client cancel/reschedule methods
└── Orders/             # Claim ClientId; loyalty discount in CreateCheckoutAsync

API/ZachHairStudio.Api/Controllers/
├── AuthController.cs   # + Register; login already returns Role
└── AccountController.cs  # me/bookings, me/orders, cancel, reschedule (Client role)

landing-page/
├── lib/auth.ts         # Mirror dashboard JWT localStorage (separate key e.g. zhs.client.auth)
├── app/account/        # login, register, bookings, orders
└── components/Navbar   # Account vs Login/Register (D-07)
```

### Pattern 1: Client role on shared Identity

**What:** Add `StaffRoles.Client` (or `AppRoles.Client`) and seed via `IdentitySeeder` alongside Owner/Staff. Register creates `ApplicationUser` + Client role only. Login already returns `Role` from `GetRolesAsync` — works for Client without Auth.js. [VERIFIED: AuthController.cs, IdentitySeeder.cs, StaffRoles.cs]

**When to use:** All client auth (ACCT-01/05).

### Pattern 2: Ownership filters (no client-supplied owner IDs)

**What:** Resolve `userId` from `ClaimTypes.NameIdentifier`. History queries: `WHERE ClientUserId == userId` (appointments) / `ClientId == userId` (orders). Detail by id: load then 404 if not owned (don't leak existence with 403 for other users' ids if preferred — pick one consistent policy; recommend 404). [ASSUMED: 404 vs 403 preference — recommend 404]

**When to use:** ACCT-02/03/06.

### Pattern 3: Cancel / reschedule transaction

**What:** Cancel = ownership check + `Confirmed → Cancelled` (existing slot `RemoveRange`). Reschedule in one DB transaction + execution strategy: (1) create new appointment + slots, (2) cancel old — if step 1 fails unique index, abort without cancelling old. Do not cancel-first (orphan gap / race). [VERIFIED: AppointmentsService AllowedTransitions + ConcurrencyTests pattern]

**When to use:** ACCT-04; D-09/D-10.

### Pattern 4: LoyaltyLedger + checkout discount

**What:** Append-only rows; balance = `SUM(Delta)`. Earn once when status becomes Completed (idempotent: no second earn for same AppointmentId). Redeem: client requests points to spend; server computes `$ = floor(points/10)*5` (D-16), caps at order subtotal, writes negative ledger row, subtracts from `TotalAmount` **after** catalog recompute. Never accept client `discountAmount`. [VERIFIED: OrdersService price recompute; CONTEXT D-14/15/16]

### Anti-Patterns to Avoid

- **Auth.js / NextAuth on landing-page:** Splits auth from API Identity — violates ACCT-05/D-01/D-02.
- **Trusting `clientId` in query/body:** IDOR (ACCT-06).
- **Cancel-then-book for reschedule:** Unique-index race / empty window.
- **Client-supplied discount $:** Price-authority bypass (Phase 6).
- **Mutable loyalty balance column without ledger:** Audit/race issues — use append-only sum.
- **Filtering AppointmentSlot unique index:** Breaks double-booking guarantee.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Password hashing / lockout | Custom crypto | Identity `UserManager` | Edge cases, timing, policies |
| JWT mint/validate | Manual HMAC | `JwtTokenService` + JwtBearer | Key rotation, claims, clock skew |
| Status transition / slot release | Duplicate cancel logic | `UpdateStatusAsync` path / shared transition map | Single source of truth |
| Stock/checkout money | Client totals | Server catalog recompute + loyalty constants | Tampering |
| Auth session on Next | Auth.js | Mirror `dashboard/lib/auth.ts` | One pattern (D-02) |

**Key insight:** Auth, status, and money already have repo standards — Phase 7 wires Client role + ownership + ledger into those seams.

## Common Pitfalls

### Pitfall 1: Identity tests on InMemory
**What goes wrong:** Relational Identity semantics pass in memory, fail on SQL Server.  
**Why:** Known Phase 3 pitfall.  
**How to avoid:** Always `SqlServerWebApplicationFactory`.  
**Warning signs:** Tests green locally without LocalDB fixture.

### Pitfall 2: IDOR via email or appointment id
**What goes wrong:** `GET /appointments/{id}` without ownership check.  
**How to avoid:** Authorize `[Authorize(Roles = Client)]` + server-side owner filter; ignore body owner fields.

### Pitfall 3: Double loyalty earn
**What goes wrong:** Re-Complete or retry writes +2 points.  
**How to avoid:** Unique constraint or existence check on `(AppointmentId, Reason=Earn)` before insert.

### Pitfall 4: Reschedule unique-index collision
**What goes wrong:** Two appointments hold same slot briefly or cancel leaves client with nothing.  
**How to avoid:** Book-new-then-cancel-old in one transaction; surface conflict as 409.

### Pitfall 5: Claiming wrong guest history
**What goes wrong:** Register email matches someone else's guest bookings.  
**How to avoid:** D-04 confirm UX when matches exist; only claim on explicit confirm; exact email match after normalize/trim.

### Pitfall 6: Staff JWT used on client routes
**What goes wrong:** Staff token calls account APIs.  
**How to avoid:** Require Client role on account endpoints; staff keep dashboard paths (D-12).

## Code Examples

### JWT already includes roles (reuse for Client)

```csharp
// Source: API/ZachHairStudio.Shared/Features/Identity/JwtTokenService.cs
claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
// NameIdentifier = user.Id — use for ownership filters
```

### Status transitions (cancel path)

```csharp
// Source: AppointmentsService AllowedTransitions
// Confirmed → {Completed, Cancelled, NoShow}; Cancelled/NoShow release AppointmentSlots
[AppointmentStatus.Confirmed] = new[] {
    AppointmentStatus.Completed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow
};
```

### Checkout price authority (loyalty hooks after this)

```csharp
// Source: OrdersService.CreateCheckoutAsync
// UnitPrice/LineTotal from Product.Price; TotalAmount = sum(LineTotal)
// Then: apply loyalty discount server-side; never trust client discount $
```

### Landing-page auth mirror (discretion: separate storage key)

```typescript
// Pattern from: dashboard/lib/auth.ts
// Use STORAGE_KEY = "zhs.client.auth" (not staff key) — same AuthSession shape
// getSession / setSession / clearSession / attachToken / requireAuth → /account/login
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Staff-only Identity | Identity + Client role | Phase 7 | One schema (ACCT-05) |
| Guest-only history | Claim + ClientId/ClientUserId | Phase 7 | Additive accounts |
| Auth.js proposal (roadmap flag) | **Rejected — Identity+JWT** | Discuss 2026-08-10 | No dual auth |

**Deprecated/outdated:**
- Auth.js / Better Auth for this app: inconsistent with Phase 3 staff JWT and ACCT-05.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Prefer 404 over 403 for cross-client resource ids | Ownership | Minor UX/info-leak tradeoff |
| A2 | Add nullable `Appointment.ClientUserId` FK to `ApplicationUser` for ownership (Email remains for guests) | Architecture | Alternate: ownership via email-only after claim — weaker if email changes |
| A3 | Loyalty discount applied to merchandise subtotal before Stripe session (no tax/shipping yet) | Loyalty | Order of operations if tax added later |
| A4 | Storage key `zhs.client.auth` distinct from `zhs.staff.auth` | Frontend | Collision if same browser used for both |

**If wrong:** Planner should confirm A1–A2 in plan checkpoints only if needed; A3–A4 are low-risk discretion.

## Open Questions

1. **Identity vs Auth.js (ACCT-05) — RESOLVED**
   - What we know: `ApplicationUser`, `AddIdentity`, `JwtTokenService`, `AuthController` login, `StaffRoles`, dashboard JWT localStorage all exist and work. [VERIFIED: codebase]
   - What's unclear: nothing blocking.
   - Recommendation: **Keep Identity + JWT.** Do **not** add Auth.js. Add `Client` role + register only.

2. **Appointment ownership column — RESOLVED (recommended)**
   - Recommendation: nullable `ClientUserId` on Appointment (mirror Order.ClientId); claim sets FK on register confirm.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK / `dotnet test` | API + tests | ✓ (project standard) | .NET 10 | — |
| SQL Server LocalDB / test SQL | Integration tests | ✓ (existing fixture) | per factory | — |
| `Jwt:SigningKey` user-secret | API boot + tests | ✓ (Program ValidateOnStart) | — | Test injects in-memory |
| Node / Next.js landing-page | Account UI | ✓ | Next 15 | — |

**Missing dependencies with no fallback:** none for this phase  
**Step 2.6:** External tools already used by repo; no new CLIs.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit + `Microsoft.AspNetCore.Mvc.Testing` (existing `ZachHairStudio.Api.Tests`) |
| Config file | `API/ZachHairStudio.Api.Tests` + `SqlServerWebApplicationFactory` |
| Quick run command | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Account\|FullyQualifiedName~Loyalty\|FullyQualifiedName~AuthGate"` |
| Full suite command | `dotnet test API/ZachHairStudio.Api.Tests` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| ACCT-01 | Register Client + login returns JWT with Client role | integration | `dotnet test --filter "FullyQualifiedName~ClientAuthTests"` | ❌ Wave 0 |
| ACCT-02 | Client lists only own appointments | integration | `dotnet test --filter "FullyQualifiedName~AccountBookingsTests"` | ❌ Wave 0 |
| ACCT-03 | Client lists only own orders | integration | `dotnet test --filter "FullyQualifiedName~AccountOrdersTests"` | ❌ Wave 0 |
| ACCT-04 | Owner can cancel; reschedule transactional; non-owner 404 | integration | `dotnet test --filter "FullyQualifiedName~ClientRescheduleTests"` | ❌ Wave 0 |
| ACCT-05 | Client role in same AspNet* schema; no second user store | integration | Extend `IdentitySeederTests` / role seed assert | ⚠️ extend existing |
| ACCT-06 | Cross-client id access rejected | integration | IDOR cases in Account*Tests | ❌ Wave 0 |
| ACCT-07 | +1 ledger on Completed; checkout discount server-side; no double-earn | integration | `dotnet test --filter "FullyQualifiedName~LoyaltyTests"` | ❌ Wave 0 |

Landing-page UI: no Jest/Vitest script yet — manual/smoke for `/account/*` navbar; API owns automated gate.

### Sampling Rate

- **Per task commit:** filtered test command for touched area  
- **Per wave merge:** `dotnet test API/ZachHairStudio.Api.Tests`  
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `Features/Identity/ClientAuthTests.cs` — ACCT-01/05 register+login+role
- [ ] `Features/Account/AccountBookingsTests.cs` — ACCT-02/06
- [ ] `Features/Account/AccountOrdersTests.cs` — ACCT-03/06
- [ ] `Features/Account/ClientRescheduleTests.cs` — ACCT-04
- [ ] `Features/Loyalty/LoyaltyTests.cs` — ACCT-07 earn/redeem/idempotency
- [ ] Extend `IdentitySeederTests` — Client role seeded
- [ ] Reuse AuthGateTests seeding pattern (`UserManager` + Jwt config inject)

## Security Domain

> `security_enforcement: true`, ASVS level 1 (`.planning/config.json`).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | Identity password hasher; register/login FluentValidation; uniform "Invalid email or password" on login |
| V3 Session Management | yes | JWT ~workday lifetime (existing JwtOptions); localStorage XSS bound by lifetime; no refresh tokens this phase |
| V4 Access Control | yes | `[Authorize(Roles = "Client")]` + ownership filters; staff endpoints unchanged |
| V5 Input Validation | yes | FluentValidation on register/reschedule/checkout redeem points |
| V6 Cryptography | yes | Identity hasher + Jwt HMAC via `Jwt:SigningKey` (user-secrets); never hand-roll |

### Known Threat Patterns for Identity + JWT + ownership

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| IDOR on booking/order id | Information Disclosure / Elevation | Server ownership filter (ACCT-06) |
| Privilege escalation Staff→Client APIs | Elevation | Role checks; separate storage keys |
| Client-supplied discount / owner id | Tampering | Ignore client money/owner fields; recompute |
| Email enumeration on register | Information Disclosure | Consistent responses / rate-limit later; login already uniform |
| XSS → JWT theft | Information Disclosure | Short-lived JWT; minimize secrets in JS; CSP polish deferred Phase 8 |
| Double-spend loyalty points | Tampering | Transaction + balance check + ledger row |

## Sources

### Primary (HIGH confidence)
- `07-CONTEXT.md` — D-01..D-16 locked decisions
- `REQUIREMENTS.md` — ACCT-01..07
- `ApplicationUser.cs`, `AuthController.cs`, `JwtTokenService.cs`, `IdentitySeeder.cs`, `StaffRoles.cs` — Identity+JWT confirmed
- `AppointmentsService.cs` — status transitions / slot release
- `OrdersService.cs` / `Order.cs` — checkout price authority, nullable ClientId
- `dashboard/lib/auth.ts` — JWT localStorage pattern to mirror
- `AuthGateTests.cs` / `StatusUpdateTests.cs` — test factory patterns
- `.planning/config.json` — nyquist_validation + security_enforcement

### Secondary (MEDIUM confidence)
- CLAUDE.md project constraints (feature folders, OpenAPI, secrets)

### Tertiary (LOW confidence)
- None material; Auth.js explicitly out of scope after codebase confirmation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Identity/JWT already in production path of this repo
- Architecture: HIGH — clear seams (Auth, Appointments status, Orders checkout)
- Pitfalls: HIGH — IDOR, unique index, double-earn drawn from existing tests/patterns

**Research date:** 2026-08-10  
**Valid until:** 2026-09-09 (30 days — stable Identity stack)
