---
phase: quick-260731-uj3
plan: 01
subsystem: ui
tags: [react, nextjs, tailwind, chat-widget, client-component]

requires: []
provides:
  - "Mock booking-assistant chat engine (landing-page/lib/chat.ts) with a single stable async seam for a future real backend"
  - "ChatWidget client component (launcher bubble + panel + message list + input) mounted on the homepage"
  - "Three new icons (ChatBubbleIcon, SendIcon, CloseIcon) in landing-page/components/icons.tsx"
affects: [landing-page-ui, future-ai-chat-backend]

tech-stack:
  added: []
  patterns:
    - "Module-scope sub-components above a client component's default export (mirrors Navbar.tsx's Logo() pattern) to avoid remount-on-keystroke bugs from nested component definitions"
    - "Single async function (sendChatMessage) as the sole seam between a UI component and a future real backend — swap the function body, not the component"
    - "Regex-anchored link renderer that only allows site-relative `/path` hrefs from generated/LLM text, never dangerouslySetInnerHTML"

key-files:
  created:
    - landing-page/lib/chat.ts
    - landing-page/components/ChatWidget.tsx
  modified:
    - landing-page/components/icons.tsx
    - landing-page/app/page.tsx

key-decisions:
  - "sendChatMessage(userText, history, services) is the only function a real backend swap touches; history is accepted but unused by the mock to keep the future request payload shape-compatible"
  - "Launcher uses right-24 (not right-6) specifically to stay clear of BackToTop's right-6/z-40, with the panel at z-50"
  - "Deterministic incrementing counter for ChatMessage ids instead of crypto.randomUUID()/Date.now() to avoid SSR/hydration id mismatches"

patterns-established:
  - "Pattern: link markup in assistant-authored text must match `\\[([^\\]]+)\\]\\((\\/[^)\\s]*)\\)` before being rendered as a next/link Link — any other href shape stays inert plain text"

requirements-completed: [QUICK-260731-UJ3]

coverage:
  - id: D1
    description: "Mock chat engine (lib/chat.ts) exporting ChatMessage/ChatRole types, STARTER_PROMPTS, createMessage, and sendChatMessage with a keyword-matching cascade (service name, book/appointment, price, hours, catalog, fallback) driven by the live Service catalog, degrading gracefully when services is empty"
    requirement: "QUICK-260731-UJ3"
    verification:
      - kind: unit
        ref: "cd landing-page && npx tsc --noEmit (exit 0) + grep checks for exported symbols"
        status: pass
    human_judgment: false
  - id: D2
    description: "ChatBubbleIcon/SendIcon/CloseIcon added to icons.tsx following the file's existing props-spread pattern"
    requirement: "QUICK-260731-UJ3"
    verification:
      - kind: unit
        ref: "grep -c 'export function ChatBubbleIcon|export function SendIcon|export function CloseIcon' components/icons.tsx (= 3)"
        status: pass
    human_judgment: false
  - id: D3
    description: "ChatWidget client component: launcher bubble + dialog panel with starter chips, typing indicator, Escape-to-close with focus return to launcher, aria-live message list, and a regex-anchored link renderer restricted to site-relative hrefs"
    requirement: "QUICK-260731-UJ3"
    verification:
      - kind: unit
        ref: "cd landing-page && npx tsc --noEmit (exit 0) + grep checks for \"use client\", aria-live=\"polite\", aria-expanded, right-24"
        status: pass
      - kind: automated_ui
        ref: "SSR HTML fetch of http://localhost:3001 confirms launcher renders with aria-label=\"Open booking assistant\", aria-expanded=\"false\", and right-24 class server-side"
        status: pass
    human_judgment: true
    rationale: "Interactive behaviors (chip click send flow, typing indicator timing, Enter/Escape key handling, focus return, mobile resize, and real-service-name keyword matching against live catalog data) require a browser session or browser-automation tool. No such tool was available in this execution environment, and the API's Azure SQL backend was unreachable in this session (pre-existing firewall block, see Issues Encountered) so the mock's real-catalog branch could not be exercised against live data. Code was reviewed line-by-line against the plan's exact spec (regex, class strings, event wiring, guard conditions) and the automated checks above passed, but a human should do the 7-point browser walkthrough from Task 3's human-check before considering the interactive UX fully proven."
  - id: D4
    description: "ChatWidget mounted on the homepage after BackToTop, reusing the already-fetched full services array with no second fetchServices() call"
    requirement: "QUICK-260731-UJ3"
    verification:
      - kind: unit
        ref: "grep -c 'ChatWidget services={services}' app/page.tsx (= 1) + grep -c 'from \"@/components/ChatWidget\"' app/page.tsx (= 1)"
        status: pass
    human_judgment: false

duration: ~20min
completed: 2026-07-31
status: complete
---

# Phase quick-260731-uj3: Add an AI Chat Widget UI to the Landing Page Summary

**Mock keyword-matched booking-assistant chat widget (gold launcher + dialog panel) mounted on the homepage, with a single async function as the entire seam for a future real chat backend.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-31T15:13:27Z
- **Tasks:** 3
- **Files modified:** 4 (2 created, 2 edited)

## Accomplishments
- Built `landing-page/lib/chat.ts`: a pure-TypeScript mock chat engine (no React import) exporting `ChatMessage`/`ChatRole` types, `STARTER_PROMPTS`, a deterministic `createMessage`, and `sendChatMessage` — a matching cascade (service name → book/appointment → price → hours → catalog → fallback) built entirely from the public `Service` catalog, with graceful degradation when the catalog is empty.
- Added `ChatBubbleIcon`, `SendIcon`, `CloseIcon` to `landing-page/components/icons.tsx` following the file's existing `SVGProps<SVGSVGElement>` + spread pattern.
- Built `landing-page/components/ChatWidget.tsx`: a `"use client"` component with a floating launcher (`right-24`, clear of `BackToTop`'s `right-6`) and a dialog panel with starter chips, send-on-Enter/chip/button, a typing indicator, Escape-to-close with focus return, an `aria-live="polite"` message list, and a link renderer that only ever turns `[label](/relative-path)` into a real `next/link` href — anything else stays inert text.
- Mounted `<ChatWidget services={services} />` on `landing-page/app/page.tsx` immediately after `<BackToTop />`, reusing the page's single `fetchServices()` call.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the mock chat engine at landing-page/lib/chat.ts** - `e973cbf` (feat)
2. **Task 2: Add three icons and build the ChatWidget client component** - `9a7d4a2` (feat)
3. **Task 3: Mount ChatWidget on the homepage and verify end to end** - `0747757` (feat)

_Note: `SUMMARY.md`/`STATE.md` metadata commit is created separately by the orchestrator per this execution's constraints._

## Files Created/Modified
- `landing-page/lib/chat.ts` - Mock chat engine: types, starter prompts, id factory, and the keyword-matched `sendChatMessage` seam
- `landing-page/components/icons.tsx` - Added `ChatBubbleIcon`, `SendIcon`, `CloseIcon`
- `landing-page/components/ChatWidget.tsx` - New client component: launcher, dialog panel, message list, input row, module-scope `MessageBubble`/`TypingIndicator`/`StarterChips`/link renderer
- `landing-page/app/page.tsx` - Imports and renders `<ChatWidget services={services} />` after `<BackToTop />`

## Decisions Made
- `sendChatMessage(userText, history, services): Promise<string>` is the sole seam a real backend needs to replace — its signature and return type stay identical, so no `ChatWidget` code changes when a real API is dropped in.
- Message ids come from a module-scoped incrementing counter (not `crypto.randomUUID()`/`Date.now()`) to avoid any SSR/hydration id mismatch on this client-only chat surface.
- The launcher's `right-24` offset (vs. `BackToTop`'s `right-6`) is a deliberate, load-bearing spacing choice — do not change it without also checking `BackToTop.tsx`.

## Deviations from Plan

None - plan executed exactly as written.

One incidental fix (not a plan deviation, no code change): the first `npx tsc --noEmit` run in Task 1 triggered an unrelated npm/lockfile normalization side effect on `landing-page/package-lock.json` (removal of some `libc` fields from optional-dependency entries). This was reverted with `git checkout -- landing-page/package-lock.json` before staging, per the plan's explicit requirement that `package.json`/`package-lock.json` stay untouched. No task file changes were needed to address this.

## Issues Encountered
- **API/database unreachable in this session (environment, not code):** `dotnet run` for `API/ZachHairStudio.Api` failed with `Login failed... Client with IP address '...' is not allowed to access the server` against `zachhairstudio.database.windows.net` (the connection string configured in this project's `dotnet user-secrets`, per the existing STATE.md note that Azure SQL firewall must allow the client IP). This is a pre-existing, documented environment limitation unrelated to this task's files, and out of scope to fix here (requires Azure Portal firewall access). As a result, the frontend dev server (`landing-page`, started on port 3001 since 3000 was already occupied by a separate stale process) served the homepage with `services = []` (the existing `fetchServices()` fail-soft behavior), which is a valid and already-covered code path (the mock's empty-catalog guard) but meant the "real seeded service name" branch of Task 3's human-check could not be exercised against live data in this run.
- **No browser-automation tool available in this execution environment:** the 7-point interactive browser walkthrough specified in Task 3's `<human-check>` (chip click → typing indicator → reply, Enter-to-send, Escape-to-close-and-refocus, mobile resize) was verified by code review against the plan's exact spec plus SSR-HTML structural checks (launcher's `aria-label`, `aria-expanded`, `right-24` class all present in the server-rendered markup), but was not exercised via live clicks/keystrokes. See `## Known Stubs` / coverage `D3` above for what remains for a human/browser-tool pass.

## Known Stubs

None. No hardcoded empty/placeholder UI values were introduced — the empty-services-array behavior is an explicitly designed and spec'd degradation path (Task 1's action explicitly requires branches a/c/e to "degrade to a sensible reply" rather than an empty stub), not a stub standing in for missing functionality.

## Threat Flags

None — this plan's `<threat_model>` (T-UJ3-01 through T-UJ3-SC, T-UJ3-04) already covers the only security-relevant surface introduced (the link renderer's regex-anchored href, the mock engine's public-catalog-only data, and the zero-new-dependency constraint), and no additional surface was introduced beyond what's modeled there.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The chat widget UI is complete and structurally verified (type-check clean, SSR markup correct, all automated grep checks pass).
- Recommended before considering this fully done: a human (or a session with a browser-automation tool) should run the 7-point walkthrough in Task 3's `<human-check>` against `http://localhost:3001` (or 3000 once the stale process there is cleared) with the API's Azure SQL firewall opened for the test IP, to confirm the real-service-name matching branch and all interactive timing/focus behaviors.
- No blockers for a future real chat backend: swapping `sendChatMessage`'s body for a `fetch` call is the only change required.

---
*Phase: quick-260731-uj3*
*Completed: 2026-07-31*

## Self-Check: PASSED

All created/modified files found on disk (`landing-page/lib/chat.ts`, `landing-page/components/ChatWidget.tsx`, `landing-page/components/icons.tsx`, `landing-page/app/page.tsx`, this SUMMARY.md). All three task commits (`e973cbf`, `9a7d4a2`, `0747757`) found in git log.
