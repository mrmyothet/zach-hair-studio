# Phase 5: Product Catalog - Context

**Gathered:** 2026-08-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Clients can browse a curated product catalog (name, description, price, image, stock) on the public site — a dedicated catalog page plus per-product detail pages — backed by a real `Product` entity with list + detail API endpoints. The catalog is explicitly framed as stylist-recommended extensions of services, not a general storefront: a service detail page surfaces a curated set of products tied to that specific service via an explicit, seeded mapping. Read-only for the public; no staff CRUD UI, no cart, no checkout (Phase 6), no payment. Mirrors Phase 1's architectural template (service layer, `Result<T>`, FluentValidation, Zod) exactly.

Requirements: PROD-01, PROD-02, PROD-03.

</domain>

<decisions>
## Implementation Decisions

### Browse & detail page placement
- **D-01:** Catalog lives at a dedicated `/products` route on `landing-page/`, mirroring Phase 1's `/services` pattern (D-01).
- **D-02:** Detail URLs are slug-based: `/products/[slug]`, mirroring Phase 1 D-02. `Product` carries a unique slug column.
- **D-03:** Catalog pages fetch in React Server Components with the same ISR revalidate window as `lib/services.ts` (60s). No client-side loading spinners for read-only content.
- **D-04:** Add a "Products" link to `navLinks` (`lib/data.ts`) pointing at `/products`. No homepage teaser section — the Core Value keeps the homepage focused on booking/services; products surface primarily through the service-detail recommendation (PROD-03), not a competing homepage section.

### Product model shape
- **D-05:** `Category` is a simple string field on `Product`, mirroring Service D-05. No separate Category entity/FK.
- **D-06:** `Stock` is a plain `int` (0 = "out of stock" badge in the UI). No reservation/locking logic — that belongs to Phase 6's atomic-decrement checkout concern (SHOP-04), out of scope here.
- **D-07:** Nullable `ImageUrl` string pointing at static files in `landing-page/public/`, mirroring Service D-08. No upload pipeline — no staff CRUD UI ships for products in this milestone.
- **D-08:** `IsActive` bool (default true) included now, mirroring Service D-09, so public queries filter to active products and no later migration is needed if/when staff CRUD arrives. Public list/detail queries filter `IsActive == true`.
- **D-09:** Single fixed `decimal Price`, displayed as-is (mirrors Service D-06).
- **D-10:** Short + long description fields (mirrors Service D-11): a short teaser for list cards, a longer detail-page description.

### Service→Product recommendation mapping (PROD-03)
- **D-11:** Mapping is a many-to-many join table `ServiceRecommendedProduct(ServiceId, ProductId)` — explicit curation, not a FK on `Product`. A product may reasonably support more than one service.
- **D-12:** The mapping is seeded via EF Core `HasData` at migration time (same pattern as D-13 Phase 1 service seeding). No staff UI to edit the mapping in this phase — PROD-03 says "curated," not "staff-editable"; a future CRUD phase can add management UI over the same table.
- **D-13:** On the service detail page, recommended products render in a new "Recommended Products" section below the existing description, reusing the `SectionHeading` component and card styling consistent with the products catalog page.
- **D-14:** The relationship is one-directional for this phase: service detail pages show recommended products; product detail pages do NOT show "used for which services" (matches ROADMAP SC3 wording exactly — can be added later without a schema change since the join table already carries both directions).

### API layer & validation scope
- **D-15:** New `API/ZachHairStudio.Shared/Features/Products/` folder mirrors `Features/Services/` exactly: `Product` entity, `ProductResponseDto`, `ProductExtensions` mapper, `ProductsService` (all `BookingDbContext` access lives here, PLAT-01), FluentValidation validators (PLAT-02) even though no write endpoints ship yet — establishes the pattern for a future write phase without rework.
- **D-16:** Read-only public API only this phase: `GET /api/products`, `GET /api/products/{slug}`, and a way to fetch a service's recommended products (either `GET /api/services/{slug}` extended to include a `recommendedProducts` array, or a dedicated `GET /api/services/{slug}/recommended-products` endpoint — left to planner/implementer judgment based on what's cleanest against the existing `ServiceResponseDto` shape). No POST/PUT/DELETE for products in this phase — PROD-01/02/03 are all client-browse requirements.
- **D-17:** Seed data is Claude-authored plausible placeholder products (name, description, price, category, stock), explicitly flagged as owner-reviewable in the plan/summary — same precedent as Phase 1 D-15. Do not block on real product data or real images.
- **D-18:** Frontend response validation via Zod in a new `landing-page/lib/products.ts`, mirroring `lib/services.ts`'s `fetchServices`/`fetchServiceBySlug` pattern (schema, list schema, fetch functions with the same try/catch-to-empty-array and 404-to-null conventions).

### Claude's Discretion
- Exact FluentValidation rules per field (lengths, price/stock bounds, slug format) — mirror `ServiceCreateDtoValidator` conventions.
- Whether the recommended-products data flows through an extended `ServiceResponseDto` or a dedicated endpoint (D-16) — implementer's call based on what keeps the existing Services feature untouched vs. cleanest wiring.
- Number and content of seeded placeholder products and categories.
- Visual details of the catalog/detail pages and the recommended-products section (consistent with existing Tailwind theme, card styling from `Services.tsx`/`/services` pages).
- Empty states (product with zero stock, service with no recommended products).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning & requirements
- `.planning/ROADMAP.md` — Phase 5 goal, success criteria, MVP mode, Phase 6 dependency (needs product price/stock to sell)
- `.planning/REQUIREMENTS.md` — PROD-01, PROD-02, PROD-03 exact wording; Out of Scope table (no general recommendation engine, no reviews/ratings)
- `.planning/PROJECT.md` — locked constraints (stack, feature folders, OpenAPI source of truth, services-first sequencing)
- `.planning/phases/01-service-catalog/01-CONTEXT.md` — the architectural template this phase mirrors (service layer, Result<T>, FluentValidation, Zod, EF HasData seeding)

### Project constitution (specs/)
- `specs/mission.md` — services-led framing; products are "stylist-recommended extensions," not a general storefront
- `specs/roadmap.md` — original P1–8 phase source
- `specs/tech-stack.md` — locked stack versions
- `specs/tooling.md` — project skills (`dev`, `ef-migrations`, `feature-scaffold`, `openapi-client`)

### Codebase maps
- `.planning/codebase/CONVENTIONS.md` — naming, error-handling, mapping conventions (DTO suffix, extension mappers, Result<T>)
- `.planning/codebase/STRUCTURE.md` — "Where to Add New Code" for a new feature
- `.planning/codebase/ARCHITECTURE.md` — current layering; PLAT-01/02 patterns already established by Services

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `API/ZachHairStudio.Shared/Features/Services/*` — direct structural template: `Service.cs`, `ServiceResponseDto.cs`, `ServiceExtensions.cs`, `ServicesService.cs`, `ServiceCreateDtoValidator.cs` (validator pattern even though Products ships read-only)
- `landing-page/lib/services.ts` — direct template for `lib/products.ts` (Zod schema, list schema, fetch-with-fallback pattern, ISR revalidate constant)
- `landing-page/app/services/page.tsx` and `[slug]/page.tsx` — direct template for `/products` and `/products/[slug]` routes
- `landing-page/components/SectionHeading.tsx` — reusable section title for new pages and the service-detail recommended-products section
- Project skills: `feature-scaffold`, `ef-migrations`, `openapi-client`, `dev`

### Established Patterns
- Feature folders: `API/ZachHairStudio.Shared/Features/{Feature}/` holding entity, DTOs, `{Entity}Extensions` mappers — Products mirrors `Features/Services/`
- DTO naming: `ProductResponseDto`; extension mappers `ToDto()`
- EF Core Code-First migrations in `API/ZachHairStudio.Shared/Migrations/`, applied by startup `db.Database.Migrate()`; `HasData` seeding precedent from Services

### Integration Points
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` — add `DbSet<Product>`, `DbSet<ServiceRecommendedProduct>` (or configure as a pure join via `HasMany().WithMany()`), `OnModelCreating` config + seed
- `API/ZachHairStudio.Api/Program.cs` — register `ProductsService` and its FluentValidation validators in DI
- `landing-page/app/` — new `/products` and `/products/[slug]` routes (App Router, server components)
- `landing-page/lib/data.ts` — add "Products" entry to `navLinks`
- Existing service detail page (`landing-page/app/services/[slug]/page.tsx`) — add the new "Recommended Products" section, fetching via whichever endpoint shape D-16 resolves to
- OpenAPI document at `http://localhost:5236/openapi/v1.json` — no generated client on `landing-page/` (hand-written fetch layer per CLAUDE.md); extend `lib/products.ts` by hand like `lib/services.ts`

</code_context>

<specifics>
## Specific Ideas

- The catalog must read as curated, not a dump — small placeholder set, category-grouped, framed throughout as "stylist-recommended."
- The service→product mapping is the phase's distinguishing feature (PROD-03) — treat its data model and the service-detail integration as the deliverable that matters most, not an afterthought bolted onto a generic product list.
- No new payment/cart/stock-decrement logic belongs in this phase — Stock is display-only (drives an "out of stock" badge), not yet load-bearing for any transaction.

</specifics>

<deferred>
## Deferred Ideas

- Staff CRUD UI for products and the recommendation mapping — natural follow-up once Phase 5/6 prove out the model, but not requested by PROD-01/02/03. Not scheduled in the current 8-phase roadmap; flagged for a future milestone/phase if the owner wants it.
- Bidirectional recommendation display (showing "used for" services on a product detail page) — deferred per D-14, no schema change needed later since the join table already supports the reverse query.

</deferred>

---

*Phase: 5-Product Catalog*
*Context gathered: 2026-08-09*
