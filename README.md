# Zach Hair Studio

A **services-led** platform for Zach Hair Studio: book hair styling and coloring
appointments (in person or online), with a supporting store for hair-related
products and a staff dashboard to run the salon.

> The full "constitution" lives in [`specs/`](specs/) —
> [mission](specs/mission.md), [tech stack](specs/tech-stack.md),
> [roadmap](specs/roadmap.md), and [tooling](specs/tooling.md).

## Mission

Booking services is the heart of the product; product sales are a supporting,
stylist-recommended add-on. Priorities, in order:

1. Service discovery and appointment booking.
2. Staff dashboard for managing the schedule and clients.
3. Product catalog and purchasing.
4. Accounts, loyalty, and retention.

The experience must be fast, attractive, and work well on modern browsers and
mobile.

## Tech stack

| Layer | Tech | Location |
|---|---|---|
| Public site | Next.js 15 (App Router), React 19, TypeScript, Tailwind 4 | `landing-page/` |
| Staff dashboard | Next.js 15, React 19, TypeScript, Tailwind 4 | `dashboard/` |
| API | .NET 10 ASP.NET Core Web API, EF Core 10, SQLite, OpenAPI | `API/` |

The frontends consume the .NET API over HTTP/JSON, with OpenAPI as the contract
source of truth. SQLite is used for local/dev; the production database is a
later decision.

## Roadmap (services first)

Small, shippable phases — services before commerce:

0. **Foundation** — repo wired up; API + frontend talk to each other.
1. **Service catalog** — browse styles & colors (read-only).
2. **Booking core** — pick a service, choose a slot, confirm an appointment.
3. **Staff dashboard** — view and update the day's appointments.
4. **Service & availability management** — staff CRUD for services and stylist
   availability.
5. **Product catalog** — browse products as recommended add-ons.
6. **Cart & checkout** — cart, orders, and payments.
7. **Accounts & retention** — client accounts, history, loyalty.
8. **Polish & launch** — responsive pass, production DB, hosting.

See [`specs/roadmap.md`](specs/roadmap.md) for details.

## Getting started

### Prerequisites

- [Node.js](https://nodejs.org/) 18+ (for the Next.js apps)
- [.NET SDK 10](https://dotnet.microsoft.com/) (for the API)

### Run the API

```bash
cd API/ZachHairStudio.Api
dotnet run
```

- API: <http://localhost:5236> (HTTPS: <https://localhost:7199>)
- OpenAPI document (dev only): <http://localhost:5236/openapi/v1.json>

On first run it creates the SQLite database at
`API/ZachHairStudio.Api/Data/bookings.db`. CORS is open in development.

### Run the public site (landing page)

```bash
cd landing-page
npm install   # first time only
npm run dev
```

- Public site: <http://localhost:3000>

### Run the staff dashboard

```bash
cd dashboard
npm install   # first time only
npm run dev -- -p 3001
```

- Dashboard: <http://localhost:3001> (port 3001 avoids clashing with the
  landing page)

> The `dashboard/` app is still being set up; once it has a Next.js project the
> command above will serve it.

### Run everything at once

If you're using Claude Code, the **`dev`** skill launches the API and both
frontends on the ports above in one step.

## Secret scanning

[gitleaks](https://github.com/gitleaks/gitleaks) scans for hardcoded secrets
(API keys, tokens, connection strings) so they never reach git history. It runs
in two places:

- **Locally** as a pre-commit hook (blocks commits that contain secrets).
- **In CI** via GitHub Actions ([`.github/workflows/gitleaks.yml`](.github/workflows/gitleaks.yml))
  on every push and pull request.

After cloning, set up the local hook once:

```bash
# 1. Install the gitleaks binary (the hook runs it from your PATH)
winget install gitleaks      # or: scoop install gitleaks / choco install gitleaks / brew install gitleaks

# 2. Install the pre-commit framework and wire up the hook
pip install pre-commit       # or: pipx install pre-commit / winget install pre-commit
pre-commit install
```

The hook config lives in [`.pre-commit-config.yaml`](.pre-commit-config.yaml)
and uses the `gitleaks-system` variant, which runs the binary above (no Go
toolchain required). To run a manual scan, use `pre-commit run --all-files` to
scan the working tree, or `gitleaks git .` to audit the full commit history.

## Repository layout

```
specs/          Mission, tech stack, roadmap, tooling — the project constitution
landing-page/   Public Next.js site (services + products)
dashboard/      Staff Next.js dashboard
API/            .NET 10 Web API (ZachHairStudio.Api / .Shared / .Admin)
mobile-app/     Reserved for future mobile work
.claude/        Claude Code agents, skills, and settings
.mcp.json       Claude Code MCP server configuration
```
