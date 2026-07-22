---
status: testing
phase: 02-booking-core
source: [02-01-SUMMARY.md, 02-02-SUMMARY.md, 02-03-SUMMARY.md, 02-04-SUMMARY.md, 02-05-SUMMARY.md, 02-06-SUMMARY.md, 02-07-SUMMARY.md]
started: 2026-07-22T15:32:17Z
updated: 2026-07-22T15:32:17Z
mvp_mode: true
---

## Current Test
<!-- OVERWRITE each test - shows where we are -->

number: 1
name: Cold Start Smoke Test
expected: |
  Kill any running ZachHairStudio.Api process and next dev. Start fresh:
  `dotnet run` (API) applies the AddBookingCore migration cleanly against
  (localdb)\MSSQLLocalDB, and `next dev` serves the landing page. Opening
  /book loads without errors and GET /api/appointments/slots returns live
  slot data (not a 500 / empty crash).
awaiting: user response

## Tests

### 1. Cold Start Smoke Test
expected: From a killed state, `dotnet run` boots the API and applies the AddBookingCore migration against LocalDB with no error; `next dev` serves /book; the slots endpoint returns live data on first request.
result: [pending]

### 2. Pick a service on /book
expected: Navigate to /book. The progressive-reveal form shows a service picker. Selecting a service (e.g. from the seeded catalog) reveals the next step (stylist/slot selection) — no page reload, no console error.
result: [pending]

### 3. See real open slots
expected: After choosing a service and a date, the slot grid shows concrete open time slots that reflect stylist working hours and already-booked cells (booked/time-off cells are absent, not selectable). Slots are salon-local (Asia/Yangon) times.
result: [pending]

### 4. Filter slots by preferred stylist
expected: Selecting a specific stylist chip re-fetches and narrows the slot grid to that stylist's availability. Clearing back to "any" restores the union across stylists.
result: [pending]

### 5. Confirm a booking (on-screen)
expected: Pick a slot, fill contact details, submit. An on-screen confirmation panel appears showing all five fields: service name, the concrete stylist, salon-local date, salon-local time WITH zone label, duration, and price. No refresh needed.
result: [pending]

### 6. Confirmation email content
expected: The confirmation email actually arrives (Resend), AND its body carries all five fields per 02-VALIDATION.md line 85: service, stylist, salon-local time WITH zone label, duration, and price.
result: [pending]

### 7. Double-booking is rejected
expected: Two attempts to take the same stylist/slot (or booking a slot that just got taken) result in exactly one success; the losing attempt gets a clear "slot taken" message and the form recovers (contact details preserved), not a generic crash.
result: [pending]

### 8. [DEFERRED — technical] Backend test suite runs green
expected: With no process holding a lock on ZachHairStudio.Shared.dll, `dotnet test API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` passes (SUMMARYs report 94/94). The initial verification could NOT run this (leftover locked process), so it is unconfirmed by direct execution.
result: [pending]

### 9. [DEFERRED — technical] SC5 DST write-path
expected: The DST-transition proof runs through the shipped write path (POST /api/appointments), OR the owner accepts SC5's DST clause as deliberately descoped for the Asia/Yangon (fixed +06:30, no DST) deployment — a documented judgment call, not a silent gap.
result: [pending]

## Summary

total: 9
passed: 0
issues: 0
pending: 9
skipped: 0
blocked: 0

## Gaps

[none yet]
