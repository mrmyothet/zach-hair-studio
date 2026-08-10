# Phase 8: Polish & Launch Readiness - Context

**Gathered:** 2026-08-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Final hardening and launch pass over the complete system: responsive/visual polish review for landing-page and dashboard, production CORS allowlist (no AllowAnyOrigin), retire legacy ZachHairStudio.Admin in favor of dashboard/, controlled production migrations (no startup Migrate in Production), structured API logs for requests and key ops, and basic rate limiting on auth and checkout. Does not add new product features, loyalty tiers, notifications, or payment redesign (v2 / deferred).

</domain>

<decisions>
## Implementation Decisions

### Production CORS & Admin retirement
- Production origins come from config/env list (`Cors:Origins`) covering landing + dashboard URLs — secrets/config pattern, never commit production URLs with credentials (D-13)
- Development/Testing may stay permissive; Production uses the allowlist only (no AllowAnyOrigin in Production)
- Delete ZachHairStudio.Admin project from the solution and remove the folder; dashboard/ is the staff app
- Remove solution/CI references that build Admin; no runtime redirect stubs required (MVC unused)

### Production migrations
- Skip `db.Database.Migrate()` in Production only; keep Migrate in Development/Testing for local convenience
- Production schema applied via documented deploy step: `dotnet ef database update` (CI/CD or release checklist)
- Fail fast if Production starts against an outdated schema (health/check or clear failure — no silent drift)
- Short runbook in CLAUDE.md / deploy notes plus Phase 8 SUMMARY

### Structured logging
- Built-in ASP.NET Core + JSON console formatter — no new vendor logging SDK
- Request middleware plus explicit structured events for auth (login/register), appointments create/cancel, and checkout/quote
- Never log passwords/tokens/full payment secrets; truncate or hash email; booking/order ids OK
- Information in Production; Debug allowed in Development

### Rate limiting & visual polish
- Rate limit auth (`/api/auth/*`) and checkout (`/api/orders/checkout*`) only
- Fixed window per IP (auth ~10/min, checkout ~20/min); respond 429 with Retry-After using ASP.NET RateLimiter
- Visual polish = pass responsive review at common breakpoints; fix clear overflow/touch-target issues; no redesign/rebrand
- Verification via checklist in VERIFICATION.md plus brief human spot-check (mobile/desktop)

### Claude's Discretion
- Exact Cors:Origins key shape and parsing (semicolon vs array)
- Exact rate-limit numeric tuning within the recommended ballpark
- Which health/migration-check API to use for outdated-schema fail-fast
- Which pages/components to touch for breakpoint fixes after audit

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `API/ZachHairStudio.Api/Program.cs` — AddCors AllowAnyOrigin today; `db.Database.Migrate()` on startup; JWT/ValidateOnStart patterns
- `dashboard/` — staff Next.js app (Phase 3+); replaces Admin MVC
- `landing-page/` — public Next.js site with existing charcoal/gold design tokens
- `.claude/skills/ef-migrations` — migration add/update workflow
- CI: `.github/workflows/ci.yml`, security/gitleaks workflows

### Established Patterns
- Feature folders under Shared; OpenAPI as API contract source
- Secrets via user-secrets / env (D-13); never tracked appsettings secrets
- Testing environment skips Owner seed; FakePaymentProvider in Testing

### Integration Points
- Program.cs middleware pipeline (CORS, auth, future RateLimiter + JSON logging)
- Solution file references ZachHairStudio.Admin — remove with project delete
- Deploy/docs touch CLAUDE.md Commands / Architecture notes for Migrate vs ef database update

</code_context>

<specifics>
## Specific Ideas

No additional specifics beyond ROADMAP LAUNCH-01..05 and the accepted grey-area recommendations above.

</specifics>

<deferred>
## Deferred Ideas

- Serilog / OpenTelemetry vendor pipelines
- Global API rate limits and per-user post-JWT limits
- Full visual redesign / rebrand
- Automated Playwright-only polish gate (human spot-check remains)
- v2: notifications, tiered loyalty, deposits, real-time dashboard sync

</deferred>
