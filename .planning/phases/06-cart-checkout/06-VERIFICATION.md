---
phase: 06-cart-checkout
verified: 2026-08-15T00:00:00Z
status: human_needed
score: 4/5 must-haves verified
behavior_unverified: 1
overrides_applied: 0
mode: mvp
behavior_unverified_items:
  - truth: "Concurrent checkout attempts against the last unit of a product result in exactly one successful order; stock never goes negative (SC3 / SHOP-04)"
    test: "On a host with real SQL Server reachable, set TEST_SQLSERVER_CONNECTION (or ConnectionStrings__DefaultConnection) to a live SQL Server instance and run: dotnet test API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj --filter FullyQualifiedName~StockConcurrencyTests"
    expected: "TwoParallelCheckoutsForLastUnit_ExactlyOneSuccessAndOne409 passes: exactly one 2xx, exactly one 409, and Products.Stock for the seeded product ends at 0 (never negative)."
    why_human: "The atomic decrement depends on real SQL Server row-locking semantics under two genuinely parallel transactions. Presence of ExecuteUpdateAsync + CreateExecutionStrategy + WHERE Stock >= qty proves the code shape, not the runtime race outcome. The only automated proof (StockConcurrencyTests) cannot execute on this Linux host — SqlServerWebApplicationFactory defaults to LocalDB and throws System.PlatformNotSupportedException before any assertion runs. SQLite cannot substitute: it serializes writes, so a green SQLite run would prove nothing about SQL Server concurrency."
human_verification:
  - test: "Run StockConcurrencyTests against a reachable SQL Server (see behavior_unverified_items above)"
    expected: "Exactly one success + one 409; final Stock == 0"
    why_human: "Race outcome requires real SQL Server; LocalDB unavailable on this Linux host"
  - test: "Real Stripe test-mode end-to-end: set Stripe:SecretKey (sk_test_...) and Stripe:WebhookSecret via user-secrets, run the API + landing-page, run `stripe listen --forward-to localhost:5236/api/stripe/webhook`. Add a product to cart, check out with an email, pay with test card 4242 4242 4242 4242."
    expected: "Stripe creates a real hosted Checkout Session; after payment the browser lands on /checkout/success?session_id=cs_...&orderId=N and the page renders 'Order Received' with the correct order; the DB order Status becomes Fulfilled."
    why_human: "StripePaymentProvider.CreateCheckoutSessionAsync is never exercised by any automated test — Testing and the test factories all bind FakePaymentProvider, and StripePaymentProvider only registers for non-Testing environments. No automated evidence exists that a real SessionCreateOptions payload is accepted by Stripe or that Session.Url/Session.Id come back usable. The prior 06-UAT.md test 8 was a confirmed false positive (it ran against FakePaymentProvider, which redirects to https://example.test/checkout/{orderId} and never reaches /checkout/success)."
  - test: "Webhook-only fulfillment negative check: with `stripe listen` STOPPED, complete a test-mode payment and land on /checkout/success."
    expected: "The success page renders 'Order Received' but the DB order Status remains Pending. Starting `stripe listen` and replaying the event then flips it to Fulfilled."
    why_human: "Static analysis confirms MarkFulfilledAsync is the only Fulfilled writer and the success page is a server component that only GETs. Proving the redirect alone does not fulfill requires observing the real redirect with the webhook suppressed."
  - test: "Guest checkout visual/UX flow in a browser: product detail Add to Cart -> navbar badge -> /cart line items, quantity steppers, Remove, 'Complete Your Routine' chips -> Proceed to Checkout -> /checkout email form -> redirect."
    expected: "Each step renders per 06-UI-SPEC with correct gold/charcoal styling, empty/error/loading states, and the chips section omitted when no recommendations exist."
    why_human: "Visual appearance, responsive layout, and state-transition feel cannot be verified by grep. landing-page has no node_modules installed on this host, so neither tsc nor a build could run."
  - test: "Cancel path: begin checkout, then abandon on the Stripe hosted page."
    expected: "Browser returns to the configured CancelUrl (http://localhost:3000/cart); the cart still holds its items; the order is not Fulfilled."
    why_human: "Requires the real Stripe hosted page's cancel affordance."
---

# Phase 6: Cart & Checkout Verification Report

**Phase Goal (MVP user story):** As a client, I want to add recommended products to a cart and check out as a guest with trustworthy, server-verified pricing and stock, so that I can complete a real purchase without creating an account.

**Verified:** 2026-08-15
**Status:** human_needed
**Re-verification:** No — initial verification

**User Story format guard:** PASS — goal matches `As a ..., I want to ..., so that ...`.

## User Flow Coverage (MVP Mode)

Outcome clause under verification: *"so that I can complete a real purchase without creating an account."*

| # | Step | Expected | Evidence in codebase | Status |
|---|------|----------|----------------------|--------|
| 1 | Discover add-ons on a service page | Stylist-recommended products render on service detail | `ServicesService.cs:49-60` joins `ServiceRecommendedProduct`; `app/services/[slug]/page.tsx:97-121` renders `RecommendedProductCard`; `lib/services.ts:21` `recommendedProducts` in Zod schema | VERIFIED |
| 2 | Add to cart from product detail | Add to Cart + stepper, no account | `AddToCartPanel.tsx:54` calls `upsertCartItem`; rendered at `app/products/[slug]/page.tsx:89`; `CartsController` is anonymous, keyed by `X-Cart-Session-Id` | VERIFIED |
| 3 | See cart count | Navbar badge updates without reload | `Navbar.tsx:6,125,184` — `cartItemCount`, `CART_UPDATED_EVENT`, `CartIcon` | VERIFIED |
| 4 | Review cart | Line items with server prices, subtotal, empty/error states | `CartPageClient.tsx`; `CartsService` enriches from `Products` at read time; empty session returns `Items = []` (CartsService.cs:22-27, 78-83) | VERIFIED |
| 5 | Add recommended chips at checkout stage | "Complete Your Routine" chips, excluded in-cart ids, max 4, omitted when empty | `ProductsService.GetRecommendedForCheckoutAsync` (join + `!cartIds.Contains` + `Take(4)`); `CartPageClient.tsx:184-186, 440`; `SuggestionChips` returns null when empty | VERIFIED |
| 6 | Check out as guest | Email-only form, no account, redirect to hosted payment | `CheckoutForm.tsx:145-155` `createCheckout` -> `window.location.href`; `OrdersController.Checkout` has no `[Authorize]`; `Order.ClientId` is `int?` | VERIFIED |
| 7 | Pay with a real provider | Real Stripe hosted Checkout Session | `StripePaymentProvider.cs` builds real `SessionCreateOptions`; registered non-Testing only (`Program.cs:119`) — **never executed by any test** | PRESENT, needs human (real keys) |
| 8 | Land on confirmation | Success page displays order, never fulfills | `app/checkout/success/page.tsx` — server component, `fetchOrderById` GET only; `notFound()` when orderId or session_id absent | VERIFIED (display path) |
| 9 | Order becomes fulfilled | Only after verified webhook | `StripeWebhookController` -> `MarkFulfilledAsync`, the sole `OrderStatus.Fulfilled` writer repo-wide | VERIFIED |

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Client can add to cart, review on cart page, check out as guest through an integrated payment provider — no account (`Order.ClientId` nullable) | ✓ VERIFIED | `Order.cs:15` `public int? ClientId`; `OrdersController.Checkout` anonymous, `TryGetClientUserId` returns null for guests; `OrdersService.cs:89` `ClientId = clientUserId` (null for guests). Test `GuestCheckout_CreateCheckoutAsync_SetsClientIdNullAndStatusPending` PASSES. Full UI path present (flow table rows 2-6, 8). |
| 2 | Totals always recomputed server-side from catalog; tampered client price/total has no effect | ✓ VERIFIED | Structurally impossible to tamper: `CheckoutRequestDto` has only `SessionKey`, `Items{ProductId,Quantity}`, `Email`, `Name`, `RedeemPoints` — no money fields. `CartItemUpsertDto` is `ProductId`+`Quantity` only. `CartItem` persists no price. `OrdersService.LoadCatalogLinesAsync` computes `product.Price * line.Quantity` from `Products`. Tests PASS: `PriceAuthority_CreateCheckoutAsync_UsesCatalogPriceIgnoringClientMoneyAbsence`, `CheckoutRequestDto_HasNoPriceOrTotalProperties`. |
| 3 | Concurrent checkout for last unit → exactly one success; stock never negative | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Code shape correct: `OrdersService.cs:98-113` conditional `ExecuteUpdateAsync` with `Where(p => p.Id == ... && p.Stock >= line.Quantity)`, 0 rows → rollback + `ConflictError` → HTTP 409, inside `CreateExecutionStrategy` + `BeginTransactionAsync`. Single-threaded insufficient-stock path PASSES (`CreateCheckoutAsync_InsufficientStock_IsConflictAndStockUnchanged`). **But the race itself is unproven**: `StockConcurrencyTests` (the only parallel proof) FAILS to execute here with `System.PlatformNotSupportedException: LocalDB is not supported on this platform` — an environment limit, not an assertion failure. See human item 1. |
| 4 | Order marked fulfilled only after verified webhook, never from client redirect | ✓ VERIFIED | `grep OrderStatus.Fulfilled` across non-test source returns exactly 2 hits, both inside `OrdersService.MarkFulfilledAsync`. Its only production caller is `StripeWebhookController` (post-`ConstructEvent`, gated on `payment_status == "paid"`). Webhook reads raw body via `StreamReader` — no `FromBody`. Success page is a server component doing a GET only. Tests PASS: `Webhook_MissingStripeSignature_Returns400`, `Webhook_InvalidStripeSignature_Returns400`, `Webhook_ValidCheckoutSessionCompletedPaid_MarksOrderFulfilled`, `Webhook_IdenticalRedelivery_Returns200AndStaysFulfilledOnce`, `Webhook_UnknownOrder_Returns503SoStripeRetries`, `Webhook_TerminalStatusOrder_Returns200AndDoesNotRetry`. |
| 5 | Stylist-recommended add-ons surfaced on service detail page AND again at checkout | ✓ VERIFIED | Service detail: `ServicesService.cs:49-60` + `app/services/[slug]/page.tsx:97-121`. Checkout stage: `GetRecommendedForCheckoutAsync` -> `GET /api/products/recommended-for-checkout` -> `CartPageClient` "Complete Your Routine" chips (`:184`, `:440`), which sit directly above the Proceed to Checkout summary. `RecommendedForCheckoutTests` PASS. See note below on placement. |

**Score:** 4/5 truths verified (1 present, behavior-unverified)

### Note on SC5 placement (not a gap)

SC5 says "again at checkout". The chips render on `/cart` (with the Order Summary and Proceed to Checkout CTA), not on `/checkout`. This matches the locked design decision **D-07** ("suggestion chips on the cart page; user adds before checkout") and 06-UI-SPEC line 243 ("Section under the line-item list"). `/checkout` deliberately has no chips — adding items there would desync the summary the user is about to pay for. Verified as satisfying the criterion per the phase's own contract; flagged here for transparency rather than treated as a deviation needing an override.

### Post-execution fixes — re-verified against current code

| Commit | Claim | Current-code evidence | Status |
|--------|-------|----------------------|--------|
| 5cebf63 (SHOP-02) | `BuildSuccessUrl` appends explicit `orderId`; success page no longer regex-derives an id from `session_id` | `StripePaymentProvider.cs:27-35` appends `&orderId={orderId}` and preserves/adds `{CHECKOUT_SESSION_ID}`. `success/page.tsx:29-32` `parseOrderId` reads only `orderId`/`order` params — no session_id regex anywhere in the file. Tests PASS: `BuildSuccessUrl_AppendsOrderIdAndKeepsSessionPlaceholder`, `BuildSuccessUrl_OperatorSuppliedPlaceholder_IsNotDuplicated`, `RealisticStripeSessionId_CarriesNoUsableOrderId`. | ✓ CONFIRMED |
| 674806e (ACCT-06/SHOP-06) | `GET /api/orders/{id}` requires `?session=` matched via `FixedTimeEquals`; missing/wrong/blank → 404 | `OrdersService.GetByIdAsync:284-301` — blank/whitespace short-circuits to NotFound; comparison via `CryptographicOperations.FixedTimeEquals`; every failure returns NotFound (never a distinguishable 403). Tests PASS: `GetById_CorrectSession_ReturnsOrder`, `GetById_WrongSession_ReturnsNotFound`, `GetById_NoSession_ReturnsNotFound`, `GetById_BlankSession_ReturnsNotFound`. | ✓ CONFIRMED |
| c1530ad (SHOP-05) | Webhook returns 503 on transient NotFound so Stripe retries; terminal returns 200 + LogError | `StripeWebhookController.cs:78-100` — `result.IsNotFound()` → `LogError` + `503`; non-success non-notfound → `LogError` + falls through to `Ok()`. Tests PASS: `Webhook_UnknownOrder_Returns503SoStripeRetries`, `Webhook_TerminalStatusOrder_Returns200AndDoesNotRetry`. | ✓ CONFIRMED |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `API/.../Features/Carts/CartsService.cs` | Session cart, enrich prices from Products | ✓ VERIFIED | Owns all Cart/CartItem DbContext access; enriches from `Products` at read time |
| `API/.../Controllers/CartsController.cs` | `api/carts`, `X-Cart-Session-Id`, no DbContext | ✓ VERIFIED | `SessionHeaderName` const; PLAT-01 clean (no `BookingDbContext` in ctor) |
| `API/.../Migrations/AddCarts` | Carts/CartItems + unique SessionKey | ✓ VERIFIED | `IX_Carts_SessionKey` `unique: true` |
| `API/.../Features/Orders/OrdersService.cs` | Server totals, atomic stock, compensate, MarkFulfilled | ✓ VERIFIED | All four paths present and substantive |
| `API/.../Features/Payments/IPaymentProvider.cs` | Payment seam | ✓ VERIFIED | Records + interface; two implementations |
| `API/.../Features/Payments/StripePaymentProvider.cs` | Real Stripe session create | ⚠️ WIRED, RUNTIME UNPROVEN | Registered non-Testing (`Program.cs:119`); real `SessionService.CreateAsync` call never executed by any test |
| `API/.../Controllers/OrdersController.cs` | POST checkout, GET by id, no DbContext | ✓ VERIFIED | PLAT-01 clean; rate-limited (`EnableRateLimiting("checkout")`) |
| `API/.../Controllers/StripeWebhookController.cs` | Raw body + ConstructEvent | ✓ VERIFIED | `StreamReader`, no `FromBody`, `tolerance: 300` |
| `API/.../Migrations/AddOrders` | Orders/OrderItems + filtered unique StripeSessionId | ✓ VERIFIED | `IX_Orders_StripeSessionId` `unique: true, filter: "[StripeSessionId] IS NOT NULL"` |
| `API/.../Tests/.../StockConcurrencyTests.cs` | SQL Server parallel last-unit proof | ⚠️ PRESENT, CANNOT RUN HERE | Correct assertions (one 2xx, one 409, Stock==0); blocked by LocalDB unavailability |
| `landing-page/lib/cart.ts` | Zod fetch layer, session header | ✓ VERIFIED | `X-Cart-Session-Id` on every call; write bodies carry productId/quantity only |
| `landing-page/lib/cartSession.ts` | localStorage UUID, no cookies | ✓ VERIFIED | — |
| `landing-page/app/cart/page.tsx` + `CartPageClient.tsx` | Line items, summary, chips, empty/error/loading | ✓ VERIFIED | All UI-SPEC strings present |
| `landing-page/app/checkout/{page,success,cancel}` | Form, display-only success, cancel | ✓ VERIFIED | Success page GET-only, `notFound()` guards |
| `landing-page/components/AddToCartPanel.tsx` | Stepper + Add to Cart, stock-aware | ✓ VERIFIED | `disabled={outOfStock \|\| submitting}` |

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `Program.cs` | `CartsService` / `OrdersService` | `AddScoped` (`:86`, `:123`) | ✓ WIRED |
| `Program.cs` | `IPaymentProvider` | Fake in Testing (`:110`), Stripe otherwise (`:119`) | ✓ WIRED |
| `CartsService` | `Products` catalog | Query join at read time for UnitPrice/LineTotal/Stock | ✓ WIRED |
| `OrdersService` | `Products.Price` | `LoadCatalogLinesAsync` recompute | ✓ WIRED |
| `StripeWebhookController` | `OrdersService.MarkFulfilledAsync` | Only production caller | ✓ WIRED |
| `CheckoutForm` | `POST /api/orders/checkout` | `createCheckout` + `X-Cart-Session-Id` | ✓ WIRED |
| `success/page.tsx` | `GET /api/orders/{id}?session=` | `fetchOrderById(orderId, sessionId)` | ✓ WIRED |
| `CartPageClient` | `GET /api/products/recommended-for-checkout` | `fetchRecommendedForCheckout` | ✓ WIRED |
| `StripePaymentProvider` | Stripe API | `SessionService.CreateAsync` | ⚠️ WIRED, never executed under test |

### Data-Flow Trace (Level 4)

| Artifact | Data variable | Source | Real data | Status |
|----------|--------------|--------|-----------|--------|
| `CartPageClient` | `cart.items[].unitPrice/lineTotal` | `CartsService` join on `Products.Price` | Yes | ✓ FLOWING |
| `CartPageClient` | `recommendations` | `GetRecommendedForCheckoutAsync` (`ServiceRecommendedProduct` join) | Yes | ✓ FLOWING |
| `success/page.tsx` | `order.items`, `order.totalAmount` | `GET /api/orders/{id}?session=` -> `Orders`+`OrderItems` | Yes | ✓ FLOWING |
| `Navbar` badge | `cartItemCount` | `fetchCart` + `CART_UPDATED_EVENT` | Yes | ✓ FLOWING |
| `services/[slug]` | `service.recommendedProducts` | `ServicesService` join | Yes | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| SQLite-backed phase tests (webhook, success-url, order scoping, retry, orders service, recommendations, carts service) | `dotnet test --filter "StripeWebhookTests\|StripeSuccessUrlTests\|OrderReadScopingTests\|StripeWebhookRetryTests\|OrdersServiceTests\|RecommendedForCheckout\|CartsServiceTests"` | `Passed! - Failed: 0, Passed: 30, Total: 30` | ✓ PASS |
| Controller + fulfillment tests | `dotnet test --filter "CartsControllerTests\|OrdersControllerTests\|MarkFulfilledTests"` | `Passed! - Failed: 0, Passed: 11, Total: 11` | ✓ PASS |
| Last-unit concurrency race | `dotnet test --filter StockConcurrencyTests` | `System.PlatformNotSupportedException: LocalDB is not supported on this platform` (0 assertions reached) | ? SKIP (env) |
| Only-Fulfilled-writer audit | `grep -rn "OrderStatus.Fulfilled" API/ --include=*.cs \| grep -v Tests` | 2 hits, both in `MarkFulfilledAsync` | ✓ PASS |
| Webhook has no `FromBody` | `grep -n "FromBody" StripeWebhookController.cs` | no matches | ✓ PASS |
| No Stripe secrets in tracked config | `grep -rniE "sk_test\|sk_live\|whsec_" appsettings*.json` | no matches | ✓ PASS |
| Stripe.net pinned 52.2.0 | `grep Stripe.net Shared.csproj` | `Version="52.2.0"` | ✓ PASS |
| Frontend typecheck | `tsc --noEmit` | `node_modules` not installed on host | ? SKIP (env) |

**Test-environment note (not an implementation defect):** the full suite reports 200 failed / 182 passed / 382 total on this Linux host. Every failure traces to the same `SqlServerWebApplicationFactory` LocalDB unavailability; zero assertion failures. Of the 8 unique failures inside this phase's filter, 7 belong to Phase 7's `AccountOrdersTests` and 1 is `StockConcurrencyTests`. This is recorded as an environment constraint and is **not** counted as passing evidence either.

### Requirements Coverage

| Requirement | Source plan(s) | Description | Status | Evidence |
|-------------|---------------|-------------|--------|----------|
| SHOP-01 | 06-01, 06-02 | Add products to cart and review it | ✓ SATISFIED | Carts API + `/cart` UI + navbar badge; CartsService/Controller tests pass |
| SHOP-02 | 06-03, 06-04, 06-05 | Check out and pay via integrated provider | ⚠️ PARTIAL — code complete, real-Stripe leg unproven | Full path present incl. real `StripePaymentProvider`; no automated test ever calls Stripe (human item 2) |
| SHOP-03 | 06-03 | Server-side total; client prices never trusted | ✓ SATISFIED | No money fields on any write DTO; catalog recompute; `PriceAuthority` + `HasNoPriceOrTotalProperties` tests pass |
| SHOP-04 | 06-03, 06-05 | Atomic stock decrement, no overselling | ⚠️ PARTIAL — single-threaded proven, race unproven | Conditional `ExecuteUpdateAsync` in strategy+transaction; `StockConcurrencyTests` cannot run (human item 1) |
| SHOP-05 | 06-04, 06-05 | Fulfillment only via verified webhook | ✓ SATISFIED | Sole Fulfilled writer reachable only post-`ConstructEvent`; 6 webhook tests pass |
| SHOP-06 | 06-03 | Guest checkout, `Order.ClientId` nullable | ✓ SATISFIED | `int? ClientId`; guest test passes; ACCT-06 session-gated read added by 674806e |
| SHOP-07 | 06-04 | Stylist-recommended add-ons at checkout | ✓ SATISFIED | `GetRecommendedForCheckoutAsync` + chips; `RecommendedForCheckoutTests` pass |

**Orphaned requirements:** none. All 7 IDs mapped to Phase 6 in REQUIREMENTS.md are claimed by at least one plan and accounted for above.

### Prohibitions

| Prohibition | Status | Evidence |
|-------------|--------|----------|
| No client-trusted Price/Total on cart/checkout DTOs | ✓ HELD | `CheckoutRequestDto`, `CartItemUpsertDto` inspected |
| Controllers must not inject `BookingDbContext` (PLAT-01) | ✓ HELD | grep clean on Carts/Orders controllers |
| No price columns on `CartItem` | ✓ HELD | `CartItem.cs` — ProductId/Quantity only |
| No `HasFilter` on AppointmentSlot unique index | ✓ HELD | `BookingDbContext.cs:369` unfiltered, with guard comment |
| Webhook must not use `FromBody` | ✓ HELD | grep clean; raw `StreamReader` |
| Success page must not set Fulfilled | ✓ HELD | Server component, GET only; no mutation refs |
| No Stripe secrets in tracked appsettings | ✓ HELD | Only SuccessUrl/CancelUrl present |
| Stripe.net pinned 52.2.0, no beta | ✓ HELD | csproj line 26 |
| StockConcurrencyTests must not use EF InMemory | ✓ HELD | `IClassFixture<SqlServerWebApplicationFactory>` |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | No `TBD`/`FIXME`/`XXX` in any file modified by this phase | — | Debt-marker gate PASSES |
| `CheckoutForm.tsx` | 28, 275, 305 | `placeholder` | ℹ️ Info | HTML input placeholder attributes — not stub markers |

### Observations (non-blocking, for the record)

1. **`CheckoutSessionRequest.TotalAmount` is dead.** `StripePaymentProvider` builds its charge purely from per-line `UnitAmount = UnitPrice * 100`; `TotalAmount` is never read by either provider. Consequence: a Phase 7 loyalty redemption reduces `Order.TotalAmount` but the amount Stripe actually charges stays at the undiscounted line sum. This does not weaken SC2 (the amount charged is still server-derived from catalog prices, and a tampered client value still has no effect), so it is not a Phase 6 gap. It is a Phase 7 loyalty concern worth filing separately.
2. **`SqlServerWebApplicationFactory` honors the connection override**, so the concurrency proof is runnable as soon as a real SQL Server is reachable — no code change needed to close human item 1.

### Prior UAT assessment

`06-UAT.md` (30/30 pass, dated 2026-08-14) was **not** used as evidence. It predates all three post-execution fixes; its test 8 was a confirmed false positive against `FakePaymentProvider` (session id `fake-{orderId}` satisfied a since-deleted regex, and that provider redirects to `https://example.test/checkout/{orderId}`, never reaching `/checkout/success`); and its test 30 asserts a SQL Server concurrency result that could not have executed on this host. All verdicts above derive from current code plus the 41 tests actually executed during this verification.

### Gaps Summary

No gaps. Every artifact exists, is substantive, and is wired; every prohibition holds; all 41 runnable phase tests pass. Two things remain genuinely unproven rather than broken, and both are blocked by this host's environment rather than by the implementation:

1. **SC3 (last-unit concurrency)** — correct code shape, but the race outcome has never been observed. Needs a reachable SQL Server.
2. **The real-Stripe leg of SC1/SC2 (SHOP-02)** — `StripePaymentProvider` is fully implemented and correctly registered, but no automated test has ever called Stripe. Needs `sk_test_` keys and the Stripe CLI.

Status is `human_needed`, not `passed`: presence and wiring are established, behavioral proof for the items above is not.

---

_Verified: 2026-08-15_
_Verifier: Claude (gsd-verifier)_
