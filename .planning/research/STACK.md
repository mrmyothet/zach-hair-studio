# Stack Research

**Domain:** Salon appointment booking + supporting product commerce (Next.js 15 / React 19 / .NET 10 / EF Core 10 / SQL Server — locked base stack)
**Researched:** 2026-07-07
**Confidence:** MEDIUM (Context7-sourced library docs = MEDIUM; web-sourced comparisons and pattern advice = LOW per source-hierarchy tiering, cross-checked where possible)

This document does **not** re-litigate the base stack (Next.js 15, React 19, Tailwind 4,
.NET 10, EF Core 10, SQL Server — see `.planning/codebase/STACK.md` and
`specs/tech-stack.md`). It covers the incremental libraries/patterns needed to build
slot-based booking, staff/client auth, product checkout, form validation, and the
OpenAPI-generated TS client.

## Recommended Stack

### Core Technologies (new, on top of the locked base)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.0.x (matches installed EF Core 10.0.9) | Staff + client identity, password hashing, roles | Ships with .NET 10, owns its schema via the *same* EF Core migrations pipeline already established (`BookingDbContext`/`ef-migrations` skill). No second user store, no second migration tool to keep in sync. |
| `AddIdentityApiEndpoints<TUser>()` + `MapIdentityApi<TUser>()` (built into ASP.NET Core, no extra package) | .NET 10 | Ready-made `/register`, `/login`, `/refresh`, `/me`, 2FA endpoints, supporting both cookie and bearer-token modes from one call | Confirmed shipping and documented for .NET 10 (Microsoft Learn: "Use Identity to secure a Web API backend for SPAs," `aspnetcore-10.0`). Removes the need to hand-roll login/register/refresh endpoints — exactly the kind of boilerplate this project's existing controllers (see `BookingsController`) would otherwise reinvent. |
| `FluentValidation` (core) | 12.x (current major; check `dotnet list package --outdated` at implementation time) | Business-rule validation beyond DataAnnotations (e.g. "no double-booking a stylist", "slot must be in the future", "cart quantity ≤ stock") | Already the documented target in `.planning/codebase/ARCHITECTURE.md`'s Anti-Patterns section ("No Validation Layer" → "Instead: FluentValidation"). This research confirms current integration pattern. |
| `FluentValidation.DependencyInjectionExtensions` | matches FluentValidation core version | `AddValidatorsFromAssemblyContaining<T>()` auto-registers all validators in `ZachHairStudio.Shared` | Avoids hand-registering one `AddScoped<IValidator<T>, ...>` per feature as feature folders grow (Services, Products, Bookings, Orders). |
| `openapi-typescript` | 7.x (dev dependency) | Generates `paths`/`components` TS types from the API's OpenAPI 3.1 document | Already the project's chosen tool — see `.claude/skills/openapi-client/SKILL.md`. This research reconfirms it against current docs; no change recommended. |
| `openapi-fetch` | 0.13.x | Thin, typed fetch wrapper consuming the generated types | Zero runtime cost (types erase at compile time) vs. NSwag's generated runtime client classes — better fit for two separate frontend apps (`landing-page/`, `dashboard/`) that should stay lean. |
| `react-hook-form` | 7.66.x | Form state/validation for booking flow, cart/checkout, staff CRUD forms | Confirmed React 19 / App Router compatible via Context7 docs (works in client components with `useForm`); minimal re-renders matter for a multi-step booking wizard (service → slot → confirm). |
| `@hookform/resolvers` (the `zod` resolver) | 3.x (paired to RHF 7.66) | Bridges Zod schemas into React Hook Form | Lets the same conceptual schema style (Zod, TypeScript-native) validate forms client-side, mirroring FluentValidation server-side — one mental model across the stack. |
| `zod` | 3.x or 4.x (pin one; 4 has a different error-map API) | Schema definitions for forms and Auth.js `Credentials.authorize()` input parsing | Already demonstrated in Auth.js's own credentials-provider example; reusable for cart/checkout form schemas too. |
| `date-fns` v4 + `@date-fns/tz` | date-fns 4.x, `@date-fns/tz` 1.x | Slot generation, timezone-safe availability math, formatting on both API-adjacent utility code (if any Node-side slot preview is needed) and the frontend | v4 added first-class IANA-timezone support via `@date-fns/tz`, tree-shakeable, no moment.js legacy baggage. Preferred over Luxon for smaller bundle in a marketing-site-adjacent app; preferred over native `Temporal` because Temporal is not yet universally supported enough to hard-depend on in 2026 client bundles. **Note:** the canonical slot/overlap computation should live server-side in C# (`DateTimeOffset`/`TimeZoneInfo`), not in JS — date-fns here is for *display/formatting* and any client-side slot-preview UI, not the source of truth for availability. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Stripe.net` (NuGet) | 47.x+ (track Stripe's API version pinning) | Server-side Checkout Session creation, webhook signature verification, PaymentIntent retrieval | Phase 6 (cart & checkout) — call from a new `Features/Orders` or `Features/Payments` feature folder, not from the frontend. |
| `stripe-node` / `@stripe/stripe-js` (npm, only if a custom Elements form is wanted instead of hosted Checkout) | latest | Client-side Elements mounting | Only if hosted Stripe Checkout's default UI is judged insufficiently on-brand; otherwise skip — hosted Checkout needs zero client-side Stripe JS. |
| `SharpGrip.FluentValidation.AutoValidation.Endpoints` (or `.Mvc`) | current (check NuGet at implementation time) | Automatic FluentValidation invocation in the ASP.NET pipeline (avoids manual `ValidateAsync()` calls in every controller action) | Optional convenience — FluentValidation's own `FluentValidation.AspNetCore` auto-validation integration is effectively unmaintained/deprecated for ASP.NET Core; this community package is the current recommended replacement **if** automatic (vs. manual, explicit) validation is wanted. Manual `IValidator<T>.ValidateAsync()` inside a thin service layer (already recommended in `ARCHITECTURE.md`'s "Stateless Controllers" anti-pattern fix) is equally valid and adds no extra dependency — prefer manual invocation for this project's size unless the boilerplate becomes painful. |
| `next-auth` (Auth.js) v5 | 5.0.0-beta.x (still beta-labeled as of mid-2026, but production-used) | Client-facing session/cookie glue in `landing-page/`, wrapping a `Credentials` provider whose `authorize()` calls the .NET API's `/login` endpoint | Use **only** as a thin session layer over API-issued identity (see Auth section below) — not as a second user store. |
| AutoMapper | current | Entity ⇄ DTO mapping as feature count grows | Already flagged in `ARCHITECTURE.md` anti-patterns; relevant again here because Products/Orders/Services will each add 2+ DTOs. Not strictly required by this research's scope, but compounds well with the above. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| Stripe CLI (`stripe listen --forward-to`) | Forward Stripe webhooks to `localhost` during dev | Needed the moment webhook-driven order confirmation is built; document in the `dev` skill once Phase 6 starts. |
| EF Core migrations (existing `ef-migrations` skill) | Add `AspNetUsers`/`AspNetRoles`/etc. Identity tables to the same migration history | No new tooling — Identity's EF Core store just adds more `DbSet<>`s to (or a sibling of) `BookingDbContext`. |

## Installation

```bash
# .NET API (from API/ZachHairStudio.Shared or ZachHairStudio.Api, whichever hosts DbContext/Program.cs)
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
dotnet add package Stripe.net

# Frontend — landing-page/ and dashboard/ (repeat per app as needed)
npm install react-hook-form @hookform/resolvers zod
npm install date-fns @date-fns/tz
npm install next-auth@beta   # landing-page/ only, if Auth.js is chosen for client session glue

# Frontend dev dependency (already used per openapi-client skill)
npm install -D openapi-typescript
npm install openapi-fetch
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| ASP.NET Core Identity + built-in Identity API endpoints (single source of identity truth in the API) | Better Auth (Node-native, DB-owning auth library) | If the project ever drops the .NET API as the schema owner, or if a pure Node/Next.js backend replaces it. Better Auth is the 2026 maintainer-recommended default for **greenfield Next.js-only** apps, but it wants to own its own database tables/migrations — that directly conflicts with this project's locked constraint that "EF Core migrations own schema" (see `ef-migrations` skill and `ARCHITECTURE.md`). Running two schema-owning migration systems (EF Core + Better Auth's) against one SQL Server DB is an avoidable footgun. |
| ASP.NET Core Identity for both staff and client accounts | Auth.js v5 as the *sole* identity system (its own adapter + DB tables) | If client accounts (Phase 7) end up needing many OAuth providers (Google/Apple/Facebook login) with minimal glue code, Auth.js's 40+ prebuilt provider configs are a real time-saver. This can still be layered *on top of* API-issued identity (Auth.js `Credentials` provider calling the API), which is what's recommended below — full Auth.js-owned storage is not recommended. |
| Stripe (direct, merchant-of-record = you) | Paddle / Lemon Squeezy (Merchant of Record) | If the salon ever sells digital goods (e.g. gift-card e-vouchers) across many tax jurisdictions, or wants to fully outsource sales-tax compliance. For a single-location physical-goods retailer in one tax jurisdiction, Stripe's lower fees (~2.9%+$0.30 vs ~5%+$0.50) and first-party ASP.NET/Next.js integration depth outweigh MoR convenience — see Payment section below for the full comparison. |
| `openapi-typescript` + `openapi-fetch` | NSwag | If a full-featured, heavier generated client (with built-in request/response classes, e.g. for a future C#-consumed client or deep IDE tooling) is wanted over a minimal types-only approach. NSwag also currently targets OpenAPI 3.0, while `Microsoft.AspNetCore.OpenApi` (already in use) emits 3.1 — 3.1 gives better nullability fidelity when paired with `openapi-typescript`. Not recommended here; it would also be a change from the already-adopted convention in the `openapi-client` skill. |
| `date-fns` v4 + `@date-fns/tz` | Luxon | If the team prefers Luxon's chainable, object-oriented API over date-fns's functional style. Both have solid IANA timezone support in 2026; this is a taste call, not a capability gap. |
| `react-hook-form` + Zod resolver | Native React 19 `useActionState`/`<form action>` + server-side-only validation | For very simple, single-field forms (e.g. a newsletter signup) plain server actions may be enough and avoid an extra client dependency. For the booking wizard and checkout forms (multi-field, need inline client-side feedback before submission), React Hook Form is worth the dependency. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|--------------|
| Hand-rolled JWT issuance/parsing code | .NET 10's `AddIdentityApiEndpoints()`/`MapIdentityApi()` already implements register/login/refresh/2FA correctly (token expiry, refresh rotation, password hashing via `PasswordHasher<TUser>`) — reinventing this is pure risk for no benefit. | `Microsoft.AspNetCore.Identity` + built-in Identity API endpoints. |
| `FluentValidation.AspNetCore`'s classic MVC auto-validation filter | Effectively unmaintained for current ASP.NET Core versions; officially the FluentValidation docs no longer recommend it. | Manual `IValidator<T>.ValidateAsync()` in a service layer, or `SharpGrip.FluentValidation.AutoValidation.*` if automatic invocation is wanted. |
| Storing every future bookable slot as a materialized DB row (e.g. a cron job pre-generating 6 months of slot rows per stylist) | Unbounded table growth, and any availability-rule change requires bulk rewriting/deleting future rows — fragile and slow. | Store availability as **rules** (recurring weekly windows + date-specific overrides) and **compute** open slots on read by generating candidate slots for the requested date range and subtracting existing bookings' time ranges. Index `Bookings(StylistId, StartTime)` for fast overlap checks. |
| Client-side-only (JS) authority over slot availability/overlap | A client can submit a stale or manipulated slot; the true "is this slot still free" check must be re-verified server-side at booking-confirmation time (inside a transaction) regardless of what the UI showed. | Server-side (C#) re-validation of slot availability at the moment `POST /api/bookings` (or `/api/appointments`) is called, ideally inside the same DB transaction as the insert, to prevent race-condition double-booking. |
| Better Auth or any auth library that wants to own its own migrations against the shared SQL Server DB | Conflicts with the project's explicit "EF Core migrations own schema" decision (see `.claude/skills/ef-migrations/SKILL.md` and `ARCHITECTURE.md`). Two independent migration tools racing against one schema is a recurring source of drift/corruption. | ASP.NET Core Identity's EF Core store, which participates in the *same* `dotnet ef migrations add` workflow already in use. |
| Moment.js | Long-deprecated, mutable-by-default, no tree-shaking, maintainers themselves recommend moving off it. | `date-fns` v4 (+ `@date-fns/tz`) as recommended above. |

## Stack Patterns by Variant

**For staff auth (dashboard):**
- Use ASP.NET Core Identity + **cookie** mode (`useCookies=true` on `/login`) with a same-site, httpOnly session cookie, since `dashboard/` is a small, low-traffic, same-organization app (few staff accounts) — no need for the added complexity of a client-managed bearer token/refresh cycle.
- Add a `RoleManager`-backed `Owner`/`Stylist` role split from day one so future permission differences (e.g. only Owner edits pricing) don't require an auth-model rewrite.
- Because `dashboard/` is a *separate* Next.js app from `landing-page/`, treat it as a confidential, first-party client of the API: the dashboard's Next.js Route Handlers can proxy the login call server-side and forward the `Set-Cookie` header, keeping the API's session cookie scoped to the API's own domain/CORS policy rather than trying to share cookies cross-origin with the public site.

**For client auth (public site accounts, Phase 7):**
- Use ASP.NET Core Identity's **bearer token** mode (`useCookies=false`) as the identity source of truth, fronted by a thin `next-auth` v5 `Credentials` provider in `landing-page/` whose `authorize()` calls the API's `/login` and returns the resulting user/token to Auth.js, which then manages the Next.js-side session cookie.
- Only reach for Auth.js's OAuth providers (Google, etc.) if/when social login is actually requested — email/password via the Credentials provider is sufficient for v1 and keeps the identity model entirely within the already-owned SQL Server schema.

**If cart/checkout needs guest checkout (no account required):**
- Don't gate Phase 6 (cart & checkout) on Phase 7 (accounts) being done — model `Order` with a nullable `ClientId` FK so guest checkout works first, and account-linked order history (Phase 7) becomes an additive join later, not a blocking dependency. This also keeps phase ordering (products/checkout before accounts, per the roadmap draft) technically consistent.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|------------------|-------|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.x | `Microsoft.EntityFrameworkCore.SqlServer` 10.0.9 (already installed) | Same major/minor version family as the rest of the EF Core packages already in `API/ZachHairStudio.Shared` — install matching `10.0.x` patch, not a different EF major version. |
| `next-auth@5` (beta) | Next.js 15, React 19 | Confirmed via Context7 docs examples using the App Router `handlers`/`auth()` pattern already targeting Next 15-style route handlers; no React 19-specific incompatibility surfaced in current docs. |
| `react-hook-form` 7.66.x | `@hookform/resolvers` (zod) | Must upgrade both together if bumping majors — resolvers versioning tracks RHF's peer-dependency range. |
| `openapi-typescript` 7.x output | `openapi-fetch` 0.13.x | Both maintained in the same `openapi-ts` project; keep them on compatible published-together versions (check `openapi-ts.dev` release notes if either is bumped independently). |
| `Stripe.net` (NuGet) | Stripe API version pinned in Stripe Dashboard | Stripe.net's major version tracks a specific default API version; if the Stripe dashboard's API version is later than the SDK's default, set `StripeConfiguration.ApiVersion` explicitly to avoid schema drift between webhook payloads and SDK types. |

## Sources

- `/dotnet/aspnetcore` (Context7) — ASP.NET Core Identity cookie/role/authorization APIs. Confidence: MEDIUM.
- `/nextauthjs/next-auth` (Context7) — Auth.js v5 App Router setup, Credentials provider + Zod validation pattern. Confidence: MEDIUM.
- `/stripe/stripe-node` (Context7) — Checkout Session creation, PaymentIntent expansion, webhook endpoint shape. Confidence: MEDIUM.
- `/react-hook-form/documentation` (Context7) — `useForm` + `zodResolver` integration, transformed-value resolver typing. Confidence: MEDIUM.
- `/fluentvalidation/fluentvalidation` (Context7) — ASP.NET Core manual validation pattern, DI auto-registration, deprecation note on auto-validation filter. Confidence: MEDIUM.
- `/websites/openapi-ts_dev` (Context7) — `openapi-typescript` CLI usage, `openapi-fetch` client creation and typed GET/PUT calls. Confidence: MEDIUM.
- Microsoft Learn: "Use Identity to secure a Web API backend for SPAs" (`learn.microsoft.com/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0`) — confirms `AddIdentityApiEndpoints`/`MapIdentityApi` shipping and cookie/bearer modes for .NET 10. Confidence: LOW-tier source per hierarchy (web search) but from a first-party Microsoft Learn domain — treat as directionally reliable, verify exact API surface against the live docs at implementation time.
- Web search: NSwag vs `openapi-typescript` comparison (multiple sources incl. `johnnyreilly.com/dotnet-openapi-and-openapi-ts`, `code-maze.com/aspnetcore-swashbuckle-vs-nswag`). Confidence: LOW — directionally consistent across sources, re-verify specifics before finalizing.
- Web search: date-fns v4 / Luxon / Temporal comparison (`pkgpulse.com` guides, `crosscheck.cloud` blog). Confidence: LOW.
- Web search: Stripe vs Paddle vs Lemon Squeezy merchant-of-record comparison. Confidence: LOW — pricing figures and MoR framing should be re-checked against each provider's current pricing page before a final commercial decision.
- Web search: Better Auth vs Auth.js v5 2026 positioning (`betterstack.com`, `supastarter.dev`, GitHub Discussion `nextauthjs/next-auth#13252` — "Auth.js is now part of Better Auth"). Confidence: LOW, but material enough to change the auth recommendation's framing — flagged as a phase-specific research item (re-verify Auth.js/Better Auth organizational status right before Phase 7 planning, since this is an actively moving situation as of mid-2026).
- Existing project artifacts (verified, not web-sourced): `.claude/skills/openapi-client/SKILL.md`, `.claude/skills/feature-scaffold/SKILL.md`, `.planning/codebase/ARCHITECTURE.md`, `specs/tech-stack.md`. Confidence: HIGH (first-party, already-decided project conventions).

---
*Stack research for: Salon appointment booking + product commerce platform (Next.js 15 / .NET 10 incremental additions)*
*Researched: 2026-07-07*
