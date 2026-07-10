# Phase 2: Booking Core - Research

**Researched:** 2026-07-09
**Domain:** Concurrency-safe slot booking (EF Core 10 / SQL Server unique-index guarantees), IANA timezone / DST-correct `DateTimeOffset` handling, transactional email (Resend REST API)
**Confidence:** HIGH (DB-constraint mechanics, EF Core transaction behavior, timezone API) / MEDIUM (Resend payload shape, exact SQL error code EF Core's generated unique index raises) / LOW (booking-horizon defaults — flagged as owner-reviewable per D-15 precedent)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Slot model & double-booking guarantee**
- D-01: Fixed 15-minute time grid within stylist working hours; a booking occupies N consecutive grid cells derived from `Service.DurationMinutes`. Arbitrary start times + overlap detection rejected (no SQL Server exclusion constraint; would need SERIALIZABLE range locks).
- D-02: Grid increment is 15 minutes (Scalp Treatment's 40min rounds up to 45, 5 min lost).
- D-03: Guarantee = occupancy rows + unfiltered unique index. One `Appointment` row + one `AppointmentSlot` child row per occupied 15-minute cell, `UNIQUE (StylistId, SlotStart)`. All cells inserted in a single transaction; a colliding concurrent booking hits a duplicate-key violation (SQL Server error 2627 per CONTEXT.md's framing — **research below shows this needs correction/clarification**, see Landmine 1), caught and translated to 409 "slot taken". A unique index on `(StylistId, StartsAt)` alone is insufficient (overlapping bookings can have different start times).
- D-04: Cancelling deletes `AppointmentSlot` rows, keeps `Appointment` with `Status = Cancelled`. Slot becomes immediately bookable. No filter predicate on the unique index (filtered unique indexes are easy to get subtly wrong). Phase 3's no-show behaves identically: terminal, slot released.

**Stylist & availability model**
- D-05: New `Stylist` entity (Id, Slug, Name, IsActive, DisplayOrder), seeded via EF `HasData` from `landing-page/lib/data.ts` team members. Public Team marketing section keeps its static content (out of scope).
- D-06: Availability = recurring weekly hours + exceptions: `StylistWorkingHours` (StylistId, DayOfWeek, StartTime, EndTime) + `StylistTimeOff` (holidays, sick days, closures). Open slots computed on the fly as `hours − timeOff − bookedCells`. No generation job. Phase 4's staff CRUD edits these same two tables.
- D-07: Stylist selection optional via "Any stylist" (default). Open slots = union across all active stylists. On confirm, server deterministically assigns one free stylist for that slot inside the same transaction (unique index requires a concrete `StylistId` at write time). Confirmation names the concrete assigned stylist. Satisfies BOOK-06.
- D-08: All stylists perform all services in Phase 2. No `Service`↔`Stylist` capability join table. A capability matrix belongs to Phase 4.

**Confirmation email delivery**
- D-09: Ship a real transactional email provider in Phase 2, not a dev-only sink.
- D-10: Provider is Resend, via a single `HttpClient` POST to its REST API. No SDK dependency. Requires verifying a sending domain.
- D-11: Booking commits first; email is best-effort. Send failure is logged and surfaced to staff but never rolls back the appointment. On-screen confirmation must carry every detail the client needs. Never hold a DB transaction open across a third-party network call.
- D-12: ⚠️ Real email sends in Development AND Testing — deliberate, user was shown the conflicts and chose this anyway. Do not reintroduce a fake sender for tests. Accepted trade-offs: `RESEND_API_KEY` becomes required to run the API and the test suite; this relaxes CLAUDE.md's dev-simplicity constraint (update that doc to match); the test suite becomes network-dependent and can burn Resend quota / go flaky.
- D-13: Resend API key lives in `dotnet user-secrets` (dev) and an environment variable (prod) — never `appsettings.json`. gitleaks blocks such commits anyway.

**Legacy booking migration**
- D-14: New `Appointment` entity; retire `Booking` wholesale. Add `Appointment` (ServiceId FK, StylistId FK, `DateTimeOffset StartsAt`, Status) alongside `AppointmentSlot`, `Stylist`, `StylistWorkingHours`, `StylistTimeOff`. Drop `Booking` entity, `Bookings` table, `BookingsController`, `BookingRequestForm` component in the same phase. No backfill needed (dev/test data only, no production users).
- D-15: Booking flow is a single `/book` page with progressive reveal: service → stylist picker → date + slot grid → contact fields (guest booking; accounts arrive Phase 7). Preserves the existing `?service={slug}` deep link. Slot availability fetched client-side as the selected date changes.
- D-16: Salon timezone is a configured IANA id (e.g. `"America/New_York"`) under a `Salon` section in `appsettings.json`. Slot grids and confirmations always render in salon-local time with the zone explicitly labelled ("Fri 10 Jul, 10:00 AM EDT"), never converted to browser timezone.

### Claude's Discretion
- Exact FluentValidation rules for appointment create DTOs (field lengths, lead-time bounds, email/phone format).
- The concrete SQL/LINQ shape of the open-slot query, and whether slot computation lives in a `SlotService` separate from `AppointmentsService`.
- Tie-breaking rule for "Any stylist" assignment (lowest `StylistId`, least-booked that day, round-robin) — any deterministic rule is acceptable.
- Entity/DTO naming, mapper extension placement, feature-folder layout (must follow `Features/Services` template).
- Booking horizon, minimum lead time, same-day booking — not discussed; pick sensible salon defaults, flag as owner-reviewable (per Phase 1's D-15 precedent for seed prices).
- `/book` page visual design, empty states, 409 recovery UX.
- Whether the frontend API client is OpenAPI-generated or hand-written.

### Deferred Ideas (OUT OF SCOPE)
- Retiring the static `team[]` content in favor of an API-backed Team section — the `Stylist` entity makes it possible, UI work is out of scope.
- `Service`↔`Stylist` capability matrix — belongs to Phase 4.
- Transactional-outbox retry for confirmation emails (durable outbox + `BackgroundService`) — set aside for D-11's simpler best-effort send. Revisit in Phase 8 if undelivered confirmations prove a real problem.
- Booking horizon / lead-time rules — left as Claude's discretion with owner-reviewable defaults.
- "Slot just got taken" recovery UX on `/book` — left to design, candidate for a `/gsd-ui-phase 2` pass.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| BOOK-01 | Client can see real open appointment slots for a chosen service, reflecting stylist working hours and existing bookings | See "Availability Model & Open-Slot Query" pattern; server-evaluable LINQ shape documented in Code Examples |
| BOOK-02 | Client can book an appointment by picking a service, then an open slot, then confirming | See "Booking Confirm Flow" pattern (candidate-stylist retry loop, single `SaveChangesAsync` atomicity) |
| BOOK-03 | Client receives an on-screen and email confirmation for a booked appointment | See "Resend Integration" pattern; D-11 best-effort send after commit |
| BOOK-04 | System prevents double-booking a stylist for the same slot, DB-level guarantee | See "DB-Level Double-Booking Guarantee" — unique index design, error-code detection, atomicity, retry-strategy interaction (Landmines 1-3) |
| BOOK-05 | Appointment/availability times stored as `DateTimeOffset` against configured salon IANA timezone | See "DateTimeOffset + IANA Timezone Across DST" — `TimeZoneInfo` cross-platform behavior, DST edge-case handling, SQL Server `datetimeoffset` semantics |
| BOOK-06 | Client can choose a preferred stylist during booking (slots filtered by stylist) | See D-07 "Any stylist" union query and deterministic assignment pattern |
</phase_requirements>

## Summary

This phase's correctness hinges on three independently well-documented but easy-to-combine-wrong mechanisms: (1) SQL Server's duplicate-key error numbering, which differs for a **unique index** (2601) versus a **unique constraint** (2627) — EF Core's `HasIndex().IsUnique()` fluent API generates a `CREATE UNIQUE INDEX` migration, meaning **2601 is the code that actually fires**, not 2627 as CONTEXT.md's D-03 assumed; defensive code should catch both, but planning must not silently rely on 2627 alone. (2) EF Core's `SaveChangesAsync()` already wraps multiple pending inserts (one `Appointment` + N `AppointmentSlot` rows) in an **implicit transaction** — no manual `BeginTransactionAsync()` is needed for D-03's atomicity requirement, and introducing one would trigger the well-documented `SqlServerRetryingExecutionStrategy` incompatibility with user-initiated transactions (`InvalidOperationException: ... does not support user-initiated transactions`) unless wrapped in `CreateExecutionStrategy().ExecuteAsync(...)`. The simplest, correct design avoids manual transactions entirely: try-insert per candidate stylist, catch the duplicate-key exception, retry the next candidate. (3) `EnableRetryOnFailure`'s transient-error detection does **not** include constraint-violation error numbers (2601/2627 are data-integrity errors, not transient connection failures), so a 409 will not be silently swallowed by a retry — but this must be verified defensively rather than assumed, since the plan should not blindly add 2601/2627 to `errorNumbersToAdd`.

The timezone requirement is solved entirely by BCL APIs available since .NET 6: `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")` resolves identically on Windows and Linux (cross-platform ICU-backed IANA/Windows ID conversion), and `TimeZoneInfo.IsInvalidTime()` / `IsAmbiguousTime()` / `GetAmbiguousTimeOffsets()` give exact, testable DST-edge detection. SQL Server's `datetimeoffset` column type compares, sorts, and indexes values by their **UTC instant**, so the unique index behaves correctly straddling a DST boundary without special-casing.

The existing test harness (`CustomWebApplicationFactory` + EF Core InMemory provider) **cannot prove SC4 or SC5** — the InMemory provider does not enforce unique indexes at all (confirmed: two rows with the same alternate key insert silently, no exception) and has no real SQL Server `datetimeoffset`/error-code semantics. A second, real-SQL-Server-backed test fixture (LocalDB) is a Wave 0 requirement, not an enhancement — this is the single most consequential planning implication of this research.

**Primary recommendation:** Build the double-booking guarantee as occupancy rows + `HasIndex(x => new { x.StylistId, x.SlotStart }).IsUnique()`, insert via a single `SaveChangesAsync()` call per candidate stylist (no manual transactions), catch `DbUpdateException` whose inner `SqlException.Number` is 2601 **or** 2627 and map to `Result<T>.DuplicateRecordError(...)` → HTTP 409; store all times as `DateTimeOffset` computed via `TimeZoneInfo` DST-aware conversion from salon-local wall time; and add a real-SQL-Server (LocalDB) `WebApplicationFactory` test fixture specifically for the concurrency and DST tests, alongside the existing InMemory fixture for everything else.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Slot grid generation & DST-safe time math | API / Backend (`SlotService`) | — | Must be server-evaluable and authoritative; client never computes slot validity |
| Open-slot query (working hours − time off − booked cells) | API / Backend | Database / Storage (source rows) | LINQ-to-Entities filters at the DB tier (stylist, date range); grid math runs in-memory over the filtered result set |
| Double-booking prevention | Database / Storage (unique index) | API / Backend (exception translation) | SC4 explicitly requires a DB-level guarantee, not an app-level check; the API tier only translates the DB's rejection into a clean 409 |
| Appointment confirmation UI | Browser / Client (`/book` page) | Frontend Server (RSC data fetch for services) | Progressive-reveal form is client interaction; initial service list can be RSC-fetched like Phase 1 |
| Confirmation email | API / Backend (`HttpClient` → Resend) | External service (Resend) | Never client-side; API holds the only secret (`RESEND_API_KEY`) |
| Timezone display formatting | Browser / Client | API / Backend (source of truth for the offset) | API returns `DateTimeOffset` (absolute instant + offset); frontend renders the zone label using the salon's known IANA id, not the browser's local zone |
| Stylist/availability persistence | Database / Storage | API / Backend (`StylistsService`, future `AvailabilityService`) | Same two tables (`StylistWorkingHours`, `StylistTimeOff`) Phase 4 later exposes via staff CRUD — one system |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 (already referenced) | ORM + unique index + `datetimeoffset` mapping | Already the project's ORM; no new package |
| FluentValidation / FluentValidation.DependencyInjectionExtensions | 12.1.1 (already referenced) | Appointment/Stylist create DTO validation | Established in Phase 1 (PLAT-02) |
| System.Net.Http.HttpClient (BCL) | net10.0 | Resend REST API calls | D-10 explicitly rejects a Resend SDK; `HttpClient` is already part of the framework, zero new dependency |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `TimeZoneInfo` (BCL, `System` namespace) | net10.0 | IANA timezone resolution + DST edge detection | Every slot-time conversion (salon-local wall time ↔ `DateTimeOffset`) |
| Microsoft.EntityFrameworkCore.SqlServer (test project) | 10.0.9 | Real-SQL-Server (LocalDB) backing for the new concurrency/DST test fixture | Wave 0 gap — the existing test project only references `Microsoft.EntityFrameworkCore.InMemory` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Try-insert-per-candidate retry loop (recommended) | Explicit `SERIALIZABLE` transaction with row locks over a availability check + insert | Rejected by D-01 already — SQL Server has no exclusion constraint, and manual isolation-level control adds exactly the `CreateExecutionStrategy` complexity this research shows is avoidable |
| `HttpClient` raw POST to Resend (D-10, locked) | `Resend` NuGet SDK package | Locked decision rejects the SDK to avoid a dependency; not re-litigated here |
| EF Core InMemory provider for concurrency test (existing pattern) | Real SQL Server LocalDB fixture (recommended) / EF Core Sqlite in-memory | InMemory provider does not enforce unique indexes at all (see Landmine 4) — unusable for SC4. Sqlite enforces uniqueness but raises `SqliteException` with a different error code than production `SqlException` 2601/2627, so it cannot validate the actual exception-translation code path — use real SQL Server LocalDB instead |

**Installation:** No new NuGet packages required for the API's production code (HttpClient and TimeZoneInfo are BCL). The test project needs `Microsoft.EntityFrameworkCore.SqlServer` added as a reference (already used by the main API project, so version-compatible):

```bash
dotnet add API/ZachHairStudio.Api.Tests/ZachHairStudio.Api.Tests.csproj package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.9
```

**Version verification:**

```
$ dotnet --version
10.0.301
$ dotnet tool list --global
dotnet-ef  10.0.9   (matches EF Core 10.0.9 already used by the project)
$ sqllocaldb info
MSSQLLocalDB   (already provisioned and working per STATE.md — resolved 2026-07-09)
```

`[VERIFIED: local environment]` — confirmed by running the commands above in this session. dotnet-ef is already at the version the `ef-migrations` skill requires (its own doc mentions the environment "currently has 9.0.15" — that note is stale; the environment already has 10.0.9).

## Package Legitimacy Audit

No new third-party NuGet packages are introduced by this phase's locked decisions:
- Resend integration uses raw `HttpClient` (D-10 explicitly rejects an SDK) — no package.
- Timezone handling uses BCL `TimeZoneInfo` — no package.
- The only new package reference is `Microsoft.EntityFrameworkCore.SqlServer` in the **test** project, which is the exact same package (same version, 10.0.9) already referenced by `ZachHairStudio.Api` and `ZachHairStudio.Shared` in this repo — not a new dependency, just a new project reference to an already-vetted, already-installed package.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| Microsoft.EntityFrameworkCore.SqlServer | nuget | 10+ yrs (Microsoft first-party) | Billions | github.com/dotnet/efcore | OK | Approved (already in use elsewhere in this repo) |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Client (browser, /book page)
  │
  │ 1. GET /api/services  (existing, Phase 1)
  ▼
[Service picker] ──selects service, optional stylist──▶ [Date + slot grid]
  │
  │ 2. GET /api/appointments/slots?serviceId=&stylistId=&date=
  ▼
API: SlotsController → SlotService.GetOpenSlotsAsync(...)
  │
  ├─▶ DB: StylistWorkingHours WHERE DayOfWeek = date.DayOfWeek   (server-evaluated)
  ├─▶ DB: StylistTimeOff WHERE overlaps [dayStartUtc, dayEndUtc)  (server-evaluated)
  ├─▶ DB: AppointmentSlot WHERE StylistId IN (...) AND SlotStart IN [dayStartUtc, dayEndUtc) (server-evaluated)
  │
  └─▶ In-memory grid math (C#, TimeZoneInfo DST-aware):
        working hours − time off − booked cells → list of DateTimeOffset candidate slot starts
        (per stylist, or unioned across all active stylists if "Any stylist")
  │
  ▼ 3. Response: [{ startsAt: "2026-07-10T10:00:00-04:00", stylistId?: 2, stylistName?: "Aria Chen" }, ...]
  │
[Client picks a slot] ──contact fields (guest)──▶ [Confirm]
  │
  │ 4. POST /api/appointments   { serviceId, stylistId?, startsAt, firstName, lastName, email, phone }
  ▼
API: AppointmentsController → AppointmentsService.CreateAsync(...)
  │
  ├─▶ FluentValidation on the create DTO
  ├─▶ Resolve candidate stylist(s): [stylistId] if specified, else deterministically ordered list of active stylists
  ├─▶ FOR EACH candidate stylist (in order):
  │     ├─▶ Build 1 Appointment row + N AppointmentSlot rows (grid cells for the duration)
  │     ├─▶ dbContext.SaveChangesAsync()   ← single implicit transaction, all-or-nothing
  │     ├─▶ SUCCESS → break, return 201 with full appointment details
  │     └─▶ DbUpdateException (SqlException.Number ∈ {2601, 2627}) → dbContext.ChangeTracker.Clear(), try next candidate
  ├─▶ All candidates exhausted → Result<T>.DuplicateRecordError → 409 "slot taken"
  │
  └─▶ AFTER commit (best-effort, D-11): EmailService.SendConfirmationAsync(...)
        → HttpClient POST https://api.resend.com/emails (never inside the DB transaction)
        → failure is logged, never rolls back the appointment
  │
  ▼ 5. On-screen confirmation (full details, D-11) + (best-effort) email
```

### Recommended Project Structure

```
API/ZachHairStudio.Shared/Features/
├── Stylists/
│   ├── Stylist.cs                       # Id, Slug, Name, IsActive, DisplayOrder
│   ├── StylistResponseDto.cs
│   └── StylistExtensions.cs
├── Availability/
│   ├── StylistWorkingHours.cs           # StylistId, DayOfWeek, StartTime, EndTime
│   ├── StylistTimeOff.cs                # StylistId, StartsAt, EndsAt, Reason
│   └── SlotService.cs                   # open-slot grid computation (Claude's discretion: separate from AppointmentsService)
└── Appointments/
    ├── Appointment.cs                   # ServiceId FK, StylistId FK, StartsAt (DateTimeOffset), Status, client fields
    ├── AppointmentSlot.cs               # AppointmentId FK, StylistId, SlotStart (DateTimeOffset) — UNIQUE (StylistId, SlotStart)
    ├── AppointmentStatus.cs             # Confirmed | Cancelled | Completed | NoShow (Phase 3 adds transitions; define now to avoid a later enum migration)
    ├── AppointmentCreateDto.cs
    ├── AppointmentCreateDtoValidator.cs
    ├── AppointmentResponseDto.cs
    ├── AppointmentExtensions.cs
    ├── AppointmentsService.cs           # candidate-stylist retry loop, calls SlotService + EmailService
    └── EmailService.cs                  # Resend HttpClient wrapper (IEmailService interface for testability)

API/ZachHairStudio.Api/Controllers/
├── StylistsController.cs                # thin, list endpoint
└── AppointmentsController.cs            # GET slots, POST appointment (replaces BookingsController)

landing-page/
├── app/book/page.tsx                    # updated: service → stylist → date/slots → contact, progressive reveal
├── components/AppointmentBookingForm.tsx  # replaces BookingRequestForm.tsx
└── lib/appointments.ts                  # fetchOpenSlots(), createAppointment() + Zod schemas (mirrors lib/services.ts)
```

### Pattern 1: DB-Level Double-Booking Guarantee (no manual transaction)

**What:** Occupancy-row model with an unfiltered composite unique index; atomicity comes from EF Core's implicit per-`SaveChangesAsync()` transaction, not a manually opened one.

**When to use:** Any write that must be atomic across multiple new rows in a single logical operation, when the operation does not need to interleave a read-then-write consistency check inside its own isolation level (the unique index itself is the consistency check here).

**Example:**
```csharp
// Source: EF Core docs — Modeling > Indexes (context7 /dotnet/entityframework.docs)
modelBuilder.Entity<AppointmentSlot>(entity =>
{
    entity.HasIndex(s => new { s.StylistId, s.SlotStart }).IsUnique();
    entity.Property(s => s.SlotStart).HasColumnType("datetimeoffset(0)");
});
```

```csharp
// AppointmentsService.CreateAsync — try-insert-per-candidate, no manual transaction.
// Source: EF Core docs — "Multiple operations in a single SaveChanges" (context7):
// "For most database providers, SaveChanges is transactional, ensuring all
//  operations either succeed or fail together." No explicit transaction needed.
public async Task<Result<AppointmentResponseDto>> CreateAsync(AppointmentCreateDto request)
{
    var validation = await _createValidator.ValidateAsync(request);
    if (!validation.IsValid)
        return Result<AppointmentResponseDto>.ValidationError(/* ... */);

    var candidates = await ResolveCandidateStylistsAsync(request); // [stylistId] or deterministic list
    if (candidates.Count == 0)
        return Result<AppointmentResponseDto>.NotFoundError("No stylist available for this slot.");

    foreach (var stylistId in candidates)
    {
        var appointment = BuildAppointment(request, stylistId); // Appointment + N AppointmentSlot children
        _dbContext.Appointments.Add(appointment);

        try
        {
            await _dbContext.SaveChangesAsync();
            _ = _emailService.SendConfirmationAsync(appointment); // fire-and-forget-safe, best-effort (D-11)
            return Result<AppointmentResponseDto>.Success(appointment.ToDto());
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
        {
            _dbContext.Entry(appointment).State = EntityState.Detached;
            foreach (var slot in appointment.Slots)
                _dbContext.Entry(slot).State = EntityState.Detached;
            // try next candidate stylist
        }
    }

    return Result<AppointmentResponseDto>.DuplicateRecordError(
        "This slot was just booked by someone else. Please choose another time.");
}

// Landmine 1: catch BOTH 2601 (unique INDEX) and 2627 (unique CONSTRAINT).
// EF Core's HasIndex().IsUnique() generates a CREATE UNIQUE INDEX migration,
// which SQL Server flags with error 2601, not 2627 — verify this against the
// actual generated migration SQL before assuming either number exclusively.
private static bool IsDuplicateKeyViolation(DbUpdateException ex)
    => ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
       && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
```

### Pattern 2: Availability Model & Open-Slot Query (server-evaluable)

**What:** Two-phase query: (1) a server-evaluated EF Core LINQ query narrows to the relevant stylist(s) + date's working hours, time-off, and already-booked cells; (2) DST-aware grid generation runs in C# over that small in-memory result set — this is not the "client-side evaluation" anti-pattern EF Core warns about, because none of the per-cell math needs SQL translation.

**When to use:** Whenever a query's filtering logic (which stylist, which date range) is cheap to push to SQL but the actual candidate-generation logic (15-minute grid + DST) is inherently procedural and does not map to SQL.

**Example:**
```csharp
// SlotService.GetOpenSlotsAsync — server-evaluated data fetch, then in-memory grid math.
public async Task<IReadOnlyList<OpenSlot>> GetOpenSlotsAsync(
    int serviceId, int? stylistId, DateOnly date)
{
    var salonTz = TimeZoneInfo.FindSystemTimeZoneById(_salonOptions.IanaTimeZoneId);
    var dayStartLocal = date.ToDateTime(TimeOnly.MinValue);
    var dayStartUtc = ToUtcSafe(dayStartLocal, salonTz);
    var dayEndUtc = ToUtcSafe(dayStartLocal.AddDays(1), salonTz);

    var stylists = await _dbContext.Stylists
        .Where(s => s.IsActive && (stylistId == null || s.Id == stylistId))
        .ToListAsync();                                             // server-evaluated

    var workingHours = await _dbContext.StylistWorkingHours
        .Where(h => h.DayOfWeek == date.DayOfWeek
                 && stylists.Select(s => s.Id).Contains(h.StylistId))
        .ToListAsync();                                             // server-evaluated

    var timeOff = await _dbContext.StylistTimeOff
        .Where(t => t.EndsAt > dayStartUtc && t.StartsAt < dayEndUtc
                 && stylists.Select(s => s.Id).Contains(t.StylistId))
        .ToListAsync();                                             // server-evaluated

    var bookedCells = await _dbContext.AppointmentSlots
        .Where(slot => slot.SlotStart >= dayStartUtc && slot.SlotStart < dayEndUtc
                     && stylists.Select(s => s.Id).Contains(slot.StylistId))
        .Select(slot => new { slot.StylistId, slot.SlotStart })
        .ToListAsync();                                             // server-evaluated

    // Everything below is in-memory (LINQ to Objects), not translated to SQL:
    var service = await _dbContext.Services.FindAsync(serviceId);
    var cellsNeeded = (int)Math.Ceiling(service!.DurationMinutes / 15.0);

    return stylists
        .SelectMany(stylist => GenerateCandidateStarts(
            stylist, workingHours, timeOff, bookedCells, dayStartLocal, salonTz, cellsNeeded))
        .OrderBy(slot => slot.StartsAt)
        .ToList();
}
```

### Pattern 3: DST-Safe Wall-Clock → `DateTimeOffset` Conversion

**What:** Never construct `DateTimeOffset` with a hardcoded offset. Always resolve the offset for the specific salon-local instant via `TimeZoneInfo`, and explicitly handle the spring-forward gap and fall-back ambiguity.

**Example:**
```csharp
// Source: TimeZoneInfo.IsInvalidTime / IsAmbiguousTime (learn.microsoft.com, .NET 10 API docs)
private static DateTimeOffset? ToSalonInstant(DateTime localWallClock, TimeZoneInfo salonTz)
{
    if (salonTz.IsInvalidTime(localWallClock))
    {
        // Spring-forward gap (e.g. 2026-03-08 02:00-02:59 America/New_York does not exist).
        // This slot cannot be offered — skip it in grid generation.
        return null;
    }

    if (salonTz.IsAmbiguousTime(localWallClock))
    {
        // Fall-back (e.g. 2026-11-01 01:00-01:59 America/New_York occurs twice).
        // Deterministic policy: always resolve to the LATER (standard-time) offset,
        // i.e. the second physical occurrence — document this choice in the plan.
        var offsets = salonTz.GetAmbiguousTimeOffsets(localWallClock);
        var standardOffset = offsets.Min(); // standard-time offset is numerically smaller in the US
        return new DateTimeOffset(localWallClock, standardOffset);
    }

    var offset = salonTz.GetUtcOffset(localWallClock);
    return new DateTimeOffset(localWallClock, offset);
}
```

### Pattern 4: Best-Effort Email After Commit (never inside the DB transaction)

**What:** The appointment commit and the email send are two independent operations; the email call happens strictly after `SaveChangesAsync()` returns successfully, wrapped in its own try/catch that only logs.

**Example:**
```csharp
// Source: Resend API reference (resend.com/docs/api-reference/introduction) — verified via WebSearch
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient; // configured via AddHttpClient<IEmailService, ResendEmailService>()
    private readonly ILogger<ResendEmailService> _logger;

    public async Task SendConfirmationAsync(Appointment appointment)
    {
        try
        {
            var payload = new
            {
                from = "bookings@zachhairstudio.com", // requires a verified sending domain (D-10)
                to = appointment.ClientEmail,
                subject = "Your appointment is confirmed",
                html = BuildConfirmationHtml(appointment),
            };

            using var response = await _httpClient.PostAsJsonAsync("emails", payload);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Resend confirmation email failed for appointment {AppointmentId}: {StatusCode}",
                    appointment.Id, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Never rethrow — D-11: a Resend outage must never cost a client their slot.
            _logger.LogError(ex, "Resend confirmation email threw for appointment {AppointmentId}", appointment.Id);
        }
    }
}

// Program.cs registration:
builder.Services.AddHttpClient<IEmailService, ResendEmailService>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["Resend:ApiKey"]);
});
```

### Anti-Patterns to Avoid

- **Manually opening a transaction for the multi-row insert:** `SaveChangesAsync()` already wraps the `Appointment` + N `AppointmentSlot` inserts atomically. Adding `BeginTransactionAsync()` on top gains nothing and — because `EnableRetryOnFailure` is already configured in `Program.cs` — throws `InvalidOperationException: The configured execution strategy 'SqlServerRetryingExecutionStrategy' does not support user-initiated transactions` unless every such call is wrapped in `dbContext.Database.CreateExecutionStrategy().ExecuteAsync(...)`. Avoid the whole problem: don't open a manual transaction for this operation.
- **Catching only SQL error 2627:** EF Core's `HasIndex().IsUnique()` produces a `CREATE UNIQUE INDEX`, not a named `UNIQUE` constraint — SQL Server's error number for a unique-index violation is 2601. Catching only 2627 risks the "concurrent booking succeeds twice, gets a 500 instead of a clean 409" failure mode SC4 exists to prevent. Catch both.
- **Testing the concurrency/DST guarantees against the EF Core InMemory provider:** confirmed via research — InMemory does not enforce unique indexes or alternate keys at all; two rows with the same key insert silently, no exception. A test built on InMemory would pass even if the unique index were never actually applied in the real migration. Use a real SQL Server (LocalDB) fixture for these specific tests.
- **Hardcoding a fixed UTC offset for the salon timezone (e.g. always `-05:00`):** breaks the moment DST changes; must resolve the offset per-instant via `TimeZoneInfo.GetUtcOffset(localTime)`.
- **Rendering appointment times in the browser's local timezone:** D-16 explicitly requires salon-local time with the zone labelled, regardless of the client's browser timezone — `Intl.DateTimeFormat` with the browser's implicit zone must NOT be used; format using the salon's known IANA id or the offset embedded in the `DateTimeOffset` the API already returns.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Overlap/double-booking detection | A custom `[start, end)` overlap-checking query with app-level locking | The occupancy-row + unique index model (D-03, already locked) | SQL Server has no native exclusion/range constraint; occupancy rows turn "overlap" into "duplicate key", which the DB engine already solves atomically and correctly under concurrency |
| DST-safe local-to-UTC conversion | A custom "is this the 2nd Sunday of March" DST calculator | `TimeZoneInfo.FindSystemTimeZoneById` + `IsInvalidTime`/`IsAmbiguousTime`/`GetUtcOffset` | The BCL already encodes every IANA zone's actual transition rules (including non-US zones, leap years, historical rule changes) — a hand-rolled version will be wrong for edge cases immediately and silently |
| Transactional retry-safe multi-row writes | Manual `BeginTransaction` + manual retry loop around transient SQL errors | `SaveChangesAsync()`'s implicit transaction (for atomicity) + do NOT combine with `EnableRetryOnFailure` manual wrapping unless a real need for read-then-write consistency emerges | EF Core's default already gives atomicity; combining custom transactions with the configured retrying execution strategy is a well-documented trap (see Landmine 2/3) |
| Email retry/outbox | A custom outbox table + background retry worker for confirmation emails | Best-effort synchronous send after commit (D-11, locked) — this problem is explicitly deferred to Phase 8 if it proves necessary | Locked decision; do not build the more robust version prematurely |

**Key insight:** Every "hand-roll" temptation in this phase (overlap detection, DST math, transactional retries) has a well-tested, already-available BCL or SQL Server primitive. The engineering risk in this phase is not in inventing new algorithms — it's in correctly wiring together primitives that individually behave as documented but interact in non-obvious ways (unique index → specific SQL error number → exception unwrapping → HTTP status; `EnableRetryOnFailure` → manual transaction incompatibility).

## Runtime State Inventory

> Included because D-14 retires the `Booking` entity/table wholesale — a schema-level migration with real runtime-state blast radius, even though this is fundamentally a greenfield feature phase.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `Bookings` table rows in the dev/prod-shaped SQL Server database (LocalDB and the Azure SQL instance noted in STATE.md). Per D-14, these are dev/test data only with no production users. | No backfill. The EF migration that adds the 5 new tables also drops `Bookings` — code edit + one migration, no data migration script needed. Confirm this is dev-only data before applying to any shared/Azure SQL environment. |
| Live service config | None found — no external service (n8n, Datadog, etc.) references "Booking" by name in this codebase. | None |
| OS-registered state | None found — no scheduled tasks, pm2 processes, or OS-level registrations reference "Booking". | None |
| Secrets/env vars | New secret required: `RESEND_API_KEY` (dev: `dotnet user-secrets`, prod: environment variable, per D-13 — locked). Not a rename of an existing secret; a net-new one this phase introduces. `ZachHairStudio.Api.csproj` already has a `UserSecretsId` configured, so `dotnet user-secrets set` works without extra setup. | Add `RESEND_API_KEY` via `dotnet user-secrets set RESEND_API_KEY <key> --project API/ZachHairStudio.Api`. Currently **not set** in this environment — the API/test suite will fail to send email (and, per D-12, the test suite depends on it) until an owner-provided key is configured. Flag as a setup/checkpoint task. |
| Build artifacts | `BookingsController.cs`, `BookingRequestForm.tsx`, and `Booking`/`BookingCreateDto`/`BookingResponseDto`/`BookingStatus`/`BookingExtensions` under `Features/Bookings/` are deleted outright (not renamed), so no stale build-artifact directory is left behind (contrast with a rename, which would leave e.g. an `.egg-info`-style stale artifact). The `landing-page/lib/api.ts` `createBooking`/`BookingRequest`/`BookingResponse` exports become dead code once `BookingRequestForm.tsx` is deleted — remove them in the same phase to avoid an unused, silently-broken client function pointing at a deleted `/api/bookings` endpoint. | Delete `Features/Bookings/*`, `BookingsController.cs`, `BookingRequestForm.tsx`, and the `createBooking`/`BookingRequest`/`BookingResponse` exports in `lib/api.ts` (or delete `lib/api.ts` entirely if nothing else uses it — verify via grep before deleting). |

**Blast radius confirmed by direct inspection:** `BookingsController.cs` (`API/ZachHairStudio.Api/Controllers/`), `Features/Bookings/` (5 files), `landing-page/lib/api.ts` (`createBooking`, `extractErrorMessage`, `BookingRequest`, `BookingResponse`), `landing-page/components/BookingRequestForm.tsx`, `landing-page/app/book/page.tsx` (imports `BookingRequestForm`, must be repointed at the new form component).

## Common Pitfalls

### Pitfall 1: Assuming SQL error 2627 without verifying against the actual generated migration
**What goes wrong:** CONTEXT.md's D-03 states the guarantee "hits a duplicate-key violation (SQL Server error 2627)". Error 2627 is for a named `UNIQUE CONSTRAINT`; EF Core's `HasIndex().IsUnique()` fluent API generates a `CREATE UNIQUE INDEX` in the migration, which SQL Server flags as error **2601**.
**Why it happens:** 2627 is the more commonly cited number in EF Core tutorials because many examples use `HasAlternateKey()` (which does create a named constraint) rather than `HasIndex().IsUnique()`.
**How to avoid:** Catch both 2601 and 2627 in the exception-translation code (defensive, future-proof against either mapping), and add a unit/integration test that provokes the real violation against real SQL Server and asserts on the observed `SqlException.Number`, rather than asserting a specific hardcoded number in application logic.
**Warning signs:** A concurrency test that "passes" only because it happens to trigger the number the code checks for, while the code silently 500s in a differently-configured environment (e.g., if a future migration changes `HasIndex` to `HasAlternateKey`).

### Pitfall 2: Wrapping the insert in a manual `BeginTransactionAsync()` "to be safe"
**What goes wrong:** `Program.cs` already configures `EnableRetryOnFailure(maxRetryCount: 10, ...)`. The moment code calls `dbContext.Database.BeginTransactionAsync()` directly (outside of `CreateExecutionStrategy().ExecuteAsync(...)`), EF Core throws `InvalidOperationException: The configured execution strategy 'SqlServerRetryingExecutionStrategy' does not support user-initiated transactions.` at runtime — this will not surface at compile time or in a quick smoke test, only when the code path actually executes.
**Why it happens:** Manual transactions feel like the "obviously correct" way to guarantee atomicity for a multi-row insert, but `SaveChangesAsync()` already provides it implicitly.
**How to avoid:** Do not open a manual transaction for the appointment+slots insert. If a future requirement genuinely needs one (e.g., a read-then-write consistency check across multiple `SaveChangesAsync` calls), wrap it in `dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () => { ... })`.
**Warning signs:** `InvalidOperationException` mentioning "does not support user-initiated transactions" surfacing only in integration tests or under load, not in casual manual testing.

### Pitfall 3: Testing SC4/SC5 against the existing EF Core InMemory fixture
**What goes wrong:** The concurrency test "passes" (no exception, both requests appear to succeed) or "passes vacuously" — but it isn't proving anything about the actual production-critical guarantee, because the InMemory provider silently allows duplicate values in a unique-indexed column.
**Why it happens:** `CustomWebApplicationFactory` (the existing, Phase-1-established test fixture) is wired to `UseInMemoryDatabase(...)`, and it's the path of least resistance to reuse it for new tests.
**How to avoid:** Add a second test fixture backed by real SQL Server LocalDB specifically for the concurrency and DST tests (see Validation Architecture below). Keep the InMemory fixture for everything else (fast, no I/O).
**Warning signs:** A "concurrency test" that runs in under 5ms — real SQL Server round-trips take measurably longer; if the whole test suite stays just as fast after adding a genuine concurrency test, it likely isn't hitting real SQL Server.

### Pitfall 4: Rounding/duration math producing off-grid slot times
**What goes wrong:** The Scalp Treatment's 40-minute duration must round up to 45 minutes (3 grid cells), consistent with D-02. A naive `DurationMinutes / 15` integer division truncates instead of rounding up, producing a 2-cell (30-minute) reservation for a 40-minute service — silently under-booking the stylist's time.
**Why it happens:** `40 / 15 == 2` in integer division; the correct computation is `Math.Ceiling(40 / 15.0) == 3`.
**How to avoid:** Always use `(int)Math.Ceiling(durationMinutes / 15.0)` for cell-count math; add a unit test specifically for the Scalp Treatment's 40-minute duration asserting 3 cells (45 minutes) are reserved, not 2.
**Warning signs:** A booked appointment's `Appointment.EndsAt` (or last `AppointmentSlot.SlotStart` + 15min) is earlier than `StartsAt + Service.DurationMinutes`.

### Pitfall 5: Ambiguous fall-back time resolved inconsistently between requests
**What goes wrong:** If the ambiguity-resolution policy (Pattern 3) isn't centralized in one helper, two different code paths (slot-grid generation vs. appointment-create validation) could resolve the same ambiguous local time to different UTC offsets, causing a slot that was "available" in the GET response to fail validation on POST — or worse, to collide with a different physical instant than the one the client saw.
**Why it happens:** `TimeZoneInfo.GetUtcOffset()` on an ambiguous `DateTime` with `Kind = Unspecified` returns *a* valid offset (documented behavior), but not necessarily the same one your grid-generation code chose if the two code paths use different methods.
**How to avoid:** Centralize wall-clock → `DateTimeOffset` conversion in exactly one method (`ToSalonInstant` in Pattern 3), used by both slot generation and appointment creation. Never re-derive the offset independently in two places.
**Warning signs:** A booking created near a DST fall-back date has a `StartsAt` value one hour off from what the slot grid displayed.

## Code Examples

### Concurrency Test (proves SC4 — real SQL Server, not mocked)
```csharp
// Source: pattern derived from EF Core connection-resiliency docs + standard xUnit
// concurrent-task pattern; error-number assertion per Landmine 1 above.
[Fact]
public async Task CreateAppointment_TwoSimultaneousRequestsForSameSlot_ExactlyOneSucceeds()
{
    // Arrange: seed a stylist + working hours in the REAL SQL Server (LocalDB) fixture,
    // not the InMemory-backed CustomWebApplicationFactory.
    var client1 = _sqlServerFactory.CreateClient();
    var client2 = _sqlServerFactory.CreateClient();
    var request = BuildAppointmentCreateDto(stylistId: 1, startsAt: "2026-07-17T10:00:00-04:00");

    // Act: fire both requests concurrently against separate HttpClients (separate
    // scoped DbContext instances per request — DbContext itself is never shared
    // across the two concurrent calls).
    var task1 = client1.PostAsJsonAsync("/api/appointments", request);
    var task2 = client2.PostAsJsonAsync("/api/appointments", request);
    var results = await Task.WhenAll(task1, task2);

    // Assert: exactly one 201, exactly one 409.
    var statusCodes = results.Select(r => r.StatusCode).OrderBy(s => s).ToList();
    Assert.Equal(new[] { HttpStatusCode.Conflict, HttpStatusCode.Created }, statusCodes);

    // Assert: exactly one Appointment row exists for this stylist+slot in the DB.
    using var scope = _sqlServerFactory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    var slotCount = await db.AppointmentSlots.CountAsync(
        s => s.StylistId == 1 && s.SlotStart == DateTimeOffset.Parse("2026-07-17T10:00:00-04:00"));
    Assert.Equal(1, slotCount); // not 2 — proves no double-booking, not just "one HTTP response looked right"
}
```

### DST Boundary Test (proves SC5 — real 2026 transition dates)
```csharp
// Verified 2026 US DST transition dates via WebSearch (timeanddate.com):
// Spring forward: Sunday, March 8, 2026, 2:00 AM -> 3:00 AM (EST -05:00 -> EDT -04:00)
// Fall back:       Sunday, November 1, 2026, 2:00 AM -> 1:00 AM (EDT -04:00 -> EST -05:00)
[Theory]
[InlineData("2026-03-07", "-05:00")] // day before spring-forward: still EST
[InlineData("2026-03-08", "-04:00")] // spring-forward day itself: business hours (9am+) are already EDT
[InlineData("2026-10-31", "-04:00")] // day before fall-back: still EDT
[InlineData("2026-11-01", "-05:00")] // fall-back day itself: business hours (9am+) are already EST
public void ToSalonInstant_ResolvesCorrectOffsetAcrossDstBoundary(string dateStr, string expectedOffset)
{
    var salonTz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    var localWallClock = DateOnly.Parse(dateStr).ToDateTime(new TimeOnly(10, 0)); // 10:00 AM local

    var instant = ToSalonInstant(localWallClock, salonTz);

    Assert.NotNull(instant);
    Assert.Equal(TimeSpan.Parse(expectedOffset), instant!.Value.Offset);
}
```

### EF Core migration — unique index (verify against actual generated SQL before finalizing)
```csharp
// Source: EF Core docs (context7 /dotnet/entityframework.docs, modeling/indexes.md)
modelBuilder.Entity<AppointmentSlot>(entity =>
{
    entity.HasIndex(s => new { s.StylistId, s.SlotStart }).IsUnique();
});
// Expected generated migration Up() includes:
//   migrationBuilder.CreateIndex(
//       name: "IX_AppointmentSlots_StylistId_SlotStart",
//       table: "AppointmentSlots",
//       columns: new[] { "StylistId", "SlotStart" },
//       unique: true);
// which SQL Server renders as CREATE UNIQUE INDEX — confirm the actual migration
// file's SQL comment/generated script during Wave 0 before locking in "2601" as
// the sole expected error number in tests.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Manually maintaining Windows↔IANA timezone ID mapping tables (or the `TimeZoneConverter` NuGet package) | `TimeZoneInfo.FindSystemTimeZoneById()` accepting either ID format natively | .NET 6 (2021), still current in .NET 10 | No mapping package needed; `"America/New_York"` resolves identically on the Windows dev machine and any Linux deployment target |
| SQL Server `datetime`/`datetime2` + a separate "timezone" varchar column | `datetimeoffset` column type, mapped automatically from C# `DateTimeOffset` by EF Core's SQL Server provider | Long-standing (SQL Server 2008+, EF Core since inception) | BOOK-05's requirement is a direct, unmodified use of a mature, well-understood column type — no custom serialization needed |

**Deprecated/outdated:** None specific to this phase's stack — EF Core 10, .NET 10, and SQL Server's `datetimeoffset` are all current, non-deprecated technology.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | EF Core's `HasIndex().IsUnique()` fluent API always generates a `CREATE UNIQUE INDEX` (not a named constraint) in SQL Server migrations, and therefore raises error 2601 rather than 2627 | Pattern 1, Pitfall 1 | If EF Core 10 specifically changed this codegen (unconfirmed against the actual generated migration file in this repo), the exception-translation code could catch the wrong number exclusively. **Mitigated** by the recommendation to catch both 2601 and 2627 — but the plan should still generate the migration during Wave 0 and inspect the actual SQL before writing the concurrency test's exact assertions. |
| A2 | Fall-back ambiguity resolution policy (always resolve to the later/standard-time offset) is an acceptable default with no locked user decision behind it | Pattern 3, Pitfall 5 | This is Claude's discretion per CONTEXT.md, but the specific tie-break rule (standard vs. daylight offset) was not discussed with the user. Low risk (affects only a 1-hour window, once a year, only if a client attempts to book during that exact hour) but worth a one-line callout in the plan for owner awareness. |
| A3 | Resend's `/emails` endpoint payload accepts flat `from`/`to`/`subject`/`html` fields via a single POST with a Bearer token, and requires a verified sending domain for the `from` address | Pattern 4 | Sourced from WebSearch summaries of Resend's public docs (resend.com/docs/api-reference), not fetched via an authoritative MCP doc tool in this session — the exact required/optional fields (e.g., `reply_to`, `text` fallback, batch limits) should be confirmed against the live Resend API reference during implementation, and the domain-verification step is an out-of-band manual setup task (owner must add DNS records) that blocks D-12's "real sends in Development" requirement until complete. |
| A4 | No booking horizon / minimum lead time / same-day booking rule was specified by the user; a sensible default (e.g., bookable from now up to 60 days ahead, no minimum lead time) is assumed as a starting point | Standard Stack / Claude's Discretion | Per D-15's Phase-1 precedent, this must be flagged as an owner-reviewable placeholder in the plan/summary, not treated as a final business rule. |

**If this table is empty:** N/A — see entries above.

## Open Questions

1. **Exact SQL Server error number EF Core 10's generated migration triggers for this specific unique index**
   - What we know: `HasIndex().IsUnique()` is documented to generate a `CREATE UNIQUE INDEX`, which SQL Server's documented error-code behavior maps to 2601 (confirmed via multiple independent web sources, not a single unverified claim).
   - What's unclear: Whether EF Core 10 specifically (vs. older/newer versions) could name the index in a way that changes SQL Server's error classification, and whether SQL Server LocalDB behaves identically to full SQL Server here.
   - Recommendation: During Wave 0 / first implementation wave, generate the actual migration, apply it to LocalDB, manually trigger a duplicate insert via `sqlcmd` or a scratch script, and record the observed `SqlException.Number` before finalizing the exception-translation code and its test assertions. Catch both 2601 and 2627 regardless, as a defensive baseline.

2. **Resend sending-domain verification — an out-of-band manual step**
   - What we know: D-10 explicitly notes "Requires verifying a sending domain." This is a DNS-level action in the Resend dashboard, not something the plan can automate.
   - What's unclear: Whether the salon owner has (or will provide) a domain to verify before Phase 2 execution begins, and whether a Resend account/API key already exists.
   - Recommendation: The plan should include an explicit `checkpoint:human-verify` task early in the phase (before any email-sending code is exercised) for: (a) creating/confirming a Resend account, (b) verifying a sending domain, (c) generating an API key, (d) the owner or developer running `dotnet user-secrets set RESEND_API_KEY ...`. Given D-12 (real sends required in Testing too), this blocks the entire test suite until resolved — sequence it first.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | API build/run/migrations | ✓ | 10.0.301 | — |
| dotnet-ef global tool | EF migrations (`ef-migrations` skill) | ✓ | 10.0.9 (matches EF Core 10.0.9 — the skill doc's "currently 9.0.15, too old" note is stale) | — |
| SQL Server LocalDB | Dev database, and the new real-SQL-Server test fixture this phase requires | ✓ | `MSSQLLocalDB` instance present (STATE.md: resolved 2026-07-09) | — |
| `RESEND_API_KEY` (user secret) | Confirmation email sending (D-09..D-13); required by the API **and** the test suite per D-12 | ✗ | — | **No fallback accepted by the user** (D-12 explicitly rejects a fake test sender) — this is a hard blocker until an owner-provided key exists. Sequence as an early `checkpoint:human-verify` task. |
| Resend sending-domain verification | Actually delivering email (not just calling the API) | ✗ (unknown/unverified) | — | Same as above — blocks real delivery even if an API key exists without a verified `from` domain. |

**Missing dependencies with no fallback:**
- `RESEND_API_KEY` + verified sending domain — D-12 locked out the dev-only fake-sender fallback. Must be resolved via a human checkpoint before implementation can proceed past the email-integration task, and before any test that exercises `AppointmentsService.CreateAsync` end-to-end (since D-12 keeps real sends on in Testing too).

**Missing dependencies with fallback:** none — everything else needed for this phase is already present in the environment.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 + `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 (established in Phase 1) |
| Config file | none — `ZachHairStudio.Api.Tests.csproj`; existing `CustomWebApplicationFactory.cs` (InMemory) stays for non-DB-constraint tests |
| Quick run command | `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName!~SqlServer"` (excludes the new real-DB fixture for fast iteration) |
| Full suite command | `dotnet test API/ZachHairStudio.slnx` |

**Wave 0 gap — new fixture required.** The existing `CustomWebApplicationFactory` uses `UseInMemoryDatabase`, which — confirmed via research — does not enforce unique indexes, alternate keys, or foreign keys at all. It cannot be used to prove BOOK-04/SC4 or BOOK-05/SC5. Add a second fixture, e.g. `SqlServerWebApplicationFactory`, that:
- Uses `UseEnvironment("Testing")` like the existing one (skips startup auto-migrate),
- Points at a real LocalDB connection string with a per-test-run unique database name (mirrors the existing `_databaseName = $"...-{Guid.NewGuid()}"` pattern, but via `UseSqlServer` instead of `UseInMemoryDatabase`),
- Calls `dbContext.Database.Migrate()` (not `EnsureCreated()`) in `CreateHost`, so the actual migration — including the real unique index — is what gets exercised,
- Calls `dbContext.Database.EnsureDeleted()` in `DisposeAsync` to avoid orphaned LocalDB databases accumulating across test runs.

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| BOOK-01 | Open slots reflect working hours, time off, existing bookings | unit (SlotService grid math) + integration (endpoint) | `dotnet test --filter FullyQualifiedName~SlotServiceTests` | ❌ Wave 0 |
| BOOK-02 | End-to-end booking flow (service → slot → confirm) | integration (InMemory fixture is fine here — no constraint semantics needed) | `dotnet test --filter FullyQualifiedName~AppointmentsControllerTests` | ❌ Wave 0 |
| BOOK-03 | On-screen + email confirmation | integration (mock/verify `IEmailService` call was attempted; do NOT assert on Resend's actual delivery in automated tests — that's what D-12's real-send requirement is for manual verification, not CI) | `dotnet test --filter FullyQualifiedName~EmailServiceTests` | ❌ Wave 0 |
| BOOK-04 | Double-booking prevented, DB-level guarantee | integration, **real SQL Server LocalDB fixture required** | `dotnet test --filter FullyQualifiedName~ConcurrencyTests` | ❌ Wave 0 |
| BOOK-05 | `DateTimeOffset` + salon IANA timezone, correct across DST | unit (pure `ToSalonInstant` logic, no DB needed) + integration (real SQL Server fixture, to prove the stored/queried `datetimeoffset` column round-trips correctly across the boundary) | `dotnet test --filter FullyQualifiedName~DstBoundaryTests` | ❌ Wave 0 |
| BOOK-06 | Stylist filter / "Any stylist" union + deterministic assignment | unit (candidate resolution logic) + integration | `dotnet test --filter FullyQualifiedName~AnyStylistAssignmentTests` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test API/ZachHairStudio.Api.Tests --filter "FullyQualifiedName!~SqlServer"` (fast InMemory-backed subset)
- **Per wave merge:** `dotnet test API/ZachHairStudio.slnx` (full suite, including the real-SQL-Server concurrency/DST tests)
- **Phase gate:** Full suite green (including the new `SqlServerWebApplicationFactory`-backed tests) before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `SqlServerWebApplicationFactory.cs` (or equivalent) — real LocalDB-backed test fixture, required for BOOK-04/SC4 and BOOK-05/SC5
- [ ] `Microsoft.EntityFrameworkCore.SqlServer` package reference added to `ZachHairStudio.Api.Tests.csproj`
- [ ] `RESEND_API_KEY` user secret configured (blocks any test that exercises the real `AppointmentsService.CreateAsync` → email path, per D-12) — see Open Question 2
- [ ] `Salon:IanaTimeZoneId` (and any other `Salon` config keys) added to `appsettings.json`/`appsettings.Development.json`
- [ ] Migration generated and inspected for the actual `CREATE UNIQUE INDEX` SQL before finalizing the exact error-number assertions (see Open Question 1)

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | This phase's booking endpoints remain unauthenticated (guest booking, per D-15) — same posture as Phase 1's dev-only exposure; staff auth arrives Phase 3 |
| V3 Session Management | No | No session state introduced this phase |
| V4 Access Control | No | No ownership/authorization boundary yet — any client can create an appointment (guest booking is intentional); IDOR concerns arrive with Phase 7 accounts |
| V5 Input Validation | Yes | FluentValidation on `AppointmentCreateDto` (email format, phone format, string lengths, `StartsAt` must be on-grid and in the future within the booking horizon) — mirrors Phase 1's `ServiceCreateDtoValidator` pattern |
| V6 Cryptography | Marginal | `RESEND_API_KEY` at rest: `dotnet user-secrets` (dev, not encrypted but out of source control) / environment variable (prod) per D-13 — not a cryptography control per se, but a secrets-handling control; no encryption/hashing work in this phase itself |
| V7 Error Handling & Logging | Yes | The 2601/2627 → 409 translation is itself a security-adjacent control: it prevents a constraint-violation stack trace from leaking to the client in a 500 (which in Development mode would otherwise expose SQL details) — must map to a clean `ProblemDetails` 409 in all environments, not just Production |
| V12 API/WebService | Yes | Server-side revalidation: the client-submitted `StartsAt` must be re-validated against the actual grid/availability server-side at write time — never trust a client-echoed slot as automatically bookable (this is the entire point of the unique-index guarantee, but also apply ordinary bounds checking: reject `StartsAt` values outside working hours or off the 15-minute grid before even attempting the insert, to avoid wasting a DB round-trip on obviously-invalid input) |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Race condition / TOCTOU on slot booking (two clients book the same slot) | Tampering (of shared state consistency) | DB-level unique index (D-03) — this phase's central design already mitigates this correctly; the threat is only realized if a plan accidentally reintroduces an app-level-only check (e.g., "SELECT to check availability, then INSERT" without relying on the unique index to catch the race) |
| Information disclosure via unhandled `DbUpdateException`/`SqlException` stack trace in a 500 response | Information Disclosure | Explicit catch + `ProblemDetails` 409 mapping (Pattern 1) — verify this holds in `Development` environment too, where ASP.NET Core's default developer exception page would otherwise show full exception details including SQL text |
| Enumeration/spam via unauthenticated booking endpoint (no auth gate until Phase 3) | Denial of Service / Repudiation | Out of scope for this phase per LAUNCH-05 (rate limiting arrives Phase 8) — acceptable given the project's phased security posture (same as Phase 1's unauthenticated write endpoints), but worth a one-line note in the plan that this endpoint is unauthenticated and unthrottled by design until Phase 8 |
| Secret leakage: `RESEND_API_KEY` committed to a config file | Information Disclosure | gitleaks pre-commit hook + CI (already wired per CLAUDE.md) + D-13's `dotnet user-secrets`/env-var-only rule — verify no plan step suggests putting the key in `appsettings.json` even "temporarily for testing" |
| Email header/HTML injection via unsanitized client-supplied name/email fields into the Resend HTML payload | Tampering | FluentValidation on `FirstName`/`LastName`/`Email` (format + length bounds) before interpolating into `BuildConfirmationHtml(...)`; treat client-supplied strings as untrusted when building the email HTML body (HTML-encode any interpolated values) |

## Sources

### Primary (HIGH confidence)
- [Entity Framework Core docs](https://github.com/dotnet/entityframework.docs) (context7 `/dotnet/entityframework.docs`) — `HasIndex().IsUnique()` fluent API and composite index configuration (`modeling/indexes.md`); connection resiliency and execution-strategy/manual-transaction incompatibility (`miscellaneous/connection-resiliency.md`); `SaveChanges` transactional behavior for multiple operations (`saving/basic.md`)
- Local environment verification (this session): `dotnet --version` (10.0.301), `dotnet tool list --global` (dotnet-ef 10.0.9), `sqllocaldb info` (MSSQLLocalDB present) — `[VERIFIED: local environment]`
- Direct codebase inspection: `API/ZachHairStudio.Shared/Db/BookingDbContext.cs`, `API/ZachHairStudio.Api/Program.cs`, `API/ZachHairStudio.Api.Tests/CustomWebApplicationFactory.cs`, `API/ZachHairStudio.Shared/Result.cs`, `API/ZachHairStudio.Shared/Features/Services/*`, `API/ZachHairStudio.Shared/Features/Bookings/*` — `[VERIFIED: codebase]`

### Secondary (MEDIUM confidence)
- [SQL Server error 2601 vs 2627](https://www.sqlserver-dba.com/2015/05/how-to-troubleshoot-error-2601-cannot-insert-duplicate-key-row-in-object-ls-with-unique-index-ls-the.html) and [SQLServerCentral: Unique Constraint vs Unique Index](https://www.sqlservercentral.com/blogs/dba-101-unique-constraint-vs-unique-index) — WebSearch, cross-checked across multiple independent community sources agreeing on the 2601 (index) vs 2627 (constraint) distinction — `[CITED, cross-verified]`
- [Cross-platform Time Zones with .NET Core - .NET Blog](https://devblogs.microsoft.com/dotnet/cross-platform-time-zones-with-net-core/) and [TimeZoneInfo.FindSystemTimeZoneById(String) — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid?view=net-10.0) — official Microsoft sources, WebSearch-retrieved — `[CITED]`
- [TimeZoneInfo.IsDaylightSavingTime — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.isdaylightsavingtime?view=net-10.0) and related `.NET` docs on `IsInvalidTime`/`IsAmbiguousTime`/`GetUtcOffset` — `[CITED]`
- [datetimeoffset (Transact-SQL) - SQL Server | Microsoft Learn](https://learn.microsoft.com/en-us/sql/t-sql/data-types/datetimeoffset-transact-sql?view=sql-server-ver17) — official Microsoft Learn source confirming UTC-normalized comparison/indexing — `[CITED]`
- [Resend API reference](https://resend.com/docs/api-reference/introduction) — official Resend docs, WebSearch-retrieved summary — `[CITED]`
- [EF Core InMemory provider does not enforce unique constraints — dotnet/efcore#3850](https://github.com/dotnet/efcore/issues/3850) and [EF Core InMemory Provider Pitfalls](https://www.dotnet-guide.com/articles/ef-core-inmemory-provider-pitfalls/) — official EF Core repo issue + community analysis, WebSearch-retrieved — `[CITED]`
- [Daylight Saving Time 2026 in the United States - timeanddate.com](https://www.timeanddate.com/time/change/usa?year=2026) — WebSearch-retrieved, standard reference source for DST transition dates — `[CITED]`

### Tertiary (LOW confidence)
- None — all findings above were either directly verified against this codebase/environment or cross-checked against official/authoritative documentation via WebSearch.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; existing versions confirmed by direct tool invocation in this session
- Architecture (double-booking guarantee, transaction/execution-strategy interaction): HIGH — sourced directly from official EF Core documentation via context7, cross-checked against the specific `EnableRetryOnFailure` configuration already present in this repo's `Program.cs`
- Architecture (timezone/DST handling): HIGH — BCL APIs, official Microsoft Learn documentation, stable since .NET 6
- Pitfalls (2601 vs 2627 exact number): MEDIUM — cross-verified across multiple independent community sources but not confirmed against this specific repo's actual generated migration SQL (flagged as Open Question 1, a Wave 0 verification step)
- Resend integration shape: MEDIUM — official docs referenced via WebSearch summary, not fetched through an authoritative structured tool in this session; exact field list should be reconfirmed during implementation
- Booking-horizon/lead-time defaults: LOW — no user decision exists; explicitly flagged as an assumption requiring owner review (A4)

**Research date:** 2026-07-09
**Valid until:** 2026-08-08 (30 days — all core findings are BCL/EF Core/SQL Server semantics unlikely to change; re-verify the Resend API shape and the exact SQL error number against the actual generated migration if implementation is delayed materially past this window)
