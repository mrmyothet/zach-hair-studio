# Phase 1: Service Catalog - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-07
**Phase:** 1-Service Catalog
**Areas discussed:** Browse & detail page placement, Service model shape, Catalog seeding & content source, Validation scope (write endpoints)

---

## Browse & detail page placement

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated /services route | Homepage keeps a compact API-backed Services section linking to a full /services catalog page with detail pages beneath it | ✓ |
| Homepage section is the catalog | Replace static Services.tsx content with the full API-backed list on the homepage | |
| /services only, homepage untouched | Leave static homepage section as-is; catalog exclusively at /services | |

**User's choice:** Dedicated /services route (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Slug-based | /services/precision-cut — readable, shareable, SEO-friendly; requires unique slug column | ✓ |
| ID-based | /services/42 — simplest, but ugly URLs and weaker SEO | |
| ID + slug | /services/42/precision-cut — robust to renames, more routing logic | |

**User's choice:** Slug-based (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Server components | Fetch in RSC with short revalidate window (ISR); fast first paint, SEO-friendly | ✓ |
| Client-side fetch | Browser fetch like Contact.tsx today; adds loading states, weaker SEO | |
| Static generation at build | Full SSG; catalog only updates on redeploy | |

**User's choice:** Server components (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Pre-filled contact form | "Book this service" links to existing contact form with service pre-selected (/#contact?service=slug) | ✓ |
| Plain link to contact section | CTA anchors to /#contact without pre-selecting | |
| No CTA until Phase 2 | Detail pages purely informational this phase | |

**User's choice:** Pre-filled contact form (Recommended)

---

## Service model shape

| Option | Description | Selected |
|--------|-------------|----------|
| Category field on Service | Simple string/enum Category column grouping the /services page | ✓ |
| Separate Category entity | Categories table with FK; heavier than a single salon needs | |
| No grouping — flat list | Flat list; grouping added later | |

**User's choice:** Category field on Service (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Single fixed price | One decimal Price displayed as-is; variable-price work as separate entries | ✓ |
| Price + 'from' flag | IsStartingPrice bool rendered as "from $X" | |
| Price range (min/max) | PriceMin/PriceMax rendered as "$60–$120" | |

**User's choice:** Single fixed price (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| ImageUrl pointing at /public | Nullable ImageUrl; Phase 1 images are static files; upload comes with Phase 4 | ✓ |
| No image column yet | Typography-only detail pages | |
| Full image handling now | Storage/upload pipeline in Phase 1 | |

**User's choice:** ImageUrl pointing at /public (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| IsActive flag from day one | bool IsActive default true; public queries filter to active | ✓ |
| Add it in Phase 4 | Minimal model now, migration later | |
| You decide | Claude picks during planning | |

**User's choice:** IsActive flag from day one (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| DurationMinutes int | Plain int of minutes; Phase 2 slot math needs minutes arithmetic | ✓ |
| TimeSpan column | SQL time mapping; friction across OpenAPI boundary | |
| Duration ranges | Min/max duration; complicates Phase 2 slot math | |

**User's choice:** DurationMinutes int (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Explicit DisplayOrder column | Owner-controlled int; seeded now, staff-editable in Phase 4 | ✓ |
| Alphabetical by name | Zero maintenance, no merchandising control | |
| You decide | Claude picks during planning | |

**User's choice:** Explicit DisplayOrder column (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Short + long description | Short teaser (~200 chars) for cards + longer detail-page description | ✓ |
| Single description | One field truncated on cards | |
| You decide | Claude picks during planning | |

**User's choice:** Short + long description (Recommended)

---

## Catalog seeding & content source

| Option | Description | Selected |
|--------|-------------|----------|
| Migrate lib/data.ts services | Static services become the seed, enriched with duration/price/category/slug/image | ✓ |
| Fresh realistic catalog | New ~10–15 service menu invented as seed data | |
| Minimal placeholder seed | 3–4 placeholder services to prove the pipeline | |

**User's choice:** Migrate lib/data.ts services (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| EF migration seed / HasData | Seed via migration pipeline; every environment gets the catalog | ✓ |
| Runtime seeder on startup | DbSeeder after Migrate() when table empty | |
| Manual SQL / seed script | Checked-in script run by hand | |

**User's choice:** EF migration seed / HasData (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Retire services from data.ts fully | Homepage section, /services pages, AND Contact dropdown all read from API | ✓ |
| Catalog only; form keeps static options | Contact.tsx keeps hard-coded serviceOptions until Phase 2 | |
| You decide | Claude picks during planning | |

**User's choice:** Retire services from data.ts fully (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Plausible values, flagged | Claude seeds realistic durations/prices marked as owner-reviewable placeholders | ✓ |
| I'll provide real numbers | User supplies actual price list during planning | |
| Hide prices until real | Null prices, hidden price UI — weakens CAT-01 | |

**User's choice:** Plausible values, flagged (Recommended)

---

## Validation scope (write endpoints)

| Option | Description | Selected |
|--------|-------------|----------|
| Ship write endpoints now | POST/PUT with full FluentValidation via Swagger/tests; unauthenticated until Phase 3 (dev-only exposure) | ✓ |
| Service layer + tests only | Validation proven by tests; no HTTP endpoint until Phase 4 | |
| Seed-time validation only | Validate only seed data; defers PLAT-02 substance | |

**User's choice:** Ship write endpoints now (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Result<T> from services | Activate existing Shared/Result.cs; controllers translate to 400/404 ProblemDetails | ✓ |
| Throw ValidationException | Global exception handler maps to ProblemDetails | |
| Validate in controller pipeline | Filter-pipeline validation before service call | |

**User's choice:** Result<T> from services (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Zod on API responses | Zod schemas parse service API responses in the frontend data layer | ✓ |
| Defer Zod to Phase 2 | Introduce with the booking form rewrite | |
| You decide | Claude picks during planning | |

**User's choice:** Zod on API responses (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Leave it — Phase 2 rebuilds it | Establish PLAT-01 on Services feature only; bookings rewritten in Phase 2 anyway | ✓ |
| Refactor bookings now too | BookingsService in Phase 1 so no controller touches DbContext | |
| You decide | Claude picks during planning | |

**User's choice:** Leave it — Phase 2 rebuilds it (Recommended)

---

## Claude's Discretion

- Exact FluentValidation rules per field (lengths, price bounds, slug format)
- OpenAPI-generated client vs extending hand-written lib/api.ts
- Homepage subset size, empty states, sorting within categories, visual details of catalog pages

## Deferred Ideas

None — discussion stayed within phase scope.
