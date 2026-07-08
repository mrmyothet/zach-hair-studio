# Architecture Research

**Domain:** Salon services + light-commerce booking platform (appointment scheduling, staff operations, product add-on commerce) on an existing .NET/EF Core + dual-Next.js codebase
**Researched:** 2026-07-07
**Confidence:** MEDIUM (official ASP.NET Core / EF Core guidance is authoritative for the layering and auth patterns; domain-model specifics for salon/e-commerce are synthesized from general web sources — LOW confidence on those details, treat as directional)

## Standard Architecture

### System Overview

This extends the existing layered N-tier architecture (`.planning/codebase/ARCHITECTURE.md`) — it does not replace it. The shape below is the same three-layer shape already in the repo (Frontend → API → Shared → Data), with one addition: a **Services layer** inserted between Controllers and the DbContext, which the codebase currently lacks (`CONCERNS.md` flags this; the existing ARCHITECTURE.md anti-pattern section calls it out explicitly).

```
┌──────────────────────────────────────────────────────────────────────┐
│                         Web Layer (Frontend)                          │
├───────────────────────────┬───────────────────────────────────────────┤
│   landing-page/ (public)  │   dashboard/ (staff, separate Next.js app) │
│   - browse services       │   - schedule / day-week view               │
│   - service detail        │   - CRUD services & availability           │
│   - pick slot → book      │   - update appointment status              │
│   - browse products, cart │   - product/stock management (later)       │
│   - account / order hist. │                                            │
└──────────────┬────────────┴───────────────────┬───────────────────────┘
               │ fetch, no-auth or client token  │ fetch, staff bearer token
               ▼                                 ▼
┌──────────────────────────────────────────────────────────────────────┐
│               API Layer — ZachHairStudio.Api (Controllers)            │
│  Thin controllers: bind request → call service → map result → return │
│  [Authorize] on staff-only routes; anonymous/customer-token on public │
├──────────────────────────────────────────────────────────────────────┤
│         NEW: Service / Application Layer (Shared, per feature)        │
│  ServicesService · AvailabilityService · BookingService               │
│  ProductsService · CartService/OrderService · AccountService          │
│  — validation, business rules, transactions, slot computation live    │
│    here, NOT in controllers and NOT in the DbContext                  │
├──────────────────────────────────────────────────────────────────────┤
│            Domain + Data Layer (Shared/Features/*, Shared/Db)         │
│  Entities, DTOs, mapper extensions (existing convention) per feature  │
│  BookingDbContext (single context, DbSets grow per feature)           │
└──────────────────────────────┬─────────────────────────────────────────┘
                                ▼
                     SQL Server (EF Core migrations own schema)
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|-------------------------|
| `landing-page/` | Public discovery + booking + shopping UI | Next.js App Router pages, calls API via `lib/api.ts` typed client |
| `dashboard/` | Staff schedule, service/availability/product management | Separate Next.js app, staff-authenticated, calls same API with bearer token |
| Controllers (`ZachHairStudio.Api/Controllers/`) | HTTP binding, status codes, `[Authorize]` gating | ASP.NET Core `ControllerBase`, one per feature, injects a service interface (not `DbContext`) |
| **Service layer (new)** | Business rules, orchestration, transactions, slot math | Plain C# classes in `Shared/Features/<Feature>/<Feature>Service.cs`, registered via DI as scoped |
| Entities/DTOs/mappers | Domain shape + API contracts | Existing feature-folder convention: `Entity.cs`, `*CreateDto.cs`, `*ResponseDto.cs`, `*Extensions.cs` |
| `BookingDbContext` | Persistence only — DbSets, fluent config, migrations | Single shared context across features (as today); no query/business logic beyond mapping |
| SQL Server | Durable storage | EF Core migrations, one migration per feature addition |

## Recommended Project Structure

Extends the existing `Shared/Features/` convention — one new feature folder per bounded concept, following the `Bookings/` template already in the repo:

```
API/ZachHairStudio.Shared/
├── Features/
│   ├── Bookings/                      # existing — becomes "Appointments" conceptually in Phase 2
│   │   ├── Booking.cs
│   │   ├── BookingCreateDto.cs / BookingResponseDto.cs
│   │   ├── BookingExtensions.cs
│   │   └── BookingStatus.cs
│   ├── Services/                      # Phase 1 — service catalog
│   │   ├── Service.cs                 # Name, Description, DurationMinutes, Price, IsActive
│   │   ├── ServiceCreateDto.cs / ServiceResponseDto.cs
│   │   ├── ServiceExtensions.cs
│   │   └── ServicesService.cs         # NEW: business logic (list active, get by id)
│   ├── Availability/                  # Phase 2 — feeds slot computation
│   │   ├── StylistAvailability.cs     # recurring weekly pattern (Stylist, DayOfWeek, StartTime, EndTime)
│   │   ├── AvailabilityException.cs   # date-specific override/blackout (Stylist, Date, IsClosed, StartTime?, EndTime?)
│   │   ├── AvailabilityExtensions.cs
│   │   └── AvailabilityService.cs     # NEW: ComputeOpenSlots(stylistId, serviceId, date) -> IEnumerable<Slot>
│   ├── Appointments/                  # Phase 2 — real slot-based booking (supersedes free-text Bookings)
│   │   ├── Appointment.cs             # CustomerId?, StylistId, ServiceId, StartAt, EndAt, Status
│   │   ├── AppointmentCreateDto.cs / AppointmentResponseDto.cs
│   │   ├── AppointmentExtensions.cs
│   │   └── BookingService.cs          # NEW: validates slot still open, creates appointment, re-checks overlap
│   ├── Staff/                         # Phase 3-4 — stylist identity + dashboard auth
│   │   ├── Stylist.cs
│   │   └── StaffAccount.cs            # credentials/roles for dashboard login
│   ├── Products/                      # Phase 5
│   │   ├── Product.cs                 # Name, Description, Price, ImageUrl, StockQty
│   │   ├── ProductCreateDto.cs / ProductResponseDto.cs
│   │   ├── ProductExtensions.cs
│   │   └── ProductsService.cs
│   ├── Orders/                        # Phase 6
│   │   ├── Cart.cs / CartItem.cs      # ephemeral, tied to session/customer
│   │   ├── Order.cs / OrderItem.cs    # immutable snapshot at checkout
│   │   ├── OrderExtensions.cs
│   │   └── OrderService.cs            # NEW: checkout transaction (create order + decrement stock atomically)
│   └── Accounts/                      # Phase 7
│       ├── Customer.cs                # replaces ad-hoc name/email/phone on Booking
│       ├── CustomerAccount.cs         # auth identity linked to Customer
│       └── AccountService.cs
├── Db/
│   └── BookingDbContext.cs            # grows one DbSet per feature; consider renaming when Phase 2 lands
├── Migrations/
└── Services/                          # alternative: cross-feature interfaces (ISlotClock, INow) if needed
```

### Structure Rationale

- **One feature folder per bounded concept, not per CRUD verb** — matches the existing `Bookings/` precedent and the project's stated constraint ("feature folders, not by technical layer").
- **A `*Service.cs` per feature folder, not a separate `Services/` layer directory** — keeps the "everything about X lives together" property the codebase already has, while still introducing the missing service-layer indirection the existing ARCHITECTURE.md and CONCERNS.md call out as the #1 anti-pattern to fix.
- **Availability and Appointments are separate features** even though they're tightly coupled — availability is staff-authored data (Phase 4 CRUD), appointments are the derived booking transaction (Phase 2). Splitting them lets Phase 2 consume a stub/seeded availability table before Phase 4 builds its management UI, which matches the roadmap's actual phase order (Phase 2 before Phase 4).
- **`DbContext` stays a single shared context** (as today) rather than one-per-feature — EF Core supports multiple DbSets in one context fine at this scale; splitting contexts adds cross-context transaction complexity the project doesn't need yet.

## Architectural Patterns

### Pattern 1: Service layer between Controller and DbContext

**What:** Controllers depend on an injected feature service interface (e.g. `IBookingService`), never on `BookingDbContext` directly. The service owns validation, business rules, and calls the DbContext.
**When to use:** From Phase 1 onward — introduce it now while the surface area is small, rather than retrofitting once Bookings, Services, Availability, Products, and Orders all directly touch the context.
**Trade-offs:** Small amount of extra boilerplate (interface + class per feature) vs. testability without a real database, and a single place to add rules like "no double-booking" or "can't order out-of-stock item" without touching HTTP concerns.

**Example:**
```csharp
// Shared/Features/Appointments/BookingService.cs
public class BookingService(BookingDbContext db)
{
    public async Task<Result<AppointmentResponseDto>> CreateAsync(AppointmentCreateDto dto)
    {
        var slots = await ComputeOpenSlotsAsync(dto.StylistId, dto.ServiceId, dto.StartAt.Date);
        if (!slots.Contains(dto.StartAt))
            return Result.Fail("Selected slot is no longer available.");

        var appointment = dto.ToEntity();
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(); // fails on overlap if a unique/exclusion constraint is also enforced in DB
        return Result.Ok(appointment.ToDto());
    }
}

// Controller stays thin
[HttpPost]
public async Task<IActionResult> CreateAppointment(AppointmentCreateDto dto)
{
    var result = await _bookingService.CreateAsync(dto);
    return result.Success ? CreatedAtAction(...) : BadRequest(result.Error);
}
```

### Pattern 2: Availability → open-slot computation as a pure query, not a materialized table

**What:** `StylistAvailability` (recurring weekly working hours) + `AvailabilityException` (date-specific overrides/time-off) are the source data staff manage. Open slots are **computed on read** by an `AvailabilityService.ComputeOpenSlotsAsync(stylistId, serviceId, date)`: take the stylist's working window for that day, subtract existing `Appointment` rows for that stylist/day, chunk the remainder into slot-sized increments using the requested service's `DurationMinutes`.
**When to use:** Always for this domain size — a "slots" table you pre-generate and mark booked/unbooked is unnecessary complexity at single-salon scale and creates a second source of truth that can drift from actual appointments.
**Trade-offs:** Computing on read is simple and always consistent with the `Appointments` table, but requires the computation to run inside the request path (acceptable — this is a low-volume, single-location system) and the overlap check must be re-validated at write time (see Pattern 3) since the read and the write are not atomic.

**Example:**
```csharp
// Shared/Features/Availability/AvailabilityService.cs
public async Task<IReadOnlyList<TimeOnly>> ComputeOpenSlotsAsync(int stylistId, int serviceId, DateOnly date)
{
    var service = await db.Services.FindAsync(serviceId);
    var window = await ResolveWorkingWindowAsync(stylistId, date); // pattern row, overridden by exception row if present
    if (window is null) return [];

    var booked = await db.Appointments
        .Where(a => a.StylistId == stylistId && a.StartAt.Date == date.ToDateTime(TimeOnly.MinValue))
        .Select(a => (a.StartAt, a.EndAt))
        .ToListAsync();

    return GenerateSlots(window.Value, service!.DurationMinutes, booked);
}
```

### Pattern 3: Re-validate overlap at write time (optimistic concurrency, not just a read check)

**What:** Because slot computation (Pattern 2) and appointment creation are two separate steps, a race is possible (two clients book the same slot near-simultaneously). Guard the write with either (a) a unique index / DB constraint on `(StylistId, StartAt)` or an exclusion constraint, or (b) re-run the overlap check inside the same transaction right before insert, and catch the DB exception on conflict.
**When to use:** Phase 2 onward, as soon as real bookings replace free-text requests — this is the one correctness property that, if skipped, causes visible double-bookings in front of customers.
**Trade-offs:** A DB-level unique constraint is the more robust guard (survives concurrent requests across processes); an app-level re-check alone is weaker but simpler to express in EF Core. Recommend both: app-level check for a friendly error message, DB constraint as the actual guarantee.

### Pattern 4: Cart as ephemeral state, Order as immutable snapshot

**What:** `Cart`/`CartItem` hold current-session product selections (price can reference the live `Product` row). At checkout, `OrderService` copies cart contents into `Order`/`OrderItem` rows that snapshot price and product name at time of purchase, then decrements `Product.StockQty`, all inside one transaction.
**When to use:** Phase 6 (Cart & checkout) — don't reuse `Cart` as `Order` with a status flag; they have different lifecycles (cart is mutable/discardable, order is a permanent record) and different consistency requirements (order needs the historical price even if the product price later changes).
**Trade-offs:** Two tables instead of one status-flagged table adds a small mapping step at checkout, but avoids corrupting historical order data when product prices/names change later, and avoids leaking abandoned-cart rows into order history/reporting.

**Example:**
```csharp
// Shared/Features/Orders/OrderService.cs
public async Task<Result<OrderResponseDto>> CheckoutAsync(int cartId)
{
    await using var tx = await db.Database.BeginTransactionAsync();
    var cart = await db.Carts.Include(c => c.Items).ThenInclude(i => i.Product).FirstAsync(c => c.Id == cartId);

    foreach (var item in cart.Items)
        if (item.Product.StockQty < item.Quantity)
            return Result.Fail($"{item.Product.Name} is out of stock.");

    var order = cart.ToOrder(); // snapshot price/name per item
    db.Orders.Add(order);
    foreach (var item in cart.Items)
        item.Product.StockQty -= item.Quantity;

    await db.SaveChangesAsync();
    await tx.CommitAsync();
    return Result.Ok(order.ToDto());
}
```

### Pattern 5: Two authentication surfaces on one API — public/customer vs. staff

**What:** Register two auth schemes side by side in `Program.cs` (bearer/JWT is the recommended default for both, since neither frontend is a traditional server-rendered browser session app — Next.js clients calling a separate API are "not the browser client" in ASP.NET Core's own terms). Staff routes get `[Authorize(Roles = "Staff")]` or a policy; public booking/catalog routes stay `[AllowAnonymous]` until Phase 7 introduces customer accounts, at which point customer-authenticated routes (order history) get a separate policy on the same bearer scheme, distinguished by claims/role rather than a second scheme.
**When to use:** Introduce the scheme plumbing in Phase 3 (staff dashboard needs *some* gate even if simplistic at first) and harden it into full JWT + roles by Phase 7-8 per the roadmap's own deferred decisions.
**Trade-offs:** One scheme with role claims is simpler to operate than two full schemes; only add a second scheme (e.g. cookie) if a server-rendered surface is added later (unlikely given both frontends are Next.js API clients).

## Data Flow

### Request Flow: Availability → Slots → Appointment (the core value path)

```
[Client picks Service + Date on landing-page]
    ↓
GET /api/availability?serviceId=&stylistId=&date=
    ↓
AvailabilityController → AvailabilityService.ComputeOpenSlotsAsync
    ↓ reads StylistAvailability (pattern) + AvailabilityException (overrides)
    ↓ reads Appointments (already booked, same stylist/day) to subtract busy time
    ↓ chunks remaining window by Service.DurationMinutes
[Response: list of open start times] ← returned to client
    ↓
[Client picks a slot, submits confirm]
    ↓
POST /api/appointments { serviceId, stylistId, startAt, customer info }
    ↓
AppointmentsController → BookingService.CreateAsync
    ↓ re-validates the slot is still open (Pattern 3)
    ↓ maps DTO → Appointment entity, Status = Pending/Confirmed
    ↓ db.Appointments.Add + SaveChangesAsync (unique constraint guards races)
[Response: created appointment] ← 201 to client, shown as confirmation
```

Direction is strictly one-way for reads (Availability + Appointments → computed slots), and the only write in this path is the final appointment insert — availability data itself is never written by this flow, only read.

### Secondary Flow: Cart → Checkout → Order

```
[Client adds Product to cart]        (client-side/session cart state, optionally persisted per Phase 6 decision)
    ↓
POST /api/cart/items → CartService
    ↓
[Client checks out]
    ↓
POST /api/orders/checkout { cartId, paymentDetails }
    ↓
OrderService.CheckoutAsync (single transaction)
    ↓ validate stock per item
    ↓ call payment provider (Phase 6 decision)
    ↓ snapshot cart → Order/OrderItem
    ↓ decrement Product.StockQty
[Response: Order confirmation] ← 201 to client
```

### Staff Dashboard Flow

```
[Staff logs into dashboard/]
    ↓ bearer token issued (Phase 3/7 auth decision)
GET /api/appointments?date=&stylistId=  (Authorize: Staff)
    ↓ AppointmentsController → BookingService.GetScheduleAsync
[Staff updates status]
POST /api/appointments/{id}/status  (Authorize: Staff)
    ↓ BookingService.UpdateStatusAsync — same service reused, not duplicated logic
```

Staff dashboard reuses the **same** feature services as the public API (`BookingService`, `ServicesService`, `AvailabilityService`) rather than a parallel admin-only code path — the difference is which controller/route calls them and the `[Authorize]` gate, not duplicated business logic. This matters because `ZachHairStudio.Admin` (the scaffolded MVC project) already exists as a second, half-started admin surface; new work should route through `ZachHairStudio.Api` + `dashboard/` per `PROJECT.md`, and the services layer should be written so either front door can call it.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| Single salon, current roadmap (v1) | Everything above is sufficient: one DbContext, on-read slot computation, single API process. Don't build a slots-materialization job, don't split microservices. |
| Multiple stylists / multiple locations later (out of scope per PROJECT.md, but noted) | Availability computation already scopes by `StylistId`; adding `LocationId` to `StylistAvailability`/`Appointment` is additive, not a redesign. |
| High concurrent booking traffic (unlikely at this scale) | The DB unique constraint from Pattern 3 is the actual bottleneck-safe mechanism; app-level checks alone would need a distributed lock at higher scale — not needed here. |

### Scaling Priorities

1. **First real risk is correctness, not throughput:** double-booking races (Pattern 3) and cart/stock consistency at checkout (Pattern 4) are the things that break trust even at low traffic — prioritize the DB-level guarantees over performance work.
2. **Second: the missing service layer becomes expensive to retrofit** the more features get added directly against controllers/DbContext. Introduce it starting Phase 1 (Services feature) rather than waiting until Phase 6 (Orders) when the pattern has already been copy-pasted three more times.

## Anti-Patterns

### Anti-Pattern 1: Controllers calling DbContext directly (already flagged in codebase CONCERNS.md)

**What people do:** Inject `BookingDbContext` straight into the controller and write LINQ queries / business rules inline in action methods (current state for `BookingsController`).
**Why it's wrong:** Untestable without a real database; business rules (overlap checks, stock checks, checkout transactions) get duplicated or inconsistently applied across controllers as more features are added.
**Do this instead:** Every new feature (Services, Availability, Appointments, Products, Orders, Accounts) gets a `*Service` class from day one; retrofit `BookingsController`/`BookingService` when Phase 2 touches it anyway (Bookings → Appointments rework).

### Anti-Pattern 2: A separate always-materialized "Slots" table

**What people do:** Pre-generate a row per bookable slot per stylist per day and flip a boolean when booked.
**Why it's wrong:** Creates a second source of truth that must stay in sync with `Appointments` and `StylistAvailability`; every availability change (staff edits hours) requires regenerating/invalidating slot rows; at single-salon scale this is pure overhead with no query-performance benefit.
**Do this instead:** Compute slots on read from `StylistAvailability` + `AvailabilityException` minus existing `Appointments` (Pattern 2). Revisit only if profiling ever shows this is a hot path at a scale this project isn't targeting.

### Anti-Pattern 3: Cart and Order sharing one table with a status flag

**What people do:** One `Order` table with `Status = InCart | Placed | Completed`, reused for both pre- and post-checkout state.
**Why it's wrong:** Historical order data (price, product name) becomes mutable if it points live at `Product`; abandoned carts pollute order reporting/history; different lifecycle rules (carts expire/merge, orders don't) get tangled into one entity.
**Do this instead:** Separate `Cart`/`CartItem` (ephemeral) from `Order`/`OrderItem` (immutable snapshot), per Pattern 4.

### Anti-Pattern 4: Building a second admin surface in `ZachHairStudio.Admin` alongside `dashboard/`

**What people do:** Continue building out the scaffolded Razor/MVC `ZachHairStudio.Admin` project for staff features because it already has a `BookingController` started.
**Why it's wrong:** `PROJECT.md` and the roadmap both commit to `dashboard/` (Next.js) as the staff surface; maintaining two staff UIs against the same API doubles the auth/UI work for no stated benefit, and contradicts the "separate `dashboard/` app for staff" key decision already logged in PROJECT.md.
**Do this instead:** Treat `ZachHairStudio.Admin` as legacy scaffold; build staff features in `dashboard/` per roadmap Phase 3-4, and either retire or explicitly repurpose the Admin project in a future decision.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Payment provider (Phase 6, decision deferred) | Server-side call from `OrderService.CheckoutAsync`, inside the same transaction boundary as stock decrement | Decide provider when Phase 6 is planned; keep the call behind an interface (`IPaymentProvider`) so the checkout transaction logic doesn't change when the provider does |
| Auth/identity (Phase 7, decision deferred) | JWT bearer issued by the API itself (ASP.NET Core Identity) or an external IdP | Given "Dev simplicity" constraint in PROJECT.md, self-hosted ASP.NET Core Identity + JWT is the lower-friction default over adding a third-party auth service, unless a decision says otherwise |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `landing-page/` ↔ API | HTTP/JSON via `lib/api.ts` typed client (existing pattern) | Public routes anonymous until Phase 7; extend the existing client, don't fork a new one |
| `dashboard/` ↔ API | HTTP/JSON, own typed client mirroring `lib/api.ts`, bearer token attached | New app — scaffold it to match `landing-page/`'s API-client convention rather than inventing a new one |
| Controllers ↔ Service layer | Direct C# method calls (in-process, same deployable) | Not a network boundary — no need for events/messaging at this scale |
| Service layer ↔ `BookingDbContext` | EF Core, scoped-per-request DbContext (existing DI lifetime) | Keep one shared context; don't split per feature |
| `dashboard/` staff services ↔ `landing-page/` public services | Same underlying `*Service` classes, different controllers/authorization | See Staff Dashboard Flow above — avoid logic duplication between the two frontends |

## Sources

- [Entity Framework Core docs — concurrency handling (rowversion/optimistic concurrency)](https://github.com/dotnet/entityframework.docs/blob/main/entity-framework/core/saving/concurrency.md) — MEDIUM confidence (official docs via Context7)
- [ASP.NET Core docs — Web API architecture (controller → service → data layer)](https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/tutorials/first-web-api/includes/first-web-api8.md) — MEDIUM confidence (official docs via Context7)
- [ASP.NET Core docs — multiple authentication schemes, cookie vs bearer tokens](https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/security/authorization/limitingidentitybyscheme.md) — MEDIUM confidence (official docs via Context7)
- General web search on salon/appointment domain modeling (Customer/Stylist/Service/Appointment entities) — LOW confidence, directional only, cross-checked against this project's own roadmap wording
- General web search on e-commerce cart/order/inventory domain modeling and EF Core transaction patterns — LOW confidence, directional only
- `.planning/codebase/ARCHITECTURE.md` and `.planning/codebase/STRUCTURE.md` — HIGH confidence (ground truth for this repo)
- `.planning/PROJECT.md` and `specs/roadmap.md` — HIGH confidence (ground truth for project intent and phase order)

---
*Architecture research for: salon booking + light-commerce platform, extending existing .NET feature-folder architecture*
*Researched: 2026-07-07*
