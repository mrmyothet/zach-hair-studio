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

<!-- GSD:skills-start source:skills/ -->

## Project Skills

| Skill | Description | Path |
|-------|-------------|------|
| dev | Launch the full Zach Hair Studio stack locally — the .NET API plus the Next.js frontends — for development and manual verification. | `.claude/skills/dev/SKILL.md` |
| ef-migrations | Add and apply EF Core migrations against BookingDbContext, including the one-time switch off EnsureCreated() so migrations own the schema. | `.claude/skills/ef-migrations/SKILL.md` |
| feature-scaffold | Scaffold a new backend feature mirroring the Features/Bookings pattern (entity, DTOs, mappers, DbSet, controller) plus a starter Next.js page. | `.claude/skills/feature-scaffold/SKILL.md` |
| openapi-client | Regenerate a typed TypeScript API client for the Next.js frontends from the .NET OpenAPI document, keeping OpenAPI as the source of truth. | `.claude/skills/openapi-client/SKILL.md` |
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
