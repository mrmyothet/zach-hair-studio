# Technology Stack

**Analysis Date:** 2026-07-07

## Languages

**Primary:**
- **C#** .NET 10 - Backend API and shared domain logic in `API/`
- **TypeScript** 5.8.0 - Frontend applications with strict mode enabled
- **JavaScript/JSX** - React components via TypeScript

**Secondary:**
- **SQL/T-SQL** - Database schema and migrations for SQL Server (EF Core generated)

## Runtime

**Environment:**
- **.NET 10** - API runtime via ASP.NET Core Web API
- **Node.js** 18+ - Frontend development and build tooling (Next.js)

**Package Manager:**
- **npm** - Node package manager for frontend dependencies
- **NuGet** - .NET package manager for API dependencies
- Lockfiles: 
  - `landing-page/package-lock.json` (npm)
  - `.csproj` files for .NET project dependencies

## Frameworks

**Backend:**
- **ASP.NET Core 10** - Web API framework (`API/ZachHairStudio.Api/ZachHairStudio.Api.csproj`)
- **Entity Framework Core 10.0.9** - ORM for data access (`API/ZachHairStudio.Shared/Db/BookingDbContext.cs`)

**Frontend:**
- **Next.js 15.3.0** - React meta-framework with App Router
- **React 19.1.0** - UI library
- **Tailwind CSS 4.1.0** - Utility-first CSS framework

**Testing:**
- **Playwright 1.61.1** - End-to-end testing framework (also configured as MCP server)

**Build/Dev:**
- **TypeScript 5.8.0** - Static type checking
- **Next.js built-in** - ESLint integration (command: `next lint`)

## Key Dependencies

**Critical (.NET):**
- `Microsoft.EntityFrameworkCore.SqlServer` 10.0.9 - SQL Server database provider
- `Microsoft.EntityFrameworkCore.Design` 10.0.9 - EF Core CLI and design-time tools
- `Swashbuckle.AspNetCore` 10.0.1 - Swagger/OpenAPI documentation generator
- `Microsoft.AspNetCore.OpenApi` 10.0.8 - OpenAPI specification support
- `Microsoft.OpenApi` 2.7.5 - OpenAPI document model

**Infrastructure (JavaScript):**
- `chrome-devtools-mcp` 1.4.0 - Chrome DevTools MCP server integration

## Configuration

**Environment:**
- **.NET Configuration Files:**
  - `API/ZachHairStudio.Api/appsettings.json` - Production settings with SQL Server connection string to `localhost:ZachHairStudio`
  - `API/ZachHairStudio.Api/appsettings.Development.json` - Local development with SQL Server LocalDB (`(localdb)\MSSQLLocalDB`)
  - `API/ZachHairStudio.Admin/appsettings.json` - Admin portal settings
  - `API/ZachHairStudio.Admin/appsettings.Development.json` - Admin development settings
  
- **.NET User Secrets:**
  - `UserSecretsId: 10efe250-8599-483e-b422-bf93845b187f` - Configured in `ZachHairStudio.Api.csproj` for development secrets management
  
- **Environment Variables (Frontend):**
  - `NEXT_PUBLIC_API_URL` - Base URL for .NET API (defaults to `http://localhost:5236` in dev; see `landing-page/lib/api.ts`)
  - `GITHUB_PERSONAL_ACCESS_TOKEN` - GitHub MCP server authentication (`.mcp.json`)

**Build:**
- `landing-page/next.config.ts` - Next.js configuration (currently minimal)
- `landing-page/tsconfig.json` - TypeScript compiler configuration with path alias `@/*` -> `./`
- `.mcp.json` - Model Context Protocol server configuration for Playwright, Context7, SQLite, GitHub

## Platform Requirements

**Development:**
- .NET SDK 10 (C# compilation, `dotnet run`, Entity Framework migrations)
- Node.js 18+ (npm, Next.js dev server)
- SQL Server LocalDB or local SQL Server instance (via `(localdb)\MSSQLLocalDB` or `localhost`)
- Git (pre-commit hooks configured in `.pre-commit-config.yaml`)
- Pre-commit framework + gitleaks binary (for secret scanning)

**Production:**
- **.NET Runtime 10** - For API hosting
- **Node.js LTS** - For Next.js frontend
- **SQL Server** - Production database (connection string configurable via `appsettings.json` or environment variables)
- Deployment targets: Not yet decided (see `specs/tech-stack.md` — "decide as phases need them")

## Database

**Type:** SQL Server (Microsoft.EntityFrameworkCore.SqlServer)

**Local Development:**
- Connection: `(localdb)\MSSQLLocalDB` in development
- Database name: `ZachHairStudio`
- Migrations: EF Core Code-First via `Microsoft.EntityFrameworkCore.Design`
- Migration files: `API/ZachHairStudio.Shared/Migrations/` (e.g., `20260702131250_InitialSqlServerMigration.cs`)
- DbContext: `API/ZachHairStudio.Shared/Db/BookingDbContext.cs`

**Retry Policy:**
- `EnableRetryOnFailure` configured in `API/ZachHairStudio.Api/Program.cs` with max 10 retries, 30-second max delay

## API Documentation

**OpenAPI/Swagger:**
- Generated via **Swashbuckle.AspNetCore** 10.0.1
- Dev URL: `http://localhost:5236/openapi/v1.json` (development only)
- Swagger UI: `http://localhost:5236/swagger` (development only)
- Endpoints exposed via `builder.Services.AddOpenApi()` and `builder.Services.AddSwaggerGen()`

## CORS Configuration

**Policy:** Unrestricted in development
- `AllowAnyOrigin()`, `AllowAnyMethod()`, `AllowAnyHeader()`
- Configured in `API/ZachHairStudio.Api/Program.cs`

---

*Stack analysis: 2026-07-07*
