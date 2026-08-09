---
phase: 02-booking-core
verified: 2026-07-10T20:30:00Z
status: passed
previous_status: reconciled
reconciled: 2026-07-25
reconciliation_basis: "Evidence-based reconciliation against 02-UAT.md (2026-07-23, 9/9 pass) plus git history — NOT a fresh adversarial re-verification. See '## Post-Verification Reconciliation (2026-07-25)' below."
score: 4/5 truths verified (1 present-but-behavior-partial)
behavior_unverified: 1
overrides_applied: 0
gaps:
  - truth: "Client receives a confirmation email carrying full appointment details (service, stylist, salon-local time + zone label, duration, price) — required by 02-VALIDATION.md's Manual-Only Verification row for BOOK-03 and restated as Plan 06's own acceptance criteria"
    status: partial
    resolution: "RESOLVED — fixed in commit ea8eb85 ('fix(02-06): confirmation email carries zone label, duration, and price'), confirmed by 02-UAT.md Test 6 (owner-confirmed receipt of the real email; ResendEmailService.cs now renders all five fields)."
    reason: "Email is genuinely sent and delivered (live-verified: Resend POST /emails -> 200, from a verified sending domain) and does carry service, stylist, and time — but is missing the zone label, duration, and price. 02-06-SUMMARY.md's own 'Known Gaps' section admits this. The phase's own validation contract (02-VALIDATION.md line 85) explicitly requires all five fields to be present in the received email; three of five are missing."
    artifacts:
      - path: "API/ZachHairStudio.Shared/Features/Appointments/ResendEmailService.cs"
        issue: "SendConfirmationAsync's HTML body (lines 41-48) interpolates only firstName/lastName/serviceName/stylist/when (date+time, no zone suffix). ServiceResponseDto already carries DurationMinutes and Price (confirmed via AppointmentExtensions/AppointmentResponseDto), so the fix is additive, not structural."
    missing:
      - "Zone label appended to the rendered `when` string (e.g. \"... +06:30\" or \"Myanmar Time\") in ResendEmailService.cs"
      - "service.DurationMinutes interpolated into the email body"
      - "service.Price interpolated into the email body"
deferred: []
behavior_unverified_items:
  - truth: "Appointment and availability times are stored as DateTimeOffset against a configured salon IANA timezone, verified correct across a DST-transition date (SC5/BOOK-05), through the full write path a real client uses (POST /api/appointments -> AppointmentsService.CreateAsync -> real SQL unique index)"
    test: "Book an appointment via POST /api/appointments (not direct DbContext insertion) for an instant that falls on one of the salon's configured zone's DST-transition dates, and assert the stored offset and instant are correct."
    expected: "AppointmentsService.CreateAsync, exercised end-to-end including FluentValidation, SlotService candidate matching, and the real unique-index insert, produces the correct DateTimeOffset for a DST-boundary instant — proving the *shipped write path*, not just the underlying SalonTimeZone helper or a hand-duplicated DbContext insert, is DST-correct."
    why_human: "DstRoundTripTests.cs (API/ZachHairStudio.Api.Tests/Features/Appointments/DstRoundTripTests.cs) inserts an Appointment + AppointmentSlots directly against BookingDbContext, bypassing AppointmentsService.CreateAsync entirely, because both calendar-fixed 2026 DST dates (Mar 8 in the past, Nov 1 beyond the 60-day horizon relative to the 2026-07-10 test clock) are rejected by the legitimate future/horizon validator before persistence. No automated test exercises AppointmentsService.BuildAppointment with a DST-transition instant. The gap is architecturally low-risk (BuildAppointment does no DST-specific math of its own — SalonTimeZone.ToSalonInstant is the only DST-sensitive code and it IS unit-tested), but it is unproven at the integration level the acceptance criterion actually names, and can only be closed by either moving the test clock or manufacturing a synthetic near-future DST-boundary date — a judgment call for the phase owner/next planner."
---

# Phase 2: Booking Core Verification Report

**Phase Goal:** As a client, I want to pick a service and book a real open slot with my chosen stylist, so that my appointment is confirmed and never double-booked.
**Verified:** 2026-07-10T20:30:00Z
**Status:** gaps_found
**Re-verification:** No — initial verification

## Verdict Summary

**PARTIALLY ACHIEVED.**

The core booking mechanism — real slot computation, end-to-end booking, optional
stylist selection, and the database-level double-booking guarantee — is genuinely
implemented, wired, and independently confirmed against source (not SUMMARY claims).
SC1–SC4 hold up under direct code inspection. SC5 (DateTimeOffset storage) is
technically true (every instant in the domain is `DateTimeOffset`, confirmed by
grep) but its DST-transition proof has two real weaknesses, detailed below, that
the phase's own SUMMARYs already flagged and that independent review confirms are
accurately characterized, not overstated.

One genuine, previously-undisclosed-as-a-blocking-gap item: the phase's own
validation contract (`02-VALIDATION.md`) requires the confirmation *email* to carry
service, stylist, salon-local time **with zone label**, duration, and price. The
shipped `ResendEmailService` omits three of the five. This was correctly recorded
as a "Known Gap" in `02-06-SUMMARY.md` and NOT silently swept under a "passed"
checkmark — but it does mean BOOK-03's manual-verify bar, as literally written in
`02-VALIDATION.md`, is not met. This is the one blocking gap in this report.

The five specific concerns raised for this verification were independently
assessed against source (not the SUMMARYs' framing of them) — see the dedicated
section below. All five are accurately characterized by the SUMMARYs; none were
found to be understated.

## Goal Achievement

### Observable Truths (Roadmap Success Criteria)

| # | Truth (ROADMAP.md SC) | Status | Evidence |
|---|---|---|---|
| 1 | Client can view real open slots for a chosen service reflecting working hours + existing bookings | VERIFIED | `SlotService.GetOpenSlotsAsync` (`API/ZachHairStudio.Shared/Features/Availability/SlotService.cs:25-119`) queries `StylistWorkingHours`, `StylistTimeOff`, and `AppointmentSlots` and computes the grid in memory; wired to `GET /api/appointments/slots` (`AppointmentsController.cs:27-35`); wired to the frontend `fetchOpenSlots` in `lib/appointments.ts` and rendered in `AppointmentBookingForm.tsx` slot grid (lines 461-505). `SlotServiceTests.cs` proves booked-cell and time-off exclusion; `AppointmentsControllerSlotsTests.cs` proves the endpoint end-to-end (InMemory). |
| 2 | Client can complete a booking end-to-end (service → slot → confirm), see on-screen confirmation, and receive a confirmation email | PARTIAL | On-screen confirmation genuinely self-sufficient: `AppointmentBookingForm.tsx:279-331` renders service, concrete stylist, salon-local date, salon-local time+zone (`timeZoneName:"short"`), duration, and price — all five fields, independently confirmed by reading the JSX. Email IS sent and delivered (live-verified in 02-06: `POST https://api.resend.com/emails ... 200`) but its body (`ResendEmailService.cs:41-48`) omits zone label, duration, and price — 3 of the 5 fields `02-VALIDATION.md` line 85 requires. **This is the report's one gap** (see Gaps Summary). |
| 3 | Client can optionally choose a preferred stylist, with slots filtered to that stylist | VERIFIED | `SlotService.GetOpenSlotsAsync(serviceId, stylistId, date)` filters `_dbContext.Stylists.Where(... stylistId == null || stylist.Id == stylistId)` (`SlotService.cs:47`); `AppointmentBookingForm.tsx` stylist chips (lines 360-394) call `handleStylistChange`, which re-fetches slots. `SlotServiceTests.GetOpenSlotsAsync_NoStylistId_ReturnsUnionAcrossActiveStylists_StylistIdFiltersToOne` and `AppointmentsControllerSlotsTests.GetSlots_StylistIdFilter_NarrowsResultSet` both pass per SUMMARY; logic independently read and confirmed correct. |
| 4 | Two near-simultaneous bookings for the same stylist/slot yield exactly one success and one "slot taken" 409, enforced by a DB-level guarantee | VERIFIED | `BookingDbContext.cs:197`: `entity.HasIndex(s => new { s.StylistId, s.SlotStart }).IsUnique()` — **no `HasFilter`**, confirmed by grep. The generated migration (`20260709144653_AddBookingCore.cs:152-156`) creates `IX_AppointmentSlots_StylistId_SlotStart` as a real SQL Server unique index. `AppointmentsService.CreateAsync` (`AppointmentsService.cs:104-120`) catches `DbUpdateException` for SQL 2601/2627 and retries the next candidate — no app-level lock, no manual transaction. `ConcurrencyTests.TwoSimultaneousRequestsForSameSlot_ExactlyOne201AndOne409` (`ConcurrencyTests.cs:37-70`) fires two real concurrent HTTP POSTs against `SqlServerWebApplicationFactory` (real LocalDB) and asserts exactly one 201 + one 409 + exactly one `AppointmentSlot` row — this is a genuine, independently-readable proof of the DB-level guarantee, not an app-level check. |
| 5 | Appointment and availability times are stored as `DateTimeOffset` against a configured salon IANA timezone, verified correct across a DST-transition date | PRESENT_BEHAVIOR_UNVERIFIED (see behavior_unverified_items) | `Appointment.StartsAt` and `AppointmentSlot.SlotStart` are `DateTimeOffset` (confirmed by grep of entity files); the migration types the columns `datetimeoffset(0)`. `SalonTimeZone.ToSalonInstant` correctly resolves DST-gap (null) and DST-ambiguity (deterministic standard-offset) cases, proven by `DstBoundaryTests.cs` (unit-level, no persistence). `DstRoundTripTests.cs` proves the *entity/column* round-trips a DST-transition instant correctly on real SQL Server — but by constructing the `Appointment` directly against `BookingDbContext`, **bypassing `AppointmentsService.CreateAsync` entirely** (confirmed by reading the test: lines 62-89 call `db.Appointments.Add(...)` directly, never `POST /api/appointments`). No test proves the actual shipped write path is DST-correct end-to-end. See dedicated concern #1/#2 analysis below. |

**Score:** 4/5 truths fully verified; 1 truth (SC5) present-and-wired but its stated behavioral proof ("verified correct across a DST-transition date") is not exercised through the shipped write path — routed to human verification, not counted as verified per this workflow's rules.

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `API/ZachHairStudio.Shared/Db/BookingDbContext.cs` | Unfiltered unique index on `(StylistId, SlotStart)` | VERIFIED | Line 197, no `HasFilter` |
| `API/ZachHairStudio.Shared/Migrations/20260709144653_AddBookingCore.cs` | Drops `Bookings`, creates 5 tables + unique index | VERIFIED | `CreateIndex` at lines 147-159 includes `IX_AppointmentSlots_StylistId_SlotStart`; `Booking` table drop confirmed absent from schema (legacy `Booking.cs`/`BookingsController.cs` confirmed deleted, grep returns nothing) |
| `API/ZachHairStudio.Shared/Features/Availability/SlotService.cs` | DST-safe open-slot grid query | VERIFIED | Reads `_dbContext.Stylists/StylistWorkingHours/StylistTimeOff/AppointmentSlots`, in-memory grid math via `SalonTimeZone.ToSalonInstant`, no hardcoded offset |
| `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs` | Retry-loop write path, 409 guarantee, best-effort email | VERIFIED | Lines 104-141: single `SaveChangesAsync` per candidate, `DbUpdateException` 2601/2627 → next candidate, email in its own try/catch after commit (D-11 honored) |
| `API/ZachHairStudio.Shared/Features/Appointments/ResendEmailService.cs` | Real Resend REST call, HTML-encoded, never rethrows | VERIFIED (delivery) / GAP (content completeness) | Delivers successfully (live-verified); body omits zone/duration/price |
| `landing-page/components/AppointmentBookingForm.tsx` | Progressive-reveal `/book` flow with self-sufficient confirmation and 409 recovery | VERIFIED | 4-step reveal (lines 337-583), confirmation panel with all 5 fields (279-331), 409 recovery preserving contact state (252-267) |
| `landing-page/lib/appointments.ts` | Typed client for slots/create/stylists | VERIFIED (existence + typecheck) | `npx tsc --noEmit` run independently by this verifier: **exit clean, no errors** |

### Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| `AppointmentBookingForm.tsx` | `GET /api/appointments/slots` | `fetchOpenSlots` in `lib/appointments.ts`, `cache: no-store` | WIRED | useEffect re-fetches on service/stylist/date/reloadKey change (lines 169-199) |
| `AppointmentBookingForm.tsx` | `POST /api/appointments` | `createAppointment`, `AppointmentApiError.isConflict` branch | WIRED | `handleSubmit` (233-277) branches 409 into recovery UX, else generic error |
| `AppointmentsController.CreateAppointment` | `AppointmentsService.CreateAsync` | direct call, never touches `BookingDbContext` (PLAT-01) | WIRED | Controller constructor-injects `SlotService`/`AppointmentsService`/validator only (confirmed by reading constructor, no `BookingDbContext` reference) |
| `AppointmentsService.CreateAsync` | real unique index (SQL 2601/2627) | `catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))` | WIRED | Confirmed via `ConcurrencyTests` real-SQL proof |
| `AppointmentsService.CreateAsync` | `IEmailService.SendConfirmationAsync` | awaited inside its own try/catch, post-commit | WIRED | `AppointmentsService.cs:124-134` — confirmed a throwing email service cannot roll back the 201 |
| `ResendEmailService` | Resend REST API | `HttpClient.PostAsJsonAsync("emails", payload)` with bearer token from `Program.cs` | WIRED (delivery) | Live-verified 200 response in 02-06; body content incomplete (see Gaps) |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|---|---|---|---|
| Frontend typechecks cleanly against the appointments client + booking form | `cd landing-page && npx tsc --noEmit` | Exit clean, no output/errors | PASS (run independently by this verifier) |
| Backend test suite (94 tests per SUMMARY, including real-SQL SC4/SC5 proofs) | `dotnet test API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` | **Could not execute** — build failed with `MSB3027`/`MSB3021`: `ZachHairStudio.Shared.dll` locked by a running `ZachHairStudio.Api.exe` process (PID 27572, started 2026-07-10 19:59, evidently left over from the Plan 06 live human-verify session) that this verifier's sandbox permissions do not allow terminating. | SKIP — see note below |

**Note on the skipped backend run:** this verifier could not independently execute `dotnet test` because a leftover live API process (started during Plan 06's human-verify session, per the SUMMARY's "API on :5236" note) holds a file lock on `ZachHairStudio.Shared.dll`, and this agent's permissions explicitly deny force-killing an unverified pre-existing process during a verification task. In place of running the suite, this verifier read the full source of every test file cited as proof for SC1–SC5 (`SlotServiceTests.cs`, `DstBoundaryTests.cs`, `ConcurrencyTests.cs`, `DstRoundTripTests.cs`, and the controllers/services they exercise) line-by-line and confirmed the test logic genuinely proves what the SUMMARYs claim, with the one caveat already surfaced in `behavior_unverified_items` (SC5's write-path gap). This is a real limitation of this verification pass — a human or a subsequent run with the lock cleared should re-run `dotnet test API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj` to obtain a fresh, directly-observed pass/fail count before treating SC1–SC4 as fully closed.

### Requirements Coverage

| Requirement | Description | Status | Evidence |
|---|---|---|---|
| BOOK-01 | Real open slots reflecting working hours + bookings | SATISFIED | SlotService, verified above |
| BOOK-02 | End-to-end booking (service → slot → confirm) | SATISFIED | AppointmentsService.CreateAsync + AppointmentBookingForm, verified above |
| BOOK-03 | On-screen + email confirmation | PARTIAL | On-screen: satisfied. Email: delivered but content incomplete against `02-VALIDATION.md`'s own bar — see Gaps |
| BOOK-04 | DB-level double-booking guarantee | SATISFIED | Unfiltered unique index + ConcurrencyTests, verified above |
| BOOK-05 | DateTimeOffset storage against configured salon timezone, DST-verified | PARTIAL | Storage type: satisfied (grep-confirmed). DST-transition proof: present but not through the shipped write path, and no longer tested against the deployed zone — see concern analysis below |
| BOOK-06 | Optional stylist selection with slot filtering | SATISFIED | Verified above |

## Independent Assessment of the Five Flagged Concerns

### Concern 1 — SC5 proven one layer below the HTTP create path

**Confirmed, accurately characterized.** Read `DstRoundTripTests.cs` directly:
lines 62-89 build an `Appointment` + `AppointmentSlot`s and call
`db.Appointments.Add(appointment); await db.SaveChangesAsync();` against
`BookingDbContext` obtained from a DI scope — never `client.PostAsJsonAsync("/api/appointments", ...)`
and never `AppointmentsService.CreateAsync`. The stated reason (both 2026 DST
dates are calendar-fixed and fall outside the create-path's future/60-day-horizon
window relative to the 2026-07-10 test clock) is verified correct by reading
`AppointmentCreateDtoValidator.cs` lines 49-56: `BeInTheFuture` and `BeWithinHorizon`
would indeed reject 2026-03-08 (past) and 2026-11-01 (>60 days from 2026-07-10).

**Is SC5 genuinely satisfied?** Partially. The DST-sensitive logic in this codebase
lives entirely in `SalonTimeZone.ToSalonInstant` (the only place `IsInvalidTime`/
`IsAmbiguousTime`/`GetUtcOffset` are called — confirmed by grep), and that helper
IS unit-tested directly across both boundary dates including gap/ambiguity edge
cases (`DstBoundaryTests.cs`). `AppointmentsService.BuildAppointment` does no
DST-specific math of its own — it takes an already-resolved `DateTimeOffset` and
adds fixed 15-minute increments. So the risk that the *shipped* write path
diverges from what `DstRoundTripTests` proves is architecturally low. But it is
not zero-risk and not proven: `AppointmentsService.CreateAsync`'s slot-matching
step (`openSlots.FirstOrDefault(slot => slot.StartsAt.ToUniversalTime() == requestedInstantUtc)`,
`AppointmentsService.cs:75`) and `SlotService.GenerateCandidateStarts`'s full-day
grid loop (`SlotService.cs:132-148`) are both DST-sensitive code paths that have
never been exercised end-to-end for a DST-boundary date — `SlotServiceTests.cs`
explicitly picks "a plain midweek Tuesday, safely inside standard time (no DST
edge nearby)" (line 21) to avoid the DST edge, and `AppointmentsControllerSlotsTests.cs`
does not touch a DST date either. **Verdict: present and low-risk, but not
behaviorally proven as SC5 literally requires ("verified correct across a
DST-transition date"). Routed to human verification, not counted as VERIFIED.**

### Concern 2 — Salon timezone changed to Asia/Yangon (fixed +06:30, no DST); DST tests still hardcode America/New_York

**Confirmed, accurately characterized.** `appsettings.json:13`,
`appsettings.Development.json:12`, `SalonOptions.cs:10` (the class default), and
`AppointmentBookingForm.tsx:19` (`const SALON_TIME_ZONE = "Asia/Yangon"`) all
independently confirm the deployed configuration is `Asia/Yangon`. `DstBoundaryTests.cs:13`
and `DstRoundTripTests.cs:49` both construct `new SalonTimeZone("America/New_York")`
directly rather than reading `SalonOptions`/config — confirmed by reading both
files. `ConcurrencyTests.cs` and `AnyStylistAssignmentTests.cs`, by contrast,
were updated in the 253ebd9 commit to resolve instants via
`SalonTimeZone.FromOptions(new SalonOptions())`, so they do follow config — this
is a real, if inconsistent, distinction the SUMMARY's "Carried forward" framing
(02-06-SUMMARY.md's Deviation 3) correctly captures.

**Is the DST coverage still meaningful?** For the *shared, generic* `SalonTimeZone`
helper class: yes — it proves the helper correctly handles any IANA zone that
observes DST, which has ongoing value if the salon's zone is ever reconfigured.
For the *deployed configuration*: no — Asia/Yangon has never observed DST (fixed
UTC+6:30 since 1920), so SC5's "verified correct across a DST-transition date"
literally cannot occur for the product as configured, and the existing DST tests
prove nothing about production behavior. This is a real, if low-stakes, gap
between what SC5 as written asks for and what the shipped system needs — flagged
for the owner: either (a) accept that SC5's DST clause is now vacuously
satisfied because Yangon has no DST edge to fail on, or (b) treat SC5 as
unverifiable-as-written for this deployment and strike/reword it.

### Concern 3 — Confirmation email missing zone label, duration, and price

**Confirmed by direct code read**, not merely accepted from the SUMMARY.
`ResendEmailService.cs:41`: `var when = appointment.StartsAt.ToString("ddd d MMM yyyy, h:mm tt");`
— no zone suffix (`zzz` or similar) anywhere in the format string or the
surrounding HTML (lines 43-48). Neither `service.DurationMinutes` nor
`service.Price` appears anywhere in the method, despite `ServiceResponseDto`
(passed in as the `service` parameter) carrying both fields — confirmed by
reading the method signature `SendConfirmationAsync(Appointment appointment, ServiceResponseDto service, string stylistName)`
and the full body.

**Impact on the phase goal:** Low-to-moderate. The core value ("booking must be
effortless… if everything else fails, this must work") is explicitly protected
by D-11: the on-screen confirmation (verified above to carry all 5 fields) is the
load-bearing artifact, and the email is documented as best-effort supplementary.
So the phase's *primary* goal — a confirmed, non-double-booked appointment — is
unaffected. But `02-VALIDATION.md`'s own Manual-Only Verifications table (line 85)
explicitly names all five fields as the acceptance bar for the email check, and
three are missing. **This is treated as this report's one actionable gap** (see
Gaps Summary) — a small, well-scoped fix (`ResendEmailService.cs` only), not a
structural problem.

### Concern 4 — Plan 06 declared `files_modified: []`, but 11 files changed

**Confirmed by independent `git show --stat 253ebd9`**: 11 code files (plus the
SUMMARY.md itself, 12 total) were changed in the single commit for this plan —
`AnyStylistAssignmentTests.cs`, `AppointmentsControllerTests.cs`,
`ConcurrencyTests.cs`, `appsettings.Development.json`, `appsettings.json`,
`ResendEmailService.cs`, `ResendOptions.cs`, `SalonOptions.cs`,
`AppointmentBookingForm.tsx`, `Navbar.tsx`, `lib/data.ts` — matching the
SUMMARY's own account exactly. This is a process-transparency issue, not a
functional one: the plan was declared as a pure human-verify checkpoint with no
code changes, but the checkpoint surfaced real defects (unverified Resend
domain, dead nav links) and an owner-directed config change (timezone), all of
which required code. **The deviation was fully disclosed in the SUMMARY and the
commit message** rather than silently absorbed — this verifier finds no
evidence of undisclosed scope creep beyond what both documents already state.
Not a gap; a process note.

### Concern 5 — "Email delivery isn't guaranteed" caption removed at owner request

**Confirmed by direct code read.** `AppointmentBookingForm.tsx:289-292` reads:
"Your appointment is confirmed. A confirmation email is on its way — but
everything you need is right here." — no explicit "delivery isn't guaranteed /
save or screenshot this" language, confirming the caption named in
`02-06-SUMMARY.md`'s Deviations section ("Save or screenshot this confirmation
— email delivery isn't guaranteed.") is indeed absent from the shipped
component. This is an explicit, owner-approved, and clearly documented reversal
of a stated D-11 UI must-have — the SUMMARY itself flags it as "a decision
reversal, not a defect fix." Operationally the confirmation remains fully
self-sufficient (all 5 fields still render), so D-11's *substantive* guarantee
(the client has everything they need even if email never arrives) still holds —
only the *prompt telling the client to rely on the screen* was removed. Given
explicit owner sign-off is documented, this verifier does not treat it as a
gap, but flags it per the adversarial brief: **the phase's own PLAN.md
acceptance criteria for this item are now stale relative to shipped behavior**,
and should be updated to reflect the owner's decision rather than left silently
contradicted.

## Anti-Patterns Found

None of severity blocking. No `TBD`/`FIXME`/`XXX` markers found in the files
touched by this phase (spot-checked `AppointmentsService.cs`,
`ResendEmailService.cs`, `AppointmentBookingForm.tsx`, `SlotService.cs`). One
`TODO`-equivalent: `AppointmentBookingForm.tsx`'s own comment notes
"CalendarIcon added but not yet placed" (icon created, unused) — cosmetic,
not user-facing, not a stub.

## Human Verification Required

### 1. SC5 full write-path DST proof

**Test:** Book an appointment through `POST /api/appointments` (not a direct
`BookingDbContext` insert) for an instant on a real DST-transition date in the
salon's *configured* zone, once the zone is one that observes DST (or, if
Asia/Yangon is permanent, accept this as a documented, deliberately-descoped
gap for the current deployment).
**Expected:** The stored `AppointmentSlot.SlotStart`/`Appointment.StartsAt`
carry the correct offset and instant, proving `AppointmentsService.CreateAsync`'s
full candidate-matching and persistence path — not just the isolated
`SalonTimeZone` helper or a hand-duplicated DbContext insert.
**Why human:** Requires either advancing the test clock past a real DST
transition or picking a synthetic near-future one; a judgment call the phase
owner should make, not something this verifier can resolve by reading source.

### 2. Confirmation email content completeness

**Test:** Book a real appointment and inspect the received email.
**Expected:** Per `02-VALIDATION.md` line 85, the email should show service,
stylist, salon-local time **with an explicit zone label**, duration, and price.
**Why human:** Already confirmed as missing by code read (this report's Gaps
Summary) — surfaced here so the owner can decide whether to route this into a
`/gsd-plan-phase 2 --gaps` fix pass before shipping, as `02-06-SUMMARY.md`
itself already recommended.

### 3. Backend test suite re-run

**Test:** Run `dotnet test API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj`
after closing any process holding a lock on `ZachHairStudio.Api.exe`/`ZachHairStudio.Shared.dll`.
**Expected:** 94/94 passing, matching `02-04-SUMMARY.md` and `02-06-SUMMARY.md`'s
reported results.
**Why human:** This verifier's sandbox permissions denied force-killing the
pre-existing locked process (PID 27572); a live, fresh run was not obtained
independently for this report. Static/code review of every cited test file was
performed instead and found consistent with the SUMMARYs' claims, but this is
not a substitute for an executed run.

## Gaps Summary

One actionable gap blocks a clean pass: the confirmation email (`ResendEmailService.cs`)
does not meet the phase's own stated Manual-Only Verification bar for BOOK-03
(`02-VALIDATION.md` line 85: service, stylist, salon-local time+zone, duration,
price — all five required; three missing). The fix is small and additive
(interpolate `service.DurationMinutes`, `service.Price`, and a zone suffix into
the existing HTML string) and does not require touching the write path,
validator, or slot logic. `02-06-SUMMARY.md` already correctly identified this
as a "Known Gap" and recommended routing it into a `/gsd-plan-phase 2 --gaps`
pass — this verification concurs.

The remaining four flagged concerns (SC5's layered proof, the non-DST deployed
zone, the `files_modified: []` mismatch, and the removed confirmation caption)
were all independently confirmed as accurately and transparently characterized
by the phase's own SUMMARYs — none understated their severity, and none rose to
the level of a blocking gap given: (a) the core double-booking guarantee (SC4)
is solidly proven on real SQL Server, (b) the on-screen confirmation (the
D-11 load-bearing artifact) is fully self-sufficient, and (c) the SC5 DST
weakness is architecturally low-risk given where the DST-sensitive code
actually lives.

---

_Verified: 2026-07-10T20:30:00Z_
_Verifier: Claude (gsd-verifier)_

## Post-Verification Reconciliation (2026-07-25)

This section reconciles the 2026-07-10 report above against what has since
shipped and been independently confirmed. It is an evidence-based
reconciliation against `02-UAT.md` (2026-07-23, 9/9 pass) plus git history —
**not** a fresh adversarial re-verification. The original report body above
is preserved unchanged as the historical record; nothing above this section
was altered beyond the frontmatter `status`/`gaps` annotations.

Every item this report raised is now closed or descoped:

- **The one blocking gap (email missing zone label, duration, price) is
  CLOSED.** Fixed in commit `ea8eb85` ("fix(02-06): confirmation email
  carries zone label, duration, and price") and confirmed by `02-UAT.md`
  Test 6: the owner confirmed receipt of the real email, and
  `ResendEmailService.cs` now renders all five required fields (service,
  stylist, salon-local time + zone label, duration, price).
- **SC5's DST-transition proof gap is OWNER-DESCOPED.** `02-UAT.md` Test 9
  (`resolution: descoped`): Asia/Yangon is fixed UTC+06:30 and never
  observes DST, a documented judgment call for this deployment. The
  DateTimeOffset-storage half of BOOK-05 remains separately confirmed live
  (slots return `+06:30` offsets).
- **The "backend suite couldn't run" limitation is CLOSED.** `02-UAT.md`
  Test 8: suite green, 116 passed / 0 failed / 0 skipped of 116.
- **UAT gap `G-02-8` is `resolved`** (`02-UAT.md` Gaps section) and is a
  Phase-3 (DASH-05) test-isolation issue (`IdentitySeederTests` class-shared
  InMemory DB leaking an orphan Owner across tests) — out of Phase-2 scope.
- **Concern 5 (removed confirmation caption)** was already correctly
  characterized by this report as owner-approved and non-blocking; Plan 09
  separately reconciles the two Phase-2 plans (`02-05-PLAN.md`,
  `02-06-PLAN.md`) that still asserted the caption as a requirement, so the
  planning record no longer contradicts shipped behavior.

This reconciliation is evidence-based — commit `ea8eb85`, direct code
(`ResendEmailService.cs`), and owner-confirmed `02-UAT.md` results — not a
new adversarial verdict.
