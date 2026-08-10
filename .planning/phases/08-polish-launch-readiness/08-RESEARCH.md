# Phase 8: Polish & Launch Readiness — Research

**Researched:** 2026-08-10  
**Status:** Complete (planning input)

## Summary

Phase 8 hardens an already-working stack. Production must stop using `AllowAnyOrigin` and startup `Migrate()`. ASP.NET Core 10 ships built-in JSON console logging and `Microsoft.AspNetCore.RateLimiting` — no Serilog/OTel required (CONTEXT D-06/D-08). Admin MVC is unused; delete from `ZachHairStudio.slnx` and remove the folder.

## Key findings

1. **CORS:** `Program.cs` currently `AllowAnyOrigin()`. Production should `WithOrigins` from `Cors:Origins` (semicolon-separated or array). Dev/Testing may keep permissive (D-01).
2. **Migrate:** Block at lines ~184–188 runs outside Testing for all envs including Production. Gate with `IsDevelopment()` (and optionally Testing already skipped) — Production skips Migrate; still seed Owner in Production after external migrate (D-03). Fail-fast: `GetPendingMigrations().Any()` → throw / stop host (D-04).
3. **Logging:** `builder.Logging.AddJsonConsole()` in Production; structured `ILogger` events in AuthController, AppointmentsService/Controller, OrdersService for create/cancel/checkout/quote. Scrub email (truncate) (D-07).
4. **Rate limiting:** `AddRateLimiter` + `EnableRateLimiting` attributes or endpoint conventions on `/api/auth` and `/api/orders/checkout*`. Fixed window per IP; 429 + Retry-After (D-08).
5. **Admin:** Only referenced in `API/ZachHairStudio.slnx`. Delete project folder + slnx entry (D-02). CI does not reference Admin.
6. **Polish:** UI-SPEC checklist — fix overflow/touch targets only; no redesign (D-09).

## Pitfalls

- Do not remove Owner seed from Production when skipping Migrate — seed still needed after external `ef database update`.
- CORS allowlist must include both landing and dashboard origins or dashboard JWT calls fail in Production.
- Rate limiter partition by IP: behind reverse proxy need `ForwardedHeaders` later (defer; document).
- Deleting Admin: ensure no project references in csproj ProjectReference (none expected).

## Stack decisions locked by CONTEXT

JSON console only; no Serilog. Prod-only skip Migrate. Auth+checkout rate limits only.
