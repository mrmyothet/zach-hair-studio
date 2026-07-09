---
status: complete
phase: 01-service-catalog
source: [01-01-SUMMARY.md, 01-02-SUMMARY.md, 01-03-SUMMARY.md, 01-04-SUMMARY.md]
started: 2026-07-09T04:12:00Z
updated: 2026-07-09T04:14:30Z
---

## Current Test

[testing complete]

## Tests

### 1. Cold Start Smoke Test
expected: Kill any running server/service. Clear ephemeral state (temp DBs, caches, lock files). Start the application from scratch. Server boots without errors, any seed/migration completes, and a primary query (health check, homepage load, or basic API call) returns live data.
result: pass

### 2. Booking Preselect and Submit (?service={slug})
expected: Opening the homepage with ?service={slug} pre-selects that service; unknown slugs fall back to the empty option; booking submit still succeeds end-to-end.
result: pass
coverage_id: D4
requirement: CAT-01

### 3. Service entity and DTO contract includes slug, descriptions, category, duration, price, image URL, active flag, and display ordering.
expected: Service entity and DTO contract includes slug, descriptions, category, duration, price, image URL, active flag, and display ordering.
result: pass
source: automated
coverage_id: D1

### 4. Service create/update validators reject missing names, negative prices, invalid slugs, invalid durations, empty descriptions/categories, and negative display order.
expected: Service create/update validators reject missing names, negative prices, invalid slugs, invalid durations, empty descriptions/categories, and negative display order.
result: pass
source: automated
coverage_id: D2

### 5. ServicesController REST endpoints are backed by ServicesService rather than direct BookingDbContext access.
expected: ServicesController REST endpoints are backed by ServicesService rather than direct BookingDbContext access.
result: pass
source: automated
coverage_id: D1

### 6. Invalid service writes return ASP.NET ProblemDetails/ModelState errors.
expected: Invalid service writes return ASP.NET ProblemDetails/ModelState errors.
result: pass
source: automated
coverage_id: D2

### 7. Services are persisted through a Services DbSet with list/detail service methods and API endpoints.
expected: Services are persisted through a Services DbSet with list/detail service methods and API endpoints.
result: pass
source: automated
coverage_id: D3

### 8. AddServices migration creates the Services table, unique Slug index, and 6 seed rows.
expected: AddServices migration creates the Services table, unique Slug index, and 6 seed rows.
result: pass
source: automated
coverage_id: D4

### 9. The AddServices migration applies to a local SQL Server database.
expected: The AddServices migration applies to a local SQL Server database.
result: pass
source: automated
coverage_id: D5

### 10. /services renders the service catalog grouped by category with name, teaser, duration, and price.
expected: /services renders the service catalog grouped by category with name, teaser, duration, and price.
result: pass
source: automated
coverage_id: D1

### 11. /services/[slug] renders a single service detail page and unknown slugs render 404.
expected: /services/[slug] renders a single service detail page and unknown slugs render 404.
result: pass
source: automated
coverage_id: D2

### 12. Frontend service data is validated with Zod schemas matching ServiceResponseDto.
expected: Frontend service data is validated with Zod schemas matching ServiceResponseDto.
result: pass
source: automated
coverage_id: D3

### 13. Book This Service opens /book?service={slug} with the selected service prefilled.
expected: Book This Service opens /book?service={slug} with the selected service prefilled.
result: pass
source: automated
coverage_id: D4

### 14. Homepage Services section renders an API-backed subset of services and links to the full /services catalog.
expected: Homepage Services section renders an API-backed subset of services and links to the full /services catalog.
result: pass
source: automated
coverage_id: D1

### 15. Contact form service dropdown is populated from the API catalog, with option value = service slug.
expected: Contact form service dropdown is populated from the API catalog, with option value = service slug.
result: pass
source: automated
coverage_id: D2

### 16. Static `services` and `serviceOptions` exports removed from lib/data.ts; no remaining importers.
expected: Static `services` and `serviceOptions` exports removed from lib/data.ts; no remaining importers.
result: pass
source: automated
coverage_id: D3

## Summary

total: 16
passed: 16
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

[none yet]
