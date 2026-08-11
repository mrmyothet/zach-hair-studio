# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

<!-- GSD:project-start source:PROJECT.md -->

## Project

**Zach Hair Studio**

A services-led platform for a hair salon. The heart of the product is the salon
experience — clients discover styles and colors, then book styling and coloring
appointments online. Selling hair-related products is a supporting offering,
framed as stylist-recommended extensions of the service relationship. It serves
three audiences: clients (discover, book, optionally buy), staff (a dashboard to
run the salon), and the owner (a modern, attractive, maintainable site).

**Core Value:** Booking a salon appointment is effortless — browsing services and reserving a
slot is the primary, friction-free path. If everything else fails, this must work.

### Constraints

- **Tech stack**: Next.js 15 (App Router) + React 19 + Tailwind 4 for `landing-page/` (public) and `dashboard/` (staff); .NET 10 / ASP.NET Core + EF Core 10 / SQL Server for the API — matches the existing repo; new work aligns unless a deliberate decision updates `specs/tech-stack.md`.
- **Architecture**: Feature folders on the backend (group by feature, e.g. `Features/Bookings`), not by technical layer. TypeScript everywhere on the frontend. OpenAPI is the source of truth for API clients.
- **Dev simplicity**: SQL Server LocalDB + `next dev` + `dotnet run` must be enough to run the whole system locally. Exception (D-12): `RESEND_API_KEY` is now REQUIRED to run the API and the test suite — real Resend sends occur in Development AND Testing (no fake sender), so both `dotnet run` and `dotnet test` need the key set via `dotnet user-secrets` (D-13, never a tracked file). This knowingly relaxes "LocalDB + next dev + dotnet run is enough."
- **Sequencing**: Services and the booking flow take priority at every step; product commerce is layered in only after the service experience is solid.
- **Security/Compliance**: gitleaks secret-scanning is wired via pre-commit hook and CI — keep secrets out of the repo.

<!-- GSD:project-end -->

## Commands

### Backend (`API/`)

- Build the solution: `dotnet build API/ZachHairStudio.slnx`
- Run the API: `cd API/ZachHairStudio.Api && dotnet run` — http://localhost:5236 (https :7199); OpenAPI JSON at `/openapi/v1.json` and Swagger UI at `/swagger` in Development.
- Run all tests: `dotnet test API/ZachHairStudio.slnx` (or target the test project directly: `dotnet test API/ZachHairStudio.Api.Tests`)
- Run a single test class or method: `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~ConcurrencyTests"` (append `.MethodName` for one test)
- Required secrets before `dotnet run` **or** `dotnet test` will start (D-12/D-13 — read in Development *and* Testing, never a tracked file):
  ```
  cd API/ZachHairStudio.Api
  dotnet user-secrets set "RESEND_API_KEY" "<resend api key>"
  dotnet user-secrets set "Jwt:SigningKey" "<32+ char random string>"
  ```
  Program.cs fails fast on startup (`ValidateOnStart`) if `Jwt:SigningKey` is missing or under 256 bits.
- EF Core migrations (`dotnet-ef` must be v10.x — `dotnet tool update --global dotnet-ef --version "10.*"`):
  - Add: `dotnet ef migrations add <Name> --project API/ZachHairStudio.Shared --startup-project API/ZachHairStudio.Api`
  - Apply: `dotnet ef database update --project API/ZachHairStudio.Shared --startup-project API/ZachHairStudio.Api`. **Production** must use this deploy step — the API does **not** call `db.Database.Migrate()` in Production and will refuse to start if pending migrations exist (LAUNCH-03). Development still migrates on `dotnet run` for local convenience.
  - See the `ef-migrations` skill for details.

### Frontends (`landing-page/`, `dashboard/`)

Each is an independent Next.js app with its own `node_modules`/`package.json` — run these from inside the respective directory (standard `dev`/`build`/`start`/`lint` scripts; no `test` script exists yet):

- Dev server: `npm run dev` (landing-page → :3000; dashboard → `npm run dev -- -p 3001`, offset to avoid clashing with the landing page)
- Regenerate the dashboard's typed API client from the live OpenAPI doc (API must be running): `npx -y openapi-typescript http://localhost:5236/openapi/v1.json -o lib/api/schema.d.ts`, run from `dashboard/`. See the `openapi-client` skill.

### Everything at once

Use the `dev` skill (`.claude/skills/dev/SKILL.md`) to launch the API and both frontends together, or run the commands above in three terminals.

## Architecture

### Backend (`API/ZachHairStudio.slnx`, 4 projects)

New backend features mirror the `Features/<Name>/` shape in `ZachHairStudio.Shared` (see the `feature-scaffold` skill, which uses this pattern as its template). The legacy `ZachHairStudio.Admin` MVC scaffold was removed in Phase 8 (LAUNCH-02) — staff UI lives in `dashboard/`.

Key invariants worth knowing before touching booking logic:
- `AppointmentSlot` has an **unfiltered** unique index on `(StylistId, SlotStart)` — this is the double-booking guarantee (`ConcurrencyTests` relies on it). Never add a `HasFilter()` to it.
- Secrets (`RESEND_API_KEY`, `Jwt:SigningKey`) are read via `dotnet user-secrets`/env vars only — `Program.cs` calls `AddUserSecrets<Program>()` unconditionally (not just in Development) because Testing needs `RESEND_API_KEY` too (real Resend sends in tests, no fake sender).

### Frontends (`landing-page/`, `dashboard/`)

Two independent Next.js 15 / App Router apps, each with its own `lib/` fetch layer pointed at `NEXT_PUBLIC_API_URL` (defaults to `http://localhost:5236`):

- **`landing-page`** — public site; Server Components by default. Hand-written fetch calls live in `lib/services.ts` and `lib/appointments.ts` (no generated client yet).
- **`dashboard`** — staff tool; has a generated typed client (`lib/api/client.ts` + `lib/api/schema.d.ts`, via `openapi-fetch`/`openapi-typescript` — regenerate, don't hand-edit `schema.d.ts`). Auth is a bearer JWT stored in `localStorage` (`lib/auth.ts`), attached to requests via an `openapi-fetch` middleware; a 401 clears the session and redirects to `/login`.

<!-- GSD:skills-start source:skills/ -->
<!-- Project skills (dev, ef-migrations, feature-scaffold, openapi-client) are already
     surfaced in the session's skill listing — not duplicated here. -->
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:

- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
