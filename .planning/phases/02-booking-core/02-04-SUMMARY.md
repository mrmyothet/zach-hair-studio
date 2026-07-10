---
phase: 02-booking-core
plan: 04
subsystem: api
tags: [dotnet, efcore, sqlserver, datetimeoffset, fluentvalidation, resend, httpclient, concurrency, dst, booking]

# Dependency graph
requires:
  - phase: 02-01
    provides: Appointment/AppointmentSlot entities, unfiltered UNIQUE(StylistId, SlotStart) index, datetimeoffset(0) column
  - phase: 02-02
    provides: SqlServerWebApplicationFactory (real LocalDB), Program.cs unconditional AddUserSecrets, RESEND_API_KEY user-secret
  - phase: 02-03
    provides: SlotService open-slot grid, SalonTimeZone DST helper, SalonOptions
provides:
  - AppointmentCreateDto + AppointmentCreateDtoValidator (future / on-15-min-grid / <=60-day horizon)
  - AppointmentResponseDto + AppointmentExtensions.ToDto(Appointment, Service, Stylist)
  - AppointmentsService.CreateAsync — server-side slot re-validation, deterministic Any-stylist assignment, single-SaveChanges try-insert retry loop (2601/2627 -> 409), best-effort post-commit email
  - IEmailService + ResendEmailService (best-effort, HTML-encoded, never rethrows)
  - POST /api/appointments (201 / 400 / 404 / 409)
  - SC4 concurrency proof + SC5 DST round-trip proof on real SQL Server

affects: [02-05, 02-06, booking-frontend, dashboard, phase-03-appointment-lifecycle]

# Tech tracking
tech-stack:
  added: [System.Net.Http.Json PostAsJsonAsync, System.Net.WebUtility.HtmlEncode, AddHttpClient typed client]
  patterns:
    - "Try-insert-per-candidate retry loop with single SaveChangesAsync (no manual transaction) — atomicity from EF Core implicit transaction; DbUpdateException 2601/2627 -> next candidate"
    - "Server-authoritative slot re-validation against SlotService before insert (never trust the echoed slot)"
    - "Best-effort side effect after commit, guarded at the service layer so no IEmailService impl can roll back the booking"
    - "Non-secret options (ResendOptions.FromEmail) bound from appsettings + IOptions->POCO bridge; secret (RESEND_API_KEY) only on the typed HttpClient header"

key-files:
  created:
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDto.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDtoValidator.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentResponseDto.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentExtensions.cs
    - API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs
    - API/ZachHairStudio.Shared/Features/Appointments/IEmailService.cs
    - API/ZachHairStudio.Shared/Features/Appointments/ResendEmailService.cs
    - API/ZachHairStudio.Shared/Features/Appointments/ResendOptions.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentCreateDtoValidatorTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/ConcurrencyTests.cs
    - API/ZachHairStudio.Api.Tests/Features/Appointments/DstRoundTripTests.cs
  modified:
    - API/ZachHairStudio.Api/Controllers/AppointmentsController.cs
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Api/appsettings.json

key-decisions:
  - "Read the Resend key from flat config RESEND_API_KEY (not Resend:ApiKey) — matches the user-secret name baked in by 02-02"
  - "From-address bookings@zachhairstudio.com (verified sending domain) in appsettings Resend:FromEmail; no ApiKey in any tracked file"
  - "Booking window defaults (owner-reviewable): no same-day/min-lead cutoff (any strictly-future instant), max horizon 60 days"
  - "Any-stylist tie-break = lowest StylistId (deterministic, D-07)"
  - "Distinguish already-booked (409 DuplicateRecord) from off-hours/invalid (404 NotFound) when no candidate is free"
  - "Email awaited but wrapped in service-layer try/catch so a throwing IEmailService can never fail the 201 (load-bearing for the @example.com test recipients)"

patterns-established:
  - "Retry loop catches BOTH SqlException 2601 and 2627; observed number under real concurrency is 2601 (unique INDEX)"
  - "SC4/SC5 proven on SqlServerWebApplicationFactory (real LocalDB), never the InMemory fixture"

requirements-completed: [BOOK-02, BOOK-03, BOOK-04, BOOK-06]

coverage:
  - id: D1
    description: "POST /api/appointments books a valid open slot and returns 201 with full appointment details (service, concrete stylist, salon-local start, status)"
    requirement: BOOK-02
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerTests.cs#Post_ValidBooking_Returns201WithFullDetails"
        status: pass
    human_judgment: false
  - id: D2
    description: "AppointmentCreateDtoValidator enforces future / on-15-min-grid / <=60-day-horizon / email / length rules"
    requirement: BOOK-02
    verification:
      - kind: unit
        ref: "API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentCreateDtoValidatorTests.cs (14 cases)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Deterministic Any-stylist assignment (lowest free StylistId) with fall-through to the next free candidate"
    requirement: BOOK-06
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs (5 cases)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Two simultaneous POSTs for the same (stylist, slot) yield exactly one 201 + one 409, and exactly one AppointmentSlot row — DB unique index, not app check (SC4)"
    requirement: BOOK-04
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Appointments/ConcurrencyTests.cs#TwoSimultaneousRequestsForSameSlot_ExactlyOne201AndOne409 (real SQL Server)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Appointment StartsAt round-trips the correct DST offset (-04:00 spring / -05:00 fall) and instant through the real datetimeoffset column (SC5)"
    requirement: BOOK-02
    verification:
      - kind: integration
        ref: "API/ZachHairStudio.Api.Tests/Features/Appointments/DstRoundTripTests.cs (2026-03-08 / 2026-11-01, real SQL Server)"
        status: pass
    human_judgment: false
  - id: D6
    description: "Confirmation email is a real Resend REST call attempted best-effort AFTER commit, HTML-encoded, and never rolls back the booking on failure"
    requirement: BOOK-03
    verification:
      - kind: integration
        ref: "AppointmentsControllerTests.cs#Post_ValidBooking_AttemptsConfirmationEmailAfterCommit + #Post_ValidBooking_EmailThrows_StillReturns201"
        status: pass
    human_judgment: true
    rationale: "Automated tests prove the send is attempted post-commit and that a failure does not break the booking, but that a real Resend message is actually delivered to a real inbox (verified domain, correct body/branding) needs a human to inspect a real mailbox — test recipients are RFC 2606 @example.com and are deliberately rejected."

# Metrics
duration: 13min
completed: 2026-07-10
status: complete
---

# Phase 2 Plan 04: Booking Write Path (AppointmentsService + Resend + SC4/SC5) Summary

**Double-booking-safe POST /api/appointments with a single-SaveChanges try-insert retry loop (2601/2627 -> 409), deterministic Any-stylist assignment, best-effort HTML-encoded Resend email after commit, and SC4 concurrency + SC5 DST round-trip proven green on real SQL Server LocalDB.**

## Performance

- **Duration:** 13 min
- **Started:** 2026-07-10T04:27:13Z
- **Completed:** 2026-07-10T04:40:51Z
- **Tasks:** 3
- **Files modified:** 16 (13 created, 3 modified)

## Accomplishments
- `AppointmentsService.CreateAsync`: FluentValidation -> server-side slot re-validation against `SlotService` (never trusts the echoed slot) -> deterministic candidate resolution (lowest free `StylistId`, D-07) -> per-candidate `SaveChangesAsync` with **no manual transaction** -> `DbUpdateException` 2601/2627 catch -> best-effort post-commit email.
- `POST /api/appointments` maps success->201 (full `AppointmentResponseDto`), validation->400, off-hours/invalid->404, and unique-index conflict->409 `ProblemDetails` in **all** environments (no SqlException leak in a 500).
- `ResendEmailService`: single Resend REST POST via typed `HttpClient`, all client fields `HtmlEncode`-d, whole body catch-and-log, never rethrows (D-11).
- **SC4 proven** on real SQL Server: two concurrent identical POSTs -> exactly one 201 + one 409, exactly one `AppointmentSlot` row. **SC5 proven**: -04:00 / -05:00 DST offsets round-trip through the real `datetimeoffset` column.
- Full suite **94 passed / 0 failed** (baseline 67 -> +27 new tests).

## Task Commits

1. **Task 1 (RED): failing validator + Any-stylist assignment tests** - `448ef2a` (test)
2. **Task 1 (GREEN): create DTO/validator/mapper + AppointmentsService retry loop** - `04fc482` (feat)
3. **Task 2: ResendEmailService + POST endpoint + DI wiring** - `b123fbb` (feat)
4. **Task 3: SC4 concurrency + SC5 DST round-trip tests (real SQL)** - `95dc3af` (test)

## Files Created/Modified
- `Appointments/AppointmentCreateDto.cs` - guest-booking input contract (ServiceId, StylistId?, StartsAt, name/email/phone)
- `Appointments/AppointmentCreateDtoValidator.cs` - future / on-grid / <=60-day-horizon / email / length rules
- `Appointments/AppointmentResponseDto.cs` - full confirmation payload (concrete stylist, duration, price, status)
- `Appointments/AppointmentExtensions.cs` - `ToDto(Appointment, Service, Stylist)`
- `Appointments/AppointmentsService.cs` - the write path (retry loop, 2601/2627 catch, post-commit email)
- `Appointments/IEmailService.cs` + `ResendEmailService.cs` + `ResendOptions.cs` - best-effort Resend integration
- `Api/Controllers/AppointmentsController.cs` - added `[HttpPost] CreateAppointment` (201/400/404/409)
- `Api/Program.cs` - `AddScoped<AppointmentsService>()`, typed `AddHttpClient<IEmailService, ResendEmailService>` with `Bearer RESEND_API_KEY`, `ResendOptions` binding (preserved 02-02's AddUserSecrets block)
- `Api/appsettings.json` - added `Resend:FromEmail` (no ApiKey)
- 5 test files: `AppointmentCreateDtoValidatorTests`, `AnyStylistAssignmentTests`, `AppointmentsControllerTests`, `ConcurrencyTests`, `DstRoundTripTests`

## Decisions Made
- **Booking window (owner-reviewable — flag for owner):** no same-day / minimum-lead cutoff (any strictly-future instant is accepted) and a **60-day maximum horizon**. Defined as `AppointmentCreateDtoValidator.BookingHorizon`. These are placeholder salon defaults per the D-15 seed-price precedent and should be confirmed by the owner.
- **Observed `SqlException.Number` = 2601** under real concurrency (matches the research prediction for a `CREATE UNIQUE INDEX`). Verified by narrowing the catch to 2601-only and re-running `ConcurrencyTests` (still green). The code catches both 2601 and 2627 defensively per D-03.
- **Real Resend send outcome during the test run: rejected (not delivered).** Test recipients are RFC 2606 `@example.com`. In the Testing environment the real send is attempted (D-12) but Resend rejects an `@example.com` recipient; `ResendEmailService` logs a warning and does not rethrow (D-11), so the appointment still commits and the SC4/SC5 assertions hold. No mail reaches any real inbox on a test run.
- Distinguish "already booked" (-> 409 `DuplicateRecord`) from "off working hours / invalid time" (-> 404 `NotFound`) when no candidate stylist is free.

## Deviations from Plan

### Orchestrator-approved deviations (all four applied)

**1. [Approved] Config key `RESEND_API_KEY`, not `Resend:ApiKey`**
- **Task:** 2. Program.cs reads the bearer token from `Configuration["RESEND_API_KEY"]` (the flat user-secret name 02-02 established), not the plan's literal `Resend:ApiKey` (which would resolve null and 401 every send).
- **Files:** `Program.cs`. **Committed in:** `b123fbb`.

**2. [Approved] Real from-address `bookings@zachhairstudio.com`**
- **Task:** 2. Added `Resend:FromEmail = bookings@zachhairstudio.com` (verified sending domain) to `appsettings.json`; no `ApiKey` field added.
- **Files:** `appsettings.json`, `ResendOptions.cs`. **Committed in:** `b123fbb`.

**3. [Approved] RFC 2606 `@example.com` test recipients**
- **Task:** 1, 2, 3. Every guest email in tests is `jane.doe@example.com` / `prior.guest@example.com`. Makes D-11's best-effort contract load-bearing: a rejected send does not fail `ConcurrencyTests`/`DstRoundTripTests`. Verified explicitly via `Post_ValidBooking_EmailThrows_StillReturns201` and the service-layer try/catch around the email call.
- **Files:** all test files. **Committed in:** `04fc482`, `b123fbb`, `95dc3af`.

**4. [Approved] Preserved Program.cs user-secrets block**
- **Task:** 2. The 02-02 `AddUserSecrets<Program>` + `AddEnvironmentVariables` block (lines 11-14) was left exactly as-is; only DI registrations were appended.
- **Files:** `Program.cs`. **Committed in:** `b123fbb`.

### Auto-fixed / execution deviations

**5. [Rule 3 - Blocking] SC5 DST round-trip proven at the SalonTimeZone + datetimeoffset layer, not through HTTP validation**
- **Found during:** Task 3. Both DST transition dates are calendar-fixed and fall outside the create-path booking window relative to the test clock (2026-07-10): `2026-03-08` is in the **past** and `2026-11-01` is **beyond the 60-day horizon**, so the (correct) future/horizon validator returned 400 before persistence. A past date can never be booked through the public API by design.
- **Fix:** `DstRoundTripTests` now resolves the stored instant with the real `SalonTimeZone` (the same helper the create path uses for its stored offset), then persists the `Appointment` + `AppointmentSlot`s exactly as `AppointmentsService.BuildAppointment` does, against the real SQL fixture, and asserts the offset (-04:00 / -05:00) and instant round-trip. This proves the two things SC5 requires — correct DST offset resolution and real `datetimeoffset` persistence — without disabling a legitimate booking guard. Documented in the test file's XML doc.
- **Files:** `DstRoundTripTests.cs`. **Verification:** both theory cases pass on real SQL Server. **Committed in:** `95dc3af`.

**6. [Rule 2 - Missing Critical] Service-layer guard around the confirmation email**
- **Found during:** Task 1/2. D-11 requires that a send failure never rolls back the booking; relying solely on `ResendEmailService`'s internal catch would let any *other* `IEmailService` implementation (including a throwing test double, or a future impl) break the 201.
- **Fix:** `AppointmentsService.CreateAsync` awaits `SendConfirmationAsync` inside its own try/catch after commit, so no implementation can cost a client their slot.
- **Files:** `AppointmentsService.cs`. **Verification:** `Post_ValidBooking_EmailThrows_StillReturns201` passes. **Committed in:** `04fc482`.

**7. [Rule 3 - Blocking] Introduced `ResendOptions` POCO for the non-secret FromEmail**
- **Found during:** Task 2. To read `Resend:FromEmail` in the Shared project without adding a configuration-abstractions dependency, mirrored the existing `SalonOptions` IOptions->POCO bridge.
- **Files:** `ResendOptions.cs`, `Program.cs`. **Committed in:** `b123fbb`.

---

**Total deviations:** 4 orchestrator-approved + 3 execution (1 blocking test-design, 1 missing-critical, 1 blocking DI).
**Impact on plan:** No scope creep. All correctness/security constraints from the plan's prohibitions hold (no BeginTransaction, 2601+2627 catch, email outside any transaction + never rethrows, HtmlEncode on client fields, server-side slot re-validation, 409 in all environments, real-SQL fixture for SC4/SC5).

## Prohibitions Verification
- **No `BeginTransaction`** in `AppointmentsService` — atomicity from a single `SaveChangesAsync` per candidate. Confirmed by grep.
- **`IsDuplicateKeyViolation` checks both 2601 and 2627.** Observed number under real concurrency: **2601**.
- **Resend call after `SaveChangesAsync`, never in a transaction, never rethrows** (service-layer try/catch + `ResendEmailService` internal catch).
- **`WebUtility.HtmlEncode`** on name/email before HTML interpolation.
- **Server re-validates `StartsAt`** against `SlotService` before insert; the unique index is the final backstop.
- **2601/2627 -> 409 in all environments** (mapping lives in the controller, not env-gated).
- **`ConcurrencyTests` / `DstRoundTripTests` use `SqlServerWebApplicationFactory`** (real LocalDB), not `CustomWebApplicationFactory`.

## Known Stubs
None — all shipped code is wired to real data sources and the real Resend REST API.

## Issues Encountered
- DST test dates violated the booking-window validator (see deviation 5) — resolved by proving SC5 at the SalonTimeZone + datetimeoffset layer.

## Verification Results
- `dotnet build API/ZachHairStudio.Api.Tests` — **7 warnings, 0 errors**. All 7 are the known pre-existing CS8601 in `Result.cs` (out of scope). New code adds 0 warnings.
- `dotnet test --filter "ConcurrencyTests|DstRoundTripTests"` — **3 passed, 0 failed** (real SQL Server).
- `dotnet test API/ZachHairStudio.Api.Tests` (full) — **94 passed, 0 failed** (baseline 67 + 27 new).
- `git grep -nE "re_[A-Za-z0-9]{10,}" -- 'API/**'` — no Resend-format key in tracked files; `appsettings.json` contains only `FromEmail`.

## User Setup Required
None new — `RESEND_API_KEY` was already configured via `dotnet user-secrets` in Plan 02-02 (confirmed present). Owner should review the placeholder booking-window defaults (no lead-time cutoff, 60-day horizon) and the verified from-address.

## Next Phase Readiness
- The booking write path is complete and double-booking-safe; `AppointmentResponseDto` carries every detail the on-screen confirmation (Plan 05) needs, since the email is best-effort.
- Plan 05 (booking UI / `/book`) and Plan 06 can consume `POST /api/appointments` and `GET /api/appointments/slots`.

## Self-Check: PASSED

---
*Phase: 02-booking-core*
*Completed: 2026-07-10*
