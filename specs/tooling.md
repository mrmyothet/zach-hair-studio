# Tooling

The Claude Code tooling that supports building against this stack. MCP servers
are configured in `.mcp.json`; agent skills live in `.claude/skills/`. Each maps
to the stack (`tech-stack.md`) and the phase it serves (`roadmap.md`).

## MCP servers (`.mcp.json`)

| Server | Purpose | Prereq | Phases |
|---|---|---|---|
| **playwright** | Drive/screenshot/verify the Next.js landing-page & dashboard UIs. | `npx` | 1–3, 8 |
| **context7** | Live, version-accurate docs (Next.js 15, React 19, EF Core 10, .NET 10). | `npx` (API key optional) | all |
| **sqlite** | Inspect the schema/data behind `BookingDbContext` (`API/ZachHairStudio.Api/Data/bookings.db`). | `uvx`; DB exists after API runs once | 2, 5, 6 |
| **github** | Manage PRs/issues from the agent. | `GITHUB_PERSONAL_ACCESS_TOKEN` (or use the `gh` CLI) | ongoing |

## Agent skills (`.claude/skills/`)

| Skill | Purpose |
|---|---|
| **dev** | Launch the API + both Next.js apps locally on known ports. |
| **ef-migrations** | Add/apply EF Core migrations; includes the one-time switch off `EnsureCreated()`. |
| **feature-scaffold** | Create a new feature mirroring `Features/Bookings` + a starter Next.js page. |
| **openapi-client** | Regenerate the typed TS API client from the OpenAPI doc. |

## Deferred

- **Stripe MCP** — payments (roadmap Phase 6).
- **Postgres MCP** — when the production DB decision lands (Phase 8).
- Auth tooling — pending the auth-provider decision.
