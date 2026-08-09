---
phase: 04
slug: staff-management-services-availability
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-08-09
---

# Phase 04 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| dashboard/browser → ServicesController writes | Untrusted callers may attempt service create/edit/image upload | Service name/description/duration/price, multipart image bytes |
| uploaded file bytes → server disk | Untrusted multipart content crosses into the filesystem | Image binary + client-declared filename/content-type |
| Staff session → /services route + Services nav link | A non-Owner staff session must not reach the Owner catalog editor | Route/nav visibility (UX only — server is authoritative) |
| dashboard/browser → AvailabilityController | Untrusted callers may attempt availability writes | Working-hours segments, time-off ranges |
| client-painted week-strip local times → server persistence | Client sends DayOfWeek/TimeOnly the server must revalidate | DayOfWeek + TimeOnly segment boundaries |
| client Save Changes → server conflict check | A malicious client could try to skip the conflict check | Proposed final availability state vs. confirmed appointments |
| conflict response → dashboard | Response must expose only allowed fields, no extra PII | AppointmentId/ClientName/ServiceName/StylistName/SalonLocalTime |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-04-01 | Elevation of Privilege | ServicesController POST/PUT | high | mitigate | Action-level `[Authorize(Roles = StaffRoles.Owner)]`; proven by ServicesControllerAuthTests (401/403/201/204), 35/35 pass | closed |
| T-04-02 | Tampering | Image storage filename (path traversal) | high | mitigate | `Path.GetRandomFileName()`; client `FileName` never referenced (zero repo matches) | closed |
| T-04-03 | DoS / Tampering | Unrestricted file type/size upload | high | mitigate | 5MB cap + content-type allowlist in `ServiceImageUploadDtoValidator`, enforced before disk write | closed |
| T-04-04 | Tampering | Relative upload path lost outside dev launch context | low | mitigate | Resolved via `IWebHostEnvironment.WebRootPath` + startup backfill fix | closed |
| T-04-05 | Info Disclosure / EoP | Services nav link + /services route visible to Staff | medium | mitigate | Nav link omitted for Staff; client redirect to /schedule; server Owner gate (T-04-01) authoritative | closed |
| T-04-06 | Elevation of Privilege | Availability write endpoints reachable anonymously | high | mitigate | Class-level `[Authorize]`; anonymous → 401, proven by WorkingHoursReplaceTests/TimeOffTests | closed |
| T-04-07 | Tampering | Client echoes DayOfWeek/TimeOnly that could be inconsistent | medium | mitigate | Server revalidates every segment via `WorkingHoursReplaceDtoValidator` (15-min alignment, End>Start, no overlap) | closed |
| T-04-08 | Tampering | Client bypasses the conflict check | high | mitigate | Conflict scan + persist share one DB transaction; ConflictCheckTests proves the 409 block | closed |
| T-04-09 | Info Disclosure | Conflict list leaks client PII beyond allowed fields | medium | mitigate | `AvailabilityConflictDto` exposes only AppointmentId/ClientName/ServiceName/StylistName/SalonLocalTime | closed |
| T-04-10 | Tampering (race) | Booking confirmed between scan and write | medium | mitigate | Scan + persist atomic in one transaction | closed |
| T-04-13 | EoP (accepted by design) | Any staff editing any stylist's availability | low | accept | D-13: any authenticated staff may edit any stylist's availability; no per-stylist ownership check by design | accepted |
| T-04-14 | Info Disclosure | Availability page reachable only behind auth | low | mitigate | `requireAuth` client guard + server `[Authorize]` (T-04-06) | closed |
| T-04-15 | Tampering | Painted segments committed from a ref rather than from state | low | mitigate | Ref written only by own pointer handlers, never sent unvalidated; server revalidates every segment | closed |
| T-04-16 | DoS | Dropped/duplicated drag commits after refactor | low | mitigate | Snapping, sub-snap rejection, gap-as-break confirmed unchanged | closed |
| T-04-17 | Tampering | Resize commit bypasses `mergeSegments` | low | mitigate | Server validator revalidates every segment regardless of client interaction; conflict scan covers full final state | closed |
| T-04-18 | DoS | Render-phase parent setState reintroduced via resize commit path | low | mitigate | `handleResizeUp` calls `emitChange` from its own function body; ESLint no-restricted-syntax guard covers project-wide; `npm run lint` clean | closed |
| T-04-19 | Tampering | Resize clamp math permits invalid segment to reach emitChange | low | mitigate | `handleResizeMove` clamps against own opposite edge + adjacent segment boundary; server validator is backstop | closed |
| T-04-SC (×7, plans 04-01…04-07) | Tampering | npm/NuGet installs | low | accept | No new packages in any of the 7 plans; confirmed via tech-stack.added:[] and git diff-tree | accepted |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on (high) count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| R-04-01 | T-04-13 | D-13: any authenticated staff may edit any stylist's availability by design — no per-stylist ownership boundary intended for this phase | Project decision log (04-CONTEXT.md D-13) | 2026-08-09 |
| R-04-02 | T-04-SC (×7) | No new npm/NuGet packages installed across all 7 plans in this phase | gsd-security-auditor | 2026-08-09 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-08-09 | 19 (+7 T-04-SC accept entries) | 24 | 0 | gsd-security-auditor (opus) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-08-09

---

## Note

One adjacent item flagged by the auditor for awareness (not a threat in this phase's register, not a security hole — fails safe): `ServicesController.GetService` (single-slug GET) does not honor the Owner `includeInactive` override the way `GetServices` (list) does — a retired service 404s even for an Owner fetching by slug. Already documented in `04-REVIEW.md:167`; tracked separately from this security register.
