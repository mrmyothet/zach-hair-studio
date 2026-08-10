# Phase 7: Accounts & Retention - Pattern Map

**Mapped:** 2026-08-10
**Files analyzed:** 24
**Analogs found:** 24 / 24

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Features/Identity/StaffRoles.cs` (+ `Client`) | config | — | same file | exact |
| `Features/Identity/IdentitySeeder.cs` | service | batch | same file | exact |
| `Features/Identity/RegisterRequestDto(+Validator).cs` | model | request-response | `LoginRequestDto(+Validator).cs` | exact |
| `Controllers/AuthController.cs` (+ Register) | controller | request-response | `AuthController.cs` + `StaffUsersController.cs` | exact |
| `Controllers/AccountController.cs` | controller | CRUD / request-response | `StaffUsersController.cs` + `ScheduleController.cs` | role-match |
| `Features/Loyalty/*` (entity + service) | model / service | CRUD | `Features/Orders/Order.cs` + `OrdersService.cs` | role-match |
| `Db/BookingDbContext.cs` (+ DbSet / FKs) | model | — | same file (`DbSet` + Identity base) | exact |
| `Features/Appointments/Appointment.cs` (+ `ClientUserId`) | model | — | `Order.ClientId` | exact |
| `Features/Appointments/AppointmentsService.cs` (cancel/reschedule/earn) | service | CRUD / transform | same file (`UpdateStatusAsync` / `CreateAsync`) | exact |
| `Features/Orders/OrdersService.cs` (claim + loyalty $) | service | CRUD | same file (`CreateCheckoutAsync`) | exact |
| EF migration (Client role schema / ledger / FK) | migration | — | existing Shared `Migrations/` via ef-migrations skill | exact |
| `landing-page/lib/auth.ts` | utility | request-response | `dashboard/lib/auth.ts` | exact |
| `landing-page/lib/account.ts` (history/cancel fetch) | utility | CRUD | `landing-page/lib/cart.ts` | role-match |
| `landing-page/app/account/login/page.tsx` | component | request-response | `dashboard/app/login/page.tsx` + `CheckoutForm` styling | role-match |
| `landing-page/app/account/register/page.tsx` | component | request-response | login page + `CheckoutForm` forms | role-match |
| `landing-page/app/account/{page,bookings,orders}*` | component / route | CRUD | `CartPageClient.tsx` + `CheckoutForm` shell | role-match |
| `landing-page/components/Navbar.tsx` | component | request-response | same file (Cart link) | exact |
| `landing-page/components/CheckoutForm.tsx` (+ redeem) | component | request-response | same file (totals + CTA) | exact |
| `Api.Tests/.../ClientAuthTests.cs` | test | request-response | `AuthGateTests.cs` | exact |
| `Api.Tests/.../AccountBookingsTests.cs` | test | CRUD | `StatusUpdateTests.cs` + AuthGate seed | exact |
| `Api.Tests/.../AccountOrdersTests.cs` | test | CRUD | AuthGate + order checkout tests | role-match |
| `Api.Tests/.../ClientRescheduleTests.cs` | test | CRUD | `StatusUpdateTests.cs` + `ConcurrencyTests.cs` | exact |
| `Api.Tests/.../LoyaltyTests.cs` | test | CRUD | `StatusUpdateTests` + `StockConcurrencyTests` | role-match |
| `Api.Tests/.../IdentitySeederTests.cs` (extend) | test | batch | same file | exact |

## Pattern Assignments

### `StaffRoles.cs` + `IdentitySeeder.cs` (config / batch)

**Analog:** same files

**Role constant** (`StaffRoles.cs` 4–8):
```csharp
public static class StaffRoles
{
    public const string Owner = "Owner";
    public const string Staff = "Staff";
    // Add: public const string Client = "Client";
}
```

**Seed loop** (`IdentitySeeder.cs` 19–25) — extend array to include `StaffRoles.Client`; do **not** seed a Client user (self-register only):
```csharp
foreach (var role in new[] { StaffRoles.Owner, StaffRoles.Staff })
{
    if (!await roleManager.RoleExistsAsync(role))
        await roleManager.CreateAsync(new IdentityRole<int>(role));
}
```

---

### `RegisterRequestDto` + validator (model, request-response)

**Analog:** `LoginRequestDto.cs` / `LoginRequestDtoValidator.cs`

**DTO + FluentValidation** (lines 7–16 validator):
```csharp
RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
RuleFor(x => x.Password).NotEmpty();
// Register: add ConfirmPassword Equal(Password); DisplayName optional/discretion
```

---

### `AuthController.cs` (+ Register) (controller, request-response)

**Analog:** `AuthController.Login` + `StaffUsersController.Create` (UserManager create + role)

**Login already returns Role** (`AuthController.cs` 55–64) — works for Client after role seed:
```csharp
var roles = await _userManager.GetRolesAsync(user);
var (token, expiresAt) = _tokenService.CreateToken(user, roles);
return Ok(new LoginResponseDto {
    Token = token, ExpiresAt = expiresAt,
    DisplayName = user.DisplayName, Role = roles.FirstOrDefault() ?? string.Empty,
});
```

**Uniform 401** (lines 43–52) — keep for login; register uses StaffUsers create pattern:
```csharp
var createResult = await _userManager.CreateAsync(user, request.Password);
if (!createResult.Succeeded) { /* ModelState + ValidationProblem */ }
var roleResult = await _userManager.AddToRoleAsync(user, StaffRoles.Client);
// On role failure: DeleteAsync(user) then Problem 500 (StaffUsersController 62–71)
```

**JWT claims for ownership** (`JwtTokenService.cs` 27–33):
```csharp
new(ClaimTypes.NameIdentifier, user.Id.ToString()),
// + ClaimTypes.Role per role — AccountController filters by NameIdentifier
```

---

### `AccountController.cs` (controller, CRUD)

**Analog:** `StaffUsersController` (role gate) + `ScheduleController` (claims + Result → ProblemDetails)

**Auth gate** (`StaffUsersController.cs` 9–12):
```csharp
[ApiController]
[Route("api/account")] // or api/me
[Authorize(Roles = StaffRoles.Client)]
public class AccountController : ControllerBase { ... }
```

**Resolve owner from claims — never body** (`ScheduleController.cs` 91–95):
```csharp
var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
// History: WHERE ClientUserId == userId / ClientId == userId
// Detail miss / cross-client: NotFound ProblemDetails (recommend 404 per RESEARCH A1)
```

**Result mapping** (`AppointmentsController.cs` 53–82 / `OrdersController.cs` 60–84):
- Validation → `ValidationProblem`
- NotFound → `NotFound(ProblemDetails)`
- Duplicate/Conflict (reschedule slot) → `Conflict` 409

---

### `LoyaltyLedger` + service (model/service, CRUD)

**Analog:** `Order.cs` (nullable ClientId) + append-only style; money authority from `OrdersService`

**Entity shape** (mirror Order FK pattern — `Order.cs` 14–15):
```csharp
public int? ClientId { get; set; } // Order already has this for claim
// LoyaltyLedger: ClientUserId (int), Delta (int), Reason (string/enum), AppointmentId?, CreatedAt
```

**Earn hook site** — after successful `UpdateStatusAsync` when `newStatus == Completed` (`AppointmentsService.cs` 278–289):
```csharp
if (newStatus is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
    _dbContext.AppointmentSlots.RemoveRange(appointment.Slots);
appointment.Status = newStatus;
// After Completed commit: append +1 ledger if ClientUserId set AND no prior Earn for AppointmentId
```

**Redeem site** — after catalog recompute (`OrdersService.cs` 92–104):
```csharp
order.TotalAmount = order.Items.Sum(item => item.LineTotal);
// THEN: if redeem points requested + authenticated ClientId → server $ = floor(pts/10)*5;
// cap at TotalAmount; append negative ledger; subtract; NEVER trust client discountAmount
_dbContext.Orders.Add(order);
```

**Txn pattern for redeem/stock** (`OrdersService.cs` 56–59):
```csharp
var strategy = _dbContext.Database.CreateExecutionStrategy();
return await strategy.ExecuteAsync(async () => {
    await using var transaction = await _dbContext.Database.BeginTransactionAsync(...);
    // ...
});
```

---

### `AppointmentsService` cancel / reschedule (service, CRUD)

**Analog:** same service — `UpdateStatusAsync` + `CreateAsync`

**Allowed cancel path** (lines 34–39, 250–289):
```csharp
[AppointmentStatus.Confirmed] = { Completed, Cancelled, NoShow };
// Client cancel: ownership check → UpdateStatusAsync(..., Cancelled, ...) OR shared internal transition
// Slot release already in UpdateStatusAsync for Cancelled/NoShow
```

**Reschedule = book-new then cancel-old** (D-10) — reuse `CreateAsync` slot insert + unique-index 409 (`CreateAsync` 96–108, 118+), then cancel old in **same** `CreateExecutionStrategy` + transaction (Orders pattern). Do **not** cancel-first.

**Create guest email still set** (`BuildAppointment` 295–308) — claim sets `ClientUserId` later without clearing Email.

---

### `OrdersService` claim + checkout (service, CRUD)

**Analog:** `CreateCheckoutAsync` (lines 31–132)

- Guest path keeps `ClientId = null` (line 63).
- Authenticated checkout: set `ClientId` from JWT `NameIdentifier` (ignore body owner ids).
- Claim-on-register: `UPDATE Orders/Appointments SET ClientId/ClientUserId WHERE Email == normalized AND Client* IS NULL` only after confirm (D-04).

---

### `landing-page/lib/auth.ts` (utility, request-response)

**Analog:** `dashboard/lib/auth.ts` (full file)

**Copy with client key** (lines 9–16, 96–152):
```typescript
const STORAGE_KEY = "zhs.client.auth"; // NOT zhs.staff.auth
export type AuthSession = { token; expiresAt; displayName; role };
// getSession / setSession / clearSession / attachToken / requireAuth
// requireAuth → /account/login (not /login)
// handleUnauthorized clears + redirect /account/login
```

Hand-written fetch (no openapi-fetch yet): attach `Authorization: Bearer` like cart session header pattern in `lib/cart.ts`.

---

### `landing-page/lib/account.ts` (utility, CRUD)

**Analog:** `landing-page/lib/cart.ts`

**Zod + ApiError + ProblemDetails extract** (`cart.ts` 16–67, 69–80):
```typescript
export class CartApiError extends Error { /* status, isConflict, isValidation, isNetwork */ }
// Mirror as AccountApiError; parse ProblemDetails errors/detail/title
const API_BASE_URL = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5236").replace(/\/$/, "");
```

On 401: clear client session + redirect `/account/login`.

---

### Account UI pages (component / route)

**Analog styling:** `CheckoutForm.tsx` + `CartPageClient.tsx`  
**Analog auth UX:** `dashboard/app/login/page.tsx`

**Shell / forms** (`CheckoutForm.tsx` 15–22, 90–98):
```tsx
const priceFormatter = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 });
const inputClass = "w-full bg-charcoal-light border border-white/10 ... focus:border-gold rounded-xl ...";
<main className="min-h-screen bg-charcoal-light pt-32">
  <div className="max-w-5xl mx-auto px-6"> {/* auth forms: max-w-md */}
    <SectionHeading eyebrow="..." title="" highlight="..." subtitle="..." />
```

**Error banner** (CheckoutForm ~104):
```tsx
className="... text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"
```

**History row card** (`CartPageClient.tsx` ~80):
```tsx
<article className="bg-charcoal-light border border-white/5 rounded-2xl p-5 md:p-7 ...">
```

**Login submit → setSession** (dashboard login 59–72 pattern): POST `/api/auth/login` (or register), validate token/expiresAt/displayName/role, `setSession`, navigate `/account`. Prefer role === `Client` for account routes.

**Icons:** `PersonIcon` already in `landing-page/components/icons.tsx` (lines 19–28).

---

### `Navbar.tsx` (component)

**Analog:** same file — Cart link (lines 83–93, 131–142)

Insert **Log In** or **Account** immediately left of Cart (desktop + mobile above Cart):
```tsx
<Link href={session ? "/account" : "/account/login"}
  className="relative inline-flex items-center gap-2 text-gray-300 hover:text-gold ... uppercase">
  <PersonIcon className="w-5 h-5 text-gold" />
  {session ? "Account" : "Log In"}
</Link>
```
Session from `getSession()` in `"use client"` Navbar; no staff key.

---

### `CheckoutForm.tsx` loyalty redeem (component)

**Analog:** same file — Order Summary + Continue CTA

When `getSession()` present: loyalty block above payment CTA; send **points to redeem** only (not discount $). Totals from server response after Apply Points. Guest path unchanged (omit block). Reuse `priceFormatter`, rose error copy for redeem failure, gold Apply Points button classes matching existing CTA.

---

### Api.Tests (test)

**Analog:** `AuthGateTests.cs` + `SqlServerWebApplicationFactory` + `StatusUpdateTests.cs`

**Factory + Jwt inject** (`AuthGateTests.cs` 23–47):
```csharp
public class ClientAuthTests : IClassFixture<SqlServerWebApplicationFactory>
{
    // WithWebHostBuilder → AddInMemoryCollection Jwt:SigningKey / Issuer / Audience
}
```

**Seed user + login** (lines 49–84):
```csharp
await userManager.CreateAsync(user, TestPassword);
await userManager.AddToRoleAsync(user, StaffRoles.Client); // or Staff for negative cases
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
```

**Seeder assert extend** (`IdentitySeederTests.cs` 38–39): add `Assert.True(await roleManager.RoleExistsAsync(StaffRoles.Client));`

**Never InMemory for Identity/ownership** — always `SqlServerWebApplicationFactory` (factory comment lines 14–17).

## Shared Patterns

### Authentication (Identity + JWT)
**Source:** `JwtTokenService.cs`, `AuthController.cs`, `StaffUsersController.cs`  
**Apply to:** Auth register/login, AccountController, checkout redeem  
- One `ApplicationUser` store; roles via `StaffRoles` (+ Client).  
- `[Authorize(Roles = StaffRoles.Client)]` on account APIs; staff keep `[Authorize]` / Owner gates.  
- Ownership = `ClaimTypes.NameIdentifier` only.

### Error Handling (Result → ProblemDetails)
**Source:** `AppointmentsController.cs`, `OrdersController.cs`, `ScheduleController.cs`  
**Apply to:** Account + Auth register + loyalty/checkout  
- FluentValidation → `ValidationProblem`  
- NotFound / Conflict / Unauthorized ProblemDetails with Title + Detail + Status

### Validation
**Source:** `LoginRequestDtoValidator`, controller-level `IValidator<T>`  
**Apply to:** Register, reschedule DTO, checkout redeem points

### Price / loyalty authority
**Source:** `OrdersService.CreateCheckoutAsync` catalog recompute  
**Apply to:** Loyalty redeem — server computes `$`; ignore client discount fields

### Frontend session
**Source:** `dashboard/lib/auth.ts`  
**Apply to:** `landing-page/lib/auth.ts` with `zhs.client.auth`; 401 clears + `/account/login`

### UI anatomy
**Source:** `CheckoutForm`, `CartPageClient`, `Navbar`, `SectionHeading`, `icons`  
**Apply to:** `/account/*`, navbar Account entry, checkout redeem — charcoal/gold tokens, rose errors, `pt-32`, no shadcn

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| — | — | — | None blocking. Loyalty is new entity but CRUD/service + Order/Appointment hooks cover it. Claim-by-email UX is UI-only; API claim is a filtered UPDATE patterned on existing Email columns. |

## Metadata

**Analog search scope:** `API/ZachHairStudio.Shared/Features/{Identity,Appointments,Orders}`, `API/ZachHairStudio.Api/Controllers`, `API/ZachHairStudio.Api.Tests`, `dashboard/lib/auth.ts`, `landing-page/{components,lib,app}`  
**Files scanned:** ~35 primary + supporting greps  
**Pattern extraction date:** 2026-08-10
