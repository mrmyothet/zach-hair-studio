# API Coverage — Phase 4 (Staff Management: Services & Availability)

> Full coverage by default. Opt-outs are explicit, reasoned decisions.

## Decision: no external API is integrated by this phase

The `api-coverage` detector fired on Phase 4's scope because the plans and roadmap
section use the words "integration endpoints", "wrap api", "wire api", and
"integration api". Every one of those refers to **first-party** ASP.NET Core
surface built inside this repository — `ServicesController`, `AvailabilityController`,
and the `dashboard/` client generated from our own OpenAPI document. No third-party
API, SDK, or hosted service is called by any code this phase added.

For completeness, the external services the wider system does touch, and why they
are out of Phase 4's scope:

- **Resend** (transactional email, added in Phase 2) — Phase 4 sends no email.
  Catalog edits and availability edits trigger no client-facing notification.
- **Azure SQL / LocalDB** — reached through EF Core as the application's own
  datastore, not as an integrated external API surface.

The matrix below records that emptiness as an explicit decision rather than an
un-enumerated hole, which is what this gate exists to prevent.

| capability | decision | reason |
|---|---|---|
| no external API in phase scope | OPT-OUT | Phase 4 adds only first-party ASP.NET Core endpoints and their generated client; no third-party API or SDK is called |
