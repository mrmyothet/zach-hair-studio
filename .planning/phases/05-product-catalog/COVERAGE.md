# API Coverage — Phase 5 (Product Catalog)

> Full coverage by default. Opt-outs are explicit, reasoned decisions.

The `api-coverage` detector fired on this phase's RESEARCH.md/CONTEXT.md text, but every matched
signal (`wire API`, `consumes API`, `GET /api/products`, `integration`) refers to this project's
**own internal REST API** being extended with new endpoints — not an external third-party API, SDK,
or service being integrated. RESEARCH.md's Sources section confirms zero new packages/dependencies
this phase; the two external references cited are Microsoft Learn documentation pages (EF Core
many-to-many modeling), not a runtime integration.

| capability | decision | reason |
|---|---|---|
| External API/SDK integration (Stripe, Resend, third-party service, etc.) | OPT-OUT | not applicable — this phase builds new endpoints on the project's own already-existing internal API (`GET /api/products`, `GET /api/products/{slug}`), matching the exact pattern Phase 1 (Services) already shipped. No external service is called. |

No further capability rows apply — there is no external capability surface to enumerate.
