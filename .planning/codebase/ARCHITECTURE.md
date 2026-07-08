<!-- refreshed: 2026-07-07 -->
# Architecture

**Analysis Date:** 2026-07-07

## System Overview

```text
┌────────────────────────────────────────────────────────────────┐
│                        Web Layer (Frontend)                     │
├──────────────────┬──────────────────────┬─────────────────────┤
│   Landing Page   │    Staff Dashboard   │   Mobile App        │
│   (Active)       │    (Next.js ready)   │   (Planned)         │
│ `landing-page/`  │  `dashboard/`        │ `mobile-app/`       │
└────────┬─────────┴──────────────────────┴──────────────┬───────┘
         │ HTTP/JSON (fetch)                            │
         │                                              │
         ▼                                              ▼
┌────────────────────────────────────────────────────────────────┐
│                  API Layer (.NET ASP.NET Core)                  │
│                 `API/ZachHairStudio.Api/`                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ Controllers (BookingsController)                         │  │
│  │ - GET /api/bookings                                      │  │
│  │ - GET /api/bookings/{id}                                 │  │
│  │ - POST /api/bookings                                     │  │
│  │ - POST /api/bookings/{id}/status                         │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────┬──────────────────────────────────────────────────────┘
         │
         ▼
┌────────────────────────────────────────────────────────────────┐
│             Shared Business Logic Layer (.NET)                  │
│              `API/ZachHairStudio.Shared/`                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ Features/Bookings/                                       │  │
│  │ - Booking (entity)                                       │  │
│  │ - BookingCreateDto                                       │  │
│  │ - BookingResponseDto                                     │  │
│  │ - BookingExtensions (mapper)                             │  │
│  │ - BookingStatus (enum)                                   │  │
│  │                                                          │  │
│  │ Db/BookingDbContext (EF Core DbContext)                 │  │
│  │ Migrations/ (schema management)                          │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────┬──────────────────────────────────────────────────────┘
         │
         ▼
┌────────────────────────────────────────────────────────────────┐
│                   Data Layer (SQL Server)                       │
│            BookingDbContext + EF Core Migrations               │
│                                                                │
│  Bookings Table                                                │
│  - Id, FirstName, LastName, Email, Phone                      │
│  - Service, PreferredDate, Message                             │
│  - Status, CreatedAt                                           │
└────────────────────────────────────────────────────────────────┘
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

**Overall:** Layered N-tier architecture with clear separation of concerns

**Key Characteristics:**
- **Separation of layers:** Frontend (React/Next.js) → API (.NET REST) → Data (EF Core)
- **Feature-based organization:** Shared library organizes domain logic by feature (Bookings/)
- **DTO pattern:** Explicit input (BookingCreateDto) and output (BookingResponseDto) contracts
- **Extension methods for mapping:** Booking ↔ DTO conversions via fluent extension methods
- **Database-first migrations:** EF Core migrations own schema definition

## Layers

**Frontend Layer:**
- Purpose: User-facing web interface for browsing services and booking appointments
- Location: `landing-page/`
- Contains: Next.js pages, React components, Tailwind CSS styling, API client
- Depends on: External API at `NEXT_PUBLIC_API_URL` (defaults to `http://localhost:5236`)
- Used by: End users via browser

**API Layer (Controllers):**
- Purpose: Expose RESTful endpoints for bookings CRUD
- Location: `API/ZachHairStudio.Api/Controllers/`
- Contains: ASP.NET Core ControllerBase subclasses with action methods
- Depends on: BookingDbContext (injected), DTOs from Shared
- Used by: Frontend, external clients

**Business Logic Layer:**
- Purpose: Encapsulate domain models, validation rules, and mapper logic
- Location: `API/ZachHairStudio.Shared/Features/Bookings/`
- Contains: Domain entities (Booking), DTOs, value objects (BookingStatus enum), mapper extensions
- Depends on: EF Core annotations (via [Required], [StringLength], etc.)
- Used by: API controllers, Data layer

**Data Layer:**
- Purpose: Persist and retrieve booking data
- Location: `API/ZachHairStudio.Shared/Db/` (context definition)
- Contains: BookingDbContext with DbSet<Booking>, OnModelCreating fluent configuration
- Depends on: Entity Framework Core, SQL Server provider
- Used by: Controllers through dependency injection

## Data Flow

### Primary Request Path: Create Booking

1. **Frontend (Contact.tsx:25-45)** — User submits form
   - `handleSubmit()` collects form data
   - Calls `createBooking(BookingRequest)` from `lib/api.ts`

2. **API Client (lib/api.ts:39-60)** — Prepare and send HTTP request
   - POST to `{API_BASE_URL}/api/bookings`
   - Headers: `Content-Type: application/json`
   - Body: JSON-stringified BookingRequest

3. **BookingsController (Controllers/BookingsController.cs:42-55)** — Receive and validate
   - `CreateBooking(BookingCreateDto request)` action
   - ModelState validation (ensures required fields present, format valid)
   - `request.ToEntity()` maps DTO → Booking entity

4. **Database (Db/BookingDbContext.cs)** — Persist
   - `_dbContext.Bookings.Add(booking)` queues entity for insert
   - `await _dbContext.SaveChangesAsync()` executes INSERT statement
   - Database applies column constraints (MaxLength, required fields, email format)

5. **Response (Controllers/BookingsController.cs:54)** — Return confirmation
   - `CreatedAtAction(nameof(GetBooking), ...)` returns HTTP 201
   - Response body: `booking.ToDto()` — entity mapped back to BookingResponseDto

6. **Frontend (Contact.tsx:46, 126-137)** — Display result
   - Catch block handles errors
   - `submitted` state toggles success message UI
   - Displays: "You're All Set! We've received your request..."

### Secondary Flow: Retrieve Bookings

1. **Frontend** — List page (not yet implemented, ready for dashboard)
2. **API** — `GET /api/bookings` (BookingsController.cs:19-28)
   - Fetches all bookings ordered by CreatedAt descending
   - Maps to BookingResponseDto collection
3. **Response** — HTTP 200 with array of BookingResponseDto

**State Management:**
- **Frontend:** React useState hooks (Contact.tsx: `submitted`, `submitting`, `error`)
- **Backend:** No in-memory state; all state persists to database immediately
- **Stateless API:** Each request is independent; no session/cache layer

## Key Abstractions

**Booking (Entity):**
- Purpose: Core domain model representing a hair service appointment
- Examples: `API/ZachHairStudio.Shared/Features/Bookings/Booking.cs`
- Pattern: POCO (Plain Old CLR Object) with data annotations for validation

**BookingCreateDto:**
- Purpose: Contract for incoming POST /api/bookings requests
- Subset of Booking fields (excludes Id, Status=auto-set to Pending, CreatedAt=auto-set to UtcNow)
- Enforces input validation (required, max-length, email format, phone format)

**BookingResponseDto:**
- Purpose: Contract for outgoing API responses (GET, POST responses)
- Includes computed field: `customerName` (FirstName + LastName concatenation)
- Safe for client consumption; no internal fields exposed

**BookingExtensions:**
- Purpose: Stateless mappers using extension methods
- Pattern: `.ToDto()` and `.ToEntity()` fluent conversions
- Keeps mapping logic DRY (single source of truth)

**BookingStatus (Enum):**
- Purpose: Type-safe appointment status domain value
- Values: Pending (default), Confirmed, Completed, Cancelled
- EF Core config: Stores as string in database (`HasConversion<string>()`)

## Entry Points

**Frontend Entry (landing-page):**
- Location: `landing-page/app/page.tsx`
- Triggers: Browser navigation to `/`
- Responsibilities:
  - Renders layout (Navbar, Hero, Services, Gallery, Team, Reviews, Contact, Footer)
  - Orchestrates all page sections via component composition
  - Contact component wired to API via `createBooking()`

**API Entry (ZachHairStudio.Api):**
- Location: `API/ZachHairStudio.Api/Program.cs`
- Triggers: Application startup
- Responsibilities:
  - Registers DbContext with SQL Server connection
  - Enables CORS (open in dev)
  - Runs migrations on startup: `db.Database.Migrate()`
  - Enables OpenAPI/Swagger UI in Development
  - Maps controller routes via attribute routing

**BookingsController:**
- Location: `API/ZachHairStudio.Api/Controllers/BookingsController.cs`
- Route: `[Route("api/[controller]")]` → `/api/bookings`
- Entry methods:
  - `GetBookings()` — GET all
  - `GetBooking(id)` — GET by id
  - `CreateBooking(dto)` — POST new
  - `UpdateStatus(id, status)` — POST status update

## Architectural Constraints

- **Threading:** Single-threaded event loop (frontend React/Next.js) + ASP.NET Core request-per-thread model on backend. No explicit multi-threading or background jobs yet.
- **Global state:** None at API level; all state in database. Frontend uses React component state (no Redux/Context API required at this phase).
- **Circular imports:** Not observed. Shared library is a base dependency; Api references Shared, but not vice versa.
- **Database connections:** Single DbContext instance per HTTP request (ASP.NET Core's dependency injection scoping).
- **CORS:** Fully open in development (`AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`). Must be restricted in production.

## Anti-Patterns

### Stateless Controllers with DB Calls

**What happens:** BookingsController directly injects DbContext and executes queries (no service layer).

**Why it's wrong:** Controllers are not testable in isolation without a real database. Adding validation or business rules later requires modifying multiple methods.

**Do this instead:** Extract a `BookingService` class (`API/ZachHairStudio.Shared/Services/BookingService.cs`) with testable methods like `CreateAsync(BookingCreateDto)`, `GetByIdAsync(int id)`, etc. Inject it into the controller.

### Manual Mapping with Extension Methods

**What happens:** `Booking.ToDto()` and `BookingCreateDto.ToEntity()` are written by hand.

**Why it's wrong:** As features grow (more DTOs, nested objects, computed fields), manual mapping code becomes scattered and hard to maintain. Inconsistencies between entity and DTO can silently corrupt data.

**Do this instead:** Introduce [AutoMapper](https://automapper.org/) (`API/ZachHairStudio.Shared/Mapping/`) with profiles (`BookingMappingProfile.cs`). Define mappings in one place; let the library handle conversions. Enables reverse mapping automatically.

### No Validation Layer

**What happens:** Validation is only via DataAnnotations (`[Required]`, `[StringLength]`) on DTOs.

**Why it's wrong:** Complex business rules (e.g., "cannot book in the past", "maximum 3 bookings per day per stylist") cannot be expressed. Validation runs late (in the controller after deserialization).

**Do this instead:** Create a `FluentValidation` validator (`API/ZachHairStudio.Shared/Validators/BookingCreateDtoValidator.cs`). Register it in Program.cs. Catch validation errors before they reach the database.

## Error Handling

**Strategy:** ASP.NET Core exception handling + model validation

**Patterns:**
- **Validation errors:** If `ModelState.IsValid` is false, return `BadRequest(ModelState)`. ASP.NET Core formats it as ProblemDetails (RFC 7807) with a 400 status.
- **Not found:** If entity lookup fails (e.g., `FindAsync(id)` returns null), return `NotFound()` (404).
- **Success:** Return `Ok(dto)` (200) or `CreatedAtAction(...)` (201 for POST).
- **Unhandled exceptions:** Global exception handler logs and returns 500 (production) or detailed stack trace (development).

Frontend (Contact.tsx) catches fetch errors and displays user-friendly messages extracted from the response.

## Cross-Cutting Concerns

**Logging:** Not implemented yet. ASP.NET Core logs framework events to console in development. Add `ILogger<BookingsController>` injection when needed.

**Validation:** DataAnnotations on entities and DTOs. FluentValidation recommended for complex rules.

**Authentication:** Not implemented. CORS is open; no auth headers required. Implement JWT bearer auth on API before public deployment.

---

*Architecture analysis: 2026-07-07*
