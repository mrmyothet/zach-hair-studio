# Coding Conventions

**Analysis Date:** 2026-07-07

## Naming Patterns

### Files

**TypeScript/React (Frontend):**
- Components: PascalCase (e.g., `Navbar.tsx`, `Contact.tsx`, `Hero.tsx`)
- Utilities: camelCase (e.g., `api.ts`, `data.ts`)
- Types/Constants: camelCase (e.g., `lib/data.ts`, `lib/api.ts`)

**C# (.NET Backend):**
- Classes/Entities: PascalCase (e.g., `Booking.cs`, `BookingsController.cs`)
- DTOs: PascalCase with "Dto" suffix (e.g., `BookingCreateDto.cs`, `BookingResponseDto.cs`)
- Extensions: PascalCase with "Extensions" suffix (e.g., `BookingExtensions.cs`)
- Enums: PascalCase (e.g., `BookingStatus.cs`)

### Functions

**TypeScript/React:**
- Handlers: camelCase with verb prefix (e.g., `handleSubmit`, `handleChange`)
- API methods: camelCase with verb prefix (e.g., `createBooking`, `extractErrorMessage`)
- Event callbacks: camelCase with "on" prefix in JSX attributes (e.g., `onClick`, `onSubmit`)
- Utility functions: camelCase (e.g., `createBooking` in `lib/api.ts`)

**C# (.NET):**
- Public methods: PascalCase (e.g., `GetBookings`, `CreateBooking`, `UpdateStatus`)
- Private methods: camelCase or PascalCase (methods are typically Pascal)
- Extension methods: PascalCase (e.g., `ToDto`, `ToEntity`)
- Async methods: PascalCase with "Async" suffix (e.g., `GetBookingsAsync`)

### Variables

**TypeScript/React:**
- Local variables: camelCase (e.g., `firstName`, `preferredDate`, `serviceLabel`)
- State variables: camelCase (e.g., `submitted`, `submitting`, `error`)
- Constants: UPPER_SNAKE_CASE for truly constant values (e.g., `API_BASE_URL`)
- Boolean flags: camelCase or prefixed with "is"/"has" (e.g., `open`, `scrolled`, `isValid`)

**C# (.NET):**
- Public properties: PascalCase (e.g., `FirstName`, `LastName`, `Email`, `PreferredDate`)
- Private fields: camelCase with underscore prefix (e.g., `_dbContext`)
- Local variables: camelCase (e.g., `booking`, `connectionString`)
- Constants: PascalCase or UPPER_SNAKE_CASE (e.g., `BookingStatus.Pending`)

### Types

**TypeScript:**
- Type aliases: camelCase (e.g., `NavLink`, `Service`, `BookingRequest`, `BookingResponse`)
- Interfaces: camelCase (convention aligns with types)
- Enums: PascalCase (e.g., exported types from `lib/data.ts`)

**C#:**
- Classes: PascalCase (e.g., `Booking`, `BookingCreateDto`)
- Enums: PascalCase with values in PascalCase (e.g., `BookingStatus.Pending`, `BookingStatus.Confirmed`)

## Code Style

### Formatting

**Tool:** Tailwind CSS for styling (frontend); built-in .NET formatting (backend)

**Frontend Key Settings:**
- Tailwind CSS v4.1.0 with PostCSS
- Custom theme colors defined in `app/globals.css` (@theme block)
- Smooth scrolling: `scroll-behavior: smooth` on html element
- Spacing and sizing: Tailwind utility classes (e.g., `py-24`, `px-6`, `gap-8`)
- Responsive design: Mobile-first with breakpoints (`md:`, `lg:`, `sm:`)

**Backend Key Settings:**
- File-scoped namespaces (C# 11+)
- Implicit using statements enabled
- Nullable enabled (`#nullable enable`)
- No external formatter configured (Visual Studio default)

### Linting

**Frontend:**
- No `.eslintrc` configured
- Uses Next.js built-in linting (via `npm run lint` → `next lint`)
- Strict TypeScript: `strict: true` in `tsconfig.json`

**Backend:**
- No external analyzers configured in .csproj
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit using statements enabled

## Import Organization

### TypeScript/React

**Order:**
1. React/Next.js imports (e.g., `import { useState } from "react"`)
2. Third-party library imports
3. Absolute path imports from `@/` (root-relative)
4. Relative imports (e.g., `./icons`, `@/components/Navbar`)

**Example from `landing-page/components/Contact.tsx`:**
```typescript
import { useState } from "react";
import { branches, contactEmail, serviceOptions } from "@/lib/data";
import { createBooking } from "@/lib/api";
import { ArrowRightIcon, MapPinIcon } from "./icons";
```

**Path Aliases:**
- `@/*` maps to project root (configured in `tsconfig.json`)
- Used for cleaner imports across the entire frontend

### C# (.NET)

**Order:**
1. System namespaces
2. Microsoft namespaces
3. Project namespaces
4. Then the file-scoped namespace declaration

**Example from `API/ZachHairStudio.Api/Controllers/BookingsController.cs`:**
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Bookings;

namespace ZachHairStudio.Api.Controllers;
```

## Error Handling

### TypeScript/React

**Pattern:** try-catch with Error objects

```typescript
try {
  res = await fetch(`${API_BASE_URL}/api/bookings`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
} catch {
  throw new Error(
    "We couldn't reach the booking service. Please check your connection and try again."
  );
}

if (!res.ok) {
  throw new Error(await extractErrorMessage(res));
}
```

**State-based Error Handling:** React state for UI feedback

```typescript
const [error, setError] = useState<string | null>(null);

try {
  await createBooking(/* ... */);
} catch (err) {
  setError(
    err instanceof Error
      ? err.message
      : "Something went wrong. Please try again."
  );
}
```

**ASP.NET Error Extraction:** Parses both ModelState validation and ProblemDetails responses

```typescript
async function extractErrorMessage(res: Response): Promise<string> {
  try {
    const body = await res.json();
    if (body?.errors && typeof body.errors === "object") {
      const messages = Object.values(body.errors as Record<string, string[]>)
        .flat()
        .filter(Boolean);
      if (messages.length > 0) return messages.join(" ");
    }
    if (typeof body?.title === "string") return body.title;
  } catch {
    // Response wasn't JSON — fall through to the generic message.
  }
  return `Something went wrong (${res.status}). Please try again.`;
}
```

### C# (.NET)

**Pattern: Result<T> Wrapper**

Generic result type that encapsulates success/failure state with error classification:

```csharp
public static class Result<T>
{
    public bool IsSuccess { get; private set; }
    public bool IsError => !IsSuccess;
    public T Data { get; private set; }
    public string Message { get; private set; }
    
    public bool IsValidationError() => Type == EnumRespType.ValidationError;
    public bool IsSystemError() => Type == EnumRespType.SystemError;
    public bool IsNotFound() => Type == EnumRespType.NotFound;
    
    public static Result<T> Success(T data, string message = "Success") => /* ... */
    public static Result<T> ValidationError(string message) => /* ... */
    public static Result<T> NotFoundError(string message) => /* ... */
}
```

Located in: `API/ZachHairStudio.Shared/Result.cs`

**Current API Controllers:** Currently use standard ASP.NET responses (e.g., `BadRequest(ModelState)`, `NotFound()`, `NoContent()`)
- See `API/ZachHairStudio.Api/Controllers/BookingsController.cs`
- Does NOT yet use Result<T> wrapper; uses direct HTTP status codes

**Validation:** Data annotations on DTOs

```csharp
public class BookingCreateDto
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = null!;
    
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = null!;
    
    [Phone, StringLength(30)]
    public string? Phone { get; set; }
}
```

## Logging

**Frontend:**
- No logging framework configured
- Uses `console.*` methods directly when needed

**Backend:**
- No logging framework configured in current code
- Standard ASP.NET Core dependency injection patterns available but not yet in use

## Comments

### TypeScript/React

**When to Comment:**
- JSDoc for exported functions and types
- Inline comments for non-obvious logic
- Comments above complex expressions

**JSDoc Pattern:**

```typescript
/**
 * Submits an appointment request to the API.
 * Throws an Error with a human-readable message when the request fails.
 */
export async function createBooking(
  data: BookingRequest
): Promise<BookingResponse>
```

**Inline Comments:**
```typescript
// Store the readable service label (with price) rather than the option key.
const serviceLabel =
  serviceOptions.find((o) => o.value === serviceValue)?.label ?? serviceValue;
```

### C# (.NET)

**Pattern:**
- Minimal comments; code is self-documenting via naming
- XML documentation (`///`) not yet in use; can add if needed
- Fluent method names for readability (e.g., `ToDto()`, `ToEntity()`)

## Function Design

### TypeScript/React

**Size:**
- Keep components under 200 lines of JSX
- Extract nested components for reusability (e.g., Logo, Field sub-components in Navbar and Contact)
- Use composition for complex UI logic

**Parameters:**
```typescript
function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (/* ... */);
}

export default function Contact() {
  // Parameters via props (functional component)
}
```

**Return Values:**
- Components return JSX.Element
- Event handlers typically return void
- Async functions return Promise<T>

### C# (.NET)

**Size:**
- Controller actions: 10-20 lines
- Service methods: keep focused on single responsibility
- Extension methods: typically 5-15 lines

**Parameters:**
```csharp
public async Task<ActionResult<BookingResponseDto>> GetBooking(int id)

public async Task<ActionResult<BookingResponseDto>> CreateBooking([FromBody] BookingCreateDto request)
```

**Return Values:**
- Controller actions return `ActionResult<T>` or `IActionResult`
- Service methods can return Result<T> (in shared Result.cs)
- Queries return IEnumerable<T> or IAsyncEnumerable<T>

## Module Design

### TypeScript/React

**Exports:**
- Named exports for utilities and types
- Default export for React components
- All types exported from `lib/data.ts` for reuse

```typescript
// lib/data.ts
export type NavLink = { label: string; href: string };
export const navLinks: NavLink[] = [ /* ... */ ];

// components/Navbar.tsx
export default function Navbar() { /* ... */ }
```

**Barrel Files:**
- Not currently in use (imports are direct)

### C# (.NET

**Exports:**
- Public classes exposed through namespaces
- File-scoped namespaces used for organization

```csharp
namespace ZachHairStudio.Shared.Features.Bookings;

public class Booking { /* ... */ }
public class BookingCreateDto { /* ... */ }
public static class BookingExtensions { /* ... */ }
```

**Extension Methods as Adapters:**
```csharp
public static class BookingExtensions
{
    public static BookingResponseDto ToDto(this Booking booking) => /* ... */
    public static Booking ToEntity(this BookingCreateDto createDto) => /* ... */
}
```

## Architecture Patterns

### Frontend Layer Pattern

```
Page Component (Next.js App Router)
├── Form State Management (useState hooks)
├── API Integration (lib/api.ts)
├── Sub-components (Logo, Field)
└── Tailwind Styling
```

### Backend Layer Pattern

```
Controller (HTTP Entry Point)
├── DbContext Injection
├── Query/Command Execution
├── DTO Mapping via Extensions
└── ActionResult Response
```

**Extension Pattern for Mapping:**
- Entity ↔ DTO conversion via static extension methods
- Keeps models and contracts separate
- See `BookingExtensions.cs` for example

---

*Convention analysis: 2026-07-07*
