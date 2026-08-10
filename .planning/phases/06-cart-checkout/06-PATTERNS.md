# Phase 6: Cart & Checkout - Pattern Map

**Mapped:** 2026-08-10
**Files analyzed:** 28
**Analogs found:** 26 / 28

> **Repo note:** CONTEXT/RESEARCH refer to `Features/Bookings/`. That feature lives as `Features/Appointments/` in this codebase. Use Appointments as the booking-create / Result-mapping / concurrency analog.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `API/.../Features/Carts/Cart.cs` (+ `CartItem.cs`) | model | CRUD | `Features/Appointments/Appointment.cs` (+ `AppointmentSlot.cs`) | role-match |
| `API/.../Features/Carts/Cart*Dto.cs` + validators | model | request-response | `AppointmentCreateDto.cs` + `AppointmentCreateDtoValidator.cs` | role-match |
| `API/.../Features/Carts/CartExtensions.cs` | utility | transform | `Features/Products/ProductExtensions.cs` | exact |
| `API/.../Features/Carts/CartsService.cs` | service | CRUD | `Features/Products/ProductsService.cs` | role-match |
| `API/.../Features/Orders/Order.cs` (+ `OrderItem.cs`, `OrderStatus.cs`) | model | CRUD | `Appointment.cs` + `AppointmentStatus.cs` | role-match |
| `API/.../Features/Orders/CheckoutRequestDto.cs` + validator | model | request-response | `AppointmentCreateDto.cs` + validator | exact |
| `API/.../Features/Orders/OrderExtensions.cs` | utility | transform | `AppointmentExtensions.cs` | exact |
| `API/.../Features/Orders/OrdersService.cs` | service | CRUD + request-response | `AppointmentsService.cs` + `AvailabilityService.cs` (tx/strategy) | exact |
| `API/.../Features/Payments/IPaymentProvider.cs` | service | request-response | `Features/Appointments/IEmailService.cs` | exact |
| `API/.../Features/Payments/StripePaymentProvider.cs` | service | request-response | `ResendEmailService.cs` | role-match |
| `API/.../Features/Payments/StripeOptions.cs` | config | — | `JwtOptions.cs` / `ResendOptions.cs` | exact |
| `API/.../Api/Controllers/CartsController.cs` | controller | request-response | `ProductsController.cs` | role-match |
| `API/.../Api/Controllers/OrdersController.cs` (or Checkout) | controller | request-response | `AppointmentsController.cs` | exact |
| `API/.../Api/Controllers/StripeWebhookController.cs` | controller | event-driven | *(none — raw body)*; DI/options from `Program.cs` Resend/Jwt | none |
| `API/.../Shared/Result.cs` (ConflictError overload) | utility | request-response | `Result.cs` existing `ConflictError` / `DuplicateRecordError` | exact |
| `API/.../Shared/Db/BookingDbContext.cs` | model | CRUD | same file — Product/AppointmentSlot config | exact |
| EF migration (Cart/Order tables) | migration | batch | prior migrations via `ef-migrations` skill | role-match |
| `API/.../Api/Program.cs` | config | — | same file — Resend/Jwt DI + ValidateOnStart | exact |
| `API/.../Features/Products/ProductsService.cs` (`GetRecommendedForCheckoutAsync`) | service | CRUD | `ServicesService.GetBySlugAsync` join | exact |
| `landing-page/lib/cart.ts` | utility | request-response | `landing-page/lib/appointments.ts` | exact |
| `landing-page/lib/cartSession.ts` | utility | — | *(localStorage session — no prior)*; header pattern from RESEARCH | none |
| `landing-page/app/cart/page.tsx` | route / component | request-response | `products/[slug]/page.tsx` layout + `AppointmentBookingForm` client UX | role-match |
| `landing-page/app/checkout/page.tsx` | route / component | request-response | `AppointmentBookingForm.tsx` submit/redirect UX | exact |
| `landing-page/app/checkout/success/page.tsx` | route / component | request-response | booking confirmation panel in `AppointmentBookingForm.tsx` | exact |
| `landing-page/app/checkout/cancel/page.tsx` | route / component | request-response | empty/error panels in booking form | role-match |
| `landing-page/components/Navbar.tsx` | component | — | same file (Cart link + badge) | exact |
| `landing-page/app/products/[slug]/page.tsx` | component | request-response | same file aside + booking CTA patterns | exact |
| `landing-page/components/icons.tsx` | component | — | same file (`CheckIcon` pattern) | exact |
| `API.Tests/.../CartsServiceTests.cs` | test | CRUD | `ProductsServiceTests.cs` | exact |
| `API.Tests/.../OrdersServiceTests.cs` | test | CRUD | `AppointmentsControllerTests` / Products service tests + fake `IEmailService` pattern | role-match |
| `API.Tests/.../StockConcurrencyTests.cs` | test | request-response | `ConcurrencyTests.cs` + `SqlServerWebApplicationFactory` | exact |
| `API.Tests/.../StripeWebhookTests.cs` | test | event-driven | *(partial)* — factory + controller tests; no signed-webhook precedent | partial |

## Pattern Assignments

### `Features/Orders/OrdersService.cs` (service, CRUD + checkout transaction)

**Analog A — create + Result mapping:** `Features/Appointments/AppointmentsService.cs`

**Imports / DI / Result factories** (lines 1–63):
```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
// ...
public async Task<Result<AppointmentResponseDto>> CreateAsync(AppointmentCreateDto request)
{
    var validation = await _validator.ValidateAsync(request);
    if (!validation.IsValid)
    {
        return Result<AppointmentResponseDto>.ValidationError(
            string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
    }
    // Find catalog row; NotFoundError if missing/inactive
```

**Post-commit external call + compensate mindset** (lines 136–150) — Stripe is stricter (must restore stock); email is best-effort only:
```csharp
try
{
    await _emailService.SendConfirmationAsync(appointment, service.ToDto(), stylist.Name);
}
catch
{
    // Swallow: booking already committed; email is best-effort.
}
return Result<AppointmentResponseDto>.Success(dto);
```

**409 path today** (lines 105–108, 153–154) — stock may use new `ConflictError(message)` overload or `DuplicateRecordError` → controller `Conflict`:
```csharp
return Result<AppointmentResponseDto>.DuplicateRecordError(
    "This slot was just booked by someone else. Please choose another time.");
```

**Analog B — CreateExecutionStrategy + explicit transaction:** `Features/Availability/AvailabilityService.cs` (lines 129–157)

```csharp
var strategy = _dbContext.Database.CreateExecutionStrategy();
return await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await _dbContext.Database.BeginTransactionAsync();
    // ... work that must be atomic with ExecuteUpdateAsync / multi-step writes ...
    await _dbContext.SaveChangesAsync();
    await transaction.CommitAsync();
    return Result<IReadOnlyList<StylistWorkingHours>>.Success(proposedHours);
});
```

**Apply to OrdersService:** wrap stock `ExecuteUpdateAsync` + Order/OrderItem insert in this strategy+transaction shell; call `IPaymentProvider` **after** commit; on Stripe failure restore stock with `Stock += qty` and mark Order Failed.

---

### `Features/Carts/CartsService.cs` (service, CRUD)

**Analog:** `Features/Products/ProductsService.cs`

**PLAT-01 ownership** (lines 6–21):
```csharp
// This class owns ALL Product BookingDbContext access (PLAT-01).
public class ProductsService
{
    private readonly BookingDbContext _dbContext;

    public ProductsService(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ProductResponseDto>> GetProductsAsync()
        => await _dbContext.Products
            .Where(product => product.IsActive)
            .OrderBy(product => product.Name)
            .Select(product => product.ToDto())
            .ToListAsync();
```

**Apply:** CartsService owns Cart/CartItem DbSets only; resolve catalog Price/Stock via Products queries for response enrichment — never store client prices on CartItem.

---

### `Features/Products/ProductsService.cs` — `GetRecommendedForCheckoutAsync` (service, CRUD)

**Analog:** `Features/Services/ServicesService.cs` recommended join (lines 48–61)

```csharp
var recommendedProducts = await _dbContext.Set<ServiceRecommendedProduct>()
    .Where(link => link.ServiceId == service.Id)
    .Join(
        _dbContext.Products.Where(product => product.IsActive),
        link => link.ProductId,
        product => product.Id,
        (link, product) => product)
    .Select(product => product.ToDto())
    .ToListAsync();
```

**Apply:** Join `ServiceRecommendedProduct` → active Products; exclude `cartProductIds`; `Take(4)`; return empty list (frontend omits chips).

---

### Entity / status / extensions

**Analog — parent + children:** `Appointment.cs` + nested `Slots` (lines 5–37)

```csharp
public class Appointment
{
    public int Id { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
    // ...
    public List<AppointmentSlot> Slots { get; set; } = new();
}
```

**Analog — enum:** `AppointmentStatus.cs`

```csharp
public enum AppointmentStatus
{
    Confirmed,
    Cancelled,
    Completed,
    NoShow
}
```

**Analog — mapper:** `AppointmentExtensions.cs` / `ProductExtensions.cs`

```csharp
public static ProductResponseDto ToDto(this Product product)
    => new ProductResponseDto
    {
        Id = product.Id,
        // ...
        Price = product.Price,
        Stock = product.Stock,
    };
```

**Catalog money fields:** `Product.cs` lines 24–26 (`Price`, `Stock`) — OrdersService recomputes from these only.

---

### Validators (Cart / Checkout DTOs)

**Analog:** `AppointmentCreateDtoValidator.cs` (guest create, no money fields from client beyond what server trusts)

```csharp
public class AppointmentCreateDtoValidator : AbstractValidator<AppointmentCreateDto>
{
    public AppointmentCreateDtoValidator()
    {
        RuleFor(x => x.ServiceId).GreaterThan(0);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        // ...
    }
}
```

**Also:** `ProductCreateDtoValidator.cs` for quantity/stock bounds style (`GreaterThanOrEqualTo(0)`).

**Apply:** Cart/checkout DTOs = `productId` + `quantity` (+ optional/required email on checkout) — **no price/total properties**. Auto-registered via existing `AddValidatorsFromAssemblyContaining` — no Program scan change.

---

### `IPaymentProvider` + `StripePaymentProvider` + `StripeOptions`

**Analog — seam:** `IEmailService.cs`

```csharp
public interface IEmailService
{
    Task SendConfirmationAsync(Appointment appointment, ServiceResponseDto service, string stylistName);
}
```

**Analog — options (non-secret vs secret split):** `ResendOptions.cs` + `JwtOptions.cs`

```csharp
// ResendOptions — non-secret FromEmail in appsettings
public class ResendOptions
{
    public string FromEmail { get; set; } = "bookings@media.zachhairstudio.com";
}

// JwtOptions — SigningKey from user-secrets/env only
public class JwtOptions
{
    public string SigningKey { get; set; } = string.Empty;
    // ...
}
```

**Analog — Program.cs registration** (lines 69–80, 89–97):

```csharp
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Resend"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ResendOptions>>().Value);
builder.Services.AddHttpClient<IEmailService, ResendEmailService>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["RESEND_API_KEY"]);
});

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(/* ... */)
    .ValidateOnStart();
```

**Apply:**
- `StripeOptions`: SuccessUrl, CancelUrl (+ WebhookSecret / SecretKey from user-secrets/env — never tracked).
- `AddScoped<IPaymentProvider, StripePaymentProvider>()`.
- Prefer `ValidateOnStart` for missing Stripe secrets (mirror Jwt).
- Tests: `ConfigureTestServices` replace with fake provider returning deterministic URL (same RemoveAll/Add pattern as DB in factories).

---

### Controllers

#### `OrdersController` / checkout POST

**Analog:** `AppointmentsController.cs` (lines 37–83)

```csharp
[HttpPost]
public async Task<ActionResult<AppointmentResponseDto>> CreateAppointment([FromBody] AppointmentCreateDto request)
{
    var validation = await _createValidator.ValidateAsync(request);
    if (!validation.IsValid)
    {
        foreach (var error in validation.Errors)
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return ValidationProblem(ModelState);
    }

    var result = await _appointmentsService.CreateAsync(request);

    if (result.IsValidationError()) { /* ValidationProblem */ }
    if (result.IsNotFound()) { return NotFound(new ProblemDetails { /* ... */ }); }
    if (result.IsDuplicateRecord())
    {
        return Conflict(new ProblemDetails
        {
            Title = "Slot taken",
            Detail = result.Message,
            Status = StatusCodes.Status409Conflict,
        });
    }
    return Created($"/api/appointments/{result.Data.Id}", result.Data);
}
```

**Conflict with Conflicts extension:** `AvailabilityController.ConflictProblem` (lines 185–195) if using `IsConflict()` + optional payload.

**Apply:** Map stock shortage → 409 ProblemDetails (message-only). No `[Authorize]` on cart/checkout (guest, SHOP-06) — same as public Products/Appointments create.

#### `CartsController`

**Analog:** `ProductsController.cs` — thin service delegation, `[Route("api/[controller]")]`, no DbContext injection.

#### `StripeWebhookController`

**No in-repo raw-body analog.** Follow RESEARCH Pattern 3:
- No `[FromBody]`; `await new StreamReader(Request.Body).ReadToEndAsync()`
- `EventUtility.ConstructEvent(json, Stripe-Signature, WebhookSecret)` → 400 on failure
- Call `OrdersService.MarkFulfilledAsync` only when `checkout.session.completed` && `payment_status == paid`
- Anonymous endpoint (guest webhook) — signature is the gate

---

### `Result.cs` — message-only ConflictError

**Current signature** (lines 60–71) — requires `IReadOnlyList<AvailabilityConflictDto>`:

```csharp
public static Result<T> ConflictError(
    string message,
    IReadOnlyList<AvailabilityConflictDto> conflicts,
    T? data = default) =>
    new Result<T>
    {
        IsSuccess = false,
        Type = EnumRespType.Conflict,
        Data = data,
        Message = message,
        Conflicts = conflicts,
    };
```

**Apply (Wave 0):** add overload `ConflictError(string message, T? data = default)` that sets `Conflicts = null` (or empty) and keeps `IsConflict() == true`. Alternative: map stock via `DuplicateRecordError` like AppointmentsController.

---

### `BookingDbContext.cs`

**Analog — unique index (unfiltered — DO NOT change AppointmentSlot):** lines 345–351

```csharp
entity.HasIndex(s => new { s.StylistId, s.SlotStart }).IsUnique();
```

**Analog — join entity seed:** `ServiceRecommendedProduct` UsingEntity (lines 253–268)

**Analog — money precision:** `Product` `HasPrecision(18, 2)` on Price

**Apply:**
- DbSets: `Carts`, `CartItems`, `Orders`, `OrderItems`
- Unique index on `Cart.SessionKey`
- **Filtered** unique index on `Order.StripeSessionId` WHERE NOT NULL (contrast: AppointmentSlot must stay unfiltered)
- Cascade Cart→CartItem / Order→OrderItem like Appointment→Slots

---

### Frontend: `lib/cart.ts`

**Analog — throw-on-error mutating client:** `lib/appointments.ts` (`AppointmentApiError`, `createAppointment`, lines 73–212)

```typescript
export class AppointmentApiError extends Error {
  readonly status: number | null;
  get isConflict(): boolean { return this.status === 409; }
  get isValidation(): boolean { return this.status === 400; }
  get isNetwork(): boolean { return this.status === null; }
}

export async function createAppointment(request: AppointmentCreateRequest): Promise<AppointmentResponse> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/appointments`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });
  } catch {
    throw new AppointmentApiError("We couldn't reach the booking system...", null);
  }
  if (!response.ok) {
    throw new AppointmentApiError(await extractErrorMessage(response), response.status);
  }
  return AppointmentResponseSchema.parse(await response.json());
}
```

**Analog — Zod catalog schemas:** `lib/products.ts` (ProductSchema, API_BASE_URL)

**Apply:** Cart DTOs with Zod; send `X-Cart-Session-Id` on every call; `cache: "no-store"` for cart reads (like slots); throw typed errors for cart load / checkout (do not swallow to `[]` like `fetchProducts`).

---

### Frontend pages / UX

**Layout (two-column cart):** `app/products/[slug]/page.tsx` lines 40–62

```tsx
<div className="mt-8 grid lg:grid-cols-[1fr_320px] gap-8 items-start">
  <article className="bg-charcoal border border-white/5 rounded-3xl p-8 md:p-10">
  {/* ... */}
  </article>
  <aside className="bg-charcoal border border-gold/20 rounded-3xl p-7 lg:sticky lg:top-28">
```

**Client form / CTA / errors / Confirming…:** `AppointmentBookingForm.tsx`

```tsx
const inputClass =
  "w-full bg-charcoal-light border border-white/10 hover:border-gold/30 focus:border-gold rounded-xl px-4 py-3 text-white placeholder-gray-600 text-sm outline-none transition-colors";

// rose alert
className="text-sm text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-3"

// submit label swap
<span>{submitting ? "Confirming…" : "Confirm Appointment"}</span>
```

**Success ring:** same file confirmation panel (lines 279–330)

```tsx
<div className="w-16 h-16 bg-gold/20 rounded-full flex items-center justify-center mx-auto mb-4">
  <CheckIcon className="w-8 h-8 text-gold" />
</div>
```

**Suggestion chips anatomy:** stylist chips `rounded-full border px-5 py-2.5` + selected `border-gold text-gold bg-gold/10` + `CheckIcon`.

**Navbar Cart entry:** modify `Navbar.tsx` — place Cart link left of Book Now pill; reuse gold CTA classes.

**Icons:** extend `icons.tsx` with `CartIcon` / `PlusIcon` / `MinusIcon` matching `CheckIcon` SVG props pattern (lines 44–49).

**Price formatting:** reuse `Intl.NumberFormat("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 })` from products pages.

---

### Tests

#### `StockConcurrencyTests.cs`

**Analog:** `ConcurrencyTests.cs` + `SqlServerWebApplicationFactory.cs`

```csharp
public class ConcurrencyTests : IClassFixture<SqlServerWebApplicationFactory>
{
    [Fact]
    public async Task TwoSimultaneousRequestsForSameSlot_ExactlyOne201AndOne409()
    {
        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();
        var task1 = client1.PostAsJsonAsync("/api/appointments", request);
        var task2 = client2.PostAsJsonAsync("/api/appointments", request);
        var responses = await Task.WhenAll(task1, task2);
        var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToList();
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, statusCodes);
        // Assert DB final state
    }
}
```

**Factory:** `SqlServerWebApplicationFactory` — InMemory **forbidden** for SHOP-04 (`ExecuteUpdateAsync` relational-only). Note: factory hardcodes LocalDB; Linux may need connection override (RESEARCH Environment Availability).

#### Service unit tests

**Analog:** `ProductsServiceTests.cs` — InMemory `BookingDbContext`, assert `Result` flags (`IsNotFound`, Success data).

#### Checkout with fake payment provider

**Analog:** `CustomWebApplicationFactory.ConfigureTestServices` RemoveAll/Add pattern — register fake `IPaymentProvider` instead of Stripe.

## Shared Patterns

### Service layer owns DbContext (PLAT-01)
**Source:** `ProductsService.cs`, `AppointmentsService.cs`  
**Apply to:** CartsService, OrdersService, Payments (no DbContext in Stripe provider if possible — OrdersService orchestrates)  
Controllers inject services only — never `BookingDbContext`.

### Result → HTTP mapping
**Source:** `AppointmentsController.cs`, `AvailabilityController.cs`  
**Apply to:** Carts/Orders controllers  
| Result | HTTP |
|--------|------|
| Success create | 201 Created |
| ValidationError | 400 ValidationProblem |
| NotFound | 404 ProblemDetails |
| DuplicateRecord / Conflict | 409 Conflict ProblemDetails |

### FluentValidation auto-scan
**Source:** `Program.cs` line 50  
```csharp
builder.Services.AddValidatorsFromAssemblyContaining<ServiceCreateDtoValidator>();
```  
**Apply to:** New validators in Shared — no Program change.

### Secrets / options
**Source:** `Program.cs` Resend + Jwt  
**Apply to:** `Stripe:SecretKey`, `Stripe:WebhookSecret` via user-secrets/env; ValidateOnStart recommended; never appsettings tracked secrets.

### CORS / session key
**Source:** `Program.cs` `AllowAnyOrigin()`  
**Apply to:** Cart session as `X-Cart-Session-Id` header + `localStorage` — **not** credentialed cookies.

### Concurrency proof on real SQL
**Source:** `ConcurrencyTests` + `SqlServerWebApplicationFactory`  
**Apply to:** `StockConcurrencyTests` only.

### Guest / anonymous public APIs
**Source:** `ProductsController`, `AppointmentsController` Create (no `[Authorize]`)  
**Apply to:** Cart, checkout, webhook (webhook gated by Stripe signature).

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `StripeWebhookController.cs` | controller | event-driven | No raw-body / HMAC webhook endpoint in repo — use RESEARCH Pattern 3 + Stripe.net `EventUtility.ConstructEvent` |
| `landing-page/lib/cartSession.ts` | utility | — | No prior localStorage session helper; implement UUID + `X-Cart-Session-Id` per RESEARCH Pattern 4 |

## Metadata

**Analog search scope:**  
`API/ZachHairStudio.Shared/Features/{Appointments,Products,Services,Availability,Identity}/`,  
`API/ZachHairStudio.Api/{Controllers,Program.cs}`,  
`API/ZachHairStudio.Api.Tests/`,  
`landing-page/{lib,app,components}/`

**Files scanned:** ~70 feature/controller/test/frontend files (targeted reads on 3–5 primary analogs)  
**Pattern extraction date:** 2026-08-10  
**Primary analogs used:** AppointmentsService, AvailabilityService, AppointmentsController, ConcurrencyTests, ProductsService, ServicesService (recommended join), IEmailService/Resend/Program DI, lib/appointments.ts, AppointmentBookingForm, products/[slug]
