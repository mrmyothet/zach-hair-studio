# Codebase Concerns

**Analysis Date:** 2026-07-07

## Security Issues

### Open CORS Policy

**Risk:** Credentials and API responses exposed to any origin; attackers can make arbitrary requests on behalf of users.

**Files:** `API/ZachHairStudio.Api/Program.cs` (line 15-19)

**Current mitigation:** Development-only concern mitigated by running on localhost. Production deployment will be severely exposed.

**Recommendations:**
- Restrict CORS to specific origins: `AllowAnyOrigin()` → `WithOrigins("https://yourdomain.com", "https://dashboard.yourdomain.com")`
- Use environment-specific configuration to load allowed origins from `appsettings.Production.json`
- Document required CORS headers per frontend deployment target

**Priority:** High — must fix before any production deployment

### Missing Authentication & Authorization

**Risk:** All booking endpoints are publicly accessible with no authentication. Staff can modify any booking status without verification.

**Files:**
- `API/ZachHairStudio.Api/Controllers/BookingsController.cs` (no `[Authorize]` attributes)
- `API/ZachHairStudio.Api/Program.cs` (no auth scheme configured)

**Current mitigation:** Runs on private network during development; relies on obscurity.

**Recommendations:**
- Add identity/auth library: consider JWT bearer tokens or OpenID Connect for future account system
- Protect staff endpoints: `[Authorize(Roles = "Staff")]` on `UpdateStatus` endpoint
- Use role-based access control before Phase 7 (Accounts & retention)

**Priority:** High — required before staff dashboard goes live

### Hardcoded Trusted Server Certificate

**Risk:** `TrustServerCertificate=True` in connection strings bypasses SSL/TLS validation; enables man-in-the-middle attacks if SQL Server connection is exposed.

**Files:**
- `API/ZachHairStudio.Api/appsettings.json` (line 3)
- `API/ZachHairStudio.Api/appsettings.Development.json` (line 3)

**Current mitigation:** Applies to localhost and local SQL Server only during development.

**Recommendations:**
- Keep in `appsettings.Development.json` for local work only
- Remove from `appsettings.Production.json` when created; proper certificate should be installed on production SQL Server
- Verify certificate validation in staging environment before go-live

**Priority:** Medium — low immediate risk, but must address for production

---

## Tech Debt

### CreatedAt Timestamp Handling Inconsistency

**Issue:** `DateTime.UtcNow` is captured client-side (at booking object creation) rather than server-side at database insert time.

**Files:**
- `API/ZachHairStudio.Shared/Features/Bookings/BookingExtensions.cs` (line 31)
- `API/ZachHairStudio.Shared/Migrations/20260702131250_InitialSqlServerMigration.cs` (line 28: no `defaultValueSql`)

**Impact:** 
- Multiple requests in same millisecond may have identical CreatedAt despite different insertion order
- Time skew between API server and database server not handled
- Audit trail unreliable for ordering bookings by creation time

**Fix approach:**
1. Add database-level default in migration: `.Annotation("SqlServer:DefaultValueSql", "GETUTCDATE()")`
2. Remove `CreatedAt` assignment from `BookingExtensions.ToEntity()`
3. EF Core will use database-generated value on SaveChanges

**Priority:** Medium — correctness issue, not blocking feature

### Database Migration at Startup

**Issue:** `db.Database.Migrate()` runs synchronously during application startup (Program.cs line 31).

**Files:** `API/ZachHairStudio.Api/Program.cs` (line 28-32)

**Impact:**
- In multi-instance deployments, all instances may attempt schema changes simultaneously → deadlock risk
- Application startup blocked if database is slow or unreachable
- Deployment becomes fragile; must manually manage migration order

**Fix approach:**
1. Separate migration into pre-deployment step (CI/CD pipeline or manual `dotnet ef database update`)
2. During startup, only verify schema compatibility with `migrationAssembly` configuration
3. Document deployment checklist: "Run EF migrations before deploying new version"

**Priority:** Medium-Low — works for current single-instance setup; becomes critical at scale

### No Input Date Validation

**Issue:** `PreferredDate` accepts any date including past dates without validation.

**Files:**
- `API/ZachHairStudio.Shared/Features/Bookings/BookingCreateDto.cs` (line 23: no `Future` attribute)
- `API/ZachHairStudio.Api/Controllers/BookingsController.cs` (line 45: only checks ModelState)

**Impact:**
- Users can book appointments for dates that have already passed
- Staff must manually reject invalid bookings
- No guard against accidental past-date submissions

**Fix approach:**
1. Add custom validation attribute: `[Future(ErrorMessage = "Preferred date must be in the future")]` to BookingCreateDto
2. Create `FutureAttribute` implementing `ValidationAttribute`
3. Server-side validation in controller if needed for business rule enforcement

**Priority:** Medium — functional gap but workaround exists (staff can reject)

### Unnecessary Production Dependencies in Landing Page

**Issue:** Development tools (`playwright`, `chrome-devtools-mcp`) are in `package.json` dependencies, not devDependencies.

**Files:** `landing-page/package.json` (lines 12-13)

**Impact:**
- Bloated production bundle: adds ~50MB to deployment size
- Security surface: testing libraries may have vulnerabilities
- Confusion: unclear what's runtime vs. build-time requirement

**Fix approach:**
```bash
# Move to devDependencies
npm install --save-dev playwright chrome-devtools-mcp
npm uninstall playwright chrome-devtools-mcp
npm install
```

**Priority:** Low — doesn't affect functionality but impacts deployment size

---

## Missing Critical Features

### No API Rate Limiting

**Problem:** Booking endpoint has no rate limit; allows unlimited requests from single IP or user.

**Files:** `API/ZachHairStudio.Api/Controllers/BookingsController.cs` (no `[RateLimit]` attribute)

**Blocks:**
- Protection against brute-force or spam booking floods
- Denial-of-service vulnerability if exposed publicly

**Fix approach:**
- Add `Microsoft.AspNetCore.RateLimit` package (built-in to .NET 10)
- Configure sliding window limiter: e.g., 10 requests per minute per IP
- Apply to `POST /api/bookings` endpoint

**Priority:** High — required before public launch

### No Logging Infrastructure

**Problem:** Application has Logging configured but no instrumentation in business logic; errors go unnoticed.

**Files:** `API/ZachHairStudio.Api/Program.cs` (Logging configured but not injected), `API/ZachHairStudio.Api/Controllers/BookingsController.cs` (no logging)

**Blocks:**
- Debugging production issues
- Monitoring booking success/failure rates
- Alerting on errors

**Fix approach:**
1. Inject `ILogger<BookingsController>` into controller constructor
2. Log at key points: `_logger.LogInformation("Booking created: {bookingId}", booking.Id)`
3. Log exceptions: `_logger.LogError(ex, "Failed to create booking from {email}", createDto.Email)`
4. Add structured logging (Serilog or built-in JSON formatter) for production

**Priority:** Medium — needed before production to diagnose issues

### No Error Handling in Controller

**Problem:** Database operations not wrapped in try-catch; exceptions propagate as unhandled 500 responses.

**Files:** `API/ZachHairStudio.Api/Controllers/BookingsController.cs`

**Impact:**
- Users see raw exception messages instead of friendly errors
- No opportunity to log or alert on failures
- Stack traces may leak sensitive information to frontend

**Fix approach:**
```csharp
try
{
    await _dbContext.SaveChangesAsync();
}
catch (DbUpdateException ex)
{
    _logger.LogError(ex, "Database error while saving booking");
    return StatusCode(500, new { message = "Failed to save booking" });
}
```

**Priority:** Medium — affects user experience and observability

### No Email Verification for Bookings

**Problem:** Email address accepted without verification; invalid or malicious emails block legitimate confirmations.

**Files:** `API/ZachHairStudio.Shared/Features/Bookings/BookingCreateDto.cs` (line 13: `[EmailAddress]` attribute only)

**Blocks:**
- Staff cannot reach customers to confirm appointments
- Typos in email prevent booking confirmations from being received
- No way to verify customer contact info before spending stylist time

**Fix approach:** (Phase 7 priority)
1. Add email confirmation flow: send verification link, mark booking as `Unverified` until confirmed
2. Use background job (Hangfire/Quartz) to cancel unverified bookings after 24 hours
3. Integrate with notification service (SendGrid/Mailgun) for email sending

**Priority:** Low (Phase 7) — functional workaround exists (manual verification by staff)

---

## Test Coverage Gaps

### Zero Automated Tests

**What's not tested:**
- Booking creation validation (empty fields, invalid email, past dates)
- Status update logic and transitions
- Database persistence (data actually saved to database)
- API response format and status codes
- Frontend form submission and error handling

**Files:**
- `API/ZachHairStudio.Api/Controllers/BookingsController.cs`
- `landing-page/components/Contact.tsx`
- `API/ZachHairStudio.Shared/Features/Bookings/`

**Risk:** Changes to business logic may silently break without detection; refactoring becomes risky.

**Fix approach:**
1. Add xUnit test project: `ZachHairStudio.Api.Tests`
2. Write unit tests for DTOs and extensions (validation, mapping)
3. Write integration tests for controller endpoints (mock DbContext or use in-memory database)
4. Add frontend tests with Vitest/Jest for Contact component form submission

**Priority:** Medium — becomes critical as features scale; add by Phase 3 (staff dashboard)

---

## Fragile Areas

### Booking Entity Lacks Auditability

**Files:** `API/ZachHairStudio.Shared/Features/Bookings/Booking.cs`

**Why fragile:**
- No `UpdatedAt` timestamp: can't track when status changed
- No `UpdatedBy` field: can't audit who changed a booking
- No soft deletes: can't recover accidentally cancelled bookings
- Status transitions not validated: entity doesn't prevent invalid transitions (e.g., Completed → Pending)

**Safe modification:**
1. Before updating status, add `UpdatedAt` and `UpdatedBy` fields to Booking
2. Implement `BookingStatus` state machine to prevent invalid transitions
3. Add integration tests validating each transition is allowed

**Test coverage:** No tests for status update logic

**Priority:** Medium — becomes important at scale; add before Phase 3

### Database Retry Policy Unconfigured for Production

**Issue:** `EnableRetryOnFailure(maxRetryCount: 10, maxRetryDelay: TimeSpan.FromSeconds(30))` in Program.cs (line 10-12) uses hardcoded values.

**Files:** `API/ZachHairStudio.Api/Program.cs`

**Risk:**
- Retry delays may be too aggressive for slow networks; waste time and connections
- 10 retries insufficient for long-running migrations or high-contention databases
- No exponential backoff: linear delay increases CPU load

**Fix approach:**
1. Move retry policy to `appsettings.json`: read `"Database:MaxRetryCount"` and `"Database:MaxRetryDelaySeconds"`
2. Implement exponential backoff instead of linear
3. Document recommended values: "5-10 retries for development; 3-5 for production SQL Server with proper backups"

**Priority:** Low-Medium — works fine for development; needs tuning before production

---

## Scaling Limits

### No Connection Pooling Configuration

**Current capacity:** Default pooling (128 connections for SQL Server).

**Limit:** At ~20 concurrent requests per connection, maximum capacity ~6,400 concurrent users before connection exhaustion.

**Files:** `API/ZachHairStudio.Api/Program.cs` (no `SetCommandTimeout` or `Max Pool Size` configured)

**Scaling path:**
1. Configure pool size in connection string: `Max Pool Size=50` for development; tune for production based on load testing
2. Monitor connection pool exhaustion: add metrics logging in DbContext
3. At scale, consider connection pooling proxy (pgBouncer for PostgreSQL, etc.)

**Priority:** Low — non-blocking until deployment; address in Phase 8 (launch readiness)

### PreferredDate Format Not Validated for Timezone

**Issue:** Frontend sends ISO date string (`YYYY-MM-DD`); API converts to `DateTime` without timezone info.

**Files:**
- `landing-page/lib/api.ts` (line 16: comment notes "ISO date string")
- `API/ZachHairStudio.Shared/Features/Bookings/BookingCreateDto.cs` (line 23: `DateTime` has no timezone info)

**Impact:** If stylists and customers are in different timezones, appointments may be off by hours.

**Scaling path:** (Phase 7 or later)
1. Agree on timezone handling: store as UTC, display in user's local timezone
2. Add `TimeZoneId` field to Booking: `America/New_York` etc.
3. Adjust `PreferredDate` calculation: `preferredDate.ToUniversalTime()`

**Priority:** Low — currently single-location business; becomes critical with multi-location expansion

---

## Dependencies at Risk

### EF Core 10 / .NET 10 (Cutting Edge)

**Risk:** .NET 10 released Feb 2025; May 2025 becomes "current" with most support. Next LTS (Nov 2026) is ~18 months away.

**Impact:**
- Limited production deployment experience
- Breaking changes possible in minor releases
- Support window expires faster than LTS versions

**Migration plan:**
- Monitor .NET 10 release notes monthly
- Run CI/CD against preview version of .NET 11 (due Nov 2025)
- Plan migration to LTS (whenever next LTS ships) before .NET 10 goes out of support

**Priority:** Low — acceptable risk for new projects; revisit after Phase 3 (3+ months in)

### Next.js 15 (App Router)

**Risk:** App Router still evolving; some APIs marked `unstable` or have RFC proposals.

**Impact:** Breaking changes in minor releases; middleware/headers API unstable.

**Migration plan:**
- Pin specific minor version in `package.json`: `"next": "15.3.0"` (currently pinned)
- Run `npm audit` monthly
- Test Next.js RC releases in CI before upgrading

**Priority:** Low — risk is well-managed by pinning

---

## Incomplete Implementation

### Dashboard Not Started

**Problem:** `dashboard/` directory only contains `.gitkeep`; Phase 3 (staff dashboard) blocked.

**Files:** `dashboard/.gitkeep` (empty placeholder)

**What's missing:**
- Next.js project setup
- Staff authentication
- Booking list view
- Booking detail / status update view

**Blocking issues:** None structural; requires feature work in Phase 3.

**Priority:** Scheduled — Phase 3 deliverable; not urgent

### Admin Project (ZachHairStudio.Admin) Not Integrated

**Files:** `API/ZachHairStudio.Admin/`

**Status:** Has Program.cs and controllers but unclear if used or maintained; not referenced in roadmap.

**Risk:** Maintenance burden; unclear if this is demo, legacy, or future feature.

**Recommendation:** Clarify purpose in documentation; consider removing if unused or move to separate Phase 4+ admin API.

**Priority:** Low — does not block current work; clarify intent in specs/

---

## Performance Bottlenecks

### N+1 Query Risk in GetBookings

**Problem:** `GetBookings` orders by `CreatedAt` after querying all bookings; if index not present, full table scan on every request.

**Files:** `API/ZachHairStudio.Api/Controllers/BookingsController.cs` (lines 22-24)

**Current impact:** Negligible (only ~100 bookings); becomes problematic at 10k+ bookings.

**Improvement path:**
1. Add database index on `CreatedAt`: `modelBuilder.Entity<Booking>().HasIndex(b => b.CreatedAt).IsDescending();`
2. Monitor query execution time; add logging if query > 100ms
3. Consider pagination if booking list grows (Phase 3+): `limit 50, offset (page * 50)`

**Priority:** Low — non-blocking now; add after Phase 3 if booking volume scales

### No Caching Layer

**Problem:** Every `GetBookings` or `GetBooking` request hits the database with no caching.

**Files:** `API/ZachHairStudio.Api/Controllers/BookingsController.cs`

**Impact:** Acceptable for current traffic; staff dashboard refresh would benefit from 30-second cache.

**Improvement path:**
- Add in-memory cache: `IMemoryCache` or `IDistributedCache` (Redis for multi-instance)
- Cache bookings for 30 seconds; invalidate on status update
- Document cache strategy before Phase 8 (production deployment)

**Priority:** Low — becomes relevant at scale; Phase 8 concern

---

## Known Gaps & Future Considerations

### No Swagger Customization

**Issue:** OpenAPI/Swagger UI auto-generated from code; no custom descriptions, examples, or request/response samples.

**Files:** `API/ZachHairStudio.Api/Program.cs` (lines 22-24: basic setup)

**Gap:** Frontend developers must reverse-engineer API contract from code or trial-and-error.

**Fix approach:** Add Swashbuckle configuration: `c.SwaggerDoc("v1", new OpenApiInfo { Title = "Zach Hair Studio API", ... }); c.AddServer(...)`

**Priority:** Low-Medium — improves DX; add before Phase 2 integration

### No API Versioning Strategy

**Problem:** No version identifier in API routes (`/api/bookings` vs. `/api/v1/bookings`).

**Impact:** Breaking changes force all clients to update; backward compatibility impossible.

**Fix approach:** (Phase 4+) Implement API versioning via URL routing (`/api/v1/`) or Accept header.

**Priority:** Low — non-blocking for current scope; required before accepting external API consumers

---

## Environment Configuration Risks

### AllowedHosts = "*" in Production

**Files:** `API/ZachHairStudio.Api/appsettings.json` (line 11)

**Risk:** Vulnerable to Host header injection attacks; middleware doesn't validate Host header.

**Mitigation:** Override in `appsettings.Production.json`: `"AllowedHosts": "api.zachhairstudio.com,dashboard.zachhairstudio.com"`

**Priority:** Medium — must fix before production deployment

### Connection String in appsettings.json

**Files:** `API/ZachHairStudio.Api/appsettings.json`, `appsettings.Development.json` (connection strings visible)

**Current status:** OK for development (`.gitignore` is set up, `.env` files not tracked).

**Production risk:** Connection strings must never be in production source code. Use Azure Key Vault, AWS Secrets Manager, or environment variables.

**Mitigation:** Document deployment procedure: "Connection string loaded from environment variable `ConnectionStrings__DefaultConnection` in production."

**Priority:** Medium — critical before production; low risk now

---

*Concerns audit: 2026-07-07*
