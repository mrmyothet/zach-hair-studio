# Phase 1: Service Catalog - Context

**Gathered:** 2026-07-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Clients can browse the salon's services (name, description, duration, price) on the public site — a dedicated catalog page plus per-service detail pages — backed by a real `Service` entity with list + detail API endpoints. The API is built on a dedicated per-feature service layer (`ServicesService`, PLAT-01) and a real validation layer (FluentValidation on the API, Zod on the frontend, PLAT-02) from this very first feature. Read-only for the public; staff CRUD UI is Phase 4; slot-based booking is Phase 2.

Requirements: PLAT-01, PLAT-02, CAT-01, CAT-02, CAT-03.

</domain>

<decisions>
## Implementation Decisions

### Browse & detail page placement
- **D-01:** Catalog lives at a dedicated `/services` route on `landing-page/`. The homepage keeps a compact Services section — now API-backed, showing a subset — that links to the full catalog page.
- **D-02:** Detail URLs are slug-based: `/services/precision-cut`. The `Service` entity carries a unique slug column.
- **D-03:** Catalog pages fetch in React Server Components with a short revalidate window (ISR). No client-side loading spinners for read-only content; SEO-friendly.
- **D-04:** Detail-page booking CTA links to the existing contact form with the service pre-selected (e.g., `/#contact?service=slug`). Phase 2 swaps this target to the real slot-based flow.

### Service model shape
- **D-05:** Category is a simple string/enum field on `Service` (e.g., Cuts, Color, Treatments), used to group the catalog page into sections. No separate Category entity/FK.
- **D-06:** Single fixed decimal `Price`, displayed as-is. Variable-price work is modeled as separate service entries (e.g., "Color — Short Hair"). Phase 2 receipts need one concrete price per service.
- **D-07:** `DurationMinutes` is a plain int (45, 90, …). Display formatting ("1h 30m") is a frontend concern. Phase 2 slot math consumes this directly.
- **D-08:** Nullable `ImageUrl` string pointing at static files in `landing-page/public/`. No upload pipeline in this phase — image management arrives with Phase 4 CRUD.
- **D-09:** `IsActive` bool (default true) from day one. Public list/detail queries filter to active services. Anticipates Phase 4 "retire service" without a later migration/query rework.
- **D-10:** Explicit `DisplayOrder` int column controls catalog ordering (merchandising, not alphabetical). Seeded now, staff-editable in Phase 4.
- **D-11:** Two description fields: a short teaser (~200 chars, for list cards) and a longer detail-page description.

### Catalog seeding & content source
- **D-12:** Initial catalog content migrates from the static services in `landing-page/lib/data.ts` — the site keeps saying what it says today, now from the database — enriched with duration, price, category, slug, and image.
- **D-13:** Seeding runs through the EF migration pipeline (`HasData` in `OnModelCreating` or an explicit seed migration), so every environment that runs migrations gets the catalog. Fits existing startup `db.Database.Migrate()` and the `ef-migrations` skill.
- **D-14:** Service entries are retired from `lib/data.ts` entirely: the homepage Services section, `/services` pages, AND the Contact form's service dropdown all read from the API. One source of truth. (Team/reviews/branches data stays static.)
- **D-15:** Seed durations/prices are plausible salon values chosen by Claude and explicitly flagged in the plan/summary as owner-reviewable placeholders (editable via Phase 4 CRUD later). Do not block on real numbers.

### Validation scope (write endpoints)
- **D-16:** Phase 1 ships POST/PUT service endpoints with full FluentValidation, exercised via Swagger/tests. They are unauthenticated until Phase 3's auth gate — same dev-only exposure as today's booking API; nothing is publicly deployed yet. Phase 4 adds the dashboard UI on top of these endpoints.
- **D-17:** `ServicesService` methods return the existing (currently unused) `Result<T>` from `API/ZachHairStudio.Shared/Result.cs`. Controllers translate ValidationError → 400 ProblemDetails and NotFound → 404. This activates the shipped pattern and sets the template every later feature follows.
- **D-18:** Zod enters on the frontend as response validation: Zod schemas parse/validate service API responses in the frontend data layer. Establishes the frontend validation pattern even though this phase has no public write forms.
- **D-19:** The existing `BookingsController` (which calls `DbContext` directly) is NOT refactored in Phase 1. Phase 2 rebuilds booking wholesale. PLAT-01's "controllers never query DbContext directly" is established and verified on the new Services feature.

### Claude's Discretion
- Exact FluentValidation rules per field (lengths, price bounds, slug format).
- Whether the API client for the new endpoints is OpenAPI-generated (via the `openapi-client` skill) or extends the hand-written `lib/api.ts` — the OpenAPI-as-source-of-truth constraint applies either way.
- Homepage subset size, empty states, sorting within categories, and visual details of the catalog pages (consistent with existing Tailwind theme and `SectionHeading` styling).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning & requirements
- `.planning/ROADMAP.md` — Phase 1 goal, success criteria, and MVP mode; Phase 2/4 dependencies on the Service model (`DurationMinutes`, price, availability handoff)
- `.planning/REQUIREMENTS.md` — PLAT-01, PLAT-02, CAT-01..03 exact wording
- `.planning/PROJECT.md` — locked constraints (stack, feature folders, OpenAPI source of truth, dev simplicity) and Key Decisions table

### Project constitution (specs/)
- `specs/mission.md` — services-led product framing, out-of-scope list
- `specs/roadmap.md` — original P1–8 phase source
- `specs/tech-stack.md` — locked stack versions; update only via deliberate decision
- `specs/tooling.md` — project skills (`dev`, `ef-migrations`, `feature-scaffold`, `openapi-client`) to use during execution

### Codebase maps
- `.planning/codebase/ARCHITECTURE.md` — current layering, Bookings feature anatomy, documented anti-patterns Phase 1 must fix (service layer, validation layer)
- `.planning/codebase/STRUCTURE.md` — "Where to Add New Code" section prescribes exact file locations for a new Services feature
- `.planning/codebase/CONVENTIONS.md` — naming, error-handling, and mapping conventions (DTO suffix, extension mappers, Result<T>)
- `.planning/codebase/CONCERNS.md` — known concerns (open CORS, no auth, startup Migrate, untested business logic)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `landing-page/components/Services.tsx` — existing static services grid; becomes the API-backed homepage subset section
- `landing-page/components/SectionHeading.tsx` — reusable section title for the new `/services` pages
- `landing-page/lib/data.ts` — source of the seed content (service names/blurbs); service entries removed after migration (D-14)
- `landing-page/lib/api.ts` — hand-written typed client with `extractErrorMessage` that already parses ModelState/ProblemDetails responses
- `API/ZachHairStudio.Shared/Result.cs` — shipped but unused `Result<T>` wrapper; Phase 1 activates it (D-17)
- Project skills: `feature-scaffold` (mirrors Features/Bookings pattern), `ef-migrations`, `openapi-client`, `dev`

### Established Patterns
- Feature folders: `API/ZachHairStudio.Shared/Features/{Feature}/` holding entity, DTOs, `{Entity}Extensions` mappers, enums — Services feature mirrors `Features/Bookings/`
- Enum-as-string persistence via `HasConversion<string>()` (BookingStatus precedent, applies if Category becomes an enum)
- DTO naming: `ServiceCreateDto`, `ServiceResponseDto`; extension mappers `ToDto()`/`ToEntity()`
- EF Core Code-First migrations in `API/ZachHairStudio.Shared/Migrations/`, applied by startup `db.Database.Migrate()`

### Integration Points
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` — add `DbSet<Service>` + `OnModelCreating` config + seed
- `API/ZachHairStudio.Api/Program.cs` — register `ServicesService` and FluentValidation in DI
- `landing-page/app/` — new `/services` and `/services/[slug]` routes (App Router, server components)
- `landing-page/components/Contact.tsx` — service dropdown switches from static `serviceOptions` to API data; accepts pre-selection via query param (D-04, D-14)
- OpenAPI document at `http://localhost:5236/openapi/v1.json` — regenerate/extend the typed client from it

</code_context>

<specifics>
## Specific Ideas

- The catalog is merchandising, not a database dump: owner-controlled `DisplayOrder`, category-grouped sections, and the homepage showing a curated subset that links to the full menu.
- The public site's visible content must not regress during the migration — the same services the static site shows today should appear from the database on day one.
- Phase 1 deliberately sets the architectural template (service layer + `Result<T>` + FluentValidation + Zod) that Phases 2–7 copy; treat pattern quality as part of the deliverable, not incidental.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 1-Service Catalog*
*Context gathered: 2026-07-07*
