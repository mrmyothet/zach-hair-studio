# Walking Skeleton — Zach Hair Studio (Service Catalog vertical + architectural template)

**Phase:** 1
**Generated:** 2026-07-07

## Capability Proven End-to-End

A salon client can open `/services` in the browser and see the real service catalog (name, teaser, duration, price, image, category-grouped) served from SQL Server through a dedicated `ServicesService` layer, and open `/services/{slug}` for a single service's detail page — with every layer of the stack (SQL Server → EF Core → `ServicesService` → `ServicesController` → RSC fetch → Zod parse → JSX) exercised on the happy path.

> Note: Phase 0 already shipped a working end-to-end stack (booting API + EF Core + SQL Server + landing page wired to the booking form). What Phase 1 proves is the **architectural template** — the per-feature service layer + `Result<T>` + FluentValidation + Zod pattern — that every later phase (2–7) copies verbatim. Treat the decisions below as a contract, not a scratchpad.

## Architectural Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Backend framework | ASP.NET Core 10 + EF Core 10 / SQL Server (LocalDB in dev) | Matches existing repo; no stack change (CLAUDE.md constraint) |
| Feature organization | Feature folders — `API/ZachHairStudio.Shared/Features/{Feature}/` holding entity, DTOs, extension mappers, validators, and the feature's service class | Mirrors `Features/Bookings/`; group-by-feature is a locked constraint |
| Service layer (PLAT-01) | A per-feature `{Feature}Service` class owns **all** `BookingDbContext` access; controllers inject the service, never the `DbContext` | Establishes PLAT-01; `BookingsController` is grandfathered (D-19) and NOT refactored this phase |
| Error contract (D-17) | `{Feature}Service` methods return the existing `Result<T>` (`API/ZachHairStudio.Shared/Result.cs`); controllers translate `Result<T>` → `ActionResult` (`IsSuccess` → 200/201, `IsNotFound()` → 404) | Activates the shipped-but-unused `Result<T>`; every later feature copies this translation |
| Validation layer (PLAT-02) | FluentValidation 12.1.1 invoked **manually** via injected `IValidator<T>.ValidateAsync` (never the deprecated `FluentValidation.AspNetCore` auto-MVC filter). Canonical flow: **controller** calls `ValidateAsync` → `AddToModelState` → `ValidationProblem(ModelState)` for the rich HTTP error; **service** also validates defensively and returns `Result<T>.ValidationError` for non-HTTP callers | Auto-MVC integration deprecated since FluentValidation v11; ModelState/ProblemDetails wire shape is already parsed by `landing-page/lib/api.ts` |
| Frontend framework | Next.js 15 App Router + React 19 + Tailwind 4 (`landing-page/`) | Matches existing repo |
| Frontend data fetching (D-03) | React Server Components `fetch(url, { next: { revalidate: 60 } })` (ISR). No client-side loading spinners for read-only catalog content. No `generateStaticParams` (per-request revalidate, since Phase 4 makes services staff-editable) | SEO-friendly, cache-efficient, survives Phase 4 live edits without a rebuild |
| Frontend response validation (D-18) | Zod 4.4.3 schemas (`ServiceSchema` / `ServiceListSchema`) parse the API JSON in a shared `landing-page/lib/services.ts` data module before JSX consumes it | Single trust boundary for API response shape; establishes the frontend validation pattern |
| API client style | Hand-written `lib/services.ts` mirroring the OpenAPI `ServiceResponseDto` schema (not OpenAPI-generated this phase — Claude's discretion). OpenAPI remains source of truth; may be regenerated via the `openapi-client` skill later | Faster to ship, matches existing `lib/api.ts`; no build-step added |
| Seeding (D-13) | EF Core `HasData()` in `OnModelCreating` → baked into a migration → applied by existing startup `db.Database.Migrate()`. NEVER `UseSeeding`/`UseAsyncSeeding` (only fire via `EnsureCreated()`, which this project does not use) | Only seeding API compatible with `Migrate()`-based startup |
| Test harness | New `API/ZachHairStudio.Api.Tests` xUnit project (first test project in the repo): validator unit tests, `ServicesService` unit tests (EF Core InMemory), controller integration tests (`WebApplicationFactory<Program>`), PLAT-01 reflection test | Nyquist coverage for the template; no test framework existed before |

## Stack Touched in Phase 1

- [x] Project scaffold — first xUnit test project added to the solution; FluentValidation + Zod added
- [x] Routing — new `/services` and `/services/[slug]` App Router routes
- [x] Database — real read (`GET /api/services`, `GET /api/services/{slug}`) AND real write (`POST`/`PUT /api/services`, seed via migration)
- [x] UI — interactive catalog pages + booking-form dropdown wired to the live API
- [x] Deployment — documented local full-stack run via the `dev` skill (`.NET API` + `next dev`); nothing publicly deployed yet (D-16)

## Out of Scope (Deferred to Later Slices)

- Staff-facing CRUD UI for services (Phase 4 — the `POST`/`PUT` endpoints ship now but unauthenticated and UI-less, D-16)
- Authentication / staff auth gate on write endpoints (Phase 3, D-16)
- Slot-based booking flow — the detail-page CTA links to the existing contact form with the service pre-selected (`/#contact?service={slug}`, D-04); Phase 2 swaps the target
- Image upload pipeline — `ImageUrl` points at static files in `landing-page/public/` (D-08)
- Refactoring `BookingsController` off direct `DbContext` access (D-19 — Phase 2 rebuilds booking wholesale)
- Frontend automated test framework (no Vitest/Jest/Playwright config exists; catalog pages verified via `npm run build` + manual `dev` pass this phase)
- Real (owner-verified) prices/durations — seed values are Claude-chosen placeholders, owner-reviewable, editable via Phase 4 CRUD (D-15)

## Subsequent Slice Plan

Each later phase adds one vertical slice on top of this skeleton without altering its architectural decisions:

- **Phase 2 — Booking Core:** client picks a service, sees real open slots, books a double-booking-safe appointment (consumes `Service.DurationMinutes`/`Price`).
- **Phase 3 — Staff Dashboard:** authenticated schedule view + appointment status management behind a staff auth gate.
- **Phase 4 — Staff Management:** staff CRUD for services + availability on top of the Phase 1 write endpoints.
- **Phase 5 — Product Catalog:** curated stylist-recommended products (copies the Services feature template exactly).
- **Phase 6 — Cart & Checkout:** server-authoritative checkout, atomic stock, webhook fulfillment.
- **Phase 7 — Accounts & Retention:** shared Identity, booking/order history, loyalty groundwork.
- **Phase 8 — Polish & Launch Readiness:** responsive polish, restricted CORS, controlled migrations, structured logging, rate limiting, retire legacy Admin scaffold.
