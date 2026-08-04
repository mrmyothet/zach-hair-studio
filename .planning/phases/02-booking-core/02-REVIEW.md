---
phase: 02-booking-core
reviewed: 2026-07-26T06:32:21Z
depth: standard
files_reviewed: 53
files_reviewed_list:
  - .claude/CLAUDE.md
  - API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentCreateDtoValidatorTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerSlotsTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentsControllerTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Appointments/ConcurrencyTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Appointments/DstRoundTripTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Appointments/ResendEmailBodyTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Appointments/ScheduleControllerTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Appointments/StatusUpdateTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Appointments/WritePathOffsetTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Availability/DstBoundaryTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Availability/SlotServiceTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Infrastructure/SqlServerFixtureSmokeTests.cs
  - API/ZachHairStudio.Api.Tests/Features/Stylists/StylistsControllerTests.cs
  - API/ZachHairStudio.Api.Tests/SqlServerWebApplicationFactory.cs
  - API/ZachHairStudio.Api.Tests/TestSupport/BookingDates.cs
  - API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj
  - API/ZachHairStudio.Api/Controllers/AppointmentsController.cs
  - API/ZachHairStudio.Api/Controllers/StylistsController.cs
  - API/ZachHairStudio.Api/Program.cs
  - API/ZachHairStudio.Api/appsettings.Development.json
  - API/ZachHairStudio.Api/appsettings.json
  - API/ZachHairStudio.Shared/Db/BookingDbContext.cs
  - API/ZachHairStudio.Shared/Features/Appointments/Appointment.cs
  - API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDto.cs
  - API/ZachHairStudio.Shared/Features/Appointments/AppointmentCreateDtoValidator.cs
  - API/ZachHairStudio.Shared/Features/Appointments/AppointmentExtensions.cs
  - API/ZachHairStudio.Shared/Features/Appointments/AppointmentResponseDto.cs
  - API/ZachHairStudio.Shared/Features/Appointments/AppointmentSlot.cs
  - API/ZachHairStudio.Shared/Features/Appointments/AppointmentStatus.cs
  - API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs
  - API/ZachHairStudio.Shared/Features/Appointments/IEmailService.cs
  - API/ZachHairStudio.Shared/Features/Appointments/ResendEmailService.cs
  - API/ZachHairStudio.Shared/Features/Appointments/ResendOptions.cs
  - API/ZachHairStudio.Shared/Features/Availability/OpenSlotDto.cs
  - API/ZachHairStudio.Shared/Features/Availability/SalonOptions.cs
  - API/ZachHairStudio.Shared/Features/Availability/SalonTimeZone.cs
  - API/ZachHairStudio.Shared/Features/Availability/SlotService.cs
  - API/ZachHairStudio.Shared/Features/Availability/StylistTimeOff.cs
  - API/ZachHairStudio.Shared/Features/Availability/StylistWorkingHours.cs
  - API/ZachHairStudio.Shared/Features/Stylists/Stylist.cs
  - API/ZachHairStudio.Shared/Features/Stylists/StylistExtensions.cs
  - API/ZachHairStudio.Shared/Features/Stylists/StylistResponseDto.cs
  - API/ZachHairStudio.Shared/Features/Stylists/StylistsService.cs
  - API/ZachHairStudio.Shared/Migrations/20260709144653_AddBookingCore.cs
  - landing-page/app/book/page.tsx
  - landing-page/components/AppointmentBookingForm.tsx
  - landing-page/components/Contact.tsx
  - landing-page/components/Navbar.tsx
  - landing-page/components/icons.tsx
  - landing-page/lib/appointments.ts
  - landing-page/lib/data.ts
findings:
  critical: 3
  warning: 16
  info: 7
  total: 26
status: issues_found
---

# Phase 02: Code Review Report

**Reviewed:** 2026-07-26T06:32:21Z
**Depth:** standard
**Files Reviewed:** 53
**Status:** issues_found

## Summary

The booking core is well-structured: the unfiltered unique index on `AppointmentSlot(StylistId, SlotStart)` is a genuine DB-level double-booking guarantee, the create path re-derives the slot grid server-side instead of trusting the echoed slot, the salon timezone is resolved per-instant through a single helper, and the confirmation email is correctly HTML-encoded and post-commit best-effort. Test coverage is unusually deliberate (real LocalDB for index/offset semantics, InMemory for pure logic).

The defects are concentrated in three places:

1. **Timezone plumbing is one call short of correct.** `AppointmentsService.CreateAsync` derives the query day from `request.StartsAt.DateTime` — the *client's* wall clock, not the salon's — despite `SalonTimeZone.ToSalonLocal` existing specifically to prevent that (its own XML doc says it "MUST" be used). Any client that normalizes the instant to UTC or its own offset gets a legitimate open slot rejected as 404. The web UI happens to echo the salon offset verbatim, which masks the bug end-to-end.
2. **No input bounds on public date parameters.** `GET /api/appointments/slots?date=9999-12-31` (anonymous, no auth) throws an unhandled `ArgumentOutOfRangeException` out of `SlotService`. Same class of crash on the staff schedule range.
3. **No abuse controls on the write path.** `POST /api/appointments` is anonymous, unauthenticated, unthrottled, requires no email verification, writes durable rows that block real revenue, and triggers an outbound email to an attacker-chosen recipient from the salon's verified sending domain.

Everything below is reachable from shipped code paths; none of it is speculative style commentary.

## Structural Findings (fallow)

No `<structural_findings>` block was supplied with this review. All findings below are narrative (direct-read) findings.

## Critical Issues

### CR-01: Booking day is derived from the client-supplied UTC offset, not the salon zone

**File:** `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs:74`

**Issue:** `var date = DateOnly.FromDateTime(request.StartsAt.DateTime);` — `DateTimeOffset.DateTime` returns the wall-clock component *of the offset the client sent*, not the salon-local wall clock. That `date` then selects which day's slot grid is recomputed (line 88). If a client sends the exact same instant with a different (equally valid) offset, the wrong day is queried, `freeCandidates` comes back empty, and the request falls through to line 108 — a 404 "That time is not an available slot" for a slot that is genuinely open.

Concretely, for a slot at `2026-08-14T09:00:00+06:30` (Asia/Yangon):
- Client echoes the salon offset → `date = 2026-08-14` → works (this is what the current web UI does, which is why tests pass).
- Client normalizes to its own zone, e.g. US Pacific `2026-08-13T19:30:00-07:00` → `date = 2026-08-13` → **404 on a valid slot.**
- For a salon configured to a negative-offset zone (the code's own comments and `landing-page/lib/appointments.ts:12` use `-04:00` as the example), a UTC-normalized evening booking (`2026-08-14T20:00-04:00` → `2026-08-15T00:00Z`) rolls the date forward → **404 on a valid slot.**

This directly contradicts the contract on `SalonTimeZone.ToSalonLocal` (`SalonTimeZone.cs:47-54`), which exists for exactly this conversion and says it must never be bypassed with a raw `.DateTime`/`.TimeOfDay` read. The instant-equality check at line 89 is a correct trust anchor, so this fails *closed* rather than open — but "fails closed" here means "the salon silently loses bookings from any non-browser client."

**Fix:**
```csharp
// Salon-local date of the requested instant, resolved through the single salon-zone
// helper — the echoed offset must not decide which day's grid is recomputed.
var date = DateOnly.FromDateTime(_salonTimeZone.ToSalonLocal(request.StartsAt));
```
Add a regression test that posts the same instant expressed with a foreign offset (e.g. `startsAt.ToUniversalTime()` and `startsAt.ToOffset(TimeSpan.FromHours(-7))`) and asserts 201 in all cases.

### CR-02: Unbounded `DateOnly` query parameter crashes the public slots endpoint (unhandled 500)

**File:** `API/ZachHairStudio.Shared/Features/Availability/SlotService.cs:27-29` (entry point `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs:27-35`)

**Issue:** `GetOpenSlotsAsync` accepts any bindable `DateOnly` and immediately does unguarded date math:

```csharp
var dayStartLocal = date.ToDateTime(TimeOnly.MinValue);
var dayStartUtc = _salonTimeZone.ToSalonInstant(dayStartLocal);
var dayEndUtc = _salonTimeZone.ToSalonInstant(dayStartLocal.AddDays(1));
```

Two reachable throws, both from an **anonymous, unauthenticated** GET:
- `GET /api/appointments/slots?serviceId=1&date=9999-12-31` → `dayStartLocal.AddDays(1)` overflows `DateTime.MaxValue` → `ArgumentOutOfRangeException`.
- `GET /api/appointments/slots?serviceId=1&date=0001-01-01` → inside `ToSalonInstant`, `new DateTimeOffset(0001-01-01T00:00, +06:30)` has a UTC representation before `DateTime.MinValue` → `ArgumentOutOfRangeException` (`SalonTimeZone.cs:40` and `:44`).

Neither is caught anywhere; the request 500s. In Development (`Program.cs:142-147`, developer exception page semantics) the stack trace is returned to the caller, which is the exact class of leak `AppointmentsController.cs:69-71` claims to have eliminated for the 2601/2627 path. The same pattern exists on the staff path: `AppointmentsService.ListByDateRangeAsync` (`AppointmentsService.cs:168-169`) calls `to.AddDays(1)` and `ToSalonInstant` on unvalidated `from`/`to`, so `GET /api/schedule?from=9999-12-31&to=9999-12-31` 500s for any authenticated staff user. `ListByDateRangeAsync` additionally has no maximum-range guard and no `from <= to` check, so a single request can dump every appointment row (guest names, emails, phones) in the database.

**Fix:** Validate bounds before doing date math, in both places.
```csharp
// SlotService.GetOpenSlotsAsync
private static readonly DateOnly MinQueryDate = new(2000, 1, 1);
private static readonly DateOnly MaxQueryDate = new(2100, 1, 1);

if (date < MinQueryDate || date >= MaxQueryDate)
{
    return Array.Empty<OpenSlotDto>();
}
```
```csharp
// AppointmentsService.ListByDateRangeAsync — guard before ToSalonInstant/AddDays
if (from < MinQueryDate || to >= MaxQueryDate || to < from || to.DayNumber - from.DayNumber > 92)
{
    return Result<IReadOnlyList<AppointmentResponseDto>>.ValidationError(
        "The requested date range is invalid or wider than the 92-day maximum.");
}
```

### CR-03: Anonymous booking endpoint has no abuse controls — calendar flooding and arbitrary-recipient email sending

**File:** `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs:37-83`

**Issue:** `POST /api/appointments` is anonymous by design (guest booking, D-15) but ships with zero abuse mitigation:
- No rate limiting anywhere in `Program.cs` (no `AddRateLimiter`/`UseRateLimiter`), no CAPTCHA/honeypot, no email ownership check, no per-IP or per-email booking cap.
- CORS is `AllowAnyOrigin()` (`Program.cs:36-40`), so any page on the internet can drive it.
- Each accepted request writes `Appointment` + N `AppointmentSlot` rows that **hold the unique index** and are only released by an authenticated staff cancel (`AppointmentsService.cs:278-281`). A trivial script can consume every 15-minute cell for all four stylists across the entire 60-day horizon, taking the product's stated core value ("booking a salon appointment is effortless… if everything else fails, this must work") completely offline, with no self-service recovery.
- Each accepted request also causes an outbound email from the salon's verified Resend domain to an **attacker-supplied recipient** (`AppointmentsService.cs:142` → `ResendEmailService.cs:77-85`, `to = appointment.Email`) whose body contains attacker-supplied first/last name. The values are HTML-encoded so this is not HTML injection, but it is an unauthenticated send-to-anyone primitive: quota burn, deliverability/reputation damage on `media.zachhairstudio.com`, and a harassment vector.

**Fix:** Gate the write path before shipping publicly.
```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("booking-write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10) }));
});
// ...
app.UseRateLimiter();
```
```csharp
// AppointmentsController
[HttpPost]
[EnableRateLimiting("booking-write")]
public async Task<ActionResult<AppointmentResponseDto>> CreateAppointment(...)
```
Also add a per-email cap on outstanding Confirmed appointments (e.g. max 3 future bookings per email address) inside `CreateAsync`, and restrict CORS to the known frontend origins rather than deferring the whole question to Phase 8.

## Warnings

### WR-01: `Created(...)` Location header points at an endpoint that does not exist, and unhandled result types can 201 with a null body

**File:** `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs:82`

**Issue:** `return Created($"/api/appointments/{result.Data.Id}", result.Data);` advertises `GET /api/appointments/{id}`, but no such action exists — appointment detail lives at `GET /api/schedule/{id}` (`ScheduleController.cs:54`). Any client that follows the Location header gets a 404. Separately, the method only branches on `IsValidationError`/`IsNotFound`/`IsDuplicateRecord`; any other error type (`SystemError`, `Error`) falls through to line 82, where `result.Data` is `default!` → `NullReferenceException` (500) or a 201 with a `null` body. `CreateAsync` does not currently return those types, so this is latent, but it is one refactor away from being live.

**Fix:** Point the header at the real resource (or drop it) and fail closed on unhandled result types.
```csharp
if (!result.IsSuccess)
{
    return Problem(title: "Booking failed", detail: result.Message,
                   statusCode: StatusCodes.Status500InternalServerError);
}

return Created($"/api/schedule/{result.Data.Id}", result.Data);
```

### WR-02: Status update has no optimistic concurrency — a concurrent Cancel + Complete can free a Completed appointment's slots

**File:** `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs:250-290`

**Issue:** `UpdateStatusAsync` re-reads the current status (good) but there is no concurrency token (`Appointment` has no `RowVersion`, `BookingDbContext.cs:194-210` configures none). Two staff acting at the same time both read `Confirmed`, both pass `IsAllowedTransition`, and both commit. If one is `Cancelled` (which executes `RemoveRange(appointment.Slots)` at line 280) and the other is `Completed`, the final state can be **status = Completed with all `AppointmentSlot` rows deleted** — the cell is now free for someone else to book on top of a completed appointment, defeating the SC4 guarantee the phase is built around. The audit line (`StatusChangedBy`) also silently records only the last writer.

**Fix:** Add a rowversion and let EF surface the conflict.
```csharp
// Appointment.cs
[Timestamp]
public byte[]? RowVersion { get; set; }
```
```csharp
// UpdateStatusAsync
try
{
    await _dbContext.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException)
{
    return Result<AppointmentResponseDto>.ValidationError(
        "This appointment was changed by someone else. Reload and try again.");
}
```

### WR-03: `SlotService` ignores `Service.IsActive`, so slots are offered for services that cannot be booked

**File:** `API/ZachHairStudio.Shared/Features/Availability/SlotService.cs:38-42`

**Issue:** `GetOpenSlotsAsync` uses `_dbContext.Services.FindAsync(serviceId)` and only checks `is null`, while `AppointmentsService.CreateAsync:66` rejects with 404 when `!service.IsActive`. The read and write paths disagree: deactivating a service still returns a full grid of bookable-looking times, and the client only discovers the truth on submit — after filling in the whole form — as a 404 "Service not found."

**Fix:**
```csharp
var service = await _dbContext.Services.FindAsync(serviceId);
if (service is null || !service.IsActive)
{
    return Array.Empty<OpenSlotDto>();
}
```

### WR-04: Past times are returned as open slots for the current day

**File:** `API/ZachHairStudio.Shared/Features/Availability/SlotService.cs:132-147`

**Issue:** `GenerateCandidateStarts` walks the whole working-hours window with no "not in the past" filter. Querying today at 16:00 returns 09:00, 09:15, … as open slots (and querying any past date returns a full grid — `SlotServiceTests` relies on this by using the fixed past date 2026-07-14). The frontend renders them as clickable buttons (`AppointmentBookingForm.tsx:476-499`), and the user only learns otherwise when `AppointmentCreateDtoValidator.BeInTheFuture` rejects the POST — surfacing the raw validator text `"StartsAt must be in the future."` in the UI via `extractErrorMessage` (`landing-page/lib/appointments.ts:100-106`). That is a broken primary path on the product's core value.

**Fix:** Filter candidates against now (with the same lead-time policy the validator uses).
```csharp
var nowUtc = DateTimeOffset.UtcNow;
// ...
if (candidateInstant.Value <= nowUtc)
{
    continue;
}
```

### WR-05: Price and duration are not snapshotted — editing a service rewrites confirmed appointment history

**File:** `API/ZachHairStudio.Shared/Features/Appointments/AppointmentExtensions.cs:17-18`

**Issue:** `ToDto` reads `service.DurationMinutes` and `service.Price` from the live `Service` row. `Appointment` stores neither (`Appointment.cs:5-38`). When the owner edits a service's price or duration, every past and pending appointment retroactively reports the new value in the staff dashboard and in `GET /api/schedule` — including appointments already `Completed`. Duration is worse than cosmetic: the number of reserved `AppointmentSlot` cells was fixed at booking time, so after a duration edit the DTO's `DurationMinutes` no longer matches the cells actually held.

**Fix:** Snapshot at booking time in `BuildAppointment` and read the snapshot in `ToDto`.
```csharp
// Appointment.cs
public int DurationMinutes { get; set; }
[Precision(18, 2)] public decimal Price { get; set; }
```

### WR-06: `RESEND_API_KEY` is not validated at startup — a missing key silently disables every confirmation email

**File:** `API/ZachHairStudio.Api/Program.cs:64-69`

**Issue:** `new AuthenticationHeaderValue("Bearer", builder.Configuration["RESEND_API_KEY"])` accepts `null` without complaint, producing a bare `Authorization: Bearer` header. Every send then fails with a 401 that is only visible as a `LogWarning` inside `ResendEmailService.cs:91-93`, and the booking still returns 201 — so a misconfigured deployment looks completely healthy while no client ever receives a confirmation. This is inconsistent with the adjacent `Jwt:SigningKey` treatment (`Program.cs:78-86`), which fails fast via `ValidateOnStart`, and with the project constraint in `.claude/CLAUDE.md:21` that declares `RESEND_API_KEY` REQUIRED to run the API.

**Fix:** Fail fast, matching the JWT precedent.
```csharp
var resendApiKey = builder.Configuration["RESEND_API_KEY"];
if (string.IsNullOrWhiteSpace(resendApiKey))
{
    throw new InvalidOperationException(
        "RESEND_API_KEY is missing. Set it via 'dotnet user-secrets set RESEND_API_KEY <key>' (D-12/D-13).");
}
```

### WR-07: CORS `AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()` applies to the whole API

**File:** `API/ZachHairStudio.Api/Program.cs:36-40`

**Issue:** The default policy admits every origin for every method, including the anonymous `POST /api/appointments` write path and the authenticated `/api/schedule` endpoints. The inline comment defers lockdown to Phase 8, which is a recorded decision, but the practical effect today is that any third-party page can drive the booking write path from a victim's browser (amplifying CR-03), and a leaked staff bearer token is usable from any origin.

**Fix:** Restrict now rather than at launch; it costs nothing during development.
```csharp
policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                   ?? ["http://localhost:3000", "http://localhost:3001"])
      .AllowAnyMethod()
      .AllowAnyHeader();
```

### WR-08: `Appointment.ServiceId` / `Appointment.StylistId` have no foreign keys — integrity is enforced only at read time

**File:** `API/ZachHairStudio.Shared/Db/BookingDbContext.cs:194-210`, `API/ZachHairStudio.Shared/Migrations/20260709144653_AddBookingCore.cs:19-38`

**Issue:** `Appointments` has an FK only from `AppointmentSlots` back to itself; `ServiceId` and `StylistId` are bare `int` columns. The service layer compensates by returning a `SystemError` when the referenced row is gone (`AppointmentsService.cs:209-214`, `:233-237`, `:272-276`), which turns into a 500 for staff (`ScheduleController.cs:128-132`). That is defensible defensive coding, but it accepts a permanently corrupt row: deleting a stylist orphans every appointment they held and there is no repair path — the appointment becomes unreadable *and* unchangeable (`UpdateStatusAsync` bails at the same check before any transition), so its slot rows can never be released.

**Fix:** Add restrict-delete FKs in a migration so the bad delete is rejected up front:
```csharp
entity.HasOne<Stylist>().WithMany().HasForeignKey(a => a.StylistId).OnDelete(DeleteBehavior.Restrict);
entity.HasOne<Service>().WithMany().HasForeignKey(a => a.ServiceId).OnDelete(DeleteBehavior.Restrict);
```
If soft-delete is the intended model, keep `IsActive` and document that rows are never hard-deleted — but then the `SystemError` branches are dead code.

### WR-09: A slot whose *later* cells are taken returns 404 instead of 409, bypassing the frontend's conflict-recovery UX

**File:** `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs:96-109`

**Issue:** The "already booked vs never a slot" probe compares only the first cell:
```csharp
.AnyAsync(slot => candidateStylistIds.Contains(slot.StylistId) && slot.SlotStart == request.StartsAt);
```
A 45-minute service reserves 3 cells. If another booking holds only the *second* or *third* cell, the candidate is correctly excluded from the open grid, but this probe finds nothing at `request.StartsAt` and returns 404 "That time is not an available slot." The frontend branches on `err.isConflict` (`AppointmentBookingForm.tsx:253-266`) and therefore skips the 409 recovery path — the user loses the "slot taken, pick another time, your details are preserved" experience and gets a generic error instead. This is the most likely real-world conflict shape, not an edge case.

**Fix:** Probe the whole span the request would occupy.
```csharp
var cellsNeeded = (int)Math.Ceiling(service.DurationMinutes / (double)GridMinutes);
var spanEnd = request.StartsAt.AddMinutes(GridMinutes * cellsNeeded);
var alreadyBooked = await _dbContext.AppointmentSlots
    .AnyAsync(slot => candidateStylistIds.Contains(slot.StylistId)
                      && slot.SlotStart >= request.StartsAt && slot.SlotStart < spanEnd);
```

### WR-10: Three integration test classes perform real Resend network sends on every run

**File:** `API/ZachHairStudio.Api.Tests/Features/Appointments/ConcurrencyTests.cs:39-55`, `API/ZachHairStudio.Api.Tests/Features/Appointments/StatusUpdateTests.cs:108-115`, `API/ZachHairStudio.Api.Tests/Features/Appointments/ScheduleControllerTests.cs:105-112`

**Issue:** Unlike `AppointmentsControllerTests` and `WritePathOffsetTests` (which stub `IEmailService`), these classes POST through the real host with the real `ResendEmailService` registered, so every run fires live HTTPS calls to `api.resend.com` addressed to `jane.doe@example.com`. Consequences: the suite cannot run offline or in CI without the secret (`SqlServerFixtureSmokeTests.cs:82-95` hard-fails when the key is absent), every run burns Resend quota, and the bounces from a non-deliverable domain accumulate against the sending domain's reputation — the same reputation CR-03 also puts at risk. Test outcomes additionally depend on a third-party service being reachable, even though the assertions never inspect the email.

**Fix:** Register a no-op `IEmailService` in these three classes (the pattern already exists in `WritePathOffsetTests.cs:35-43`) and keep exactly one narrowly-scoped, explicitly-traited test for the real-send D-12 proof:
```csharp
private HttpClient CreateClientWithNoOpEmail()
    => _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
    {
        s.RemoveAll<IEmailService>();
        s.AddSingleton<IEmailService, NoOpEmailService>();
    })).CreateClient();
```

### WR-11: Salon timezone is hardcoded in the frontend, duplicating server configuration

**File:** `landing-page/components/AppointmentBookingForm.tsx:19-20`

**Issue:** `const SALON_TIME_ZONE = "Asia/Yangon";` (plus the human-readable caption "Myanmar Time") duplicates `Salon:IanaTimeZoneId` from `appsettings.json:12-14`. Changing the salon's zone in configuration silently leaves the client rendering every slot time and every confirmation in the old zone — a wrong-time-shown bug with no compile-time or test signal. The stale example in `landing-page/lib/appointments.ts:12` (`"2026-08-14T10:00:00-04:00"`) shows the drift has already started.

**Fix:** Return the zone id from the API (e.g. a `salonTimeZone` field on the slots response or a small `/api/salon/settings` endpoint) and format against that, or at minimum read it from `NEXT_PUBLIC_SALON_TIME_ZONE` so a single deploy-time value drives both sides.

### WR-12: Date picker bounds are computed from the browser's local date, not the salon's

**File:** `landing-page/components/AppointmentBookingForm.tsx:57-64`, `:160-164`

**Issue:** `isoDateOffsetFromToday` builds `YYYY-MM-DD` from `new Date()` using the *viewer's* local calendar, then those strings become `min`/`max` on the date input and are sent verbatim to the API as **salon-local** dates. For a viewer whose local date differs from the salon's (routinely true for a UTC+06:30 salon and any Western viewer), `min` is the wrong day: it can block booking the salon's current day, or allow selecting a date that is already past in salon terms (which combines with WR-04 to show unbookable times). The component otherwise takes great care to format every rendered instant with an explicit `timeZone` — this is the one place the browser's zone leaks through.

**Fix:**
```typescript
const salonDateFormatter = new Intl.DateTimeFormat("en-CA", { timeZone: SALON_TIME_ZONE });
function salonDateOffsetFromToday(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return salonDateFormatter.format(d); // en-CA yields YYYY-MM-DD
}
```

### WR-13: Homepage contact form collects name/email/phone and silently discards them

**File:** `landing-page/components/Contact.tsx:128-145` (submit handler at `:54-57`)

**Issue:** The form renders First Name, Last Name, Email Address, and Phone Number inputs, but `handleSubmit` only does `router.push(selectedSlug ? \`/book?service=${selectedSlug}\` : "/book")`. Every value the visitor typed is thrown away and must be retyped on `/book`. The inputs are also uncontrolled and unvalidated, so nothing signals the loss. This is added friction on the exact flow the project defines as its core value.

**Fix:** Either drop the four inputs (leaving service selection + "Continue to Booking"), or carry them through:
```typescript
const params = new URLSearchParams();
if (selectedSlug) params.set("service", selectedSlug);
if (firstName) params.set("firstName", firstName);
// ...then read them as defaults in AppointmentBookingForm
router.push(`/book?${params.toString()}`);
```

### WR-14: Advertised opening hours contradict the bookable schedule

**File:** `landing-page/components/Contact.tsx:109`

**Issue:** The site states "Open Daily: 9:00 AM – 7:30 PM" while the seeded working hours are 09:00–18:00 for every stylist, every day (`BookingDbContext.cs:158-186`). Visitors will look for 18:00–19:30 slots that the grid can never produce and conclude the booking system is broken.

**Fix:** Drive the hours copy from the same source as availability, or correct the static string to 9:00 AM – 6:00 PM until the owner-reviewable schedule is finalized.

### WR-15: Malformed `tel:` link for the second branch

**File:** `landing-page/lib/data.ts:162`

**Issue:** `phone: { display: "09-753 011 309", tel: "+9509753011309" }` keeps the national trunk `0` after the `+95` country code. The correct E.164 form drops it: `+959753011309`. Branch 1 (`:157`) does this correctly (`09-777 190 314` → `+959777190314`), which confirms the intended convention. Tapping the number on mobile fails to dial — a direct loss of phone bookings, the fallback path when online booking fails.

**Fix:**
```typescript
phone: { display: "09-753 011 309", tel: "+959753011309" },
```

### WR-16: Booking validation runs twice, and the service-layer validation branch is unreachable over HTTP

**File:** `API/ZachHairStudio.Api/Controllers/AppointmentsController.cs:40-49` and `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs:58-63`

**Issue:** The controller runs `_createValidator.ValidateAsync(request)` and returns `ValidationProblem` on failure; `CreateAsync` then runs the identical validator again. Over HTTP the service's `ValidationError` return can never be reached, so the controller's `IsValidationError()` branch (`:53-57`) is dead code on this path. Two independent validation sites is a maintenance trap: adding a rule to the controller-side pipeline only (or reordering error mapping) will produce inconsistent responses between the HTTP path and direct service callers (which `AnyStylistAssignmentTests` exercises).

**Fix:** Keep validation in exactly one place. Preferred: delete the controller's explicit validation, let `CreateAsync` own it, and map the returned `ValidationError` to `ValidationProblem` — that also preserves per-property error keys if `CreateAsync` returns the `ValidationResult` rather than a joined string.

## Info

### IN-01: Empty `catch` around the confirmation send with no local logging

**File:** `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs:144-148`

**Issue:** `catch { }` swallows every exception type from `SendConfirmationAsync`, including cancellation and programming errors. The comment reasons that `ResendEmailService` logs its own failures, which is true today — but the catch is on the `IEmailService` abstraction, and any future or test implementation that throws will disappear without a trace.

**Fix:** `catch (Exception ex) { _logger.LogError(ex, "Confirmation email failed for appointment {AppointmentId}", appointment.Id); }` (inject `ILogger<AppointmentsService>`).

### IN-02: Unused field in `AnyStylistAssignmentTests`

**File:** `API/ZachHairStudio.Api.Tests/Features/Appointments/AnyStylistAssignmentTests.cs:31`

**Issue:** `private static readonly SalonTimeZone SalonTz = SalonTimeZone.FromOptions(new SalonOptions());` is never read — all instants come from `BookingDates`.

**Fix:** Delete the field and its comment.

### IN-03: Stale "seed covers Tue-Sat" comments after the seven-day schedule change

**File:** `API/ZachHairStudio.Api.Tests/TestSupport/BookingDates.cs:18-20` (also `ScheduleControllerTests.cs:29-30`)

**Issue:** The seed now covers all seven days (`BookingDbContext.cs:155-186`, migration `20260723023751_OpenSalonEveryDay`), so the documented Tue-Sat rationale for pinning to Wednesday no longer describes reality and will mislead the next reader into thinking the day choice is load-bearing.

**Fix:** Update the comments to state the current invariant (any day is seeded; Wednesday is chosen for determinism).

### IN-04: Stale "test env today is 2026-07-10" comments

**File:** `API/ZachHairStudio.Api.Tests/Features/Appointments/AppointmentCreateDtoValidatorTests.cs:15`, `API/ZachHairStudio.Api.Tests/Features/Appointments/DstRoundTripTests.cs:19-25`

**Issue:** Both files justify their design against a fixed "test clock" of 2026-07-10. The suite has since been made relative to `UtcNow`, so the stated premise (and the DST-date-out-of-horizon rationale) drifts a little further from truth every day.

**Fix:** Restate the rationale relative to "now" rather than a fixed calendar date.

### IN-05: Service slug interpolated into a URL without encoding

**File:** `landing-page/components/Contact.tsx:56`

**Issue:** `router.push(\`/book?service=${selectedSlug}\`)` interpolates raw. Slugs are server-controlled and currently URL-safe, so this is not exploitable, but it breaks the moment a slug contains `&`, `#`, or a space.

**Fix:** `router.push(\`/book?service=${encodeURIComponent(selectedSlug)}\`)`.

### IN-06: `unavailableSlots` is never cleared when the grid reloads

**File:** `landing-page/components/AppointmentBookingForm.tsx:134-136`, `:258`

**Issue:** The 409-recovery set is reset on service/stylist/date changes but not after the triggered refetch. If the refetch fails (`slotsFailed`) and later succeeds via "Try Again", stale entries can keep a legitimately re-freed slot rendered as struck-through and disabled.

**Fix:** Clear the set in the fetch `.then` alongside `setSlots(result)`.

### IN-07: `GridMinutes = 15` duplicated in two classes

**File:** `API/ZachHairStudio.Shared/Features/Appointments/AppointmentsService.cs:23`, `API/ZachHairStudio.Shared/Features/Availability/SlotService.cs:14`

**Issue:** The grid size is the shared contract between slot generation and slot reservation; two private copies means changing one without the other silently produces appointments whose reserved cells do not line up with the advertised grid.

**Fix:** Promote to a single `public const int GridMinutes = 15;` on a shared type (e.g. `SlotGrid`) and reference it from both.

---

_Reviewed: 2026-07-26T06:32:21Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
