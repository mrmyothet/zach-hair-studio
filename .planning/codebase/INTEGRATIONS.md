# External Integrations

**Analysis Date:** 2026-07-07

## APIs & External Services

**No external APIs currently integrated.** The system is self-contained and does not consume third-party services. Future integrations planned:

- **Payment provider** - For product checkout (not yet chosen; see `specs/tech-stack.md`)
- **Email service** - For appointment confirmations (not implemented; currently no email sent)
- **Auth provider** - For staff and client authentication (not yet chosen)

## Data Storage

**Databases:**
- **SQL Server** (Microsoft SQL Server or LocalDB)
  - Connection string (dev): `Server=(localdb)\MSSQLLocalDB;Database=ZachHairStudio;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`
  - Connection string (prod): `Server=localhost;Database=ZachHairStudio;Trusted_Connection=True;TrustServerCertificate=True;`
  - Client/ORM: Entity Framework Core 10.0.9 via `API/ZachHairStudio.Shared/Db/BookingDbContext.cs`
  - Schema managed by: EF Core migrations (`API/ZachHairStudio.Shared/Migrations/`)

**File Storage:**
- **Local filesystem only** - No S3, Azure Blob Storage, or CDN configured
- Static assets served via Next.js (`landing-page/` - no documented public/ or assets/ directory)

**Caching:**
- **None** - No Redis, Memcached, or application-level caching layer currently implemented

## Authentication & Identity

**Auth Provider:** None currently implemented

**Planned:**
- Staff/admin authentication for dashboard access (not yet chosen)
- Client authentication for loyalty and account management (Phase 7, `specs/roadmap.md`)

**Current State:**
- No authentication middleware in `API/ZachHairStudio.Api/Program.cs` or `API/ZachHairStudio.Admin/Program.cs`
- API CORS is open to all origins in development
- `.UseAuthorization()` call present but no policies configured

## Monitoring & Observability

**Error Tracking:** None configured

**Logs:**
- **Built-in .NET Logging**: Configured in `appsettings.json` (both API and Admin)
  - Default log level: `Information`
  - ASP.NET Core log level: `Warning` (to reduce noise)
- **No external logging service** (e.g., Application Insights, Serilog with remote sink, ELK)

## CI/CD & Deployment

**Hosting:** Not yet decided (future decision per `specs/tech-stack.md`)

**CI Pipeline:**
- **GitHub Actions** present in `.github/workflows/`
- **Secret scanning** via gitleaks (`.github/workflows/gitleaks.yml`):
  - Runs on every push and pull request
  - Blocks commits containing secrets via pre-commit hook (local)
  - Uses [gitleaks](https://github.com/gitleaks/gitleaks) binary

**Deployment Strategy:** Not documented (Phase 8 "Polish & launch" will define production hosting)

## Environment Configuration

**Required env vars:**
- `.NET Configuration (`appsettings.Development.json`, `appsettings.json`):
  - `ConnectionStrings:DefaultConnection` - SQL Server connection string (configured in appsettings; uses Windows Authentication in dev)
- **Frontend:**
  - `NEXT_PUBLIC_API_URL` - Base URL for backend API (defaults to `http://localhost:5236` in dev)
- **MCP Servers (`.mcp.json`):**
  - `GITHUB_PERSONAL_ACCESS_TOKEN` - GitHub API authentication for MCP server

**Secrets location:**
- **.NET User Secrets**: Development secrets stored securely via `dotnet user-secrets` CLI (referenced in `ZachHairStudio.Api.csproj` with `UserSecretsId`)
- **.env files**: Not present (`.gitignore` configured to exclude `.env*` files)
- **Pre-commit hooks**: `gitleaks` scans to prevent secrets in git history (`.pre-commit-config.yaml`)

## Webhooks & Callbacks

**Incoming:**
- None - No external services sending webhooks to this application

**Outgoing:**
- None - Application does not send webhooks to external services

**Note:** Email confirmation flow planned but not yet implemented. When added, would likely integrate with email service provider (e.g., SendGrid, Mailgun).

## Data Flow & Integration Points

**Frontend → Backend:**
- **HTTP/JSON** via TypeScript `fetch` API in `landing-page/lib/api.ts`
- Endpoints:
  - `POST /api/bookings` - Create booking (from Contact form)
  - `GET /api/bookings` - List bookings (admin/dashboard)
  - `GET /api/bookings/{id}` - Get booking details
  - `POST /api/bookings/{id}/status` - Update booking status

**Backend → Database:**
- **Entity Framework Core** to SQL Server via connection string in `appsettings.json`
- DbContext: `BookingDbContext` in `API/ZachHairStudio.Shared/Db/BookingDbContext.cs`
- Tables: `Bookings` (single table, no related entities currently)

**Frontend Configuration:**
- API URL resolved at runtime via `NEXT_PUBLIC_API_URL` environment variable or hardcoded default `http://localhost:5236`
- See: `landing-page/lib/api.ts` (lines 4-6)

## Development-Only Integrations

**MCP (Model Context Protocol) Servers** (`.mcp.json`):
- **Playwright** - Browser automation for E2E testing and debugging
- **Context7** (@upstash/context7-mcp) - Contextual information lookup
- **SQLite** - Direct database query access (development only, points to `API/ZachHairStudio.Api/Data/bookings.db`)
  - Note: Current setup uses SQL Server, not SQLite; this may be for local quick queries
- **GitHub** - GitHub API access for issues, PRs, branches

## Security Considerations

**CORS:**
- Unrestricted in development (`AllowAnyOrigin()`, `AllowAnyMethod()`, `AllowAnyHeader()`)
- **Must be restricted in production** before deployment

**Secrets Scanning:**
- Gitleaks pre-commit hook blocks secrets from entering git
- CI/CD also scans on push/PR

**Connection Strings:**
- Stored in `appsettings.json` (dev) and `appsettings.Development.json`
- Production values should be injected via environment variables or Azure Key Vault

**HTTPS:**
- `app.UseHttpsRedirection()` configured in both `Program.cs` files
- Development: HTTPS on port 7199 (generated self-signed cert)
- Production: Requires valid SSL certificate

---

*Integration audit: 2026-07-07*
