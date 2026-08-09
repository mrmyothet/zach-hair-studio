---
status: testing
phase: 05-product-catalog
source: [05-VERIFICATION.md]
started: 2026-08-09T11:45:00Z
updated: 2026-08-09T11:45:00Z
---

## Current Test

number: 1
name: Catalog browsing (/products — category grouping, card fields, Out-of-Stock badge, inactive product absent)
expected: |
  Category-grouped product cards with all fields visible; Out-of-Stock badge on the zero-stock product; no inactive product in the list.
awaiting: user response

## Tests

### 1. Catalog browsing
expected: Category-grouped product cards with all fields visible; Out-of-Stock badge on 'Texturizing Styling Cream' (stock=0); no 'Discontinued Styling Wax' in list.
result: [pending]

### 2. Product detail + 404
expected: /products/texturizing-styling-cream renders detail with long description, category, price, Out-of-Stock sidebar; /products/not-a-real-product renders 404 page.
result: [pending]

### 3. Recommended Products presence/absence
expected: /services/color-and-highlights shows 'Recommended Products' section with 2 product cards; /services/precision-cut shows no section, no heading, no empty box.
result: [pending]

### 4. Nav link
expected: 'Products' link appears in nav bar between 'Services' and 'Gallery'.
result: [pending]

### 5. Empty-state (API unavailable)
expected: Stop API, reload /products — 'Products Are Being Curated' box renders, no crash, no white screen.
result: [pending]

### 6. Ordering stability
expected: Two products with identical Name inserted via EF seed — identical relative order across repeated GET /api/products calls.
result: [pending]

### 7. Long-text shortDescription grid stability (backstop)
expected: Product with max-length shortDescription (200 chars) does not break card grid row alignment on /products.
result: [pending]

### 8. Ordering across repeated requests with identical sort keys (backstop)
expected: Identical relative order across repeated requests under tie-break conditions.
result: [pending]

## Summary

total: 8
passed: 0
issues: 0
pending: 8
skipped: 0
blocked: 0

## Gaps
