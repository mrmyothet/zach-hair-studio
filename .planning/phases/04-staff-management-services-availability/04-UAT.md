---
status: partial
phase: 04-staff-management-services-availability
mode: mvp
source: [04-01-SUMMARY.md, 04-02-SUMMARY.md, 04-03-SUMMARY.md, 04-04-SUMMARY.md, 04-05-SUMMARY.md, 04-06-SUMMARY.md]
user_story: "As a salon staff member, I want to keep the service catalog and stylist availability accurate from the dashboard without a code deploy, so that clients always see and book real services and open slots, and no availability edit silently orphans a confirmed booking."
started: 2026-07-26T08:07:00Z
updated: 2026-07-27T00:05:00Z
---

## Current Test

[testing paused — user-flow test 13 failed (MVP mode halts on a user-flow failure); tests 14-22 remain pending until G-04-6 is fixed and retested]

## Tests

<!-- ============ SECTION A — USER FLOW (MVP: runs first, halts on failure) ============ -->

### 1. Cold Start the Stack
expected: Stop every running dotnet/next process and delete API/ZachHairStudio.Api/wwwroot/ (gitignored, recreated at startup). Start the API with `ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=ZachHairStudio;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true' dotnet run` (the user-secrets value points at firewall-blocked Azure SQL, so the override is required), the dashboard with `npm run dev -- -p 3001`, the landing page with `npm run dev`. API boots clean, /openapi/v1.json returns the spec, landing page renders on :3000, dashboard login renders on :3001.
result: pass
section: user-flow
covers: [04-01 startup path]

### 2. Log In as Owner
expected: Log in to the dashboard at :3001 with the Owner credentials. You land on the dashboard and the shared header shows a nav row with Schedule, Services, and Availability, plus the session/Add-staff/logout cluster on the right.
result: pass
section: user-flow
covers: [04-02 D1]

### 3. Open the Services Page
expected: Click Services. The page lists the current catalog in a table with Name, Category, Duration, Price, Status and Actions, each row showing a small image thumbnail or a placeholder where no image is set.
result: pass
first_attempt: issue
reported: "images are missing. use the images from zach-hair-studio\dashboard\public folder"
severity: major
fixed_by: "3475fd2 — seed ImageUrl on all 6 services + SeedAssets startup copy (gap G-04-3)"
section: user-flow
covers: [04-02 D2]

### 4. Create a Service
expected: Click the add/new-service control, fill in name, description, duration and price, and Save. Save stays disabled until the required fields are filled. After saving, the form stays open (so an image can be added) and the new service appears in the list.
result: issue
reported: "I fill all input and also upload image. when I click \"Save Service\" button, it does not work"
severity: major
section: user-flow
covers: [04-02 D4]
investigation: "Create + image upload both confirmed persisted in the API SQL log. Backend PUT /api/Services/{id} verified working via curl with the exact post-upload payload (204). A browser repro of the same click (without the image step) issued the PUT and succeeded. Cause not yet isolated — awaiting the on-screen symptom and a request-logged retry."

### 5. Upload an Image for That Service
expected: In the still-open form, use the dashed 160x160 upload box to pick a JPG, PNG or WebP under 5MB. It shows an uploading state, then the image renders in the box, and the service's row thumbnail in the list shows that image.
result: pass
section: user-flow
covers: [04-02 D5]

### 6. Edit the Service
expected: Reopen the service for editing. Every field pre-fills with its current value. Change the name or price, Save, and the list reflects the change. The service's public URL slug does not change when you edit the name.
result: pass
section: user-flow
covers: [04-02 D4]

### 7. Retire and Reactivate the Service
expected: Click Retire. A confirmation dialog appears with retire-specific copy. Confirm, and the row moves below the active ones with a muted Retired chip. Click Reactivate on that row — no dialog this time — and it rejoins the active group.
result: pass
section: user-flow
covers: [04-02 D3]

### 8. Client Sees the New Service on the Public Site
expected: Open the landing page at :3000 and browse to services/booking. The service you created in step 4 appears with its image, correct duration and price, and can be selected to start a booking. (This is the first half of the user story's outcome — clients see real services.)
result: pass
section: user-flow
covers: [outcome clause — clients see real services]

### 9. Open Availability and Pick a Stylist
expected: Back in the dashboard, click Availability. A stylist picker appears (pre-selected if there is only one stylist). Selecting a stylist loads that stylist's current weekly hours into the week strip and any existing time off into the month calendar.
result: pass
section: user-flow
covers: [04-04 D1]

### 10. Paint Weekly Hours and Save
expected: Click and drag across a weekday row in the week strip to paint a working-hours block; it snaps to 15-minute boundaries and renders as a gold-dark segment. Click Save Changes. You see an "Availability saved." confirmation and the strip re-loads showing the saved hours.
result: pass
section: user-flow
covers: [04-04 D2, 04-04 D4]

### 11. Add Time Off and Save
expected: Click Add Time Off, then click a start day and an end day in the month calendar to paint a range. The range renders as a dashed muted band and appears in the list below the grid. Click Save Changes and see the success confirmation.
result: pass
first_attempt: issue
reported: "Cannot update a component (`AvailabilityPage`) while rendering a different component (`WeekStripEditor`). To locate the bad setState() call inside `WeekStripEditor`, follow the stack trace as described in https://react.dev/link/setstate-in-render"
severity: major
fixed_by: "04-06 — previewRangeRef-based commit path (G-04-5)"
section: user-flow
covers: [04-04 D3]

### 12. Public Booking Reflects Both Changes
expected: On the landing page booking flow, pick that stylist. The bookable slots match the hours you painted in step 10, and no slots are offered on the days you blocked as time off in step 11. (This is the second half of the outcome — clients book real open slots.)
result: pass
section: user-flow
covers: [04-04 D4, outcome clause — clients book real open slots]

### 13. Conflicting Edit Is Blocked, Not Silently Applied
expected: Book a real appointment through the public site so it is Confirmed. Back in Availability for that stylist, shrink the working hours so the booked slot falls outside them, and click Save Changes. Instead of saving, a rose "Can't Save — Conflicting Appointments" panel appears inline below Save Changes, listing the client name, service, stylist and salon-local time of the booking. Reload the page and confirm the hours were NOT changed. (This is the final clause of the outcome — no edit silently orphans a confirmed booking.)
result: issue
reported: "I am not able to shrik the existing ones, only can add by dragging"
severity: major
section: user-flow
covers: [04-05 D7, outcome clause — no orphaned bookings]

### 14. Non-Owner Staff Cannot Reach Services
expected: Log out and log back in as a Staff (non-Owner) user. The header nav shows Schedule and Availability but no Services link at all (hidden, not greyed out). Typing /services in the address bar redirects you to /schedule. Availability is still fully usable as Staff.
result: [pending]
section: user-flow
covers: [04-02 D1]

<!-- ============ SECTION B — TECHNICAL CHECKS (deferred: only after Section A passes) ============ -->

### 15. Service Form Rejects Invalid Input
expected: Create a service using a slug that already exists, or a duration outside the allowed range. The save fails with a validation message in a banner at the top of the form card, and Save is re-enabled so you can correct and retry rather than being stuck.
result: [pending]
section: technical
covers: [04-02 D4]

### 16. Image Upload Rejects Bad Files, and Replace/Remove Work
expected: Try uploading a .gif and a file over 5MB — each is rejected inline with an error message and nothing uploads. Then Replace an existing image with a different valid one and confirm it swaps, and Remove an image and confirm the row falls back to the placeholder thumbnail.
result: [pending]
section: technical
covers: [04-02 D5]

### 17. Services List Empty and Error States
expected: Stop the API and reload /services — you get an error state, not a blank page or a crash. Restart the API and reload — the list returns. Also confirm a very long service name truncates with an ellipsis and shows the full text on hover.
result: [pending]
section: technical
covers: [04-02 D2]

### 18. Week Strip Gaps and Closed Days
expected: On one weekday, paint two separate blocks with a gap between them (e.g. morning and afternoon). Both render without overlapping and the gap stays empty — that gap is the stylist's break. On another weekday paint nothing and confirm it shows a Closed overlay.
result: [pending]
section: technical
covers: [04-04 D2]

### 19. Time-Off Calendar Band Styling and Empty State
expected: Paint a 3-day time-off range. All three cells show one continuous dashed muted band — never gold, never red. Add a reason and confirm it shows on the band, truncated with the full text on hover. Remove the range and confirm the grid returns to "No time off scheduled." with the month grid still fully rendered.
result: [pending]
section: technical
covers: [04-04 D3]

### 20. Conflict Panel Retry and Clear Behavior
expected: With the conflict panel from step 13 showing, click Save Changes again — the panel stays and Save remains enabled (in-place retry, never disabled). The rose conflict panel is visually distinct from the plain grey/red banner you get from a network or 500 error. Now cancel the conflicting appointment from the Schedule page, return and Save Changes again — the panel clears and you get the success flash. If you can create more than six conflicting bookings, confirm the panel scrolls internally instead of growing without bound.
result: [pending]
section: technical
covers: [04-05 D7]

### 21. Known Stub — Retired Services Disappear After Reload
expected: Retire a service, then reload /services. The retired service is gone from the list entirely and there is no way to find or reactivate it from the UI. This is a documented limitation, not a regression — GET /api/Services only returns active rows and 04-02 was frontend-only, so retire/reactivate only round-trips within a single session. Confirm you accept this as a follow-up (reply "later" / "follow-up") or report it as a real problem if it blocks you.
result: [pending]
section: technical
covers: [04-02 Known Stubs]

<!-- ============ SECTION C — COVERAGE CHECK (goal-backward on the outcome clause) ============ -->

### 22. Coverage — User Story Outcome Delivered
expected: Reading back the phase's user story — "so that clients always see and book real services and open slots, and no availability edit silently orphans a confirmed booking" — all three outcome clauses were observed end to end above: step 8 (clients see real services), step 12 (clients book real open slots), step 13 (no silent orphaning). Confirm nothing in the promised outcome is missing or only half-delivered.
result: [pending]
section: coverage

<!-- ============ AUTO-COVERED BY PASSING TESTS (not presented) ============ -->

### 23. Owner-only gate on ServicesController writes; anonymous/Staff callers rejected, Owner succeeds, public GETs stay anonymous
expected: Owner-only gate on ServicesController writes; anonymous/Staff callers rejected, Owner succeeds, public GETs stay anonymous
result: pass
source: automated
coverage_id: 04-01 D1

### 24. Service image upload endpoint: allowed-type/size success sets a served ImageUrl; disallowed type/oversize rejected 400 before disk write; re-upload replaces the stored file
expected: Service image upload endpoint behavior, proven by ServiceImageUploadTests.cs (6 tests)
result: pass
source: automated
coverage_id: 04-01 D2

### 25. Any authenticated staff can replace a stylist's whole week of working hours; the open-slot query reflects it immediately (adjacency, gap-as-break, empty week, idempotent resubmission)
expected: Working-hours replace reflected through SlotService, proven by WorkingHoursReplaceTests.cs
result: pass
source: automated
coverage_id: 04-03 D1

### 26. Any authenticated staff can add and delete a one-off time-off block; SlotService blocks and unblocks the corresponding slots
expected: Time-off write path reflected through SlotService, proven by TimeOffTests.cs
result: pass
source: automated
coverage_id: 04-03 D2

### 27. Availability write endpoints require authentication (anonymous 401) and are open to any staff role, not Owner-gated
expected: Any-staff authorization gate, proven by WorkingHoursReplaceTests.cs#Put_Anonymous_Returns401 and TimeOffTests.cs#Post_Anonymous_Returns401
result: pass
source: automated
coverage_id: 04-03 D3

### 28. Shrinking hours past a Confirmed appointment is hard-blocked 409 with the conflict list, and no partial apply
expected: Hard block with conflict shape and rollback, proven by ConflictCheckTests.cs#Put_ShrinkingHoursExcludesConfirmedAppointment_Returns409WithConflictShape_AndNoPartialApply
result: pass
source: automated
coverage_id: 04-05 D1

### 29. Adding time off that overlaps a Confirmed appointment is hard-blocked 409 and no time-off row persists
expected: Time-off overlap hard block, proven by ConflictCheckTests.cs#Post_TimeOffOverlapsConfirmedAppointment_Returns409_AndNoPartialApply
result: pass
source: automated
coverage_id: 04-05 D2

### 30. Only Confirmed appointments conflict: Cancelled/NoShow release the slot, Completed is never flagged
expected: Confirmed-only scoping, proven by ConflictCheckTests.cs#Put_AfterCancelOrNoShowReleasesSlot_SameShrinkSucceeds and #Put_CompletedAppointment_NeverAppearsInConflictList_ShrinkSucceeds
result: pass
source: automated
coverage_id: 04-05 D3

### 31. Boundary correctness: a slot ending exactly at the new closing time is allowed; one 15-minute cell past is blocked
expected: Exact boundary behavior, proven by ConflictCheckTests.cs#Put_BoundaryExactlyAtNewClose_IsAllowed and #Put_BoundaryOneCellPastNewClose_IsBlocked
result: pass
source: automated
coverage_id: 04-05 D4

### 32. SalonTimeZone.ToSalonLocal resolves salon-local weekday/time against the fixed UTC+06:30 Asia/Yangon offset, including round-trip and midnight rollover
expected: Local-time conversion correctness, proven by ConflictCheckLocalTimeTests.cs
result: pass
source: automated
coverage_id: 04-05 D5

### 33. Idempotency and empty case: a repeated conflicting save returns the identical conflict set with no partial apply; zero Confirmed appointments always saves cleanly
expected: Idempotency and empty case, proven by ConflictCheckTests.cs#Put_ConflictingSaveRepeatedTwice_ReturnsSameConflictSet_NeverPartiallyApplies and #Put_NoConfirmedAppointments_SucceedsWithNoConflictPanel
result: pass
source: automated
coverage_id: 04-05 D6

## Summary

total: 33
passed: 22
issues: 2
pending: 8
skipped: 0
blocked: 0
resolved_issues: 2

## Gaps

- gap_id: G-04-6
  truth: "Availability lets staff shrink an existing working-hours segment on the week strip, not only add new ones by dragging"
  status: failed
  reason: "User reported: I am not able to shrik the existing ones, only can add by dragging"
  severity: major
  test: 13
  artifacts: []
  missing: []

- gap_id: G-04-3
  truth: "Every service row shows a real image thumbnail; the seeded catalog is not a wall of placeholders"
  status: resolved
  resolved_by: "3475fd2 (inline fix during UAT session)"
  resolved_at: 2026-07-26
  retested: "Test 3 re-run — pass"
  reason: "User reported: images are missing. use the images from zach-hair-studio\\dashboard\\public folder"
  severity: major
  test: 3
  root_cause: "All six seeded Service rows in BookingDbContext.HasData have ImageUrl = null, so RowThumbnail correctly renders its dashed placeholder for every row. The 04-01 image-upload endpoint and static-file root exist and work, but nothing has ever populated ImageUrl — the seed data was never given images. The six JPGs the user added to dashboard/public are not reachable: RowThumbnail resolves src as `${API_BASE_URL}${imageUrl}` (the API origin on :5236), and the landing page serves from its own separate public/ folder on :3000, so a Next.js public/ asset in dashboard/ cannot serve either surface."
  artifacts:
    - path: "API/ZachHairStudio.Shared/Db/BookingDbContext.cs"
      issue: "All 6 seeded Service rows hard-code ImageUrl = null (lines 62, 76, 90, 104, 118, 132)"
    - path: "dashboard/app/services/page.tsx"
      issue: "RowThumbnail is correct — placeholder is the designed null-imageUrl behavior, not a bug"
    - path: ".gitignore"
      issue: "API/ZachHairStudio.Api/wwwroot/ is gitignored (04-01 deviation 4), so seed images cannot simply be committed into the served upload root"
  missing:
    - "A tracked location for seed service images that the API can serve from /uploads/services/ (wwwroot is gitignored and recreated at startup)"
    - "Seed ImageUrl values on the 6 Service rows pointing at those served paths"
    - "A startup copy step (or equivalent) so a cold start reproduces the seeded images on a fresh machine"
  status_note: "Fixed inline during the UAT session (user chose api_seed_assets + fix_now)."
  resolution:
    decision: "Images are tracked in API/ZachHairStudio.Api/SeedAssets/services/ (slug-named), copied into wwwroot/uploads/services/ at startup, with seed ImageUrl pointing at /uploads/services/<slug>.jpg — one source of truth serving both the dashboard and the public landing page."
    changes:
      - "API/ZachHairStudio.Api/SeedAssets/services/*.jpg — 6 images added, renamed to match service slugs"
      - "API/ZachHairStudio.Api/ZachHairStudio.Api.csproj — SeedAssets copied to output so published/test hosts resolve them"
      - "API/ZachHairStudio.Api/Program.cs — startup copy into the upload root, never overwriting an existing file"
      - "API/ZachHairStudio.Shared/Db/BookingDbContext.cs — ImageUrl set on all 6 seeded Service rows"
      - "API/ZachHairStudio.Shared/Migrations/20260726091543_SeedServiceImages.cs — 6 UpdateData calls, no schema change"
    verified:
      - "dotnet test — 157/157 green"
      - "GET /api/Services returns all 6 imageUrl values"
      - "All 6 images serve HTTP 200 as image/jpeg from /uploads/services/"
    follow_up_found: "A TOCTOU race in the first version of the startup copy (File.Exists then File.Copy) broke 3 tests when parallel WebApplicationFactory hosts raced on the same upload root; fixed by catching IOException. Caught by the full suite, which is exactly the class of cold-start bug test 1 exists to surface."

- gap_id: G-04-5
  truth: "Adding time off in the month calendar does not trigger a React setState-during-render error involving the week-strip editor"
  status: resolved
  resolved_by: "04-06-PLAN.md — RED commit db389e5 (ESLint state-updater purity guard), GREEN commit d635e9f (previewRangeRef-based commit path)"
  resolved_at: 2026-07-26
  verification: "Plan 04-06 structural check (handleUp commits via emitChange, no call to the previewRange setter) + npm run lint (clean, guard rule green) + npm run build (passes) — all automated proof recorded in 04-06-SUMMARY.md. Manual browser retest of UAT tests 10/11/18 in one console-open session is still outstanding (flagged as coverage item D3, human_judgment: true)."
  reason: "User reported: Cannot update a component (`AvailabilityPage`) while rendering a different component (`WeekStripEditor`). To locate the bad setState() call inside `WeekStripEditor`, follow the stack trace as described in https://react.dev/link/setstate-in-render"
  severity: major
  test: 11
  debug_session: .planning/debug/resolved/G-04-5-weekstrip-render-setstate.md
  retested: "Test 11 re-run — pass"
  root_cause: "dashboard/components/WeekStripEditor.tsx handleUp() calls emitChange() -> onChange() (AvailabilityPage's setLocalHours) from inside the setPreviewRange functional updater callback. React invokes that updater while WeekStripEditor is still the currentlyRenderingFiber, so the reach-into-parent setState call fires mid-render of a different component, tripping React's render-phase-update guard."
  artifacts:
    - path: "dashboard/components/WeekStripEditor.tsx"
      issue: "handleUp() (~lines 144-156) calls emitChange()/onChange() inside the setPreviewRange(prev => ...) updater body instead of after it, as a side effect of resolving prev via the updater form (used to avoid a stale-closure read of previewRange)."
    - path: "dashboard/app/availability/page.tsx"
      issue: "handleHoursChange (~lines 67-71) is the setLocalHours-owning callback invoked out-of-turn; not itself at fault, just the setter caught mid-render of a different component."
  missing:
    - "Track the live drag range in a ref (updated on every pointermove) instead of relying on the setPreviewRange updater's prev to compute the committed range."
    - "Have handleUp read that ref directly and call emitChange/onChange in its own function body (a legitimate event-handler context), not inside setPreviewRange's updater callback."

- gap_id: G-04-4
  truth: "After creating a service and attaching an image, clicking Save Service persists the edit and gives the Owner clear feedback"
  status: investigating
  reason: "User reported: I fill all input and also upload image. when I click \"Save Service\" button, it does not work"
  severity: major
  test: 4
  evidence_gathered:
    - "API SQL log confirms the user's create (INSERT INTO Services) and image upload (UPDATE Services SET ImageUrl) both persisted."
    - "No subsequent full UPDATE (SET Name, ...) appears for their session — but a FluentValidation 400 short-circuits before any SQL, so this does NOT prove the request never arrived."
    - "Microsoft.AspNetCore log level is Warning in appsettings, so request/response lines (Information) are suppressed — the HTTP status of their PUT is not recoverable from the existing log."
    - "Backend verified working: curl POST create (201) -> POST {id}/image (200) -> PUT {id} with the returned imageUrl (204)."
    - "Browser repro of create-then-Save-Service (image step skipped) issued the PUT and returned 204 with a 'Service saved.' banner; button was enabled, form.checkValidity() true, no JS errors."
  user_observed: "Absolutely nothing changed — no message at all, button still read 'Save Service' (not 'Saving…'). Button looked like a normal solid gold button with a normal pointer cursor, i.e. NOT disabled."
  ruled_out:
    - "Backend failure — curl create(201) -> upload(200) -> PUT(204) all succeed with the exact post-upload payload."
    - "Validation 400 — would render the red error banner via extractErrorMessage; user saw no message."
    - "Successful-but-invisible save — would render the grey 'Service saved.' banner; user saw no message."
    - "Disabled submit button — user confirms it renders solid gold with a normal cursor, not the disabled:opacity-60/cursor-not-allowed styling."
    - "HTML5 native constraint validation blocking submit — browser repro WITH an image attached reported form.checkValidity() true, no invalid events, and no 'invalid form control is not focusable' console message."
    - "401 session expiry — both handleUnauthorized and the client's onResponse middleware hard-navigate to /login, which would be plainly visible."
    - "Environment drift — API :5236, dashboard :3001 and landing :3000 all healthy; API_BASE_URL resolves to http://localhost:5236 with no .env override."
  not_reproducible: "Two independent browser reproductions (with and without the image upload step) both succeeded: submit event fired, PUT issued, 'Service saved.' rendered. The failure appears specific to the user's live page state rather than the code path."
  next_step: "Awaiting a user retry against the now request-logged API (Microsoft.AspNetCore at Information via --Logging:LogLevel:Microsoft.AspNetCore=Information). A retry will show definitively whether any request leaves the browser; DevTools console output would isolate a client-side throw."
  data_outcome: "The user's service DID fully persist: id 7 'Another Service Added' (slug another-service-added) is Active with imageUrl /uploads/services/vkdc4zce.vc1.jpg. Both the create and the image upload committed, so no user work was lost — and since nothing was edited after the upload, the failing 'Save Service' PUT would have been a data no-op anyway. The only real defect in test 4 is therefore the missing feedback / no return to the list, which afc5aae fixes."
  test_data_cleanup: "Removed investigation rows 8-11 (uat-repro-28453, browser-repro-test-2, image-repro, fix-verify) from LocalDB via sqlcmd; the user's id 7 and the 6 seeded rows were left intact."
  ux_hardening_applied:
    commit: afc5aae
    rationale: "The root cause of the silent no-op is still unidentified, but three genuine UX gaps in the same flow were confirmed and fixed. Two of them could each produce the reported symptom, and all three make any future occurrence self-describing rather than silent."
    changes:
      - "Disabled submit button now renders muted, reads 'Fill every field to save', and lists the missing fields — a disabled button can no longer read 'Save Service' and look clickable."
      - "A successful update now closes the form and returns to the list; a create still keeps it open (the new id unlocks image upload) and says so explicitly."
      - "Added noValidate to the form: it contains a visually-hidden file input, and native constraint validation blocks submit with NO message when an invalid control is not focusable — exactly the reported symptom class. Completeness is gated by canSave; all rules are enforced server-side."
    verified:
      - "npx tsc --noEmit — clean"
      - "npm run lint — no ESLint warnings or errors"
      - "Browser verification: disabled button rgb(226,217,200) + cursor not-allowed + 'Fill every field to save'; noValidate true; create keeps form open with the new message; Save Service closes the form and 'Fix Verify' appears in the list with its thumbnail; no JS errors"
