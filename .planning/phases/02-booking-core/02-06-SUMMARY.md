---
phase: 02-booking-core
plan: 06
subsystem: verification
tags: [human-verify, checkpoint, resend, timezone, booking, ux]

# Dependency graph
requires:
  - phase: 02-05
    provides: /book progressive-reveal UI wired to the real appointments API
  - phase: 02-04
    provides: POST /api/appointments with best-effort Resend confirmation email
  - phase: 02-02
    provides: RESEND_API_KEY in user-secrets, resolving in Development and Testing (D-12/D-13)
provides:
  - "Human sign-off closing the Manual-Only Verifications in 02-VALIDATION.md"
  - "Verified real Resend delivery from the salon's verified sending domain"
affects: [phase-verification, notifications]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Salon zone is configuration, not a constant: tests resolve instants via SalonTimeZone.FromOptions(new SalonOptions()) rather than hardcoding a UTC offset"
    - "Third-party failure bodies are logged, not just status codes — a bare 'Forbidden' is not diagnosable"

key-files:
  created:
    - .planning/phases/02-booking-core/02-06-SUMMARY.md
  modified:
    - API/ZachHairStudio.Api/appsettings.json
    - API/ZachHairStudio.Api/appsettings.Development.json
    - API/ZachHairStudio.Shared/Features/Availability/SalonOptions.cs
    - API/ZachHairStudio.Shared/Features/Appointments/ResendOptions.cs
    - API/ZachHairStudio.Shared/Features/Appointments/ResendEmailService.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/ConcurrencyTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs
    - landing-page/components/AppointmentBookingForm.tsx
    - landing-page/components/Navbar.tsx
    - landing-page/lib/data.ts
  deleted: []

key-decisions:
  - "Salon timezone changed from America/New_York to Asia/Yangon (owner-directed). SalonOptions default, both appsettings files, and the frontend SALON_TIME_ZONE now agree."
  - "Verified Resend sending domain is media.zachhairstudio.com; from-address is bookings@media.zachhairstudio.com."
  - "The 'email delivery isn't guaranteed' confirmation caption was REMOVED at owner request, reversing a stated D-11 must-have in this plan's own acceptance criteria."
  - "Nav links made root-relative (/#services) and routed through next/link so site navigation works from /book, not just the homepage."
---

## What Happened

Plan 06 is the phase's blocking human-verify gate. No new feature code was
planned; in practice the manual pass surfaced three defects and two
owner-directed configuration changes, all fixed and re-verified before sign-off.

**Sign-off: approved** by the owner after the full `/book` flow was driven in a
browser against the live stack (API on :5236, landing-page on :3001).

## Verified Behaviors

| # | Check | Result |
|---|-------|--------|
| 1 | Progressive reveal gates service → stylist → date/slot → details | pass |
| 2 | Slot grid renders salon-local time, not browser time | pass |
| 3 | On-screen confirmation is self-sufficient (service, concrete stylist, salon-local time + zone, duration, price) | pass |
| 4 | Real confirmation email arrives | pass (after domain fix) |
| 5 | 409 recovery preserves details, marks slot taken, refreshes grid | pass |
| 6 | `?service=precision-cut` deep link preselects; homepage routes into /book | pass |

Check 4 was confirmed at the wire: the API log records
`POST https://api.resend.com/emails … 200` with no rejection warning, against
one committed appointment.

## Defects Found and Fixed During Verification

**1. Confirmation email never arrived — unverified Resend sending domain.**
Resend was returning `403 validation_error` on every send. `ResendEmailService`
logged only `response.StatusCode`, so the log read `Forbidden` and nothing more,
which is why the cause was invisible. The service now reads and logs the response
body, which carries Resend's actionable reason and no secret. Root cause was the
from-address domain; the correct verified domain is `media.zachhairstudio.com`.
Because the send is best-effort (D-11), every affected booking still committed
and still rendered its on-screen confirmation — the failure was silent by design.

**2. "Save or screenshot this confirmation — email delivery isn't guaranteed."
removed from the confirmation panel** at owner request. See Deviations.

**3. Site navigation was dead on `/book`.** `navLinks` used bare fragments
(`#services`), which resolve against the current path — so from `/book` they
pointed at `/book#services` and did nothing. Made root-relative (`/#services`)
and routed through `next/link`, so they navigate home and scroll, and still
scroll client-side without a reload when already on the homepage. The `Book Now`
button (both desktop and mobile-menu instances) and the logo were switched from
`href="#contact"` to `next/link href="/book"` / `"/#home"` in the same pass.

## Owner-Directed Configuration Changes

**Salon timezone: `America/New_York` → `Asia/Yangon`.**
Not a one-line swap. Three test files hardcoded `TimeSpan.FromHours(-4)` as
"10:00 salon-local" — an absolute instant that is 20:30 in Yangon, outside the
seeded 09:00–18:00 working hours. Left alone those bookings would have fallen
off the slot grid and failed for the wrong reason. `AppointmentsControllerTests`,
`ConcurrencyTests`, and `AnyStylistAssignmentTests` now resolve their instants
through `SalonTimeZone.FromOptions(new SalonOptions())`, following
`Salon:IanaTimeZoneId` instead of silently drifting when it changes.
`Asia/Yangon` resolves on Windows via ICU — no `TimeZoneConverter` needed.
The API now returns `+06:30` offsets; verified live.

**Sending domain: `media.zachhairstudio.com`**, from-address
`bookings@media.zachhairstudio.com`. Key remains in user-secrets only (D-13).

## Deviations

**1. [Owner-directed, contradicts this plan's acceptance criteria] Confirmation
caption removed.** This plan's `truths` and `acceptance_criteria` both require the
on-screen confirmation to carry the "email delivery isn't guaranteed" caption
(a stated D-11 must-have). The owner directed its removal and it is gone. The
confirmation remains self-sufficient — service, concrete stylist, salon-local
date/time with zone, duration, and price all still render — so nothing is lost
operationally; the client is simply no longer prompted to save it. **This is a
decision reversal, not a defect fix, and D-11's UI obligation should be amended
rather than left silently divergent.**

**2. [Scope] Files modified under a `files_modified: []` plan.** Plan 06 declared
no code changes. Eleven files changed, because the manual pass found real defects
and the owner changed configuration mid-checkpoint. All changes were re-verified:
94/94 backend tests pass, `tsc --noEmit` clean.

**3. [Carried forward] DST tests no longer cover the salon's actual zone.**
`DstBoundaryTests` and `DstRoundTripTests` construct `America/New_York` directly
rather than reading config, so they still pass. But Yangon has fixed +06:30 with
no DST, so these now prove `SalonTimeZone` is DST-safe *generically* rather than
for the deployed configuration. Kept deliberately — the helper is shared and the
coverage is still worth having — but SC5's DST proof is no longer
configuration-specific.

## Known Gaps

**The confirmation email body is incomplete.** It contains service, stylist, and
time, but **no zone label, no duration, and no price**. This plan's check 4
requires all five. The owner approved with this outstanding. `ServiceResponseDto`
already carries duration and price, so the fix is a small change to the HTML in
`ResendEmailService`. Route into a `/gsd-plan-phase 2 --gaps` pass or a follow-up.

**`Navbar` renders on `/book`,** so the `Book Now` button is a self-link on that
page. Cosmetic; not fixed.

**SC5 is proven one layer below the HTTP create path.** Carried from 02-04:
`DstRoundTripTests` builds the `Appointment` directly against `BookingDbContext`
because both calendar-fixed DST dates fall outside the create-path booking window
relative to the test clock. It will not catch `AppointmentsService.BuildAppointment`
drifting from what the test duplicates.

## Verification Evidence

- Backend: `dotnet test API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` → **94/94 pass, 0 errors**
- Frontend: `npx tsc --noEmit` → **exit 0**
- Live API: `GET /api/appointments/slots?serviceId=1&date=2026-07-17` → `"startsAt":"2026-07-17T09:00:00+06:30"`
- Live web: `/book` → 200; nav renders `/#home /#services /#gallery /#team /#reviews /#contact`; `Book Now` → `href="/book"`
- Resend: `POST https://api.resend.com/emails … 200`, no rejection warning
- Secrets: `RESEND_API_KEY` present in user-secrets only; no value in any tracked file; gitleaks green
