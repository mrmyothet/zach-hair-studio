---
phase: 01-service-catalog
verified: 2026-07-09T00:00:00Z
status: passed
score: 8/8 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 1: Service Catalog Verification Report

**Phase Goal:** As a client, I want to browse the salon's services and see everything I need to know about them, so that I can decide what to book.
**Verified:** 2026-07-09
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth (Roadmap Success Criterion) | Status | Evidence |
|---|---|---|---|
| 1 | Client can browse a list of services showing name, description, duration, and price | ✓ VERIFIED | `landing-page/app/services/page.tsx` is an async Server Component rendering `service.name`, `service.shortDescription`, `formatDuration(service.durationMinutes)`, and formatted `price` for each card, grouped by `category` and sorted by `displayOrder`. Live check: `GET http://localhost:5236/api/services` returns 6 seeded rows with all fields populated. `npm run build` compiles `/services` as a static ISR route (revalidate 1m). |
| 2 | Client can open a service detail page for a single service | ✓ VERIFIED | `landing-page/app/services/[slug]/page.tsx` calls `fetchServiceBySlug(slug)`, renders `name`, `longDescription`, `formatDuration`, and price. Live check: `GET /api/services/precision-cut` → `200`. `notFound()` is called when the fetch returns null. |
| 3 | Submitting invalid service data (e.g., missing name, negative price) returns a clear validation error before it reaches the database | ✓ VERIFIED | Live check: `POST /api/services` with `{"name":"","price":-5,...}` → `400` with ProblemDetails body `{"errors":{"Name":["The Name field is required."]}}`. `ServiceCreateDtoValidator` (`RuleFor(x => x.Price).GreaterThanOrEqualTo(0)`, `RuleFor(x => x.Name).NotEmpty()`) runs in the controller before `ServicesService.CreateAsync` is ever called. Backend test suite: 49/49 passing (validators + service + controller + PLAT-01 reflection), run live in this verification pass. |
| 4 | Service catalog requests are handled by a dedicated `ServicesService` layer — controllers never query `BookingDbContext` directly | ✓ VERIFIED | `ServicesController` constructor: `ServicesController(ServicesService servicesService, IValidator<ServiceCreateDto> createValidator, IValidator<ServiceUpdateDto> updateValidator)` — no `BookingDbContext` parameter, no `BookingDbContext` reference anywhere in `ServicesController.cs` (grep confirms zero matches). All `_dbContext` access lives in `ServicesService.cs`. The PLAT-01 reflection test `ServicesController_DoesNotDependOnBookingDbContext` was run in isolation in this verification pass and passed. |

**Score:** 4/4 roadmap success criteria verified (0 present-but-behavior-unverified)

### PLAN-Level Must-Haves (all 4 plans)

| # | Truth | Status | Evidence |
|---|---|---|---|
| 5 | `Service` entity carries Slug, Name, ShortDescription, LongDescription, Category, DurationMinutes, Price, ImageUrl (nullable), IsActive (default true), DisplayOrder | ✓ VERIFIED | `Service.cs` declares exactly these properties with matching types/annotations. |
| 6 | GET /api/services returns only IsActive services ordered by DisplayOrder; GET /api/services/{slug} 200/404 correctly | ✓ VERIFIED | `ServicesService.GetActiveServicesAsync` filters `IsActive` + orders by `DisplayOrder`; `GetBySlugAsync` filters `IsActive` too (inactive/unknown slugs 404). Live-checked both paths. |
| 7 | Service API responses are parsed through a Zod schema in lib/services.ts before rendering | ✓ VERIFIED | `ServiceSchema`/`ServiceListSchema` in `lib/services.ts` field-match `ServiceResponseDto` exactly (id, slug, name, shortDescription, longDescription, category, durationMinutes, price, imageUrl nullable, displayOrder); `fetchServices`/`fetchServiceBySlug` call `.parse()`. |
| 8 | Static `services`/`serviceOptions` retired from lib/data.ts; homepage + Contact dropdown are API-backed (single source of truth, D-14/CAT-03) | ✓ VERIFIED | `grep -n "export const services\|export const serviceOptions" lib/data.ts` → no match. `grep -rn "serviceOptions" app components lib` → no match. `app/page.tsx` fetches once and passes props to `Services`/`Contact`; both components are prop-driven with no `@/lib/data` service imports. |

**Overall score: 8/8 must-haves verified.**

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `API/ZachHairStudio.Shared/Features/Services/Service.cs` | Entity | ✓ VERIFIED | All 10 fields present, correct annotations |
| `API/ZachHairStudio.Shared/Features/Services/Service{Create,Update,Response}Dto.cs` | DTOs | ✓ VERIFIED | Create excludes Id; Update includes IsActive, excludes Id; Response excludes IsActive |
| `API/ZachHairStudio.Shared/Features/Services/ServiceExtensions.cs` | Mappers | ✓ VERIFIED | `ToDto`, `ToEntity`, `ApplyTo` all present and correct |
| `API/ZachHairStudio.Shared/Features/Services/Service{Create,Update}DtoValidator.cs` | FluentValidation | ✓ VERIFIED | Rules match RESEARCH spec (kebab-slug regex, price >= 0, duration 1-480, etc.) |
| `API/ZachHairStudio.Shared/Features/Services/ServicesService.cs` | Service layer | ✓ VERIFIED | Owns all `BookingDbContext` access for the feature |
| `API/ZachHairStudio.Api/Controllers/ServicesController.cs` | Controller | ✓ VERIFIED | No `BookingDbContext` dependency; dual validation flow implemented |
| `API/ZachHairStudio.Shared/Migrations/20260707190502_AddServices.cs` | Migration | ✓ VERIFIED | `CreateTable`, `IX_Services_Slug` unique index, `InsertData` with 6 rows present |
| `API/ZachHairStudio.Api.Tests/*` | Test project | ✓ VERIFIED | 49 tests, run live in this pass, all green |
| `landing-page/lib/services.ts` | Zod data layer | ✓ VERIFIED | Schema + fetchers present, field names match backend DTO |
| `landing-page/lib/formatDuration.ts` | Formatter | ✓ VERIFIED | Shared by list, detail, homepage cards |
| `landing-page/app/services/page.tsx` | List route | ✓ VERIFIED | Async Server Component, no `use client`, category-grouped |
| `landing-page/app/services/[slug]/page.tsx` | Detail route | ✓ VERIFIED | Async Server Component, `notFound()` on unknown slug |
| `landing-page/app/page.tsx`, `Services.tsx`, `Contact.tsx` | Homepage/Contact API wiring | ✓ VERIFIED | Props-driven, single `fetchServices()` call in the page |
| `landing-page/lib/data.ts` | Static catalog removed | ✓ VERIFIED | `services`/`serviceOptions` exports gone; other content (nav, gallery, team, reviews, branches) intact |

### Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| `ServiceCreateDtoValidator` | AddValidatorsFromAssemblyContaining | `Program.cs` DI registration | ✓ WIRED | `Program.cs` registers `AddValidatorsFromAssemblyContaining<ServiceCreateDtoValidator>()` and `AddScoped<ServicesService>()`; live 400/200 responses prove DI resolves at runtime |
| `ServiceResponseDto` fields | Zod `ServiceSchema` | field-for-field match | ✓ WIRED | Confirmed identical field set/casing (camelCase JSON matches Zod keys) |
| `lib/services.ts` fetchers | `/services`, `/services/[slug]`, `Services.tsx`, `Contact.tsx` | direct imports | ✓ WIRED | Single shared data layer reused across all 4 surfaces — grep confirms no duplicate fetch logic |
| `/services/[slug]` Book CTA | `/book?service={slug}` | `<Link href>` | ✓ WIRED (deviation, human-approved) | Plan 03's original spec was `/#contact?service={slug}`; changed to a dedicated `/book` route via the Task 4 human checkpoint (see 01-03-SUMMARY.md "Checkpoint fix" commit `45355a8`). Documented and approved deviation, not a gap. |
| Homepage `?service={slug}` | `Contact` dropdown preselect | `searchParams` → `initialServiceSlug` prop → `servicesBySlug.has()` guard | ✓ WIRED | Validated against unknown slugs (falls back to empty option); confirmed via UAT test #2 (pass) |

### Behavioral Spot-Checks (live stack)

| Behavior | Command | Result | Status |
|---|---|---|---|
| List returns seeded catalog | `curl http://localhost:5236/api/services` | 6 rows, correct DTO shape | ✓ PASS |
| Detail by known slug | `curl -o /dev/null -w "%{http_code}" .../api/services/precision-cut` | 200 | ✓ PASS |
| Detail by unknown slug | `curl -o /dev/null -w "%{http_code}" .../api/services/not-a-real-slug` | 404 | ✓ PASS |
| Invalid POST rejected | `curl -X POST .../api/services` with empty name + negative price | 400, ProblemDetails `errors.Name` | ✓ PASS |
| Backend full test suite | `dotnet test API/ZachHairStudio.Api.Tests` | 49/49 passed | ✓ PASS |
| PLAT-01 reflection test in isolation | `dotnet test --filter DoesNotDependOnBookingDbContext` | 1/1 passed | ✓ PASS |
| Frontend typecheck | `npx tsc --noEmit` (landing-page) | no errors | ✓ PASS |
| Frontend production build | `npm run build` (landing-page) | Compiled successfully, `/services` static ISR, `/services/[slug]` dynamic | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|---|---|---|---|---|
| PLAT-01 | 01-02 | API features served through per-feature service layer; controllers don't call DbContext directly | ✓ SATISFIED | `ServicesController` has no `BookingDbContext` dependency; reflection test passes |
| PLAT-02 | 01-01, 01-03 | Dedicated validation layer (FluentValidation API-side, Zod frontend-side) | ✓ SATISFIED | FluentValidation validators wired server-side; Zod schema wired client-side |
| CAT-01 | 01-03, 01-04 | Client can browse a list of services showing name, description, duration, price | ✓ SATISFIED | `/services` and homepage subset both render this; live-checked |
| CAT-02 | 01-03 | Client can open a service detail page | ✓ SATISFIED | `/services/[slug]` renders detail, 404s on unknown slug |
| CAT-03 | 01-01, 01-02, 01-04 | Services backed by `Service` entity with list + detail API endpoints | ✓ SATISFIED | Entity + endpoints + migration + single-source-of-truth (lib/data.ts retired) all confirmed |

**Note on REQUIREMENTS.md staleness:** `.planning/REQUIREMENTS.md` still shows `CAT-01` and `CAT-02` as unchecked (`[ ]`) and the traceability table marks them "Pending," even though this verification confirms both are implemented and working end-to-end. This is a documentation-sync gap in the requirements tracker, not a code/functionality gap — it does not block the phase goal. Recommend updating `REQUIREMENTS.md` checkboxes/traceability status to reflect Phase 1 completion (CAT-01, CAT-02 → complete) as a housekeeping follow-up.

No orphaned requirements: all 5 requirement IDs declared across the 4 plans (PLAT-01, PLAT-02, CAT-01, CAT-02, CAT-03) match the phase's requirement scope from ROADMAP.

### Anti-Patterns Found

None. Scanned all files created/modified across the 4 plans for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER`, empty-return stubs, and hardcoded-empty state. No blockers, no warnings. The only `placeholder` matches were legitimate HTML `<input placeholder="...">` attributes in `Contact.tsx`, not stub markers. `FluentValidation.AspNetCore` and `UseSeeding`/`UseAsyncSeeding` prohibitions both confirmed absent (grep returns zero matches for each).

### Human Verification

Not required — the phase's blocking human-verify checkpoints (Plan 03 Task 4: catalog/detail/CTA/404; Plan 04 Task 4: homepage subset/dropdown/preselect/booking submit) are both closed out by the existing `.planning/phases/01-service-catalog/01-UAT.md` (status: complete, 16/16 passed, 0 issues), including test #2 "Booking Preselect and Submit (?service={slug})" which specifically covers the Plan 04 outstanding item flagged in 01-04-SUMMARY.md. No new human verification items were identified during this codebase-level pass.

### Gaps Summary

No gaps found. All 4 roadmap success criteria are independently verified against live-running code (not just SUMMARY claims): the backend test suite (49/49), a live API session (list/detail/404/invalid-POST), the frontend build/typecheck, and direct file inspection of every artifact and key link declared across the phase's 4 plans. The one documented deviation (Book CTA target changed from `/#contact?service={slug}` to `/book?service={slug}`) was human-approved via the Plan 03 checkpoint and does not affect any roadmap success criterion. The only non-blocking observation is `REQUIREMENTS.md` checkbox/traceability staleness for CAT-01/CAT-02, which is a documentation housekeeping item, not a functional gap.

---

*Verified: 2026-07-09*
*Verifier: Claude (gsd-verifier)*
