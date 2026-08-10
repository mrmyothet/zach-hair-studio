---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
current_phase: 07
current_phase_name: Accounts & Retention
status: verifying
stopped_at: Completed 07-05-PLAN.md (gap closure)
last_updated: "2026-08-10T15:50:00.000Z"
last_activity: 2026-08-10
last_activity_desc: Phase 07 gap closure 07-05 executed (owned public book)
progress:
  total_phases: 8
  completed_phases: 7
  total_plans: 37
  completed_plans: 37
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-08-09)

**Core value:** Booking a salon appointment is effortless — browsing services and reserving a slot is the primary, friction-free path.
**Current focus:** Phase 07 — Accounts & Retention

## Current Position

Phase: 07 (Accounts & Retention) — VERIFYING
Plan: 5 of 5
Status: Gap closure executed — ready for re-verification
Last activity: 2026-08-10 — Phase 07 gap closure 07-05 (owned public book)

Progress: [██████████] 100%

## Performance Metrics

**Velocity:**

- Total plans completed: 31
- Average duration: 60 min
- Total execution time: 3h 58m

**By Phase:**

| Phase              | Plans | Total  | Avg/Plan |
| ------------------ | ----- | ------ | -------- |
| 1. Service Catalog | 4     | 3h 58m | 60 min   |
| 01                 | 4     | -      | -        |
| 03                 | 5     | -      | -        |
| 04                 | 7     | -      | -        |
| 2                  | 9     | -      | -        |
| 05                 | 2     | -      | -        |

**Recent Trend:**

- Last 5 plans: 72m, 101m, 51m, 14m
- Trend: Accelerating

_Updated after each plan completion_
| Phase 02 P04 | 13min | 3 tasks | 16 files |
| Phase 03 P01 | 14min | 3 tasks | 13 files |
| Phase 03 P02 | 10min | 3 tasks | 9 files |
| Phase 03 P03 | 25min | 3 tasks | 10 files |
| Phase 02 P07 | 25min | 3 tasks | 8 files |
| Phase 03 P05 | close-out | 4 tasks | 12 files |
**Per-Plan Metrics:**

| Plan         | Duration | Tasks   | Files    |
| ------------ | -------- | ------- | -------- |
| Phase 04 P01 | 25min    | 3 tasks | 10 files |
| Phase 04 P02 | 45min    | 3 tasks | 9 files  |
| Phase 04 P03 | 30min    | 3 tasks | 9 files  |
| Phase 04 P04 | 45min    | 3 tasks | 9 files  |
| Phase 04 P05 | 55min    | 3 tasks | 11 files |
| Phase 02 P09 | 20min    | 2 tasks | 4 files  |
| Phase 04 P06 | 20min    | 2 tasks | 2 files  |
| Phase 04 P07 | 20min    | 1 tasks | 1 files  |
| Phase 05 P01 | 27min    | 3 tasks | 14 files |
| Phase 05 P02 | 6min     | 3 tasks | 6 files  |
| Phase 06 P01 | 6min | 2 tasks | 17 files |
| Phase 06 P02 | 3min | 3 tasks | 9 files |
| Phase 06 P03 | 5min | 2 tasks | 25 files |
| Phase 06 P04 | 5min | 3 tasks | 10 files |
| Phase 07 P01 | 7min | 3 tasks | 12 files |
| Phase 07 P02 | 8min | 3 tasks | 20 files |
| Phase 07 P03 | 10min | 3 tasks | 9 files |
| Phase 07 P04 | 8min | 3 tasks | 28 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- Roadmap: Full P1-8 scope from specs/roadmap.md preserved as-is (8 integer phases); granularity=standard compression guidance overridden by explicit user scope choice.
- Roadmap: Per-feature service layer (PLAT-01) and validation layer (PLAT-02) introduced starting Phase 1, not deferred.
- Roadmap: Phase 2 ships a minimal/seeded availability model; Phase 4 makes the same model staff-editable — one system, not two.
- Roadmap: No-show modeled as a first-class terminal status starting Phase 3.
- Roadmap: Guest checkout (Phase 6) is independent of Accounts (Phase 7) — `Order.ClientId` nullable.
- Roadmap: `ZachHairStudio.Admin` MVC scaffold flagged as legacy/retire — noted at Phase 3, retirement criterion in Phase 8.
- Phase 1 Plan 02: ServicesController injects ServicesService and validators only; all Services DbContext access lives in ServicesService.
- Phase 1 Plan 02: Service write endpoints use controller-shaped ProblemDetails plus defensive service-layer FluentValidation.
- Phase 1 Plan 02: Services seed data uses EF Core HasData through AddServices migration; no UseSeeding/UseAsyncSeeding.
- Phase 1 Plan 03: Service detail booking CTA uses dedicated `/book?service={slug}` route, not the homepage contact anchor.
- Phase 1 Plan 04: Homepage shows first 6 services by `displayOrder`; `app/page.tsx` fetches once and passes services to both `Services` and `Contact` as props.
- Phase 1 Plan 04: `?service={slug}` preselect is validated against the fetched catalog and falls back to the empty option for unknown slugs (mitigates T-01-09).
- Phase 1 Plan 04: Booking API contract preserved — `createBooking` still receives a human-readable service string; Phase 2 rebuilds booking against real slots.
- Phase 1 Plan 04: `lib/data.ts` now holds only presentational site content; catalog data has a single database-backed source (D-14).
- [Phase ?]: Phase 3 Plan 01: IdentityRole<int> (not string-keyed IdentityRole) is the correct TRole for IdentityDbContext<ApplicationUser, IdentityRole<int>, int> given int-keyed ApplicationUser.
- [Phase 03]: Phase 3 Plan 01: base.OnModelCreating(modelBuilder) moved to the start of BookingDbContext.OnModelCreating per ASP.NET Core IdentityDbContext convention.
- [Phase 03]: Phase 3 Plan 01: JwtTokenService uses a custom 'displayName' claim type since ClaimTypes has no built-in slot distinct from ClaimTypes.Name (login UserName).
- [Phase 03]: Phase 3 Plan 02: AuthGateTests uses anonymous request objects + JsonDocument response parsing instead of the Shared DTO types, so the RED-phase test file compiles standalone before AuthController/StaffUsersController exist.
- [Phase 03]: Phase 3 Plan 02: Test JWT signing key injected via WithWebHostBuilder(...).ConfigureAppConfiguration(...) in-memory config, relying on the same mutable ConfigurationManager instance Program.cs's AddJwtBearer closure reads from at request time.
- [Phase 03]: Phase 3 Plan 02: StaffUsersController uses an explicit [Route("api/staff-users")] (not the [controller] token) since the default token would yield /api/staffusers with no hyphen.
- [Phase 03]: Phase 3 Plan 03: Global JsonStringEnumConverter registered in Program.cs so enum request/response fields (AppointmentStatusUpdateDto.NewStatus) round-trip as strings, matching AppointmentResponseDto.Status's existing string shape.
- [Phase 03]: Phase 3 Plan 03: UpdateStatusAsync re-reads the current status from the DB and checks the single AllowedTransitions map before mutating - the one reusable slot-release path for Cancel/NoShow, never a forked copy.
- [Phase ?]: Phase 2 Plan 07: SC5's DST-transition clause is descoped for the Asia/Yangon deployment (fixed UTC+06:30, never observes DST); DstBoundaryTests, DstRoundTripTests, and WritePathOffsetTests remain as the standing DST/offset proofs.
- [Phase ?]: Phase 2 Plan 07: Create-path test dates derive from a shared TestSupport.BookingDates helper (relative-to-now via SalonTimeZone) instead of hardcoded calendar literals, for any test crossing the future-gated AppointmentCreateDtoValidator.
- [Phase ?]: Phase 3 Plan 05: Hand-rolled CSS Grid day view with Asia/Yangon scheduleTime helpers — no calendar library.
- [Phase ?]: Phase 3 Plan 05: Schedule fetches all statuses; Cancelled/NoShow reveal is client-side includeCancelled toggle (D-08).
- [Phase ?]: Phase 3 Plan 05: POST /api/staff-users 201 Created treated as success on Owner add-staff form.
- [Phase ?]: Phase 4 Plan 01: Action-level (not class-level) [Authorize(Roles=Owner)] on ServicesController writes only — GET stays anonymous (Pitfall 5).
- [Phase ?]: Phase 4 Plan 01: Backfill IWebHostEnvironment.WebRootPath explicitly in Program.cs in addition to an explicit PhysicalFileProvider — ASP.NET Core leaves WebRootPath empty (not just the file provider) when wwwroot is absent at host-build time.
- [Phase ?]: Phase 4 Plan 01: Uploaded image filenames are Path.GetRandomFileName() + a content-type-derived extension, never the client FileName (path-traversal safe).
- [Phase ?]: Phase 4 Plan 02: GET /api/Services had no way to fetch retired services (frontend-only plan, no new API filter param) — retired/reactivated services were tracked in local session state (retiredOverrides). **Resolved by quick task 260725-mx3:** `includeInactive` query param added, honored only for an authenticated Owner (silently ignored otherwise, DD-1); `IsActive` is `bool?` omitted when null so the anonymous catalog response stays byte-identical (DD-2). The retiredOverrides workaround is gone.
- [Phase ?]: Phase 4 Plan 02: Service slug (required by DTOs, not a UI-SPEC field) is derived from Name via slugify() at first create and held fixed afterward so edits never change an already-public service URL.
- [Phase ?]: Phase 4 Plan 02: ServiceForm branches POST vs PUT on local serviceId state (persisted or not), not the static create/edit mode prop, so the form stays open after create to unlock image upload immediately.
- [Phase ?]: Phase 4 Plan 02: Multipart image upload uses a direct fetch() to POST /api/Services/{id}/image instead of the typed openapi-fetch client — the .NET OpenAPI doc documents the [FromForm] IFormFile body as application/x-www-form-urlencoded (a Swashbuckle quirk, not the real wire format).
- [Phase ?]: Phase 4 Plan 03: PUT working-hours replace returns 204 NoContent; POST time-off returns 201 Created via a string-URI Created(...) (no CreatedAtAction, no matching GET action exists); DELETE time-off returns 204 NoContent.
- [Phase ?]: Phase 4 Plan 03: AvailabilityService writes exclusively to StylistWorkingHours/StylistTimeOff (D-08) via a single implicit-transaction SaveChangesAsync (delete-then-insert), no manual BeginTransaction — mirrors AppointmentsService's EnableRetryOnFailure-safe pattern.
- [Phase ?]: Phase 4 Plan 04: Added GET /api/availability/{stylistId} — Plan 03 shipped write-only endpoints; the dashboard editor needs a read path, added reading only StylistWorkingHours/StylistTimeOff (D-08 preserved).
- [Phase ?]: Phase 4 Plan 04: DayOfWeek (and other server enums) are typed number by Swashbuckle but serialize as string names via the global JsonStringEnumConverter — confirmed via live GET; cast explicitly on write, matching the AppointmentStatus precedent in scheduleStatus.ts.
- [Phase ?]: Phase 4 Plan 04: Kept the UI-SPEC's wider 06:00-22:00 week-strip default window over the narrower seeded 09:00-18:00 per-stylist default, giving staff painting room beyond the placeholder.
- [Phase ?]: Phase 4 Plan 05: Result<T>.Conflicts is a T-independent side-channel (not a repurposing of Data) since AddTimeOffAsync's success Data must stay the created StylistTimeOff entity.
- [Phase ?]: Phase 4 Plan 05: The conflict scan + persist for both working-hours and time-off writes run inside Database.CreateExecutionStrategy().ExecuteAsync wrapping a manual BeginTransactionAsync -- the one write path in this codebase needing a real transaction under EnableRetryOnFailure.
- [Phase ?]: Phase 4 Plan 05: Removing time off is never conflict-scanned -- it can only widen availability, never orphan a Confirmed appointment.
- [Phase ?]: Phase 4 Plan 05: AvailabilityConflictDto.SalonLocalTime carries the raw UTC instant; the dashboard formats it via the existing formatSalonDateTime helper rather than server-side pre-formatting.
- [Phase ?]: Phase 2 Plan 09: Reconcile-don't-delete pattern for stale planning docs — annotate with provenance markers instead of rewriting history
- [Phase ?]: Phase 4 Plan 06: ESLint no-restricted-syntax core rule (no plugin/package) guards against onChange/emit* calls nested inside set*() state updaters -- the exact G-04-5 defect shape.
- [Phase ?]: Phase 4 Plan 06: WeekStripEditor commits the painted drag range via a previewRangeRef mirror (written on pointerdown/pointermove), letting handleUp call emitChange from its own event-handler body instead of inside the previewRange state updater -- closing G-04-5's render-phase parent setState.
- [Phase ?]: Phase 4 Plan 07: WeekStripEditor resize commits as a direct per-segment array replace via emitChange, bypassing mergeSegments, so a shrink can never be silently re-expanded by the additive union logic (closes gap G-04-6).
- [Phase ?]: Phase 5 Plan 01: ServiceRecommendedProduct is an explicit join POCO configured via UsingEntity<T>() rather than EF's implicit shadow join table, so HasData seeding works with typed objects.
- [Phase ?]: Phase 5 Plan 01: Recommended-products query lives in ServicesService.GetBySlugAsync (not a separate ProductsService method) since both entities share BookingDbContext.
- [Phase ?]: Phase 5 Plan 01: ServiceResponseDto.RecommendedProducts extends the existing DTO rather than a dedicated endpoint, resolving D-16 discretion.
- [Phase ?]: Phase 5 Plan 02: SectionHeading's subtitle prop passed an empty string on the Recommended Products section (required by type, renders no visible text)
- [Phase ?]: Phase 5 Plan 02: RecommendedProductCard is a deliberate small markup duplication of app/products/page.tsx's ProductCard — both Server Components, no shared client bundle concern, extraction deferred
- [Phase 06]: Cart upsert sets absolute Quantity clamped to Products.Stock; unknown session GET returns empty items
- [Phase 06]: Guest cart session via X-Cart-Session-Id header (max 64) because AllowAnyOrigin blocks credentialed cookies
- [Phase 06]: Message-only Result.ConflictError overload added for stock 409 (Pitfall 7)
- [Phase ?]: Add-to-cart is additive (current qty + selected) clamped to stock, then absolute upsert
- [Phase ?]: Suggestion chips omitted until Plan 04 — no fake chip data for SHOP-07
- [Phase ?]: Proceed to Checkout links to /checkout (acceptable interim until checkout plan)
- [Phase ?]: Checkout money recomputed only from Products.Price; CheckoutRequestDto has no price/total fields (D-05)
- [Phase ?]: Order.ClientId nullable for guest checkout; Status starts Pending and is never Fulfilled from checkout POST
- [Phase ?]: SqliteWebApplicationFactory for checkout tests because ExecuteUpdateAsync is relational-only
- [Phase ?]: GET /api/products/recommended-for-checkout uses repeated productIds query params
- [Phase ?]: Reuse Plan 03 GET /api/orders/{id} for success display; no new fulfillment writer
- [Phase ?]: Success accepts orderId/order or trailing digits from session_id (fake-{id})
- [Phase ?]: Phase 07 Plan 01: DisplayName defaults to email local-part when omitted on client register
- [Phase ?]: Phase 07 Plan 01: landing getSession/setSession reject non-Client roles; AUTH_UPDATED_EVENT refreshes Navbar same-tab
- [Phase ?]: Phase 07 Plan 02: Appointment.ClientUserId FK OnDelete Restrict (no cascade-delete appointments)
- [Phase ?]: Phase 07 Plan 02: /account redirects to /account/bookings; claim skip posts confirm=false
- [Phase ?]: Reschedule book-new shares TryBookNewAsync with guest CreateAsync; ClientUserId set on new row
- [Phase ?]: Until-start gate compares StartsAt <= UtcNow (same clock as create validator)
- [Phase ?]: UI Cancel/Reschedule only for upcoming Confirmed; past/terminal view-only
- [Phase ?]: LoyaltyReasons Earn/Redeem strings; filtered unique index on AppointmentId where Reason=Earn
- [Phase ?]: Payment-failure compensation appends positive Redeem delta (append-only; no ledger deletes)
- [Phase ?]: Phase 07 Plan 05: Public create attaches Client NameIdentifier only (Staff/guest null); landing Bearer + embedded claim on Bookings
- [Phase ?]: Optional Client JWT on checkout/quote via NameIdentifier; RedeemPoints-only money authority

### Pending Todos

- **REQUIREMENTS.md doc-sync (non-blocking).** CAT-01/CAT-02 were still marked `[ ]` Pending at Phase 1 verification despite being functionally complete — noted in `01-VERIFICATION.md` as a documentation-sync item, not a code gap.

### Blockers/Concerns

- REQUIREMENTS.md header/coverage text said "34 requirements" but the actual v1 list totals 41 — corrected in the Traceability/Coverage section during roadmap creation; worth a quick sanity check with the user.
- Phase 2 (Booking Core), Phase 6 (Cart & Checkout), and Phase 7 (Accounts & Retention) are flagged for a deeper per-phase research pass before planning (see ROADMAP.md Research flag annotations and research/SUMMARY.md Research Flags section).
- Payment provider (Phase 6) and auth provider/session strategy (Phase 7) remain open decisions per PROJECT.md Key Decisions — confirm before planning those phases.
- ~~Default `MSSQLLocalDB` fails on this machine~~ — **resolved 2026-07-09.** The corrupted automatic instance was deleted and recreated (now v17.0.4025.3); migrations apply cleanly to `(localdb)\MSSQLLocalDB`, database `ZachHairStudio`. The API also runs against Azure SQL (`zachhairstudio.database.windows.net`) via a `ConnectionStrings__DefaultConnection` env-var override — note the Azure SQL firewall must allow the client IP.
- `appsettings.json` `DefaultConnection` is `Server=localhost;...`, which disagrees with the `(localdb)\MSSQLLocalDB` documented in CLAUDE.md. Use `dotnet user-secrets` (not `appsettings.json`) for any connection string carrying a password — gitleaks scanning is wired to the pre-commit hook.

### Quick Tasks Completed

| #          | Description                                                                                                                        | Date       | Commit  | Directory                                                                                                           |
| ---------- | ---------------------------------------------------------------------------------------------------------------------------------- | ---------- | ------- | ------------------------------------------------------------------------------------------------------------------- |
| 260712-tds | Fix semgrep CI findings (semgrepignore vendored tooling, pin gitleaks workflow action SHAs)                                        | 2026-07-12 | 40e3207 | [260712-tds-fix-semgrep-ci-findings-semgrepignore-ve](./quick/260712-tds-fix-semgrep-ci-findings-semgrepignore-ve/) |
| 260716-qfe | Fix gitleaks false positives on GSD manifest checksums (rule-targeted regex allowlist replaces stale .gitleaksignore fingerprints) | 2026-07-16 | 8e7a1d2 | [260716-qfe-fix-gitleaks-false-positives-on-gsd-mani](./quick/260716-qfe-fix-gitleaks-false-positives-on-gsd-mani/) |
| 3          | Bump CI gitleaks v8.18.4 -> v8.30.1 so [[allowlists]] config applies in security.yml scan                                          | 2026-07-16 | 1221ba5 | —                                                                                                                   |
| 4          | Pin GITLEAKS_VERSION 8.30.1 in gitleaks-action workflow so [[allowlists]] config applies                                           | 2026-07-16 | b130c2d | —                                                                                                                   |
| 260731-uj3 | Add an AI chat widget UI to the landing page - booking-assistant floating chat bubble with mock responses (no real backend yet)    | 2026-07-31 | 0747757 | [260731-uj3-add-an-ai-chat-widget-ui-to-the-landing-](./quick/260731-uj3-add-an-ai-chat-widget-ui-to-the-landing-/) |
| 6          | Fix plural "hours" match in chat.ts hours-keyword regex (\\b(hour\|open\|close)\\b -> \\b(hours?\|open\|close)\\b)                 | 2026-07-31 | dcbbfdf | —                                                                                                                   |
| 260801-irn | Add an MCP tool exposing appointment slot availability via GetSlots                                                                | 2026-08-01 | 27f256c | [260801-irn-add-an-mcp-tool-exposing-appointment-slo](./quick/260801-irn-add-an-mcp-tool-exposing-appointment-slo/) |

| 260809-gpw | Fix semgrep SAST finding (detect-non-literal-regexp) in dashboard adminChat weekday matching | 2026-08-09 | ba50770 | [260809-gpw-fix-semgrep-sast-finding-detect-non-lite](./quick/260809-gpw-fix-semgrep-sast-finding-detect-non-lite/) |
| 260809-adm | Keep AdminChat starter-prompt buttons visible after the first message instead of disappearing | 2026-08-09 | 589470e | [260809-adm-keep-starter-prompts-visible](./quick/260809-adm-keep-starter-prompts-visible/) |
| 260809-sz1 | Increase AdminChat dialog panel size (24rem x 32rem -> 28rem x 40rem desktop) | 2026-08-09 | c05a2ec | [260809-sz1-increase-adminchat-box-size](./quick/260809-sz1-increase-adminchat-box-size/) |
| 260809-sf1 | Add slot-filling conversation state to AdminChat (bare service/date follow-ups now resolve against the pending question) | 2026-08-09 | 148dda6 | [260809-sf1-adminchat-slot-filling](./quick/260809-sf1-adminchat-slot-filling/) |

| 260809-gd7 | chat widget generic service term matching for haircut | 2026-08-09 | 69d81fd | [260809-gd7-chat-widget-generic-service-term-matchin](./quick/260809-gd7-chat-widget-generic-service-term-matchin/) |
| 260809-hui | Fix gitleaks docker "dubious ownership" error in security workflow (mark /repo safe in container's own gitconfig) | 2026-08-09 | becd367 | [260809-hui-fix-gitleaks-docker-dubious-ownership-er](./quick/260809-hui-fix-gitleaks-docker-dubious-ownership-er/) |
| 260809-ipz | Fix gitleaks CI still failing after becd367 — replace Docker invocation with direct binary install (dubious-ownership was never the real cause; image already sets safe.directory at build time) | 2026-08-09 | f324da1 | [260809-ipz-fix-gitleaks-ci-still-failing-binary-not-do](./quick/260809-ipz-fix-gitleaks-ci-still-failing-binary-not-do/) |
| 260809-k3d | Fix gitleaks CI still failing (real root cause) — extend .gitleaks.toml's GSD manifest checksum allowlist to .codex/ paths; 3 genuine generic-api-key false positives in .codex/gsd-file-manifest.json, unmasked only once 260809-ipz let the scan complete | 2026-08-09 | 979f316 | [260809-k3d-fix-gitleaks-codex-manifest-allowlist](./quick/260809-k3d-fix-gitleaks-codex-manifest-allowlist/) |
| 260809-m2q | Suppress semgrep detect-non-literal-regexp false positive on chat.ts/chat.selfcheck.mjs alias RegExp — alias is hardcoded (CATEGORY_ALIASES), never user input, no ReDoS surface; targeted nosemgrep comment + rationale | 2026-08-09 | 9ae1533 | [260809-m2q-fix-semgrep-redos-false-positive-alias-r](./quick/260809-m2q-fix-semgrep-redos-false-positive-alias-r/) |
| 260809-n8x | Fix misplaced nosemgrep comment in chat.ts (260809-m2q's comment sat 2 lines above the flagged new RegExp() call, never actually suppressed it); moved directly above the call, verified 0 findings via real local semgrep run | 2026-08-09 | 973b2fb | [260809-n8x-fix-semgrep-nosemgrep-comment-placement](./quick/260809-n8x-fix-semgrep-nosemgrep-comment-placement/) |

| 260809-4d3 | Implement shell files for the same functionality of start-dev.bat and stop-dev.bat files | 2026-08-09 | 2c5e7c6 | [260809-4d3-implement-shell-files-for-the-same-funct](./quick/260809-4d3-implement-shell-files-for-the-same-funct/) |

## Deferred Items

Items acknowledged and carried forward from previous milestone close:

| Category | Item | Status | Deferred At |
| -------- | ---- | ------ | ----------- |
| _(none)_ |      |        |             |

## Deferred Verification

| Phase | State | Resume |
|-------|-------|--------|
| 6 | verification_deferred_human | /gsd-verify-work 6 |

## Session Continuity

Last session: 2026-08-10T10:34:35.534Z
Stopped at: Completed 07-05-PLAN.md (gap closure)
Resume file: None

Next action: Re-verify Phase 07 after gap closure (`/gsd-execute-phase 7` verify step or autonomous post-execution routing), then continue to Phase 8.
