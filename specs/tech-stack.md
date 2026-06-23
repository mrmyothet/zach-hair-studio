# Tech Stack

The standard stack is **Next.js (TypeScript) frontend + .NET Web API backend**,
matching the existing repository. New work should align with these choices
unless a deliberate decision updates this document.

## Frontend (web)

- **Framework:** Next.js 15 (App Router) with React 19.
- **Language:** TypeScript 5.x (strict).
- **Styling:** Tailwind CSS 4.
- **Location:** `landing-page/` (public site) and `dashboard/` (staff).
- **Rendering:** Server Components by default; client components only where
  interactivity requires it.

## Staff dashboard

- Same Next.js + TypeScript stack as the public site, separated for clear
  access boundaries and independent deployment.
- Lives in `dashboard/`.

## Backend (API)

- **Runtime:** .NET 10, ASP.NET Core Web API.
- **Language:** C# with nullable reference types and implicit usings enabled.
- **Data access:** Entity Framework Core 10.
- **Database:** SQLite for local/dev (via `Microsoft.EntityFrameworkCore.Sqlite`);
  revisit a server database (e.g. PostgreSQL/SQL Server) before production scale.
- **API docs:** OpenAPI (`Microsoft.AspNetCore.OpenApi`).
- **Solution layout** (`API/`):
  - `ZachHairStudio.Api` — HTTP API / controllers, composition root.
  - `ZachHairStudio.Shared` — domain, `BookingDbContext`, feature folders
    (e.g. `Features/Bookings`).
  - `ZachHairStudio.Admin` — admin-side concerns.

## Contracts & integration

- Frontends consume the .NET API over HTTP/JSON.
- API surface is described by OpenAPI; keep it the source of truth for clients.

## Conventions

- **Feature folders** on the backend (group by feature, e.g. Bookings, not by
  technical layer).
- **TypeScript everywhere** on the frontend; no plain JS for app code.
- Keep dev setup simple: SQLite + `next dev` + `dotnet run` should be enough to
  run the whole system locally.

## Not chosen yet (decide as phases need them)

- Auth provider / session strategy (staff vs. client).
- Payment provider for product checkout.
- Hosting / deployment targets.
- Production database.
