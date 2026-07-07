# Codebase Structure

**Analysis Date:** 2026-07-07

## Directory Layout

```
zach-hair-studio/
├── specs/                          # Project constitution (mission, tech stack, roadmap, tooling)
│   ├── mission.md
│   ├── tech-stack.md
│   ├── roadmap.md
│   └── tooling.md
│
├── landing-page/                   # Public Next.js site (services + booking)
│   ├── app/                        # Next.js App Router
│   │   ├── layout.tsx              # Root layout with Playfair/Inter fonts
│   │   ├── page.tsx                # Home page (Hero → Reviews → Contact)
│   │   └── globals.css             # Global styles + Tailwind directives
│   │
│   ├── components/                 # Reusable React sections
│   │   ├── Hero.tsx                # Hero banner with slideshow + CTA
│   │   ├── Services.tsx            # Service grid (Precision Cut, Color, etc.)
│   │   ├── Gallery.tsx             # Image gallery of completed work
│   │   ├── Team.tsx                # Team members with status indicators
│   │   ├── Reviews.tsx             # Client testimonials
│   │   ├── Contact.tsx             # Booking form (client component)
│   │   ├── Navbar.tsx              # Navigation header
│   │   ├── Footer.tsx              # Footer with links
│   │   ├── BackToTop.tsx           # Scroll-to-top button
│   │   ├── SectionHeading.tsx      # Reusable section title
│   │   └── icons.tsx               # SVG icon components
│   │
│   ├── lib/                        # Utilities and data
│   │   ├── api.ts                  # Typed HTTP client for backend API
│   │   └── data.ts                 # Static data (services, team, reviews, branches)
│   │
│   ├── public/                     # Static assets
│   │   ├── hair_color_1.jpg        # Gallery images
│   │   ├── hair_color_7.jpg        # Hero slideshow images
│   │   └── ...
│   │
│   ├── .next/                      # Build output (generated)
│   ├── package.json                # Dependencies (Next.js, React, Tailwind)
│   ├── tsconfig.json               # TypeScript config (@/* path alias)
│   ├── next.config.ts              # Next.js config (empty for now)
│   └── .claude/                    # Claude Code agent settings
│
├── dashboard/                      # Staff Next.js dashboard (scaffolded, not yet populated)
│   └── .gitkeep
│
├── mobile-app/                     # Reserved for mobile app (future)
│   └── .gitkeep
│
├── API/                            # .NET 10 backend
│   │
│   ├── ZachHairStudio.Api/         # REST API (ASP.NET Core Web API)
│   │   ├── Controllers/
│   │   │   └── BookingsController.cs    # GET /api/bookings, POST /api/bookings, etc.
│   │   ├── Data/                        # Database-related files (empty; DB context in Shared)
│   │   ├── Program.cs                   # Startup configuration (DbContext, CORS, routes)
│   │   ├── appsettings.json             # Configuration (connection strings, logging)
│   │   ├── appsettings.Development.json # Dev overrides
│   │   ├── ZachHairStudio.Api.csproj    # Project file (references Shared)
│   │   ├── Properties/                  # Launch settings (http://localhost:5236)
│   │   └── bin/, obj/                   # Build output
│   │
│   ├── ZachHairStudio.Shared/      # Shared business logic and data models
│   │   ├── Features/
│   │   │   └── Bookings/
│   │   │       ├── Booking.cs                # Entity (POD with data annotations)
│   │   │       ├── BookingCreateDto.cs       # Input DTO for POST /api/bookings
│   │   │       ├── BookingResponseDto.cs     # Output DTO for responses
│   │   │       ├── BookingExtensions.cs      # Mapper methods (ToDto, ToEntity)
│   │   │       └── BookingStatus.cs          # Enum (Pending, Confirmed, Completed, Cancelled)
│   │   │
│   │   ├── Db/
│   │   │   └── BookingDbContext.cs     # EF Core DbContext (DbSet<Booking>, OnModelCreating)
│   │   │
│   │   ├── Migrations/
│   │   │   ├── 20260702131250_InitialSqlServerMigration.cs       # Initial schema
│   │   │   ├── 20260702131250_InitialSqlServerMigration.Designer.cs
│   │   │   └── BookingDbContextModelSnapshot.cs                   # Latest schema version
│   │   │
│   │   ├── Result.cs                      # Generic result wrapper (not yet used in API)
│   │   ├── ZachHairStudio.Shared.csproj    # Project file (EF Core dependencies)
│   │   └── bin/, obj/                      # Build output
│   │
│   ├── ZachHairStudio.Admin/       # MVC Admin portal (scaffolded)
│   │   ├── Controllers/
│   │   │   ├── BookingController.cs        # Booking management views
│   │   │   └── HomeController.cs           # Home page
│   │   ├── Models/
│   │   │   └── ErrorViewModel.cs           # Error page model
│   │   ├── Views/
│   │   │   ├── Booking/                    # Booking-related views
│   │   │   ├── Home/                       # Home views
│   │   │   └── Shared/                     # Shared layouts (_Layout.cshtml, etc.)
│   │   ├── wwwroot/
│   │   │   ├── css/                        # Custom styles
│   │   │   └── lib/                        # Client libraries (Bootstrap, jQuery, etc.)
│   │   ├── Program.cs                      # Startup (MVC setup)
│   │   ├── ZachHairStudio.Admin.csproj     # Project file
│   │   └── Properties/                     # Launch settings
│   │
│   └── images/                     # Shared images (gallery, etc.)
│
├── .planning/                      # GSD planning directory
│   └── codebase/                   # Codebase mapping documents
│       ├── ARCHITECTURE.md         # Architecture overview
│       ├── STRUCTURE.md            # This file
│       ├── STACK.md                # Technology stack
│       ├── INTEGRATIONS.md         # External APIs
│       ├── CONVENTIONS.md          # Code style rules
│       ├── TESTING.md              # Test patterns
│       └── CONCERNS.md             # Technical debt & issues
│
├── .claude/                        # Claude Code agent settings
│   ├── settings.json               # Workspace settings
│   ├── skills/                     # Custom project skills
│   │   ├── dev/                    # Launch full stack locally
│   │   ├── ef-migrations/          # EF Core migrations
│   │   └── feature-scaffold/       # Scaffold new features
│   │
│   ├── agents/                     # Custom agents
│   ├── commands/                   # Custom commands
│   ├── hooks/                      # Lifecycle hooks
│   └── scripts/                    # Utility scripts
│
├── .github/
│   └── workflows/
│       └── gitleaks.yml            # Secret scanning CI pipeline
│
├── .mcp.json                       # Claude Code MCP server configuration
├── .pre-commit-config.yaml         # Pre-commit hooks (gitleaks)
├── .gitignore                      # Git exclusions
├── README.md                       # Project README (getting started, tech stack)
└── LICENSE                         # MIT License
```

## Directory Purposes

**specs/**
- Purpose: Project constitution and design documents
- Contains: Mission statement, technology decisions, roadmap, tooling guide
- Key files: `roadmap.md` (phases 0–8), `tech-stack.md` (framework versions)

**landing-page/**
- Purpose: Public-facing Next.js site for service discovery and booking
- Contains: App Router pages, React components, Tailwind styling, API client
- Key files: `app/page.tsx` (entry), `components/Contact.tsx` (booking form), `lib/api.ts` (backend integration)

**API/ZachHairStudio.Api/**
- Purpose: REST API server for booking CRUD
- Contains: Controllers with action methods, entry point (Program.cs)
- Key files: `Program.cs` (startup, DbContext registration, migrations), `Controllers/BookingsController.cs` (endpoints)

**API/ZachHairStudio.Shared/**
- Purpose: Shared business logic, domain models, and data access
- Contains: Feature folders (Bookings/), DbContext, DTOs, mapper extensions, migrations
- Key files: `Db/BookingDbContext.cs` (EF context), `Features/Bookings/` (all domain logic), `Migrations/` (schema versions)

**API/ZachHairStudio.Admin/**
- Purpose: Internal staff dashboard for managing bookings
- Contains: MVC controllers, Razor views, static assets
- Status: Scaffolded but not yet integrated with bookings

**dashboard/**
- Purpose: Placeholder for Next.js staff dashboard (phase 3)
- Status: Empty; ready for Next.js project scaffold

**mobile-app/**
- Purpose: Placeholder for mobile app (future phase)
- Status: Reserved; not started

**.claude/**
- Purpose: Claude Code workspace configuration and extensions
- Contains: Skills, agents, custom commands, hooks, settings
- Key skills: `dev/` (launch stack), `feature-scaffold/` (generate features), `ef-migrations/` (database)

## Key File Locations

**Entry Points:**
- `landing-page/app/page.tsx` — Frontend home page (renders all sections)
- `API/ZachHairStudio.Api/Program.cs` — API startup (DbContext setup, route mapping)
- `landing-page/components/Contact.tsx` — Booking form (client-side entry point)

**Configuration:**
- `landing-page/tsconfig.json` — TypeScript paths (`@/*` → project root)
- `landing-page/package.json` — Frontend dependencies (Next.js 15, React 19, Tailwind 4)
- `API/ZachHairStudio.Api/appsettings.json` — Database connection string, logging
- `API/ZachHairStudio.Api/Properties/launchSettings.json` — Server ports (HTTP 5236, HTTPS 7199)

**Core Logic:**
- `API/ZachHairStudio.Shared/Features/Bookings/` — All booking domain logic
  - `Booking.cs` — Entity model with validation
  - `BookingCreateDto.cs` — Input contract
  - `BookingResponseDto.cs` — Output contract
  - `BookingExtensions.cs` — Mapper (ToDto, ToEntity)
  - `BookingStatus.cs` — Status enum
- `landing-page/lib/api.ts` — Typed HTTP client (createBooking function)
- `landing-page/lib/data.ts` — Static data (services, team, reviews, branches)

**Testing:**
- No test files yet (Testing.md will detail structure when tests are added)

**Database:**
- `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` — EF Core context
- `API/ZachHairStudio.Shared/Migrations/` — Migration scripts (schema management)

## Naming Conventions

**Files:**
- `[Entity].cs` — Entity class (e.g., `Booking.cs`)
- `[Entity]Dto.cs` — Data transfer object (e.g., `BookingCreateDto.cs`, `BookingResponseDto.cs`)
- `[Entity]Extensions.cs` — Mapper/helper extension methods (e.g., `BookingExtensions.cs`)
- `[Entity]Controller.cs` — ASP.NET controller class (e.g., `BookingsController.cs`)
- `[Service]Service.cs` — Business logic service (not yet used; recommended for future)
- `[Page].tsx` — Next.js page component (e.g., `page.tsx`)
- `[Component].tsx` — React component (e.g., `Hero.tsx`, `Contact.tsx`)
- `.env.local` — Local environment overrides (gitignored)

**Directories:**
- `Features/[Feature]/` — All files related to a feature (entity, DTOs, extensions, validators)
- `Controllers/` — ASP.NET controller classes
- `Views/` — Razor view templates (.cshtml)
- `components/` — React UI components
- `lib/` — Utilities and data modules (api.ts, data.ts)
- `public/` — Static assets (images, fonts)

**C# Classes:**
- PascalCase: `BookingCreateDto`, `BookingsController`, `BookingDbContext`
- Enums: PascalCase (e.g., `BookingStatus`)

**TypeScript/React:**
- PascalCase for components: `Hero.tsx`, `Contact.tsx`, `BookingsController` (types)
- camelCase for functions: `createBooking()`, `handleSubmit()`
- UPPER_SNAKE_CASE for constants: `API_BASE_URL`

## Where to Add New Code

**New Feature (e.g., Services CRUD):**
- **Backend entity + DTOs:**
  - Create: `API/ZachHairStudio.Shared/Features/Services/` folder
  - Add: `Service.cs` (entity), `ServiceCreateDto.cs`, `ServiceResponseDto.cs`, `ServiceExtensions.cs`, `ServiceStatus.cs`
  - Add migration: Run `dotnet ef migrations add AddServices` in `API/ZachHairStudio.Api/` directory
  - Update: `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` — add `public DbSet<Service> Services => Set<Service>();`

- **API controller:**
  - Create: `API/ZachHairStudio.Api/Controllers/ServicesController.cs`
  - Implement: GET all, GET by id, POST create, PUT update, DELETE

- **Frontend page/component:**
  - Add page: `dashboard/app/services/page.tsx` (staff dashboard)
  - Or add section: `landing-page/components/ServiceCatalog.tsx` (if public-facing)

**New Component (UI):**
- Location: `landing-page/components/[ComponentName].tsx`
- Import in: `landing-page/app/page.tsx` or other components
- Use TypeScript, write as functional component with React.ReactNode props

**Utilities:**
- Shared helpers: `landing-page/lib/` (functions, data)
- Validators: `API/ZachHairStudio.Shared/Validators/` (when adding FluentValidation)
- Services: `API/ZachHairStudio.Shared/Services/` (business logic, testable)

## Special Directories

**Migrations:**
- Purpose: Track schema changes over time
- Location: `API/ZachHairStudio.Shared/Migrations/`
- Generated: Yes (by `dotnet ef migrations add [Name]`)
- Committed: Yes (migrations are part of version control)
- How to run: `dotnet ef database update` in `API/ZachHairStudio.Api/`

**Build Output (bin/ and obj/):**
- Purpose: Compiled code and intermediate build artifacts
- Location: Each project has its own (e.g., `API/ZachHairStudio.Api/bin/`)
- Generated: Yes (by `dotnet build`)
- Committed: No (.gitignored)

**.next/**
- Purpose: Next.js build output and type definitions
- Location: `landing-page/.next/`
- Generated: Yes (by `npm run build`)
- Committed: No (.gitignored)

**node_modules/**
- Purpose: NPM dependencies
- Location: Each frontend project (e.g., `landing-page/node_modules/`)
- Generated: Yes (by `npm install`)
- Committed: No (.gitignored)

**.vs/**
- Purpose: Visual Studio project metadata and user settings
- Location: Project root and API folder
- Generated: Yes (by Visual Studio)
- Committed: No (.gitignored)

---

*Structure analysis: 2026-07-07*
