# Testing Patterns

**Analysis Date:** 2026-07-07

## Test Framework

### Frontend

**Runner:**
- Not configured yet
- No Jest, Vitest, or other test runner installed

**Test Dependencies:**
- `playwright` (v1.61.1) is available for E2E testing but not integrated into test pipeline

**Next.js Linting:**
- `npm run lint` invokes Next.js built-in linter
- Performs static analysis and type checking via TypeScript
- No unit/integration test framework configured

### Backend

**Status:**
- No test framework configured (xUnit, NUnit, or MSTest)
- No test projects in solution

**Available Test Tools:**
- .NET 10 SDK supports xUnit, NUnit, MSTest test discovery
- EF Core provides in-memory database for testing
- No test infrastructure currently implemented

## Test File Organization

### Current State

**No test files exist in the codebase.**

Frontend:
- No `*.test.tsx`, `*.spec.tsx`, or `__tests__` directories
- No jest.config.js or vitest.config.ts

Backend:
- No test project files (*.Tests.csproj)
- No xUnit, NUnit, or MSTest reference in dependencies

### Recommended Future Organization

**Frontend Pattern (if added):**
```
landing-page/
├── app/
│   └── __tests__/
│       └── page.test.tsx
├── components/
│   ├── Contact.tsx
│   └── __tests__/
│       └── Contact.test.tsx
└── lib/
    ├── api.ts
    └── __tests__/
        └── api.test.ts
```

**Backend Pattern (if added):**
```
API/
├── ZachHairStudio.Api/
├── ZachHairStudio.Shared/
└── ZachHairStudio.Api.Tests/
    ├── Controllers/
    │   └── BookingsControllerTests.cs
    └── Features/
        └── Bookings/
            └── BookingExtensionsTests.cs
```

## Test Structure

### Not Yet Established

**Patterns to Consider (when implementing):**

**Frontend (Next.js + React Testing Library):**
```typescript
import { render, screen, fireEvent } from "@testing-library/react";
import Contact from "@/components/Contact";

describe("Contact Form", () => {
  it("should submit form data to API", async () => {
    render(<Contact />);
    
    fireEvent.change(screen.getByLabelText("First Name"), {
      target: { value: "John" },
    });
    
    fireEvent.click(screen.getByText("Request Appointment"));
    
    expect(await screen.findByText("You're All Set!")).toBeInTheDocument();
  });
});
```

**Backend (xUnit Pattern):**
```csharp
using Xunit;
using ZachHairStudio.Api.Controllers;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Bookings;

public class BookingsControllerTests
{
    private readonly BookingDbContext _context;
    private readonly BookingsController _controller;

    public BookingsControllerTests()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;
        
        _context = new BookingDbContext(options);
        _controller = new BookingsController(_context);
    }

    [Fact]
    public async Task CreateBooking_ValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = new BookingCreateDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Service = "Precision Cut – $35",
            PreferredDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = await _controller.CreateBooking(request);

        // Assert
        Assert.NotNull(result);
    }
}
```

## Mocking

### Not Yet Established

**Framework:** None configured

**When to Mock (future guidelines):**

**Frontend:**
```typescript
// Mock API calls in tests
jest.mock("@/lib/api", () => ({
  createBooking: jest.fn()
    .mockResolvedValue({ id: 1, status: "Pending" }),
}));

// Mock fetch for API layer testing
global.fetch = jest.fn(() =>
  Promise.resolve({
    ok: true,
    json: () => Promise.resolve({ id: 1 }),
  })
);
```

**Backend:**
```csharp
// Use in-memory DbContext
var options = new DbContextOptionsBuilder<BookingDbContext>()
    .UseInMemoryDatabase("TestDb")
    .Options;
var dbContext = new BookingDbContext(options);

// Mock external services (when added)
// Dependency injection allows passing mock implementations
```

**What to Mock:**
- External API calls (e.g., `fetch` in frontend)
- Database operations (use in-memory DB for tests)
- Time-dependent operations (consider using time abstractions)

**What NOT to Mock:**
- Business logic (test actual behavior)
- DTOs and data classes (test actual structure)
- Extension methods (test actual mapping logic)
- Core validation logic (ensure it runs)

## Fixtures and Factories

### Not Yet Established

**When implemented, follow patterns:**

**Frontend Test Fixtures (TypeScript):**
```typescript
// lib/__tests__/fixtures.ts
export const createMockBooking = (overrides?: Partial<BookingResponse>): BookingResponse => ({
  id: 1,
  firstName: "John",
  lastName: "Doe",
  email: "john@example.com",
  service: "Precision Cut – $35",
  preferredDate: "2026-07-15",
  status: "Pending",
  createdAt: new Date().toISOString(),
  customerName: "John Doe",
  ...overrides,
});

export const createMockServiceOptions = () => [
  { value: "cut", label: "Precision Cut – $35" },
  { value: "color", label: "Color & Highlights – $80" },
];
```

**Backend Test Fixtures (C#):**
```csharp
// ZachHairStudio.Api.Tests/Fixtures/BookingFixtures.cs
public static class BookingFixtures
{
    public static Booking CreateValidBooking(int id = 1)
    {
        return new Booking
        {
            Id = id,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "09-777190314",
            Service = "Precision Cut – $35",
            PreferredDate = DateTime.UtcNow.AddDays(1),
            Message = "Test booking",
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static BookingCreateDto CreateValidCreateDto()
    {
        return new BookingCreateDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "09-777190314",
            Service = "Precision Cut – $35",
            PreferredDate = DateTime.UtcNow.AddDays(1),
        };
    }
}
```

**Location (when added):**
- Frontend: `landing-page/__tests__/fixtures/` or co-located in test file
- Backend: `ZachHairStudio.Api.Tests/Fixtures/` or `ZachHairStudio.Api.Tests/Builders/`

## Coverage

### Current State

**Requirements:** None enforced

**Tools Available:**
- Frontend: Can use `nyc` (Istanbul) via Jest/Vitest
- Backend: Can use OpenCover or dotCover via .NET tooling

### When Implemented

**Frontend Coverage View:**
```bash
npm run test:coverage
# or
npx vitest run --coverage
```

**Backend Coverage View:**
```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

## Test Types

### Unit Tests

**Definition:** Test individual functions/methods in isolation

**Frontend Scope (when added):**
- API client functions (e.g., `createBooking` in `lib/api.ts`)
- Utility functions
- Individual component rendering (minimal props)
- Error handling in API functions

**Example:**
```typescript
describe("createBooking", () => {
  it("should throw error when API is unreachable", async () => {
    global.fetch = jest.fn().mockRejectedValue(new Error("Network error"));
    
    await expect(createBooking(/* ... */)).rejects.toThrow(
      "We couldn't reach the booking service"
    );
  });
});
```

**Backend Scope:**
- Extension methods (e.g., `ToDto()`, `ToEntity()` in `BookingExtensions.cs`)
- Data validation (attribute validation)
- Enum conversions
- DTO instantiation

### Integration Tests

**Definition:** Test multiple components working together

**Frontend Scope (when added):**
- Contact form submission end-to-end (user interaction → API call)
- Navigation and routing
- Form validation and error display

**Backend Scope:**
- Controller action → DbContext → Database → Response DTO flow
- EF Core queries with actual in-memory DB
- ModelState validation through controller pipeline

**Example Pattern (Backend):**
```csharp
[Fact]
public async Task CreateBooking_WithValidData_SavesToDatabaseAndReturnsDto()
{
    // Arrange
    var createDto = BookingFixtures.CreateValidCreateDto();
    
    // Act
    var result = await _controller.CreateBooking(createDto);
    
    // Assert
    var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
    var returnedDto = Assert.IsType<BookingResponseDto>(createdResult.Value);
    Assert.Equal(createDto.FirstName, returnedDto.FirstName);
    
    // Verify persisted to DB
    var savedBooking = await _context.Bookings.FirstAsync();
    Assert.Equal(createDto.Email, savedBooking.Email);
}
```

### E2E Tests

**Framework:** Playwright (v1.61.1 available)

**Status:** Not yet integrated

**When implemented, example:**
```typescript
import { test, expect } from "@playwright/test";

test("user can book an appointment", async ({ page }) => {
  await page.goto("http://localhost:3000");
  
  await page.fill("[name='firstName']", "John");
  await page.fill("[name='lastName']", "Doe");
  await page.fill("[name='email']", "john@example.com");
  await page.selectOption("[name='service']", "cut");
  await page.fill("[name='preferredDate']", "2026-07-15");
  
  await page.click("button:has-text('Request Appointment')");
  
  await expect(page.locator("text=You're All Set!")).toBeVisible();
});
```

## Common Patterns

### Async Testing

**Frontend (when implemented):**
```typescript
it("should load and display bookings", async () => {
  jest.mock("@/lib/api", () => ({
    getBookings: jest.fn().mockResolvedValue([
      { id: 1, firstName: "John", status: "Pending" },
    ]),
  }));

  render(<BookingsList />);
  
  // Wait for async data fetch
  await screen.findByText("John");
  
  expect(screen.getByText("John")).toBeInTheDocument();
});
```

**Backend:**
```csharp
[Fact]
public async Task GetBookings_ReturnsOrderedByCreatedAtDescending()
{
    // Arrange
    var bookings = new[] {
        BookingFixtures.CreateValidBooking(1) { CreatedAt = DateTime.UtcNow.AddDays(-2) },
        BookingFixtures.CreateValidBooking(2) { CreatedAt = DateTime.UtcNow },
    };
    _context.Bookings.AddRange(bookings);
    await _context.SaveChangesAsync();

    // Act
    var result = await _controller.GetBookings();

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var returnedDtos = Assert.IsAssignableFrom<IEnumerable<BookingResponseDto>>(okResult.Value);
    var dtoList = returnedDtos.ToList();
    
    Assert.Equal(2, bookings.Id);  // Most recent first
    Assert.Equal(1, dtoList[1].Id);  // Oldest second
}
```

### Error Testing

**Frontend Pattern (when implemented):**
```typescript
it("should display error message when API fails", async () => {
  global.fetch = jest.fn().mockResolvedValue({
    ok: false,
    status: 400,
    json: () => Promise.resolve({
      errors: { FirstName: ["First name is required"] }
    }),
  });

  render(<Contact />);
  fireEvent.click(screen.getByText("Request Appointment"));

  await expect(screen.findByRole("alert")).toBeInTheDocument();
  expect(screen.getByRole("alert")).toHaveTextContent("First name is required");
});
```

**Backend Pattern (xUnit):**
```csharp
[Fact]
public async Task CreateBooking_WithMissingFirstName_ReturnsBadRequest()
{
    // Arrange
    var invalidDto = BookingFixtures.CreateValidCreateDto();
    invalidDto.FirstName = "";  // Invalid

    // Act
    var result = await _controller.CreateBooking(invalidDto);

    // Assert
    var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
    var modelState = Assert.IsType<SerializableError>(badRequestResult.Value);
    Assert.True(modelState.ContainsKey("FirstName"));
}
```

---

*Testing analysis: 2026-07-07*
