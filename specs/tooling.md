# Tooling

The Claude Code tooling that supports building against this stack. MCP servers
are configured in `.mcp.json`; agent skills live in `.claude/skills/`. Each maps
to the stack (`tech-stack.md`) and the phase it serves (`roadmap.md`).

## MCP servers (`.mcp.json`)

| Server | Purpose | Prereq | Phases |
|---|---|---|---|
| **playwright** | Drive/screenshot/verify the Next.js landing-page & dashboard UIs. | `npx` | 1–3, 8 |
| **context7** | Live, version-accurate docs (Next.js 15, React 19, EF Core 10, .NET 10). | `npx` (API key optional) | all |
| **sqlserver** | Inspect the schema/data behind `BookingDbContext` in the configured SQL Server database. | SQL Server LocalDB or local SQL Server instance | 2, 5, 6 |
| **github** | Manage PRs/issues from the agent. | `GITHUB_PERSONAL_ACCESS_TOKEN` (or use the `gh` CLI) | ongoing |

## Agent skills (`.claude/skills/`)

| Skill | Purpose |
|---|---|
| **dev** | Launch the API + both Next.js apps locally on known ports. |
| **ef-migrations** | Add/apply EF Core migrations; includes the one-time switch off `EnsureCreated()`. |
| **feature-scaffold** | Create a new feature mirroring `Features/Bookings` + a starter Next.js page. |
| **openapi-client** | Regenerate the typed TS API client from the OpenAPI doc. |

## Secret scanning

[gitleaks](https://github.com/gitleaks/gitleaks) keeps secrets out of the repo:

| Where | Config | Prereq |
|---|---|---|
| **pre-commit hook** (blocks secrets before commit) | `.pre-commit-config.yaml` (`gitleaks-system`) | gitleaks binary on PATH + `pre-commit` framework; run `pre-commit install` once per clone |
| **CI** (scans every push/PR) | `.github/workflows/gitleaks.yml` | none — uses `gitleaks/gitleaks-action@v2` |

Both use the gitleaks default rule set. Add a `.gitleaks.toml` to allowlist false
positives if/when one appears.

## Deferred

- **Stripe MCP** — payments (roadmap Phase 6).
- SQL Server production operations tooling — finalize with the hosting/deploy
  decision in Phase 8.
- Auth tooling — pending the auth-provider decision.
