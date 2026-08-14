---
status: complete
phase: 06-cart-checkout
source: 06-01-SUMMARY.md, 06-02-SUMMARY.md, 06-03-SUMMARY.md, 06-04-SUMMARY.md, 06-05-SUMMARY.md
started: 2026-08-14T00:00:00Z
updated: 2026-08-14T00:40:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Cold Start Smoke Test
expected: Kill any running server. Start API from scratch — boots without errors, AddCarts/AddOrders migrations apply, and a primary query (GET /api/products) returns live data. Landing page loads.
result: pass

### 2. Add to Cart from Product Detail
expected: On a product detail page, the aside shows Add to Cart with a quantity stepper. Adding updates the navbar cart count immediately (no refresh).
result: pass
coverage_id: 06-02/D1

### 3. Cart Page Line Items
expected: /cart lists each line with server-computed unit price and line total, working quantity steppers, Remove per line, an Order Summary, and Proceed to Checkout.
result: pass
coverage_id: 06-02/D2

### 4. Cart Badge and Out-of-Stock CTA
expected: Navbar cart badge appears when count >= 1 and disappears at 0. On an out-of-stock product, Add to Cart is disabled.
result: pass
coverage_id: 06-02/D5

### 5. Complete Your Routine Recommendations
expected: On /cart, recommendation chips load for services tied to cart products; the section is omitted entirely when there are none. Clicking a chip adds it to the cart (Added / out-of-stock states visible).
result: pass
coverage_id: 06-04/D2

### 6. Checkout Form Validation and Redirect
expected: /checkout requires a valid email before submit. Valid submit redirects to the payment provider checkout URL. A failed create shows "Couldn't Start Checkout" and the form re-enables.
result: pass
coverage_id: 06-04/D4

### 7. Stripe Checkout Session (test mode)
expected: With Stripe:SecretKey set to a sk_test_ key, checkout redirects to a real Stripe-hosted Checkout page showing the correct line items and totals in the salon's currency.
result: pass
coverage_id: 06-05/D1

### 8. Stripe End-to-End Fulfillment
expected: With `stripe listen --forward-to .../api/stripe/webhook` running, pay with test card 4242 4242 4242 4242. Stripe redirects back to a success page that shows the order (not a 404), and the order flips Pending → Fulfilled exactly once with stock decremented.
result: pass
coverage_id: 06-05/D5

### 9. Anonymous cart upsert/get with server-enriched prices
expected: Anonymous client can upsert/get cart lines under X-Cart-Session-Id with server-enriched UnitPrice/LineTotal from Products.Price
result: pass
source: automated
coverage_id: 06-01/D1

### 10. CartItem stores no money columns
expected: CartItem persists ProductId and Quantity only — no Price/Total columns
result: pass
source: automated
coverage_id: 06-01/D2

### 11. Unknown cart session returns empty list
expected: GET cart for unknown/empty session returns empty items list (not null, not 404)
result: pass
source: automated
coverage_id: 06-01/D3

### 12. CartsController has no DbContext dependency
expected: CartsController does not depend on BookingDbContext (PLAT-01)
result: pass
source: automated
coverage_id: 06-01/D4

### 13. AddCarts migration shape
expected: AddCarts migration creates Carts/CartItems with unique SessionKey; AppointmentSlot unique index remains unfiltered
result: pass
source: automated
coverage_id: 06-01/D5

### 14. Result.ConflictError message overload
expected: Message-only Result.ConflictError overload compiles and IsConflict() is true
result: pass
source: automated
coverage_id: 06-01/D6

### 15. Empty cart state
expected: Empty cart renders Your Cart Is Empty with Browse Products CTA
result: pass
source: automated
coverage_id: 06-02/D3

### 16. Cart load failure banner
expected: Cart load failure shows Couldn't Load Your Cart rose banner with Try Again
result: pass
source: automated
coverage_id: 06-02/D4

### 17. Upsert body carries no client money fields
expected: Upsert request body is productId+quantity only (no client money fields) — T-06-04
result: pass
source: automated
coverage_id: 06-02/D6

### 18. Guest checkout returns provider URL
expected: Anonymous POST /api/orders/checkout with X-Cart-Session-Id creates Pending guest order and returns FakePaymentProvider checkoutUrl
result: pass
source: automated
coverage_id: 06-03/D1

### 19. Server price authority on orders
expected: Order totals and OrderItem UnitPrice/LineTotal come from catalog Price (DTO has no money fields)
result: pass
source: automated
coverage_id: 06-03/D2

### 20. Insufficient stock is a Conflict
expected: Insufficient stock returns Conflict and leaves Stock unchanged (atomic UPDATE path)
result: pass
source: automated
coverage_id: 06-03/D3

### 21. Guest order has null ClientId
expected: Guest Order.ClientId is null on successful checkout
result: pass
source: automated
coverage_id: 06-03/D4

### 22. MarkFulfilledAsync is real and idempotent
expected: MarkFulfilledAsync is thin idempotent Pending→Fulfilled (not a stub); already Fulfilled is success no-op
result: pass
source: automated
coverage_id: 06-03/D5

### 23. Provider failure restores stock
expected: Payment provider failure after commit restores stock and marks Order Failed
result: pass
source: automated
coverage_id: 06-03/D6

### 24. Checkout recommendations query
expected: GetRecommendedForCheckoutAsync joins ServiceRecommendedProduct, excludes in-cart, Take(4), empty when no join
result: pass
source: automated
coverage_id: 06-04/D1

### 25. createCheckout sends session header
expected: createCheckout POSTs /api/orders/checkout with required X-Cart-Session-Id header
result: pass
source: automated
coverage_id: 06-04/D3

### 26. Success page never fulfills
expected: /checkout/success shows Order Received via GET only; never fulfills; invalid ref → notFound()
result: pass
source: automated
coverage_id: 06-04/D5

### 27. Cancel page
expected: /checkout/cancel shows Checkout Cancelled + Return to Cart
result: pass
source: automated
coverage_id: 06-04/D6

### 28. Webhook signature verification
expected: POST /api/stripe/webhook rejects bad/missing Stripe-Signature with 400; paid checkout.session.completed fulfills once
result: pass
source: automated
coverage_id: 06-05/D2

### 29. Fulfillment idempotency
expected: MarkFulfilledAsync Pending→Fulfilled is idempotent no-op when already Fulfilled
result: pass
source: automated
coverage_id: 06-05/D3

### 30. Stock concurrency on SQL Server
expected: Two parallel last-unit checkouts → one success + one 409; Stock==0 on SQL Server
result: pass
source: automated
coverage_id: 06-05/D4

## Summary

total: 30
passed: 30
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

[none yet]
