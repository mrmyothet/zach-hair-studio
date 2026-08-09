# Phase 6: Cart & Checkout - Context

**Gathered:** 2026-08-09
**Status:** Ready for planning
**Mode:** Smart discuss (autonomous)

<domain>
## Phase Boundary

Client adds recommended products to a cart, reviews it on a cart page, and checks out as a guest through Stripe — no account required (`Order.ClientId` nullable). Order totals are always recomputed server-side from the product catalog; tampered client-submitted price/total has no effect on the amount charged. Concurrent checkout against the last unit of a product results in exactly one successful order; stock never goes negative. An order is marked fulfilled only after a verified Stripe webhook fires, never from the client redirect alone. Stylist-recommended add-ons surface on the service detail page and again at checkout.

</domain>

<decisions>
## Implementation Decisions

### Payment Provider
- Stripe direct integration via Stripe.net (server-side only) — Checkout Session creation + webhook signature verification for fulfillment.
- `IPaymentProvider` interface keeps the provider swappable; implementation behind it is Stripe.
- Lowest fees and deepest first-party ASP.NET/Next.js integration for a single-jurisdiction physical-goods retailer (per research/SUMMARY.md).

### Cart Architecture
- Cart/CartItem as ephemeral DB tables keyed by a client session identifier (no account).
- Order/OrderItem as immutable snapshot tables created at checkout.
- Two separate tables, not one status-flagged table (per research).

### Stock Concurrency
- Atomic conditional UPDATE on Order creation: `UPDATE Products SET Stock = Stock - @qty WHERE Id = @id AND Stock >= @qty`; 0 affected rows = insufficient stock → 409.
- Exactly-one-winner under concurrent checkout against the last unit (mirrors Phase 2's AppointmentSlot unique-index guarantee).
- Server-side total always recomputed from catalog prices by ProductId; never trusts client-submitted price/total (SHOP-03).

### Checkout Flow
- Guest checkout via Stripe Checkout Session; `Order.ClientId` nullable (SHOP-06).
- Order created with Pending status; Stripe webhook (signature-verified) flips to Fulfilled (SHOP-05) — client redirect alone never fulfills.
- SHOP-07: stylist-recommended add-ons rendered as suggestion chips on the cart page; user adds before checkout.

### Claude's Discretion
- Exact Stripe mode (test mode vs live), webhook endpoint path, idempotency-key strategy, and cart session-key mechanism (cookie vs header) — follow codebase conventions and research guidance.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Features/Products/` (Phase 5) — Product entity, `ProductsService`, `ProductResponseDto`, `Price`/`Stock` fields, seeded catalog.
- `Features/Services/` — `ServicesService` extended with RecommendedProducts join (Phase 5).
- `Features/Bookings/` (Phase 2) — atomic-slot reservation pattern (unique index guarantee) to mirror for stock.
- Landing-page `lib/products.ts` — Zod-validated fetch layer; `lib/data.ts` — nav links.

### Established Patterns
- Feature folders on backend (`Features/<Name>/`), service layer owns all DbContext access (PLAT-01).
- Services/Controllers with DTOs + FluentValidation validators (PLAT-02).
- OpenAPI is source of truth for frontend clients; landing-page uses hand-written `lib/` fetch (no generated client yet).
- EF Core migrations via `dotnet-ef` v10; `HasData` seeding.

### Integration Points
- `BookingDbContext` — add Cart/Order DbSets + config.
- `ProductsService`/`Products` — price/stock source of truth for totals and decrement.
- Landing-page nav — cart entry point alongside Products nav link (D-04).
- Stripe — Checkout Session API + webhook endpoint.

</code_context>

<specifics>
## Specific Ideas

- Research flags Phase 6 as highest external-integration risk after Phase 2 — a focused research pass (Stripe.net, webhook-verified fulfillment, idempotency, atomic stock decrement) precedes planning per ROADMAP research flag.
- Payment provider decision (Stripe direct) resolved in this discuss; log to PROJECT.md Key Decisions on transition.

</specifics>

<deferred>
## Deferred Ideas

- Accounts/loyalty (Phase 7) — guest checkout intentionally independent (`Order.ClientId` nullable per roadmap decision).
- Deposit/cancellation policy at booking time — flagged as near-term add-on once payment provider exists (research flag).
- IDOR hardening for client account order lookups — Phase 7 concern, guest checkout must not block on it.

</deferred>
