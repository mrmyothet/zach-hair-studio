# Phase 6: Cart & Checkout - Research

**Researched:** 2026-08-09
**Domain:** Guest cart + server-authoritative checkout, Stripe direct integration (Checkout Session + webhook-verified fulfillment), atomic stock decrement, EF Core entity design
**Confidence:** MEDIUM

## Summary

Phase 6 is the highest external-integration-risk phase after Phase 2: it introduces the payment provider (Stripe), the second read-then-write race (stock), and the price-authority boundary. The good news is that every *internal* pattern this phase needs already exists and shipped: the Phase 2 booking path proves the "single implicit transaction / unique-index guarantee / best-effort-after-commit email" shape; Phase 4 proves the `Database.CreateExecutionStrategy().ExecuteAsync` + explicit transaction wrapper that the atomic stock decrement needs under `EnableRetryOnFailure`; the `Result<T>` + FluentValidation + controller-mapping shape is identical across every feature. What is genuinely new is the Stripe wire contract: `SessionCreateOptions` → `Session.Url` redirect, `EventUtility.ConstructEvent` signature verification, and idempotent webhook fulfillment keyed on the `checkout.session.completed` event.

The design is fully constrained by CONTEXT.md's locked decisions and the ROADMAP research flag: Cart/CartItem and Order/OrderItem are two separate tables (ephemeral vs immutable snapshot); the stock guarantee is the atomic conditional UPDATE (`UPDATE Products SET Stock = Stock - @qty WHERE Id = @id AND Stock >= @qty`) in the same transaction as order creation, returning 0 affected rows → 409; totals are recomputed server-side from the catalog by ProductId, never trusted from the client; fulfillment only ever happens from a signature-verified Stripe webhook, never the client redirect; `Order.ClientId` is nullable; recommended add-ons surface on the cart page. The three genuine open research calls are: the guest cart session-key mechanism (header vs cookie — this repo's `AllowAnyOrigin` CORS rules out the cookie path), the `IPaymentProvider` seam shape, and which Stripe session parameter carries the order linkage back to the webhook (`client_reference_id`/`metadata`).

**Primary recommendation:** Implement `Features/Orders/` and `Features/Carts/` (or a single `Features/Checkout/` feature — see Open Questions) mirroring the `Features/Bookings/` service-layer shape; add `Cart`, `CartItem`, `Order`, `OrderItem` to `BookingDbContext` with the immutable-snapshot pattern (OrderItem copies price/name at checkout); decrement stock via `ExecuteUpdateAsync` with a `WHERE Stock >= qty` guard inside `Database.CreateExecutionStrategy().ExecuteAsync` + explicit transaction, all in the same transaction as order creation; add `Stripe.net` behind a small `IPaymentProvider` interface; create the Checkout Session server-side with `price_data` (never client prices), redirect the client to `Session.Url`; handle `checkout.session.completed` in a raw-body webhook endpoint using `EventUtility.ConstructEvent`, check `payment_status == "paid"`, and flip the order Pending→Fulfilled idempotently.

**Highest-risk items the plan must treat as first-class tasks:** (1) the transaction + retry-strategy wrapper for the atomic decrement (Phase 4 precedent, not Phase 2's single-`SaveChanges` shape), (2) raw-body webhook reading (model binding silently breaks signature verification), (3) webhook idempotency (Stripe retries non-2xx for up to 3 days; a unique index on `Order.StripeSessionId` is the duplicate guard), and (4) Stripe test-mode local development via the Stripe CLI (`stripe listen`), which the environment does not yet have installed.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Cart/CartItem persistence (ephemeral, session-keyed) | Database / Storage | API / Backend | DB tables keyed by a client session identifier per CONTEXT.md decision; `CartsService` owns all `BookingDbContext` access (PLAT-01) |
| Order/OrderItem immutable snapshot creation | API / Backend | Database / Storage | `OrdersService.CreateAsync` recomputes totals server-side from the catalog (SHOP-03) and writes the snapshot in one transaction |
| Atomic stock decrement | Database / Storage | — | The conditional `UPDATE ... WHERE Stock >= @qty` is a single SQL statement — only the DB can make the exactly-one-winner guarantee (SHOP-04) |
| Checkout Session creation | API / Backend | — | Server-side only, via `IPaymentProvider` → Stripe.net; prices come from `price_data` derived from the DB catalog, never the client |
| Webhook signature verification + fulfillment | API / Backend | — | `EventUtility.ConstructEvent` in a raw-body endpoint; only a verified `checkout.session.completed` flips the order to Fulfilled (SHOP-05) |
| Guest cart session-key issuance/validation | API / Backend | — | Server generates and accepts the session id; client stores it (header mechanism — see Pattern 4) |
| Cart page / checkout UX + add-on chips | Frontend Server (SSR) | — | RSC pages + a client cart-state layer; add-ons render as suggestion chips per SHOP-07 |
| Recommended-add-on lookup at checkout | API / Backend | Database / Storage | Reuses `ServiceRecommendedProduct` join (PROD-03) — surfaced again at checkout (SHOP-07) |
| Stripe CLI local webhook forwarding | Developer tooling | — | `stripe listen --forward-to` is the only way to receive real `checkout.session.completed` events locally |

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
| SHOP-01 | Client can add products to a cart and review it | Cart/CartItem entity shape (Pattern 1), session-key mechanism (Pattern 4), cart page (Pattern 5) |
| SHOP-02 | Client can check out and pay through an integrated payment provider | `IPaymentProvider` seam (Pattern 2), Stripe.net Checkout Session creation (Pattern 3), Stripe CLI local flow (Pattern 6) |
| SHOP-03 | Order total is computed server-side from the catalog; client-supplied prices are never trusted | `OrdersService.CreateAsync` recomputes totals by ProductId (Pattern 1); `price_data` from catalog prices; OrderItem snapshot stores the authoritative amounts |
| SHOP-04 | Product stock is decremented atomically on order creation, with no overselling under concurrent checkout | Atomic conditional `ExecuteUpdateAsync` with `WHERE Stock >= qty` in the same transaction (Pattern 1 + Pattern 7) |
| SHOP-05 | Order fulfillment is confirmed only via a verified payment webhook, not the client redirect | Raw-body webhook endpoint + `EventUtility.ConstructEvent` (Pattern 3), `payment_status == "paid"` check, idempotent flip via unique `StripeSessionId` index (Pattern 8) |
| SHOP-06 | Guest checkout works without an account (`Order.ClientId` nullable) | `Order.ClientId` nullable int, no auth requirement on cart/checkout endpoints |
| SHOP-07 | Stylist-recommended add-ons are surfaced at checkout | Reuses the Phase 5 `ServiceRecommendedProduct` join; cart page renders suggestion chips (Pattern 5) |
</phase_requirements>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Stripe.net | 52.2.0 (2026-07-29) | Checkout Session creation + webhook signature verification | Official Stripe .NET SDK; owner `stripe`, 95.5M downloads, repo github.com/stripe/stripe-dotnet. This is the locked decision from CONTEXT.md. `[VERIFIED: nuget.org — Stripe.net package page, owner stripe, version 52.2.0, released 2026-07-29]` |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 | `ExecuteUpdateAsync` atomic stock decrement + transactional order creation | Already pinned in repo `[VERIFIED: repo file — ZachHairStudio.Shared.csproj]` |
| FluentValidation | 12.1.1 | Cart/checkout DTO validators | Existing PLAT-02 layer, auto-registered by the existing assembly scan — no `Program.cs` change needed `[VERIFIED: repo file — Program.cs line 49]` |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | Unit tests for services (NOT for the atomic decrement — InMemory does not execute SQL) | Already pinned in the test project `[VERIFIED: repo file — ZachHairStudio.Api.Tests.csproj]` |
| zod | 4.4.3 | Response validation in the landing-page cart/checkout fetch layer | Already the frontend convention `[VERIFIED: repo file — landing-page/package.json]` |

**Target-framework note (critical):** Stripe.net 52.2.0 ships `netstandard2.0`, `net6.0`, `net8.0`, `net9.0`, `net462` targets — **no `net10.0` target**. This is safe: .NET 10 apps can reference libraries targeting older runtimes (official .NET versioning doc: "An app that's upgraded to a newer major .NET Runtime version can reference libraries and NuGet packages that target older .NET Runtime versions"). A `net10.0` consumer resolves the package's `net8.0` or `net9.0` target. Verify at `dotnet add package` time that NuGet resolves cleanly on net10.0 (it will pick the nearest compatible TFM), and let the first `dotnet build API/ZachHairStudio.slnx` be the proof. `[VERIFIED: nuget.org Stripe.net page — TFM list; CITED: learn.microsoft.com/en-us/dotnet/core/versions — runtime compatibility]`

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Stripe CLI | latest (not installed) | Local webhook forwarding: `stripe listen --forward-to localhost:5236/...`; test events via `stripe trigger checkout.session.completed`; prints the `whsec_...` signing secret | Required for any manual/end-to-end verification of SHOP-05 fulfillment in dev — a human-verify checkpoint should include installing it `[CITED: docs.stripe.com/cli/listen]` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Stripe.net (direct) | Paddle / Lemon Squeezy (merchant-of-record) | MoR providers charge more and add a middleman; the project research (research/SUMMARY.md) already locked Stripe direct — not a live alternative this phase |
| `IPaymentProvider` interface | Direct `StripeClient` in `OrdersService` | CONTEXT.md locks the interface; it also makes the Stripe session-creation call unit-testable with a fake (no live Stripe API in tests — the test suite has no Stripe keys) |
| Header `X-Cart-Session-Id` for guest cart | Server-set HttpOnly cookie | The cookie path requires `AllowCredentials()` + a specific CORS origin; this repo's `AllowAnyOrigin` default (Phase 8 tightens it) is incompatible with credentialed cookies. The header keeps `AllowAnyOrigin` working and the session id is a non-sensitive random value (Pattern 4) |
| `ExecuteUpdateAsync` for the stock decrement | Raw `ExecuteSqlInterpolated($"UPDATE Products SET Stock = Stock - {qty} WHERE Id = {id} AND Stock >= {qty}")` | Both emit one atomic SQL statement. `ExecuteUpdateAsync` is the typed EF Core API (no string SQL, no provider-escape risk) and returns affected-row count; prefer it. `ExecuteSqlInterpolated` is the fallback if the plan needs to prove the exact `@qty` parameter shape |
| `price_data` on Session line items | Pre-created Stripe Price objects (`price_...`) | `price_data` prices from the external DB catalog directly — exactly this project's "server recomputes from catalog" model (SHOP-03); Stripe Price objects would require syncing the catalog to Stripe, an extra moving part with zero benefit for a small catalog |
| `Order.ClientId` nullable int | No client column at all | CONTEXT.md locks nullable for Phase 7 account linkage; keep it |

**Installation:**
```bash
cd API/ZachHairStudio.Api
dotnet add package Stripe.net   # resolves latest stable 52.2.0 on net10.0
```

**Version verification:** Verified against nuget.org (2026-08-09): `Stripe.net` 52.2.0, released 2026-07-29, owner `stripe`. Stripe.net releases monthly — re-check the latest stable at plan-execution time, but pin to a specific version in the `.csproj` (e.g. `Version="52.2.0"`) to keep the lockfile honest.

## Package Legitimacy Audit

> NuGet is not one of the seam's supported ecosystems (npm/pypi/crates), so the automated seam check could not run for `Stripe.net`. Manual verification performed against nuget.org instead.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| Stripe.net | NuGet | 11+ yrs (first published 2014) | 95.5M total / 1.2M per day | github.com/stripe/stripe-dotnet (owner `stripe`) | OK | Approved |

**Verification method:** nuget.org package page (owner `stripe`, version 52.2.0, released 2026-07-29, license link to GitHub stripe/stripe-dotnet, 95.5M downloads). This is the same package the official Stripe quickstart documents (`dotnet add package Stripe.net`), so it is the authoritative package, not a slopsquat. `[VERIFIED: nuget.org]`

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

*Note: the seam's `package-legitimacy check --ecosystem npm stripe` returned `SUS` ("too-new") for the npm `stripe` package — this is a false positive of the age heuristic on Stripe's monthly-republished official Node SDK (17.5M weekly downloads, repo stripe/stripe-node) and is not used this phase (this is a server-side Stripe.net integration; no client-side `@stripe/stripe-js` is needed for the hosted Checkout flow).*

## Architecture Patterns

### System Architecture Diagram

```text
Client browser (landing-page)
   │
   ├── POST /api/carts/{sessionId}/items  ───────────► CartsController ──► CartsService (Cart/CartItem upsert, PLAT-01)
   │                                                    ▲ server reads product price/stock from catalog, ignores client price
   │ GET /api/carts/{sessionId} ────────────────────────┘
   │
   ├── GET /api/products/recommended-for-checkout (or reuse service detail join) ──► ProductsService / ServicesService
   │                                                    │  ServiceRecommendedProduct join → active products (SHOP-07 chips)
   │                                                    ▼
   │                                    BookingDbContext.ServiceRecommendedProducts / Products
   │
   ├── POST /api/orders/checkout  { sessionId, lineItems[], email? } ──► OrdersController ──► OrdersService.CreateCheckoutSessionAsync
   │                                                    │  1. Load Products by ProductId; recompute totals server-side (SHOP-03)
   │                                                    │  2. Transaction (CreateExecutionStrategy + BeginTransaction):
   │                                                    │       a. conditional UPDATE Products SET Stock = Stock - qty
   │                                                    │            WHERE Id = @id AND Stock >= @qty   ← atomic, SHOP-04
   │                                                    │         0 rows → rollback → 409 Conflict
   │                                                    │       b. INSERT Order (Pending) + OrderItem snapshot rows
   │                                                    │       c. SaveChanges + commit
   │                                                    │  3. Stripe Checkout Session via IPaymentProvider (Pattern 2/3):
   │                                                    │       price_data from catalog prices, client_reference_id = orderId
   │                                                    │  4. Return { checkoutUrl: Session.Url } → client 303-redirects
   │                                                    ▼
   │                                          Stripe Checkout (hosted payment page)
   │                                                    │
   │  success_url redirect (NEVER fulfills) ◄───────────┤  customer pays → Stripe fires checkout.session.completed
   │                                                    ▼
   │                                    Stripe → POST /api/stripe/webhook (raw body + Stripe-Signature)
   │                                                    │  EventUtility.ConstructEvent → 400 on signature failure
   │                                                    │  type == checkout.session.completed && payment_status == "paid"
   │                                                    ▼
   │                                    OrdersService.MarkFulfilledAsync(sessionId)
   │                                                    │  unique index on Order.StripeSessionId = idempotency guard
   │                                                    │  Order: Pending → Fulfilled (SHOP-05) — stock already decremented
   │                                                    ▼
   │                                          BookingDbContext.Orders
```

**File-to-implementation mapping** (diagram shows data flow only):

| Diagram element | Implementation |
|-----------------|----------------|
| CartsService | `API/ZachHairStudio.Shared/Features/Carts/CartsService.cs` |
| OrdersService | `API/ZachHairStudio.Shared/Features/Orders/OrdersService.cs` |
| IPaymentProvider / StripePaymentProvider | `API/ZachHairStudio.Shared/Features/Payments/IPaymentProvider.cs`, `StripePaymentProvider.cs` |
| Webhook endpoint | `API/ZachHairStudio.Api/Controllers/StripeWebhookController.cs` |
| Stripe session creation options | Pattern 3 below |

### Recommended Project Structure

```
API/ZachHairStudio.Shared/Features/Carts/
├── Cart.cs                    # ephemeral cart: Id, SessionKey (unique index), CreatedAtUtc
├── CartItem.cs                # CartId FK, ProductId, Quantity (1..stock, no price stored)
├── CartResponseDto.cs / CartItemResponseDto.cs
├── CartItemCreateDto.cs + Validator.cs   # productId + quantity only — NO price/total field
├── CartExtensions.cs          # entity ⇄ DTO
└── CartsService.cs            # all Cart/CartItem DbContext access (PLAT-01)

API/ZachHairStudio.Shared/Features/Orders/
├── Order.cs                   # immutable snapshot header: ClientId (nullable), Status, StripeSessionId, StripeSessionUrl,
│                              #   TotalAmount (server-recomputed), CustomerEmail?, PlacedAtUtc, FulfilledAtUtc?
├── OrderItem.cs               # immutable snapshot line: OrderId, ProductId, ProductName (copied), UnitPrice (copied),
│                              #   Quantity, LineTotal (= UnitPrice * Quantity, server-computed)
├── OrderStatus.cs             # enum { Pending, Fulfilled, Failed } — string-converted like Appointment.Status
├── OrderResponseDto.cs / OrderItemResponseDto.cs
├── CheckoutRequestDto.cs + Validator.cs   # sessionId + line items (productId, quantity) + optional email
├── OrderExtensions.cs
└── OrdersService.cs           # CreateCheckoutSessionAsync + MarkFulfilledAsync (all Order/OrderItem/Stock access)

API/ZachHairStudio.Shared/Features/Payments/
├── IPaymentProvider.cs        # CreateCheckoutSessionAsync(CheckoutSessionRequest) → CheckoutSessionResult (Url, Id)
└── StripePaymentProvider.cs   # Stripe.net SessionCreateOptions; reads StripeOptions

API/ZachHairStudio.Api/Controllers/
├── CartsController.cs         # GET /api/carts/{sessionKey}, POST /api/carts/{sessionKey}/items, DELETE item
├── CheckoutController.cs      # POST /api/orders/checkout → 303 redirect URL (or 200 { checkoutUrl })
└── StripeWebhookController.cs # POST /api/stripe/webhook — raw body, no [FromBody]

API/ZachHairStudio.Api/Options/  (or alongside)
└── StripeOptions.cs           # SecretKey, WebhookSecret, SuccessUrl, CancelUrl — bound from config, never a tracked file

landing-page/lib/
├── cart.ts                    # Zod schemas + fetch helpers: getCart, addToCart, removeItem, createCheckout
├── checkout.ts                # (optional) thin wrapper returning the Stripe redirect URL
└── cartSession.ts             # session-id generation + localStorage persistence + header attach

landing-page/app/cart/
└── page.tsx                   # cart review page + add-on chips (SHOP-07) + Checkout button (client-side redirect)
```

### Pattern 1: Immutable-snapshot Order/OrderItem + server-recomputed totals (SHOP-03, SHOP-04, SHOP-06)

**What:** `Order` is the immutable header created at checkout; `OrderItem` snapshots the product name and unit price *as they were at purchase time* (a later catalog price edit must not rewrite history). `Cart`/`CartItem` never hold prices — only `ProductId` + quantity. The authoritative total is recomputed by loading `Product` rows from the catalog and multiplying `Price × Quantity`, so a tampered client payload (a forged price or a forged total) is ignored because the client payload carries no price at all.

**When to use:** Every checkout path. This is the one place in the system where money moves — never derive the amount from anything the client sent.

**Example — the checkout transaction (the heart of SHOP-03/SHOP-04):**
```csharp
// Source: pattern synthesized from learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete
// and the repo's Phase 4 AvailabilityService transaction precedent
public async Task<Result<CheckoutResponseDto>> CreateCheckoutAsync(CheckoutRequestDto request)
{
    var validation = await _validator.ValidateAsync(request);
    if (!validation.IsValid) return Result<CheckoutResponseDto>.ValidationError(...);

    var strategy = _dbContext.Database.CreateExecutionStrategy();  // EnableRetryOnFailure-safe
    return await strategy.ExecuteAsync(async () =>
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        // 1. Atomic stock decrement — one SQL UPDATE per line, guarded by Stock >= qty.
        //    0 rows affected = sold out/insufficient stock → rollback + 409.
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
                    $"Sorry, only {product.Stock} left of {product.Name}.");

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,   // snapshot — catalog edits won't rewrite history
                UnitPrice = product.Price,    // snapshot of the authoritative server price
                Quantity = line.Quantity,
                LineTotal = product.Price * line.Quantity,
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.LineTotal);  // server-recomputed (SHOP-03)
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        // 2. After commit: create the Stripe Checkout Session (Pattern 2/3) and return its URL.
        //    Order already exists with Pending status; webhook flips it to Fulfilled (SHOP-05).
        var session = await _paymentProvider.CreateCheckoutSessionAsync(
            new CheckoutSessionRequest(order.Id, order.TotalAmount, request.CustomerEmail,
                order.Items.Select(i => new CheckoutLine(i.ProductName, i.UnitPrice, i.Quantity))));
        return Result<CheckoutResponseDto>.Success(new CheckoutResponseDto { CheckoutUrl = session.Url });
    });
}
```
Key invariants to keep:
- **Stock never goes negative** — the `WHERE Stock >= qty` guard makes the decrement atomic; two concurrent checkouts of the last unit produce exactly one row-update of 1 and one of 0 (SHOP-04). The Phase 2 `AppointmentSlot` unique-index precedent already proved this "exactly-one-winner" shape on real SQL Server.
- **Client payload carries no prices** — `CheckoutRequestDto` has `productId`/`quantity` only. A tampered price/total in the request is structurally impossible (SHOP-03). The Stripe `price_data` derives from the DB prices, not the request.
- **`Order.ClientId` is nullable** (SHOP-06) — no account required; Phase 7 backfills it.
- **Relational-only decrement** — `ExecuteUpdateAsync` does not work on the InMemory provider; the concurrency proof must run against `SqlServerWebApplicationFactory` (real LocalDB), exactly like `ConcurrencyTests`.

### Pattern 2: `IPaymentProvider` seam

**What:** A small interface (locked by CONTEXT.md) that the Stripe implementation sits behind. The seam's value here is twofold: it keeps Stripe.net importable only where it is used, and it lets `OrdersService` be tested with a fake provider (no Stripe keys in CI).

**Example:**
```csharp
namespace ZachHairStudio.Shared.Features.Payments;

public record CheckoutSessionRequest(int OrderId, decimal TotalAmount, string? CustomerEmail,
    IReadOnlyList<CheckoutLine> Lines);
public record CheckoutLine(string ProductName, decimal UnitPrice, int Quantity);
public record CheckoutSessionResult(string SessionId, string Url);

public interface IPaymentProvider
{
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken ct = default);
}
```
The Stripe implementation reads `StripeOptions` (secret key, webhook secret, success/cancel URLs) from config — secrets via `dotnet user-secrets`/env, never a tracked file (same D-13 discipline as `RESEND_API_KEY`/`Jwt:SigningKey`). Register in `Program.cs`: `builder.Services.AddSingleton(sp => new StripeClient(config["Stripe:SecretKey"]))` + `AddScoped<IPaymentProvider, StripePaymentProvider>()` (the Stripe sample registers `new StripeClient(...)` as a singleton; the provider can be scoped like every other service).

### Pattern 3: Stripe Checkout Session creation + webhook verification

**What:** The two Stripe wire contracts. Create the session with `SessionCreateOptions`; verify inbound webhooks with `EventUtility.ConstructEvent` against the raw body.

**When to use:** Every order checkout (session creation) and every fulfillment (webhook).

**Example — server-side session creation (official Stripe.net shape, adapted):**
```csharp
// Source: docs.stripe.com/checkout/quickstart (C# / .NET sample shape)
using Stripe;
using Stripe.Checkout;

var options = new SessionCreateOptions
{
    Mode = "payment",
    SuccessUrl = _options.SuccessUrl + "?session_id={CHECKOUT_SESSION_ID}",
    CancelUrl = _options.CancelUrl,
    ClientReferenceId = request.OrderId.ToString(),   // ← order linkage back from the webhook
    Metadata = new Dictionary<string, string> { ["order_id"] = request.OrderId.ToString() },
    CustomerEmail = request.CustomerEmail,            // guest prefill (SHOP-06)
    LineItems = request.Lines.Select(line => new SessionLineItemOptions
    {
        Quantity = line.Quantity,
        PriceData = new SessionLineItemPriceDataOptions
        {
            Currency = "usd",
            UnitAmountDecimal = line.UnitPrice * 100m,   // minor units, from the server-recomputed price
            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = line.ProductName },
        },
    }).ToList(),
};

var session = await _client.Checkout.Sessions.CreateAsync(options);  // or client.V1.Checkout.Sessions.Create
// → return new CheckoutSessionResult(session.Id, session.Url);  client 303-redirects to session.Url
```
- `price_data` (not `Price = "price_..."`) because the catalog lives in our DB, not Stripe (SHOP-03).
- `UnitAmountDecimal` is in **minor units** (`price * 100`) for `usd`.
- `ClientReferenceId` + `Metadata["order_id"]` both carry the order id so the webhook can find the order without trusting the client.

**Example — webhook endpoint (raw body; must NOT bind a body model):**
```csharp
// Source: docs.stripe.com/webhooks/quickstart (C# shape) + docs.stripe.com/webhooks/signature
[ApiController]
[AllowAnonymous]                     // auth = signature verification, not [Authorize]
[Route("api/stripe/webhook")]
public class StripeWebhookController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        string json;
        using (var reader = new StreamReader(Request.Body))      // raw body — model binding would
            json = await reader.ReadToEndAsync();                // re-serialize JSON and break the signature

        var signatureHeader = Request.Headers["Stripe-Signature"];
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, _options.WebhookSecret);

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session?.PaymentStatus == "paid")            // delayed methods can fire completed while unpaid
                {
                    await _ordersService.MarkFulfilledAsync(session.ClientReferenceId, session.Id);
                }
            }

            return Ok();                                          // 200 fast; Stripe retries non-2xx
        }
        catch (StripeException)
        {
            return BadRequest();                                  // invalid signature/payload → 400
        }
    }
}
```
- **Never `[FromBody]`** — ASP.NET model binding reconstructs the JSON (whitespace/key order), and `ConstructEvent` hashes the exact bytes; any mutation breaks verification `[CITED: docs.stripe.com/webhooks/signature — framework body-mutation list]`.
- **Check `payment_status == "paid"`** before fulfilling — `checkout.session.completed` can fire while payment is still `unpaid` for delayed methods `[CITED: docs.stripe.com/checkout/fulfillment]`.
- The Stripe event `id` itself is NOT persisted as the fulfillment guard; the **order's `StripeSessionId` unique index** is (Pattern 8), because a retry of the same event is idempotent via the index, and a different event for the same session is also blocked.

### Pattern 4: Guest cart session-key mechanism — header, not cookie

**What:** The client's anonymous cart identity. CONTEXT.md leaves cookie-vs-header to Claude's discretion; research rules out the cookie.

**Why header:** A server-set cookie must be read/written with credentials, which requires `Access-Control-Allow-Credentials: true` and a specific CORS origin. This repo's `Program.cs` uses `AllowAnyOrigin()` today (production lockdown is Phase 8, LAUNCH-02). `AllowAnyOrigin` + credentials is a browser error, so the cookie path would force a CORS change this phase for no functional gain. Instead: the server generates a random session id on first cart touch, the client stores it in `localStorage`, and sends it as an `X-Cart-Session-Id` request header on every cart/checkout call. Plain headers work fine under `AllowAnyOrigin`. The id is a random nonce (e.g. `Guid.NewGuid()` or a crypto-random 32-hex), not a personal identifier, so it carries no sensitive data if leaked. The DB enforces `Cart.SessionKey` uniqueness (unique index), mirroring how the system already treats `Slug`/`(StylistId, SlotStart)` unique keys.

**When to use:** All cart/checkout endpoints. Add the header client-side in `landing-page/lib/cartSession.ts` and have the fetch helpers attach it.

### Pattern 5: Cart page + add-on chips (SHOP-01, SHOP-07)

**What:** The cart review page is an RSC that reads `GET /api/carts/{sessionKey}` (fresh, no ISR caching — like `lib/appointments.ts`'s `cache: "no-store"`), renders line items with quantities and unit prices from the server DTO, and shows a "Checkout" button. The Checkout button is a client action: `POST /api/orders/checkout` returns `{ checkoutUrl }`, and the client does `window.location.assign(checkoutUrl)` (Stripe 303s from the server, or the client navigates directly).

**SHOP-07 add-on chips:** The cart page fetches the recommended add-ons for the services whose products are in the cart (or a curated subset — simplest correct version: reuse the Phase 5 `ServiceRecommendedProduct` join and fetch recommended products for services linked to the products already in the cart), renders them as "add to cart" chips alongside the line items. Keep it server-fetched (RSC) so the data is fresh and the recommendation logic stays on the backend. The Phase 5 decision to keep recommendations inside `ServicesService.GetBySlugAsync` was service-detail-scoped; checkout is a different surface, so a small read-only `ProductsService.GetRecommendedForCheckoutAsync(cartProductIds)` (reusing the join table) is the clean seam.

### Pattern 6: Local webhook development with the Stripe CLI

**What:** The only realistic way to receive real `checkout.session.completed` events locally (an endpoint must be publicly reachable for Stripe to deliver; the CLI forwards from Stripe's servers to localhost).

**When to use:** Every manual/end-to-end verification of SHOP-05 in dev (the phase's UAT). This is a **new external dependency** — the environment does not currently have the Stripe CLI installed (checked 2026-08-09). The plan should include a human-verify checkpoint to install it.

```bash
stripe login                                  # one-time, links a Stripe account (test mode)
stripe listen --forward-to localhost:5236/api/stripe/webhook
# → Ready! Your webhook signing secret is whsec_...   (put it in user-secrets as Stripe:WebhookSecret)
stripe trigger checkout.session.completed     # in another terminal — sends a signed test event
```
Test card for the manual checkout flow: `4242 4242 4242 4242` (any future expiry, any 3-digit CVC). The CLI's `whsec_...` secret differs from a Dashboard-registered endpoint's secret — don't mix them. `[CITED: docs.stripe.com/cli/listen, docs.stripe.com/checkout/fulfillment]`

### Pattern 7: Transaction + retry strategy (the one place this repo needs an explicit transaction)

**What:** The atomic decrement and order creation must commit or roll back together. Two subtle EF Core facts make this different from Phase 2's "single `SaveChangesAsync`" shape:
1. `ExecuteUpdateAsync` executes immediately and does **not** participate in the change tracker or in `SaveChangesAsync`'s implicit transaction.
2. The repo enables `EnableRetryOnFailure` on the SQL Server options, which is incompatible with manually-started transactions unless the transaction is created inside `Database.CreateExecutionStrategy().ExecuteAsync(...)` (retry re-executes the whole delegate on a transient failure, including re-opening the transaction).

**When to use:** Exactly one place — `OrdersService.CreateCheckoutAsync`. The repo has one existing precedent: Phase 4's `AvailabilityService` conflict-scan + persist path wraps a manual `BeginTransactionAsync` inside `CreateExecutionStrategy().ExecuteAsync` — reuse that exact shape (STATE.md Phase 4 Plan 05).

### Pattern 8: Idempotent fulfillment — unique index on `Order.StripeSessionId`

**What:** Stripe retries webhook deliveries on non-2xx for up to **3 days** with exponential backoff, and duplicate deliveries are documented. The fulfillment must therefore be idempotent: if the order is already Fulfilled, the handler is a no-op, and two concurrent deliveries cannot double-process.

**When to use:** `MarkFulfilledAsync`. Two guards:
- A **unique index** on `Order.StripeSessionId` (nullable for orders that never reached Stripe — use a *filtered* unique index `WHERE StripeSessionId IS NOT NULL`, or a `HasFilter()`; unlike the `AppointmentSlot` index, this one *should* be filtered). This means the DB itself rejects a second order row for the same Stripe session — the strongest duplicate guard.
- The `MarkFulfilledAsync` method reads the current status and transitions `Pending → Fulfilled` only (mirroring the Phase 3 `AllowedTransitions`-map precedent), so a second call sees `Fulfilled` and no-ops.

Note the contrast with the Phase 2 invariant: the `AppointmentSlot` unique index must be **unfiltered** (it is the double-booking guarantee); the `StripeSessionId` index should be **filtered** because many orders will legitimately have no session yet (Pending, never reached Stripe, failed). Also set `StripeSessionUrl` on the order at session-creation time so the success page and any staff view can link back to Stripe.

### Anti-Patterns to Avoid

- **[FromBody] on the webhook action** — model binding re-serializes the JSON body and breaks Stripe signature verification. Read `Request.Body` raw (Pattern 3). This is the #1 Stripe integration bug in the wild `[CITED: docs.stripe.com/webhooks/signature]`.
- **Fulfilling from the success_url redirect** — the client may never reach the success page (dropped connection); Stripe's docs are explicit that webhooks are required for guaranteed fulfillment. The success page may *show* the order state and even trigger a best-effort poll, but the order must not be marked Fulfilled there (SHOP-05). `[CITED: docs.stripe.com/checkout/fulfillment]`
- **Two-step check-then-decrement for stock** — reading `Stock`, comparing in app code, then updating is the exact race the phase forbids. The conditional UPDATE is one atomic statement (SHOP-04).
- **Trusting any price/total from the client** — `CheckoutRequestDto` structurally omits prices (Pattern 1), so there is nothing to trust. Do not add a `total` or `unitPrice` field to it "for convenience."
- **Trusting the client's cart session key blindly for order retrieval** — a session key is not an authorization boundary (anyone could guess/steal another's key and read their cart). Keep the guest cart surface read-only-for-its-key and non-sensitive (no PII, no prices stored in the cart), matching the deferred-IDOR decision in CONTEXT.md; real ownership enforcement is Phase 7.
- **The InMemory provider for the concurrency proof** — `ExecuteUpdateAsync` and the unique index are relational/SQL-Server behaviors; the SHOP-04 proof must run on `SqlServerWebApplicationFactory` (real LocalDB), exactly like `ConcurrencyTests` does for BOOK-04.
- **Storing client prices in CartItem** — the cart must stay price-less (server reads catalog price at cart-read time and checkout time); storing a price in the cart re-introduces a client-influenced value into the money path.

<!-- gsd:write-continue -->

