<!-- GSD:project-start source:PROJECT.md -->

## Project

**Zach Hair Studio**

A services-led platform for a hair salon. The heart of the product is the salon
experience — clients discover styles and colors, then book styling and coloring
appointments online. Selling hair-related products is a supporting offering,
framed as stylist-recommended extensions of the service relationship. It serves
three audiences: clients (discover, book, optionally buy), staff (a dashboard to
run the salon), and the owner (a modern, attractive, maintainable site).

**Core Value:** Booking a salon appointment is effortless — browsing services and reserving a
slot is the primary, friction-free path. If everything else fails, this must work.

### Constraints

- **Tech stack**: Next.js 15 (App Router) + React 19 + Tailwind 4 for `landing-page/` (public) and `dashboard/` (staff); .NET 10 / ASP.NET Core + EF Core 10 / SQL Server for the API — matches the existing repo; new work aligns unless a deliberate decision updates `specs/tech-stack.md`.
- **Architecture**: Feature folders on the backend (group by feature, e.g. `Features/Bookings`), not by technical layer. TypeScript everywhere on the frontend. OpenAPI is the source of truth for API clients.
- **Dev simplicity**: SQL Server LocalDB + `next dev` + `dotnet run` must be enough to run the whole system locally. Exception (D-12): `RESEND_API_KEY` is now REQUIRED to run the API and the test suite — real Resend sends occur in Development AND Testing (no fake sender), so both `dotnet run` and `dotnet test` need the key set via `dotnet user-secrets` (D-13, never a tracked file). This knowingly relaxes "LocalDB + next dev + dotnet run is enough."
- **Sequencing**: Services and the booking flow take priority at every step; product commerce is layered in only after the service experience is solid.
- **Security/Compliance**: gitleaks secret-scanning is wired via pre-commit hook and CI — keep secrets out of the repo.

<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->

## Technology Stack

## Languages

- **C#** .NET 10 - Backend API and shared domain logic in `API/`
- **TypeScript** 5.8.0 - Frontend applications with strict mode enabled
- **JavaScript/JSX** - React components via TypeScript
- **SQL/T-SQL** - Database schema and migrations for SQL Server (EF Core generated)

## Runtime

- **.NET 10** - API runtime via ASP.NET Core Web API
- **Node.js** 18+ - Frontend development and build tooling (Next.js)
- **npm** - Node package manager for frontend dependencies
- **NuGet** - .NET package manager for API dependencies
- Lockfiles: 

## Frameworks

- **ASP.NET Core 10** - Web API framework (`API/ZachHairStudio.Api/ZachHairStudio.Api.csproj`)
- **Entity Framework Core 10.0.9** - ORM for data access (`API/ZachHairStudio.Shared/Db/BookingDbContext.cs`)
- **Next.js 15.3.0** - React meta-framework with App Router
- **React 19.1.0** - UI library
- **Tailwind CSS 4.1.0** - Utility-first CSS framework
- **Playwright 1.61.1** - End-to-end testing framework (also configured as MCP server)
- **TypeScript 5.8.0** - Static type checking
- **Next.js built-in** - ESLint integration (command: `next lint`)

## Key Dependencies

- `Microsoft.EntityFrameworkCore.SqlServer` 10.0.9 - SQL Server database provider
- `Microsoft.EntityFrameworkCore.Design` 10.0.9 - EF Core CLI and design-time tools
- `Swashbuckle.AspNetCore` 10.0.1 - Swagger/OpenAPI documentation generator
- `Microsoft.AspNetCore.OpenApi` 10.0.8 - OpenAPI specification support
- `Microsoft.OpenApi` 2.7.5 - OpenAPI document model
- `chrome-devtools-mcp` 1.4.0 - Chrome DevTools MCP server integration

## Configuration

- **.NET Configuration Files:**
- **.NET User Secrets:**
- **Environment Variables (Frontend):**
- `landing-page/next.config.ts` - Next.js configuration (currently minimal)
- `landing-page/tsconfig.json` - TypeScript compiler configuration with path alias `@/*` -> `./`
- `.mcp.json` - Model Context Protocol server configuration for Playwright, Context7, SQLite, GitHub

## Platform Requirements

- .NET SDK 10 (C# compilation, `dotnet run`, Entity Framework migrations)
- Node.js 18+ (npm, Next.js dev server)
- SQL Server LocalDB or local SQL Server instance (via `(localdb)\MSSQLLocalDB` or `localhost`)
- Git (pre-commit hooks configured in `.pre-commit-config.yaml`)
- Pre-commit framework + gitleaks binary (for secret scanning)
- **.NET Runtime 10** - For API hosting
- **Node.js LTS** - For Next.js frontend
- **SQL Server** - Production database (connection string configurable via `appsettings.json` or environment variables)
- Deployment targets: Not yet decided (see `specs/tech-stack.md` — "decide as phases need them")

## Database

- Connection: `(localdb)\MSSQLLocalDB` in development
- Database name: `ZachHairStudio`
- Migrations: EF Core Code-First via `Microsoft.EntityFrameworkCore.Design`
- Migration files: `API/ZachHairStudio.Shared/Migrations/` (e.g., `20260702131250_InitialSqlServerMigration.cs`)
- DbContext: `API/ZachHairStudio.Shared/Db/BookingDbContext.cs`
- `EnableRetryOnFailure` configured in `API/ZachHairStudio.Api/Program.cs` with max 10 retries, 30-second max delay

## API Documentation

- Generated via **Swashbuckle.AspNetCore** 10.0.1
- Dev URL: `http://localhost:5236/openapi/v1.json` (development only)
- Swagger UI: `http://localhost:5236/swagger` (development only)
- Endpoints exposed via `builder.Services.AddOpenApi()` and `builder.Services.AddSwaggerGen()`

## CORS Configuration

- `AllowAnyOrigin()`, `AllowAnyMethod()`, `AllowAnyHeader()`
- Configured in `API/ZachHairStudio.Api/Program.cs`

<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

## Naming Patterns

### Files

- Components: PascalCase (e.g., `Navbar.tsx`, `Contact.tsx`, `Hero.tsx`)
- Utilities: camelCase (e.g., `api.ts`, `data.ts`)
- Types/Constants: camelCase (e.g., `lib/data.ts`, `lib/api.ts`)
- Classes/Entities: PascalCase (e.g., `Booking.cs`, `BookingsController.cs`)
- DTOs: PascalCase with "Dto" suffix (e.g., `BookingCreateDto.cs`, `BookingResponseDto.cs`)
- Extensions: PascalCase with "Extensions" suffix (e.g., `BookingExtensions.cs`)
- Enums: PascalCase (e.g., `BookingStatus.cs`)

### Functions

- Handlers: camelCase with verb prefix (e.g., `handleSubmit`, `handleChange`)
- API methods: camelCase with verb prefix (e.g., `createBooking`, `extractErrorMessage`)
- Event callbacks: camelCase with "on" prefix in JSX attributes (e.g., `onClick`, `onSubmit`)
- Utility functions: camelCase (e.g., `createBooking` in `lib/api.ts`)
- Public methods: PascalCase (e.g., `GetBookings`, `CreateBooking`, `UpdateStatus`)
- Private methods: camelCase or PascalCase (methods are typically Pascal)
- Extension methods: PascalCase (e.g., `ToDto`, `ToEntity`)
- Async methods: PascalCase with "Async" suffix (e.g., `GetBookingsAsync`)

### Variables

- Local variables: camelCase (e.g., `firstName`, `preferredDate`, `serviceLabel`)
- State variables: camelCase (e.g., `submitted`, `submitting`, `error`)
- Constants: UPPER_SNAKE_CASE for truly constant values (e.g., `API_BASE_URL`)
- Boolean flags: camelCase or prefixed with "is"/"has" (e.g., `open`, `scrolled`, `isValid`)
- Public properties: PascalCase (e.g., `FirstName`, `LastName`, `Email`, `PreferredDate`)
- Private fields: camelCase with underscore prefix (e.g., `_dbContext`)
- Local variables: camelCase (e.g., `booking`, `connectionString`)
- Constants: PascalCase or UPPER_SNAKE_CASE (e.g., `BookingStatus.Pending`)

### Types

- Type aliases: camelCase (e.g., `NavLink`, `Service`, `BookingRequest`, `BookingResponse`)
- Interfaces: camelCase (convention aligns with types)
- Enums: PascalCase (e.g., exported types from `lib/data.ts`)
- Classes: PascalCase (e.g., `Booking`, `BookingCreateDto`)
- Enums: PascalCase with values in PascalCase (e.g., `BookingStatus.Pending`, `BookingStatus.Confirmed`)

## Code Style

### Formatting

- Tailwind CSS v4.1.0 with PostCSS
- Custom theme colors defined in `app/globals.css` (@theme block)
- Smooth scrolling: `scroll-behavior: smooth` on html element
- Spacing and sizing: Tailwind utility classes (e.g., `py-24`, `px-6`, `gap-8`)
- Responsive design: Mobile-first with breakpoints (`md:`, `lg:`, `sm:`)
- File-scoped namespaces (C# 11+)
- Implicit using statements enabled
- Nullable enabled (`#nullable enable`)
- No external formatter configured (Visual Studio default)

### Linting

- No `.eslintrc` configured
- Uses Next.js built-in linting (via `npm run lint` → `next lint`)
- Strict TypeScript: `strict: true` in `tsconfig.json`
- No external analyzers configured in .csproj
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit using statements enabled

## Import Organization

### TypeScript/React

- `@/*` maps to project root (configured in `tsconfig.json`)
- Used for cleaner imports across the entire frontend

### C# (.NET)

## Error Handling

### TypeScript/React

### C# (.NET)

- See `API/ZachHairStudio.Api/Controllers/BookingsController.cs`
- Does NOT yet use Result<T> wrapper; uses direct HTTP status codes

## Logging

- No logging framework configured
- Uses `console.*` methods directly when needed
- No logging framework configured in current code
- Standard ASP.NET Core dependency injection patterns available but not yet in use

## Comments

### TypeScript/React

- JSDoc for exported functions and types
- Inline comments for non-obvious logic
- Comments above complex expressions

### C# (.NET)

- Minimal comments; code is self-documenting via naming
- XML documentation (`///`) not yet in use; can add if needed
- Fluent method names for readability (e.g., `ToDto()`, `ToEntity()`)

## Function Design

### TypeScript/React

- Keep components under 200 lines of JSX
- Extract nested components for reusability (e.g., Logo, Field sub-components in Navbar and Contact)
- Use composition for complex UI logic
- Components return JSX.Element
- Event handlers typically return void
- Async functions return Promise<T>

### C# (.NET)

- Controller actions: 10-20 lines
- Service methods: keep focused on single responsibility
- Extension methods: typically 5-15 lines
- Controller actions return `ActionResult<T>` or `IActionResult`
- Service methods can return Result<T> (in shared Result.cs)
- Queries return IEnumerable<T> or IAsyncEnumerable<T>

## Module Design

### TypeScript/React

- Named exports for utilities and types
- Default export for React components
- All types exported from `lib/data.ts` for reuse
- Not currently in use (imports are direct)

### C# (.NET

- Public classes exposed through namespaces
- File-scoped namespaces used for organization

## Architecture Patterns

### Frontend Layer Pattern

### Backend Layer Pattern

- Entity ↔ DTO conversion via static extension methods
- Keeps models and contracts separate
- See `BookingExtensions.cs` for example

<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

## System Overview

```text

```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| Landing Page | Public-facing service discovery and booking interface | `landing-page/app/page.tsx` |
| Contact Section | Booking form with API integration | `landing-page/components/Contact.tsx` |
| API Client | Typed HTTP client for backend communication | `landing-page/lib/api.ts` |
| BookingsController | REST endpoints for CRUD operations on bookings | `API/ZachHairStudio.Api/Controllers/BookingsController.cs` |
| BookingDbContext | EF Core context defining schema and relationships | `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` |
| Booking Entity | Core domain model for appointment | `API/ZachHairStudio.Shared/Features/Bookings/Booking.cs` |
| DTOs | Data transfer objects for API contracts | `API/ZachHairStudio.Shared/Features/Bookings/` |
| BookingExtensions | Mapper methods (entity ↔ DTO) | `API/ZachHairStudio.Shared/Features/Bookings/BookingExtensions.cs` |

## Pattern Overview

- **Separation of layers:** Frontend (React/Next.js) → API (.NET REST) → Data (EF Core)
- **Feature-based organization:** Shared library organizes domain logic by feature (Bookings/)
- **DTO pattern:** Explicit input (BookingCreateDto) and output (BookingResponseDto) contracts
- **Extension methods for mapping:** Booking ↔ DTO conversions via fluent extension methods
- **Database-first migrations:** EF Core migrations own schema definition

## Layers

- Purpose: User-facing web interface for browsing services and booking appointments
- Location: `landing-page/`
- Contains: Next.js pages, React components, Tailwind CSS styling, API client
- Depends on: External API at `NEXT_PUBLIC_API_URL` (defaults to `http://localhost:5236`)
- Used by: End users via browser
- Purpose: Expose RESTful endpoints for bookings CRUD
- Location: `API/ZachHairStudio.Api/Controllers/`
- Contains: ASP.NET Core ControllerBase subclasses with action methods
- Depends on: BookingDbContext (injected), DTOs from Shared
- Used by: Frontend, external clients
- Purpose: Encapsulate domain models, validation rules, and mapper logic
- Location: `API/ZachHairStudio.Shared/Features/Bookings/`
- Contains: Domain entities (Booking), DTOs, value objects (BookingStatus enum), mapper extensions
- Depends on: EF Core annotations (via [Required], [StringLength], etc.)
- Used by: API controllers, Data layer
- Purpose: Persist and retrieve booking data
- Location: `API/ZachHairStudio.Shared/Db/` (context definition)
- Contains: BookingDbContext with DbSet<Booking>, OnModelCreating fluent configuration
- Depends on: Entity Framework Core, SQL Server provider
- Used by: Controllers through dependency injection

## Data Flow

### Primary Request Path: Create Booking

### Secondary Flow: Retrieve Bookings

- **Frontend:** React useState hooks (Contact.tsx: `submitted`, `submitting`, `error`)
- **Backend:** No in-memory state; all state persists to database immediately
- **Stateless API:** Each request is independent; no session/cache layer

## Key Abstractions

- Purpose: Core domain model representing a hair service appointment
- Examples: `API/ZachHairStudio.Shared/Features/Bookings/Booking.cs`
- Pattern: POCO (Plain Old CLR Object) with data annotations for validation
- Purpose: Contract for incoming POST /api/bookings requests
- Subset of Booking fields (excludes Id, Status=auto-set to Pending, CreatedAt=auto-set to UtcNow)
- Enforces input validation (required, max-length, email format, phone format)
- Purpose: Contract for outgoing API responses (GET, POST responses)
- Includes computed field: `customerName` (FirstName + LastName concatenation)
- Safe for client consumption; no internal fields exposed
- Purpose: Stateless mappers using extension methods
- Pattern: `.ToDto()` and `.ToEntity()` fluent conversions
- Keeps mapping logic DRY (single source of truth)
- Purpose: Type-safe appointment status domain value
- Values: Pending (default), Confirmed, Completed, Cancelled
- EF Core config: Stores as string in database (`HasConversion<string>()`)

## Entry Points

- Location: `landing-page/app/page.tsx`
- Triggers: Browser navigation to `/`
- Responsibilities:
- Location: `API/ZachHairStudio.Api/Program.cs`
- Triggers: Application startup
- Responsibilities:
- Location: `API/ZachHairStudio.Api/Controllers/BookingsController.cs`
- Route: `[Route("api/[controller]")]` → `/api/bookings`
- Entry methods:

## Architectural Constraints

- **Threading:** Single-threaded event loop (frontend React/Next.js) + ASP.NET Core request-per-thread model on backend. No explicit multi-threading or background jobs yet.
- **Global state:** None at API level; all state in database. Frontend uses React component state (no Redux/Context API required at this phase).
- **Circular imports:** Not observed. Shared library is a base dependency; Api references Shared, but not vice versa.
- **Database connections:** Single DbContext instance per HTTP request (ASP.NET Core's dependency injection scoping).
- **CORS:** Fully open in development (`AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`). Must be restricted in production.

## Anti-Patterns

### Stateless Controllers with DB Calls

### Manual Mapping with Extension Methods

### No Validation Layer

## Error Handling

- **Validation errors:** If `ModelState.IsValid` is false, return `BadRequest(ModelState)`. ASP.NET Core formats it as ProblemDetails (RFC 7807) with a 400 status.
- **Not found:** If entity lookup fails (e.g., `FindAsync(id)` returns null), return `NotFound()` (404).
- **Success:** Return `Ok(dto)` (200) or `CreatedAtAction(...)` (201 for POST).
- **Unhandled exceptions:** Global exception handler logs and returns 500 (production) or detailed stack trace (development).

## Cross-Cutting Concerns

<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

| Skill | Description | Path |
|-------|-------------|------|
| dev | Launch the full Zach Hair Studio stack locally — the .NET API plus the Next.js frontends — for development and manual verification. | `.claude/skills/dev/SKILL.md` |
| ef-migrations | Add and apply EF Core migrations against BookingDbContext, including the one-time switch off EnsureCreated() so migrations own the schema. | `.claude/skills/ef-migrations/SKILL.md` |
| feature-scaffold | Scaffold a new backend feature mirroring the Features/Bookings pattern (entity, DTOs, mappers, DbSet, controller) plus a starter Next.js page. | `.claude/skills/feature-scaffold/SKILL.md` |
| openapi-client | Regenerate a typed TypeScript API client for the Next.js frontends from the .NET OpenAPI document, keeping OpenAPI as the source of truth. | `.claude/skills/openapi-client/SKILL.md` |
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:

- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
