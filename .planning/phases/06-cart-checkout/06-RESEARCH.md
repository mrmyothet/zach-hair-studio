# Phase 6: Cart & Checkout - Research

**Researched:** 2026-08-10 (force-refresh; prior 2026-08-09 draft was truncated before Validation Architecture)
**Domain:** Guest cart + server-authoritative checkout, Stripe.net (Checkout Session + webhook-verified fulfillment), atomic stock decrement, EF Core entity design
**Confidence:** MEDIUM

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Payment Provider**
- Stripe direct integration via Stripe.net (server-side only) — Checkout Session creation + webhook signature verification for fulfillment.
- `IPaymentProvider` interface keeps the provider swappable; implementation behind it is Stripe.
- Lowest fees and deepest first-party ASP.NET/Next.js integration for a single-jurisdiction physical-goods retailer (per research/SUMMARY.md).

**Cart Architecture**
- Cart/CartItem as ephemeral DB tables keyed by a client session identifier (no account).
- Order/OrderItem as immutable snapshot tables created at checkout.
- Two separate tables, not one status-flagged table (per research).

**Stock Concurrency**
- Atomic conditional UPDATE on Order creation: `UPDATE Products SET Stock = Stock - @qty WHERE Id = @id AND Stock >= @qty`; 0 affected rows = insufficient stock → 409.
- Exactly-one-winner under concurrent checkout against the last unit (mirrors Phase 2's AppointmentSlot unique-index guarantee).
- Server-side total always recomputed from catalog prices by ProductId; never trusts client-submitted price/total (SHOP-03).

**Checkout Flow**
- Guest checkout via Stripe Checkout Session; `Order.ClientId` nullable (SHOP-06).
- Order created with Pending status; Stripe webhook (signature-verified) flips to Fulfilled (SHOP-05) — client redirect alone never fulfills.
- SHOP-07: stylist-recommended add-ons rendered as suggestion chips on the cart page; user adds before checkout.

### Claude's Discretion

- Exact Stripe mode (test mode vs live), webhook endpoint path, idempotency-key strategy, and cart session-key mechanism (cookie vs header) — follow codebase conventions and research guidance.

### Deferred Ideas (OUT OF SCOPE)

- Accounts/loyalty (Phase 7) — guest checkout intentionally independent (`Order.ClientId` nullable per roadmap decision).
- Deposit/cancellation policy at booking time — flagged as near-term add-on once payment provider exists (research flag).
- IDOR hardening for client account order lookups — Phase 7 concern, guest checkout must not block on it.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SHOP-01 | Client can add products to a cart and review it | Cart/CartItem entity shape (Pattern 1), session-key mechanism (Pattern 4), cart page (Pattern 5); Validation: CartsController/CartsService tests |
| SHOP-02 | Client can check out and pay through an integrated payment provider | `IPaymentProvider` seam (Pattern 2), Stripe.net Checkout Session creation (Pattern 3), Stripe CLI local flow (Pattern 6); Validation: fake provider + manual Stripe CLI UAT |
| SHOP-03 | Order total is computed server-side from the catalog; client-supplied prices are never trusted | `OrdersService.CreateCheckoutAsync` recomputes totals by ProductId (Pattern 1); `price_data` from catalog; OrderItem snapshot; Validation: price-authority tests |
| SHOP-04 | Product stock is decremented atomically on order creation, with no overselling under concurrent checkout | Atomic conditional `ExecuteUpdateAsync` with `WHERE Stock >= qty` in the same transaction (Pattern 1 + Pattern 7); Validation: SqlServer concurrency proof mirroring `ConcurrencyTests` |
| SHOP-05 | Order fulfillment is confirmed only via a verified payment webhook, not the client redirect | Raw-body webhook + `EventUtility.ConstructEvent` (Pattern 3), `payment_status` check, idempotent flip via unique `StripeSessionId` (Pattern 8); Validation: webhook signature tests |
| SHOP-06 | Guest checkout works without an account (`Order.ClientId` nullable) | `Order.ClientId` nullable int; no auth on cart/checkout endpoints; Validation: create-order asserts null ClientId |
| SHOP-07 | Stylist-recommended add-ons are surfaced at checkout | Reuses Phase 5 `ServiceRecommendedProduct` join; cart chips (Pattern 5 / UI-SPEC); Validation: recommended-for-checkout service/API test |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Actionable directives the planner must honor:

- **Stack:** Next.js 15 App Router + React 19 + Tailwind 4 (`landing-page/`, `dashboard/`); .NET 10 / ASP.NET Core + EF Core 10 / SQL Server API.
- **Architecture:** Feature folders (`Features/<Name>/`); TypeScript on frontend; OpenAPI source of truth for typed clients (landing-page currently hand-writes `lib/` fetches).
- **Dev simplicity:** LocalDB + `next dev` + `dotnet run` baseline; secrets via `dotnet user-secrets` / env only (never tracked files) — same D-13 discipline as `RESEND_API_KEY` / `Jwt:SigningKey`.
- **Booking invariant (do not break):** `AppointmentSlot` unique index on `(StylistId, SlotStart)` must remain **unfiltered**.
- **Service layer:** Controllers never inject `BookingDbContext` (PLAT-01); FluentValidation for DTOs (PLAT-02).
- **Tests:** `dotnet test API/ZachHairStudio.slnx`; concurrency proofs use `SqlServerWebApplicationFactory`, not InMemory.
- **EF migrations:** `dotnet-ef` v10.x; Shared project + Api startup; skill `ef-migrations`.
- **Skills to follow:** `feature-scaffold`, `ef-migrations`, `openapi-client` (optional for landing-page this phase — hand-written `lib/cart.ts` matches Phase 5 `lib/products.ts`), `dev`.
- **GSD:** Do not make direct repo edits outside a GSD workflow unless explicitly asked to bypass.

## Summary

Phase 6 is the highest external-integration-risk phase after Phase 2: it introduces Stripe, the second read-then-write race (stock), and the price-authority boundary. Internal patterns already shipped: Phase 2 proves exactly-one-winner under concurrency; Phase 4 proves `Database.CreateExecutionStrategy().ExecuteAsync` + explicit transaction (required because `ExecuteUpdateAsync` does not participate in `SaveChanges`’s implicit transaction and the repo enables `EnableRetryOnFailure`); `Result<T>` + FluentValidation + feature folders are stable. What is new is the Stripe wire contract: `SessionCreateOptions` → `Session.Url` redirect, `EventUtility.ConstructEvent` on a raw body, and idempotent webhook fulfillment keyed on the Checkout Session.

Design is fully constrained by CONTEXT.md: Cart/CartItem vs Order/OrderItem as separate tables; atomic conditional stock UPDATE in the same transaction as Pending order creation; server-recomputed totals; fulfillment only from a signature-verified webhook; nullable `Order.ClientId`; add-on chips on the cart page. Discretion resolutions recommended below: header session key (not cookie), test-mode Stripe, `/api/stripe/webhook`, filtered unique index on `Order.StripeSessionId`.

**Primary recommendation:** Implement `Features/Carts/`, `Features/Orders/`, and `Features/Payments/` mirroring Bookings’ service-layer shape; decrement stock via `ExecuteUpdateAsync` with `WHERE Stock >= qty` inside `CreateExecutionStrategy` + explicit transaction; add `Stripe.net` 52.2.0 behind `IPaymentProvider`; create Checkout Sessions with `price_data` from catalog prices; handle `checkout.session.completed` in a raw-body webhook; prove SHOP-03/04/05 with automated tests before UAT.

**Highest-risk plan items:** (1) transaction + retry-strategy wrapper for atomic decrement, (2) raw-body webhook reading, (3) webhook idempotency + unique `StripeSessionId`, (4) compensating path if Stripe session creation fails after stock decrement, (5) Stripe CLI install for SHOP-05 UAT, (6) `Result.ConflictError` currently requires `AvailabilityConflictDto` — stock 409 needs a message-only overload or `DuplicateRecordError`→409 mapping like Appointments.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Cart/CartItem persistence (ephemeral, session-keyed) | Database / Storage | API / Backend | DB tables keyed by client session id; `CartsService` owns DbContext (PLAT-01) |
| Order/OrderItem immutable snapshot | API / Backend | Database / Storage | `OrdersService` recomputes totals (SHOP-03) and writes snapshot in one transaction |
| Atomic stock decrement | Database / Storage | — | Conditional `UPDATE ... WHERE Stock >= @qty` is the exactly-one-winner guarantee (SHOP-04) |
| Checkout Session creation | API / Backend | — | Server-only via `IPaymentProvider` → Stripe.net; `price_data` from DB catalog |
| Webhook signature verification + fulfillment | API / Backend | — | Raw-body + `EventUtility.ConstructEvent`; only verified paid session flips Fulfilled (SHOP-05) |
| Guest cart session-key | API / Backend | Browser / Client | Server issues/accepts id; client stores in `localStorage` and sends header |
| Cart / checkout UX + add-on chips | Browser / Client | Frontend Server (SSR) | `"use client"` cart/checkout per UI-SPEC; suggestion chips SHOP-07 |
| Recommended-add-on lookup | API / Backend | Database / Storage | Reuses `ServiceRecommendedProduct` join (PROD-03) |
| Stripe CLI local forwarding | Developer tooling | — | Required for SHOP-05 manual/e2e verification locally |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Stripe.net | 52.2.0 (latest stable on nuget.org as of 2026-08-10; 52.3.0-beta.1 exists — do not use) | Checkout Session + webhook signature verification | Official Stripe .NET SDK; locked by CONTEXT.md. `[VERIFIED: nuget.org flat container — latest stable 52.2.0]` |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 | `ExecuteUpdateAsync` + transactional order creation | Already pinned `[VERIFIED: repo — ZachHairStudio.Shared.csproj / Api.Tests.csproj]` |
| FluentValidation | (existing assembly scan) | Cart/checkout DTO validators | PLAT-02; auto-registered — no Program.cs scan change `[VERIFIED: repo — Program.cs AddValidatorsFromAssemblyContaining]` |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | Unit tests for services (**not** atomic decrement) | Already pinned `[VERIFIED: repo — Api.Tests.csproj]` |
| xUnit | 2.9.3 | Test runner | Already pinned `[VERIFIED: repo — Api.Tests.csproj]` |
| zod | 4.4.3 | Landing-page cart/checkout response validation | Existing frontend convention `[VERIFIED: repo — landing-page/package.json]` |

**Target-framework note:** Stripe.net 52.2.0 ships older TFMs (no `net10.0`); .NET 10 apps may reference older-runtime libraries. Confirm clean restore/build after `dotnet add package`. `[CITED: learn.microsoft.com/dotnet/core/versions — runtime compatibility; ASSUMED TFM list matches prior nuget page inspection]`

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Stripe CLI | latest (not installed in this environment) | `stripe listen --forward-to localhost:5236/api/stripe/webhook`; `stripe trigger`; prints `whsec_...` | SHOP-05 local UAT / human-verify checkpoint `[CITED: docs.stripe.com/cli/listen]` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Stripe.net (direct) | Paddle / Lemon Squeezy (MoR) | Locked out by CONTEXT.md |
| `IPaymentProvider` | Direct `StripeClient` in OrdersService | Locked interface; also enables fake provider in tests (no Stripe keys in CI) |
| Header `X-Cart-Session-Id` | HttpOnly cookie | Cookie needs `AllowCredentials` + specific origin; repo uses `AllowAnyOrigin()` until Phase 8 `[VERIFIED: repo — Program.cs]` |
| `ExecuteUpdateAsync` | Raw `ExecuteSqlInterpolated` | Prefer typed EF API; same atomic SQL semantics |
| `price_data` | Pre-created Stripe Price objects | Would force catalog sync to Stripe; zero benefit for small catalog `[CITED: docs.stripe.com/checkout/quickstart — price_data vs predefined prices]` |

**Installation:**
```bash
cd API/ZachHairStudio.Api
dotnet add package Stripe.net --version 52.2.0
# Pin Version in the csproj that owns the Payments feature (Shared or Api — prefer Shared if StripePaymentProvider lives there, else Api)
```

If `StripePaymentProvider` lives in `ZachHairStudio.Shared`, add the package to **Shared** (keep Api free of payment SDK if possible). Shared already holds feature services.

**Discretion resolutions (Claude's Discretion → recommended locks for planner):**

| Topic | Recommendation |
|-------|----------------|
| Stripe mode | **Test mode** for MVP implementation + UAT; live keys only at launch (Phase 8) |
| Webhook path | `POST /api/stripe/webhook` (`StripeWebhookController`) |
| Cart session key | Client-generated or server-issued UUID stored in `localStorage`, sent as `X-Cart-Session-Id` on every cart/checkout call |
| Idempotency | Filtered unique index on `Order.StripeSessionId` WHERE NOT NULL; `MarkFulfilledAsync` no-ops if already Fulfilled; optional Stripe `Idempotency-Key` header on Session create = `order-{orderId}` |

## Package Legitimacy Audit

> NuGet is not a supported ecosystem for `gsd-tools query package-legitimacy check` (npm/pypi/crates only). Manual verification against nuget.org + official Stripe quickstart (`dotnet add package Stripe.net`).

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| Stripe.net | NuGet | 11+ yrs (2014+) | High (official Stripe SDK) | github.com/stripe/stripe-dotnet (owner stripe) | OK | Approved — pin 52.2.0 |

**Packages removed due to [SLOP] verdict:** none  
**Packages flagged as suspicious [SUS]:** none for NuGet Stripe.net

*Note: `package-legitimacy check --ecosystem npm stripe` returned `SUS` ("too-new") — false positive on monthly-republished official Node SDK; **not used this phase** (hosted Checkout needs no `@stripe/stripe-js`).*

## Architecture Patterns

### System Architecture Diagram

```text
Client browser (landing-page)
   │
   ├── POST/GET/DELETE /api/carts/{sessionKey}/... ──► CartsController ──► CartsService
   │         header X-Cart-Session-Id (or path key)     ▲ reads catalog price/stock; CartItem stores ProductId+qty only
   │
   ├── GET recommended-for-checkout ──► ProductsService / ServicesService (ServiceRecommendedProduct join)
   │
   ├── POST /api/orders/checkout { sessionKey, items[{productId,qty}], email? }
   │         ──► OrdersController ──► OrdersService.CreateCheckoutAsync
   │                1. Load Products; recompute totals (SHOP-03) — ignore any client price
   │                2. CreateExecutionStrategy + BeginTransaction:
   │                     a. ExecuteUpdateAsync Stock -= qty WHERE Stock >= qty  (SHOP-04)
   │                        0 rows → rollback → 409
   │                     b. INSERT Order(Pending, ClientId=null) + OrderItem snapshots
   │                     c. SaveChanges + commit
   │                3. IPaymentProvider.CreateCheckoutSession (price_data from snapshots)
   │                     on Stripe failure → compensate (restore stock, Order Failed)
   │                4. Return { checkoutUrl }
   │         ──► client window.location = checkoutUrl
   │
   │                    Stripe hosted Checkout
   │                         │
   │  success_url (display only — NEVER Fulfilled) ◄──┤
   │                         │ checkout.session.completed
   │                         ▼
   │         POST /api/stripe/webhook (raw body + Stripe-Signature)
   │              EventUtility.ConstructEvent → 400 on bad sig
   │              type completed && payment_status paid
   │              OrdersService.MarkFulfilledAsync (idempotent)  (SHOP-05)
```

### Recommended Project Structure

```
API/ZachHairStudio.Shared/Features/Carts/
├── Cart.cs / CartItem.cs
├── Cart*Dto.cs + CartItemCreateDtoValidator.cs   # productId + quantity only — NO price
├── CartExtensions.cs
└── CartsService.cs

API/ZachHairStudio.Shared/Features/Orders/
├── Order.cs / OrderItem.cs / OrderStatus.cs
├── CheckoutRequestDto.cs + Validator.cs          # productId + quantity + optional email
├── OrderExtensions.cs
└── OrdersService.cs                              # CreateCheckoutAsync + MarkFulfilledAsync

API/ZachHairStudio.Shared/Features/Payments/
├── IPaymentProvider.cs
├── StripePaymentProvider.cs
└── StripeOptions.cs                              # SecretKey, WebhookSecret, SuccessUrl, CancelUrl

API/ZachHairStudio.Api/Controllers/
├── CartsController.cs
├── CheckoutController.cs                         # or OrdersController
└── StripeWebhookController.cs                    # raw body, [AllowAnonymous]

landing-page/lib/cart.ts + cartSession.ts
landing-page/app/cart/page.tsx
landing-page/app/checkout/page.tsx
landing-page/app/checkout/success/page.tsx
landing-page/app/checkout/cancel/page.tsx
```

### Pattern 1: Immutable-snapshot Order + atomic stock (SHOP-03/04/06)

**What:** Cart holds only `ProductId` + quantity. At checkout, load catalog rows, recompute `LineTotal = Price * Quantity`, snapshot name/price onto `OrderItem`, decrement stock with conditional UPDATE, insert `Order` with `ClientId = null` and `Status = Pending`.

**Critical API note:** `Result<T>.ConflictError` today **requires** `IReadOnlyList<AvailabilityConflictDto>` `[VERIFIED: repo — Result.cs]`. For stock 409, planner must either:
1. Add overload `ConflictError(string message, T? data = default)` (preferred — keeps `IsConflict()` mapping), or
2. Map via `DuplicateRecordError` → controller `Conflict(...)` like `AppointmentsController` `[VERIFIED: repo — AppointmentsController.cs]`.

**Example — checkout transaction:**
```csharp
// Source: learn.microsoft.com/ef/core/saving/execute-insert-update-delete
// + repo AvailabilityService CreateExecutionStrategy pattern (Phase 4)
var strategy = _dbContext.Database.CreateExecutionStrategy();
return await strategy.ExecuteAsync(async () =>
{
    await using var tx = await _dbContext.Database.BeginTransactionAsync();

    var order = new Order { Status = OrderStatus.Pending, ClientId = null, PlacedAtUtc = DateTimeOffset.UtcNow };
    foreach (var line in request.Items)
    {
        var product = await _dbContext.Products.FindAsync(line.ProductId);
        if (product is null || !product.IsActive)
            return Result<CheckoutResponseDto>.NotFoundError($"Product {line.ProductId} not found.");

        var updated = await _dbContext.Products
            .Where(p => p.Id == line.ProductId && p.Stock >= line.Quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - line.Quantity));
        if (updated == 0)
            return Result<CheckoutResponseDto>.ConflictError(
                $"Sorry, only {product.Stock} left of {product.Name}.",
                Array.Empty<AvailabilityConflictDto>()); // replace with message-only overload when added

        order.Items.Add(new OrderItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = line.Quantity,
            LineTotal = product.Price * line.Quantity,
        });
    }

    order.TotalAmount = order.Items.Sum(i => i.LineTotal);
    _dbContext.Orders.Add(order);
    await _dbContext.SaveChangesAsync();
    await tx.CommitAsync();

    try
    {
        var session = await _paymentProvider.CreateCheckoutSessionAsync(...);
        order.StripeSessionId = session.SessionId;
        order.StripeSessionUrl = session.Url;
        await _dbContext.SaveChangesAsync();
        return Result<CheckoutResponseDto>.Success(new() { CheckoutUrl = session.Url, OrderId = order.Id });
    }
    catch
    {
        // Compensate: restore stock + mark Failed (same strategy/tx pattern)
        throw; // planner: implement restore loop with ExecuteUpdateAsync Stock += qty
    }
});
```

`ExecuteUpdateAsync` runs immediately, ignores the change tracker, and does **not** auto-start a transaction — wrap with explicit transaction. Returns rows affected — `0` means concurrency/insufficient stock. Relational providers only. `[CITED: learn.microsoft.com/ef/core/saving/execute-insert-update-delete]`

### Pattern 2: `IPaymentProvider` seam

```csharp
public record CheckoutSessionRequest(int OrderId, decimal TotalAmount, string? CustomerEmail,
    IReadOnlyList<CheckoutLine> Lines);
public record CheckoutLine(string ProductName, decimal UnitPrice, int Quantity);
public record CheckoutSessionResult(string SessionId, string Url);

public interface IPaymentProvider
{
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken ct = default);
}
```

Register `StripeClient` singleton from `Stripe:SecretKey` (user-secrets/env); `AddScoped<IPaymentProvider, StripePaymentProvider>()`. Tests inject a fake that returns a deterministic URL without network I/O.

### Pattern 3: Stripe Checkout Session + webhook

```csharp
// Source: docs.stripe.com/checkout/quickstart + docs.stripe.com/webhooks?lang=dotnet
var options = new SessionCreateOptions
{
    Mode = "payment",
    SuccessUrl = _options.SuccessUrl + "?session_id={CHECKOUT_SESSION_ID}",
    CancelUrl = _options.CancelUrl,
    ClientReferenceId = request.OrderId.ToString(),
    Metadata = new Dictionary<string, string> { ["order_id"] = request.OrderId.ToString() },
    CustomerEmail = request.CustomerEmail,
    LineItems = request.Lines.Select(line => new SessionLineItemOptions
    {
        Quantity = line.Quantity,
        PriceData = new SessionLineItemPriceDataOptions
        {
            Currency = "usd",
            UnitAmount = (long)(line.UnitPrice * 100m), // minor units; prefer long UnitAmount for whole-dollar catalog
            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = line.ProductName },
        },
    }).ToList(),
};
```

Webhook (no `[FromBody]`):
```csharp
// Source: docs.stripe.com/webhooks?lang=dotnet
var json = await new StreamReader(Request.Body).ReadToEndAsync();
var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], _options.WebhookSecret);
if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
{
    var session = stripeEvent.Data.Object as Session;
    if (session?.PaymentStatus == "paid")
        await _ordersService.MarkFulfilledAsync(session.ClientReferenceId, session.Id);
}
return Ok();
```

- Raw body required — framework mutation breaks HMAC. `[CITED: docs.stripe.com/webhooks/signature]`
- Webhooks required for reliable fulfillment; success_url alone is insufficient. `[CITED: docs.stripe.com/checkout/fulfillment]`
- SHOP-05 is stricter than Stripe’s “also fulfill from landing page” tip: **do not** flip Fulfilled from success page; page may poll/display Pending→Fulfilled.
- For card MVP, require `payment_status == "paid"`. Delayed methods need `checkout.session.async_payment_succeeded` — out of MVP scope unless accepting ACH. `[CITED: docs.stripe.com/checkout/fulfillment]`

### Pattern 4: Guest cart session — header, not cookie

`AllowAnyOrigin()` + credentialed cookies is invalid. Use `X-Cart-Session-Id` + `localStorage`. Unique index on `Cart.SessionKey`. Session id is a nonce, not auth — IDOR hardening deferred (CONTEXT).

### Pattern 5: Cart page + add-on chips (SHOP-01, SHOP-07)

Follow `06-UI-SPEC.md`: `/cart` client page, Order Summary CTA, suggestion chips “Complete Your Routine”. Data: `ProductsService.GetRecommendedForCheckoutAsync(cartProductIds)` reusing `ServiceRecommendedProduct`, excluding items already in cart, limit ~4, omit section when empty. Fallback if join yields nothing: same-category in-stock (UI-SPEC open question — prefer join reuse first).

### Pattern 6: Stripe CLI local development

```bash
stripe login
stripe listen --forward-to localhost:5236/api/stripe/webhook
# whsec_... → dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
stripe trigger checkout.session.completed
```

Test card `4242 4242 4242 4242`. CLI `whsec_` ≠ Dashboard endpoint secret. `[CITED: docs.stripe.com/cli/listen, docs.stripe.com/checkout/fulfillment]`

### Pattern 7: Transaction + retry strategy

`ExecuteUpdateAsync` + `EnableRetryOnFailure` ⇒ wrap in `CreateExecutionStrategy().ExecuteAsync` + `BeginTransactionAsync`. Precedent: `AvailabilityService` (Phase 4 Plan 05) `[VERIFIED: repo — AvailabilityService.cs; STATE.md]`.

### Pattern 8: Idempotent fulfillment

Filtered unique index on `Order.StripeSessionId` WHERE NOT NULL (contrast: AppointmentSlot index must stay **unfiltered**). `MarkFulfilledAsync`: Pending→Fulfilled only; already Fulfilled → no-op 200. Stripe retries non-2xx for days. `[CITED: docs.stripe.com/checkout/fulfillment — fulfill only once]`

### Anti-Patterns to Avoid

- **`[FromBody]` on webhook** — breaks signature verification.
- **Fulfilling from success_url** — violates SHOP-05.
- **Check-then-decrement stock in app code** — race; use conditional UPDATE.
- **Price/total fields on cart/checkout DTOs** — structural SHOP-03 violation.
- **InMemory for SHOP-04 proof** — `ExecuteUpdateAsync` is relational-only.
- **Storing prices on CartItem** — reintroduces client-influenced money path.
- **Calling `ConflictError` with one string arg without adding an overload** — will not compile against current `Result.cs`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Card/PCI payment UI | Custom card form + tokenization | Stripe Checkout (hosted) | PCI scope collapse; locked decision |
| Webhook HMAC verification | Custom HMAC SHA-256 | `EventUtility.ConstructEvent` | Timestamp tolerance, scheme handling, secret rotation `[CITED: docs.stripe.com/webhooks]` |
| Stock race control | App-level read-modify-write | Conditional `ExecuteUpdateAsync` | Exactly-one-winner needs single SQL statement |
| Retry-safe transactions | Manual retry loops | `CreateExecutionStrategy().ExecuteAsync` | Required with `EnableRetryOnFailure` |
| Money formatting | Ad-hoc string concat | Existing `Intl.NumberFormat` `priceFormatter` | UI-SPEC parity with catalog |
| Feature scaffolding | Novel folder layout | `feature-scaffold` / Bookings mirror | Repo convention |

**Key insight:** The hard problems (PCI, webhook crypto, SQL races under retry) already have official solutions; inventing them is how money bugs ship.

## Runtime State Inventory

> Not a rename/refactor phase — omitted categories answered briefly for planner awareness.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None for cart/order yet; Products.Stock will be mutated at checkout | Code writes new tables + stock updates; no migration of old cart data |
| Live service config | Stripe Dashboard webhook endpoint (post-local) not in git | Human configures Dashboard endpoint at deploy time; local uses CLI |
| OS-registered state | None — verified no stripe systemd/pm2 units | none |
| Secrets/env vars | New: `Stripe:SecretKey`, `Stripe:WebhookSecret` (user-secrets/env, never tracked) | Add alongside existing RESEND/Jwt secrets; ValidateOnStart recommended |
| Build artifacts | Stripe.net not yet in any csproj | Package add during execution |

## Common Pitfalls

### Pitfall 1: Model-bound webhook body
**What goes wrong:** Signature verification always fails.  
**Why:** JSON re-serialization changes bytes.  
**How to avoid:** Raw `Request.Body` only; integration test with signed payload.  
**Warning signs:** 400s from Stripe CLI despite correct `whsec_`.

### Pitfall 2: Stock decrement without compensating Stripe failure
**What goes wrong:** Pending order holds stock forever after Stripe API error.  
**Why:** Pattern commits DB before Stripe call.  
**How to avoid:** On Stripe failure, restore stock via `Stock += qty` conditional updates and set `Order.Status = Failed`.  
**Warning signs:** Stock drops without a corresponding Fulfilled/Pending-with-session order.

### Pitfall 3: InMemory concurrency “proof”
**What goes wrong:** Green tests that don't prove SHOP-04.  
**Why:** `ExecuteUpdateAsync` / real row locking need SQL Server.  
**How to avoid:** `SqlServerWebApplicationFactory` like `ConcurrencyTests`.  
**Warning signs:** Test project only references InMemory for Orders.

### Pitfall 4: Fulfilling on redirect
**What goes wrong:** Unpaid/abandoned sessions marked Fulfilled; or paid orders never Fulfilled if user closes tab.  
**Why:** success_url is not reliable.  
**How to avoid:** Webhook-only status flip; success page reads order status.  
**Warning signs:** Controller action on `/checkout/success` that writes Fulfilled.

### Pitfall 5: Mixing CLI and Dashboard webhook secrets
**What goes wrong:** Local verification fails.  
**How to avoid:** Document which secret is in user-secrets for which environment.

### Pitfall 6: Abandoned Pending orders hold stock
**What goes wrong:** Users who never pay permanently reduce catalog stock (CONTEXT locks decrement-at-order-creation).  
**How to avoid:** Accept as MVP limitation; optional later job to expire Pending > N hours and restore stock (not in phase scope unless planner adds thin TTL).  
**Warning signs:** Support reports “in stock but checkout 409s” with many Pending rows.

### Pitfall 7: `Result.ConflictError` signature mismatch
**What goes wrong:** Compile error or wrong 409 mapping.  
**How to avoid:** Add message-only overload early (Wave 0 / Plan 01).

## Code Examples

### Webhook signature verification (.NET)
```csharp
// Source: docs.stripe.com/webhooks?lang=dotnet
var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
try
{
    var stripeEvent = EventUtility.ConstructEvent(
        json,
        Request.Headers["Stripe-Signature"],
        endpointSecret);
    // handle EventTypes.CheckoutSessionCompleted
    return Ok();
}
catch (StripeException)
{
    return BadRequest();
}
```

### Conditional stock update (EF Core)
```csharp
// Source: learn.microsoft.com/ef/core/saving/execute-insert-update-delete
var numUpdated = await context.Products
    .Where(p => p.Id == id && p.Stock >= qty)
    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - qty));
if (numUpdated == 0) { /* insufficient / raced → 409 */ }
```

### Concurrent checkout proof shape (mirror Phase 2)
```csharp
// Source: repo ConcurrencyTests.cs pattern
public class StockConcurrencyTests : IClassFixture<SqlServerWebApplicationFactory>
{
    [Fact]
    public async Task TwoSimultaneousCheckouts_LastUnit_ExactlyOneSuccessAndOne409()
    {
        // Seed product Stock = 1; fire two parallel POST /api/orders/checkout qty=1
        // Assert status codes {200|201|302-ish success, 409} and final Stock == 0
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Trust client totals | Server recomputes from catalog + Stripe `price_data` | PCI/fraud baseline | SHOP-03 |
| Fulfill on redirect | Webhook-first fulfillment | Stripe Checkout guidance | SHOP-05 |
| Check-then-update stock | Single conditional UPDATE | SQL concurrency practice | SHOP-04 |
| Custom card fields | Hosted Checkout Session | Stripe Checkout | SHOP-02 |

**Deprecated/outdated:**
- Relying solely on success_url for fulfillment — Stripe documents this as unreliable.
- Stripe.net major versions move monthly — pin exact version in csproj.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Stripe.net 52.x TFM resolution on net10.0 is seamless | Standard Stack | Package restore/build failure — verify at first plan task |
| A2 | Whole-dollar catalog prices → `(long)(price * 100)` is safe (no fractional cents) | Pattern 3 | Rounding bugs if catalog later allows decimals |
| A3 | MVP accepts only immediate card payments (`payment_status == paid`) | Pattern 3 | Delayed methods would need async_payment_succeeded |
| A4 | Linux codespaces without LocalDB can still run SHOP-04 against Azure SQL / Docker SQL with connection override | Environment / Validation | Concurrency tests skipped or red in this environment |
| A5 | Abandoned Pending stock hold is acceptable MVP tradeoff | Pitfall 6 | Inventory leakage until Phase 8 cleanup |

**If empty:** N/A — table above lists assumptions needing confirmation.

## Open Questions (RESOLVED)

1. **Stock restore on Stripe session failure / abandoned Pending**
   - What we know: CONTEXT locks decrement at order creation.
   - What's unclear: whether planner must ship compensating restore + optional Pending TTL.
   - Recommendation: **Must** compensate on Stripe create failure; TTL cleanup optional/deferred with explicit note in PLAN risks.
   - RESOLVED: Compensate on Stripe create failure (restore stock + Order.Status=Failed) per Plan 03; no Pending TTL in this phase.

2. **Feature folder split vs single Checkout feature**
   - Recommendation: `Carts` + `Orders` + `Payments` (three folders) — clearest PLAT-01 boundaries.
   - RESOLVED: Three folders — `Features/Carts/`, `Features/Orders/`, `Features/Payments/` (Plans 01–05).

3. **SHOP-07 recommendation source when join is empty**
   - UI-SPEC allows same-category fallback; prefer `ServiceRecommendedProduct` first.
   - Recommendation: join-based endpoint; omit chips when empty (UI-SPEC).
   - RESOLVED: Join-only chips via `ServiceRecommendedProduct`; omit section when empty (Plan 04) — no same-category fallback.

4. **Email capture**
   - UI-SPEC: Email on `/checkout` prefill Stripe `customer_email`, or collect only on Stripe.
   - Recommendation: collect Email on `/checkout` (required) so Order has contact even before webhook.
   - RESOLVED: Email required on `/checkout` (Plan 04 Zod + Plan 03 CheckoutRequestDto validator); prefills Stripe `customer_email`.

5. **Where to add Stripe.net package (Shared vs Api)**
   - Recommendation: Shared if `StripePaymentProvider` lives there; keep webhook controller in Api.
   - RESOLVED: Stripe.net 52.2.0 on Shared (`StripePaymentProvider` lives there); webhook controller stays in Api (Plan 05).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build/test | ✓ | 10.0.200 | — |
| Node / npm | landing-page | ✓ | Node 24.14 / npm 11.9 | — |
| Stripe.net package | SHOP-02/05 | ✓ on nuget.org | 52.2.0 stable | — |
| Stripe CLI | SHOP-05 local UAT | ✗ | — | Human-verify install; webhook unit tests use synthetic signed payloads without CLI |
| SQL Server LocalDB | SHOP-04 concurrency / SqlServerWebApplicationFactory | ✗ in this Linux codespace (`sqllocaldb`/`sqlcmd` missing) | — | Azure SQL via `ConnectionStrings__DefaultConnection` **or** Docker SQL Server; factory currently hardcodes LocalDB — planner may need test factory connection override for Linux |
| RESEND_API_KEY / Jwt:SigningKey | `dotnet test` host boot (D-12) | env-dependent | — | Required for full suite / SqlServer factory like existing tests |
| Root CI `dotnet test` | Continuous verification | ✗ CI workflow has no dotnet job | — | Local/full-suite sampling; Phase 8 may add CI — not blocking Phase 6 if local Nyquist holds |

**Missing dependencies with no fallback:**
- None that block coding; SHOP-04 automated proof needs *some* real SQL Server reachable by tests.

**Missing dependencies with fallback:**
- Stripe CLI → synthetic webhook signature unit tests + human UAT when CLI available
- LocalDB on Linux → Azure SQL / Docker SQL + factory override

## Validation Architecture

> Nyquist validation enabled (`workflow.nyquist_validation: true` in `.planning/config.json`).

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + `Microsoft.AspNetCore.Mvc.Testing` 10.0.9; EF InMemory 10.0.9 for service/unit tests; real SQL via `SqlServerWebApplicationFactory` for stock concurrency (SHOP-04) — mirrors `ConcurrencyTests` `[VERIFIED: repo — ZachHairStudio.Api.Tests.csproj]` |
| Config file | `API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` (+ factories `CustomWebApplicationFactory.cs`, `SqlServerWebApplicationFactory.cs`) |
| Quick run command | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Carts\|FullyQualifiedName~Orders\|FullyQualifiedName~Stripe\|FullyQualifiedName~StockConcurrency\|FullyQualifiedName~Checkout"` |
| Full suite command | `dotnet test API/ZachHairStudio.slnx` |

**Environment note (Linux codespace):** `SqlServerWebApplicationFactory` currently hardcodes LocalDB (`Server=(localdb)\\MSSQLLocalDB;...`). LocalDB / `sqllocaldb` / `sqlcmd` are unavailable here — SHOP-04 automation needs Azure SQL or Docker SQL Server plus a connection-string override on the factory (see Environment Availability). InMemory must never be used for the SHOP-04 proof (`ExecuteUpdateAsync` is relational-only).

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SHOP-01 | Add/review cart lines keyed by session (`X-Cart-Session-Id`); cart DTOs carry ProductId+qty only | unit + integration | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Carts"` | ❌ Wave 0 |
| SHOP-02 | Checkout creates Pending order + returns Stripe Checkout URL via `IPaymentProvider` (fake in CI) | integration | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~Orders\|FullyQualifiedName~Checkout"` | ❌ Wave 0 |
| SHOP-03 | Server recomputes line/total from catalog by ProductId; client-submitted price/total ignored | unit (+ integration assert charged total) | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~PriceAuthority\|FullyQualifiedName~OrdersService"` | ❌ Wave 0 |
| SHOP-04 | Concurrent checkout on last unit → exactly one success and one 409; final Stock == 0; uses `ExecuteUpdateAsync` on real SQL | integration (**SqlServerWebApplicationFactory**, not InMemory) | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~StockConcurrencyTests"` | ❌ Wave 0 |
| SHOP-05 | Bad/missing Stripe-Signature → 400; verified `checkout.session.completed` + `payment_status=paid` → Fulfilled; success_url path never fulfills; idempotent re-delivery | unit + integration (synthetic signed payload; CLI optional for UAT) | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~StripeWebhook\|FullyQualifiedName~MarkFulfilled"` | ❌ Wave 0 |
| SHOP-06 | Guest checkout: `Order.ClientId` is null; no auth required on cart/checkout | unit + integration | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~GuestCheckout\|FullyQualifiedName~OrdersService"` | ❌ Wave 0 |
| SHOP-07 | Recommended add-ons for checkout reuse `ServiceRecommendedProduct`; excludes in-cart products; empty → omit | unit (+ optional API) | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName~RecommendedForCheckout\|FullyQualifiedName~GetRecommendedForCheckout"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** Quick filter above (Carts/Orders/Stripe/StockConcurrency/Checkout subset) — prefer InMemory-backed tests when iterating; run `StockConcurrencyTests` only when touching the decrement transaction
- **Per wave merge:** `dotnet test API/ZachHairStudio.slnx` (includes SqlServer-backed concurrency when SQL is reachable)
- **Phase gate:** Full suite green before `/gsd-verify-work`; SHOP-05 also needs human Stripe CLI UAT when CLI is available (`stripe listen` + test card) — synthetic signature tests cover the automated gate

### Wave 0 Gaps

- [ ] `API/ZachHairStudio.Api.Tests/Features/Carts/CartsServiceTests.cs` (+ controller tests as needed) — SHOP-01 session-keyed cart CRUD
- [ ] `API/ZachHairStudio.Api.Tests/Features/Orders/OrdersServiceTests.cs` — SHOP-02/03/06 (fake `IPaymentProvider`, price-authority cases, null `ClientId`)
- [ ] `API/ZachHairStudio.Api.Tests/Features/Orders/StockConcurrencyTests.cs` — SHOP-04; `IClassFixture<SqlServerWebApplicationFactory>`; mirror `ConcurrencyTests` two-parallel-POST shape
- [ ] `API/ZachHairStudio.Api.Tests/Features/Payments/StripeWebhookTests.cs` (or `Orders/MarkFulfilledTests.cs`) — SHOP-05 signature reject + fulfill-once + no fulfill from redirect helper
- [ ] Recommended-for-checkout tests under Products/Services — SHOP-07 join reuse
- [ ] Message-only `Result<T>.ConflictError` overload (or DuplicateRecord→409 mapping) so stock 409 compiles — Pitfall 7
- [ ] Linux/CI: `SqlServerWebApplicationFactory` connection override (Azure SQL / Docker) — LocalDB unavailable in this codespace; without it SHOP-04 cannot run here
- [ ] Secrets for host boot: `RESEND_API_KEY`, `Jwt:SigningKey`, plus test `Stripe:WebhookSecret` for ConstructEvent cases (user-secrets/env — never tracked)
- [ ] No new test framework install — xUnit + Mvc.Testing + SqlServer/InMemory already present
- [ ] No frontend Wave 0 gap — `landing-page` has no test script (prior-phase precedent); cart/checkout UX via UAT / UI-SPEC

## Security Domain

> ASVS Level 1 (`workflow.security_enforcement: true`, `security_asvs_level: 1`).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | No | Guest cart/checkout intentionally unauthenticated (SHOP-06); staff JWT not on these endpoints |
| V3 Session Management | Partial | Cart session is a nonce (`X-Cart-Session-Id` + `localStorage`), **not** an auth session — no cookie/credentials; IDOR hardening deferred to Phase 7 (CONTEXT) |
| V4 Access Control | Partial | No account ownership boundary this phase; webhook endpoint `[AllowAnonymous]` but gated by Stripe signature (SHOP-05); success_url must not mutate order status |
| V5 Input Validation | Yes | FluentValidation on cart/checkout DTOs (ProductId + quantity bounds only — **no price/total fields**); PLAT-02 assembly scan |
| V6 Cryptography | Yes (verify, don't invent) | Stripe webhook HMAC via `EventUtility.ConstructEvent` only — never hand-rolled HMAC; secrets `Stripe:SecretKey` / `Stripe:WebhookSecret` via user-secrets/env (D-13), never tracked files; gitleaks already wired |
| V7 Error Handling & Logging | Yes | Map insufficient stock → clean 409 ProblemDetails (no SQL/stack leak); Stripe failures → compensate + safe client error |
| V12 API/WebService | Yes | Server price authority (SHOP-03); atomic stock UPDATE (SHOP-04); raw-body webhook; fulfill only on verified paid webhook (SHOP-05) |

### Known Threat Patterns for cart / checkout / Stripe webhook

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Client tampers unit price / order total in JSON | Tampering | Ignore client money fields; recompute from `Products.Price` by ProductId; Stripe `price_data` from server snapshots (SHOP-03) |
| Concurrent checkout oversells last unit | Tampering / Elevation | Conditional `ExecuteUpdateAsync` `WHERE Stock >= qty` in same transaction; prove with SqlServer concurrency test (SHOP-04) |
| Spoofed webhook marks unpaid order Fulfilled | Spoofing / Tampering | Raw body + `EventUtility.ConstructEvent` + `WebhookSecret`; reject bad sig with 400; require `payment_status == paid` (SHOP-05) |
| Fulfillment from success_url / client poll write | Spoofing | Webhook-only `MarkFulfilledAsync`; success page is display/poll only |
| Replay / duplicate Stripe events double-fulfill | Tampering | Idempotent Pending→Fulfilled; filtered unique index on `Order.StripeSessionId` |
| Secret leakage (Stripe keys in repo/appsettings) | Information Disclosure | user-secrets/env only + gitleaks; ValidateOnStart recommended for Stripe options |
| Cart session IDOR (guess another `SessionKey`) | Information Disclosure | Accept as Phase 6 MVP (nonce obscurity); harden with accounts in Phase 7 — do not block guest checkout |
| Mass assignment of `ClientId` / `Status` / `StripeSessionId` on create DTOs | Tampering | Server-owned fields only on entities; checkout DTO excludes status/session/client id |
| SQL injection via product/cart filters | Tampering | EF parameterized queries / LINQ only — no raw string SQL for user input |
