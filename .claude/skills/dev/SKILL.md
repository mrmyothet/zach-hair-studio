---
name: dev
description: Launch the full Zach Hair Studio stack locally — the .NET API plus the Next.js frontends — for development and manual verification.
---

# dev — run the full stack locally

Use this to bring up the whole system so changes can be exercised end to end.

## Services & ports

| Service | Directory | Command | URL |
|---|---|---|---|
| API (.NET 10) | `API/ZachHairStudio.Api` | `dotnet run` | http://localhost:5236 (https 7199) |
| Landing page (Next.js) | `landing-page` | `npm run dev` | http://localhost:3000 |
| Dashboard (Next.js) | `dashboard` | `npm run dev -- -p 3001` | http://localhost:3001 *(once it exists)* |

OpenAPI document (dev only): http://localhost:5236/openapi/v1.json

## Steps

1. **API:** from `API/ZachHairStudio.Api`, run `dotnet run`. On first run it
   creates `Data/bookings.db` (via `EnsureCreated()` until migrations are
   adopted — see the `ef-migrations` skill). CORS allows any origin in dev.
2. **Landing page:** from `landing-page`, run `npm install` (first time) then
   `npm run dev`.
3. **Dashboard:** only when `dashboard/` has a Next.js app — run it on port 3001
   so it doesn't collide with the landing page.
4. Run long-lived servers in the background and report the URLs.

## Verify

- API responds and `http://localhost:5236/openapi/v1.json` returns the spec.
- Landing page renders at http://localhost:3000.
- Frontend → API requests succeed (CORS is open in dev).
