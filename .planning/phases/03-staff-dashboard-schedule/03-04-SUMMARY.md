---
phase: 03-staff-dashboard-schedule
plan: 04
subsystem: ui
tags: [nextjs, openapi-fetch, swr, jwt-bearer, dashboard, staff-login]

requires:
  - phase: 03-staff-dashboard-schedule
    provides: POST /api/auth/login + Owner-only staff-users (03-02); GET/PATCH schedule API (03-03); OpenAPI document on :5236
provides:
  - "dashboard/ Next.js 15 app scaffold (Playfair+Inter, light tool-like surface, salon gold tokens)"
  - "Generated OpenAPI typed client (schema.d.ts + openapi-fetch client with bearer attach + 401 redirect)"
  - "lib/auth.ts localStorage JWT session (get/set/clear, requireAuth, handleUnauthorized)"
  - "Staff login page + protected /schedule stub that redirects unauthenticated callers to /login"
affects: [03-05, 04-staff-service-availability-management]

tech-stack:
  added:
    - swr ^2.4.2 (dashboard)
    - openapi-fetch ^0.17.0 (dashboard)
    - openapi-typescript ^7.13.0 (dashboard, devDep)
  patterns:
    - "Bearer-token staff session in localStorage (D-03); login requests set X-Skip-Auth-Redirect so wrong credentials stay inline"
    - "openapi-fetch createClient<paths> + onRequest attachToken + onResponse 401→handleUnauthorized"
    - "Dashboard mirrors landing-page Next 15 / React 19 / Tailwind 4 pins on a denser light surface (D-15)"

key-files:
  created:
    - dashboard/package.json
    - dashboard/next.config.ts
    - dashboard/tsconfig.json
    - dashboard/postcss.config.mjs
    - dashboard/app/layout.tsx
    - dashboard/app/globals.css
    - dashboard/app/page.tsx
    - dashboard/app/login/page.tsx
    - dashboard/app/schedule/page.tsx
    - dashboard/lib/api/schema.d.ts
    - dashboard/lib/api/client.ts
    - dashboard/lib/auth.ts
  modified:
    - .gitignore

key-decisions:
  - "JWT stored in localStorage under zhs.staff.auth (token/expiresAt/displayName/role) — acceptable for this internal tool; T-03-11 accepted with ~12h lifetime."
  - "Login uses generated path /api/Auth/login (OpenAPI controller-token casing); ASP.NET routing is case-insensitive at runtime."
  - "Login POST sets X-Skip-Auth-Redirect so the global 401 middleware does not bounce wrong-password attempts off the login page."
  - "Safe-resume close-out 2026-07-16: implementation was already committed as 5bde199 without SUMMARY; SUMMARY written after automated re-verify + human login walkthrough approval."

patterns-established:
  - "dashboard/lib/auth.ts is the single session store; client.ts middleware is the only place that attaches Authorization."
  - "Protected pages call requireAuth() on mount; schedule UI body is intentionally stubbed until 03-05."

requirements-completed: [DASH-05]

coverage:
  - id: D1
    description: "dashboard/ scaffolds on the same Next/React/Tailwind/TS pins as landing-page, with swr + openapi-fetch + openapi-typescript, and builds clean."
    requirement: DASH-05
    verification:
      - kind: other
        ref: "cd dashboard && npm run build (2026-07-16 close-out re-verify)"
        status: pass
      - kind: other
        ref: "cd dashboard && npm run lint (2026-07-16 close-out re-verify)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Generated OpenAPI schema includes /api/Auth/login and /api/Schedule paths; client attaches Bearer via onRequest and defaults NEXT_PUBLIC_API_URL to http://localhost:5236."
    requirement: DASH-05
    verification:
      - kind: other
        ref: "dashboard/lib/api/schema.d.ts path entries + dashboard/lib/api/client.ts middleware"
        status: pass
    human_judgment: false
  - id: D3
    description: "Staff login page POSTs credentials, stores JWT on success and redirects to /schedule; wrong credentials show inline error without redirect; no next-auth/iron-session."
    requirement: DASH-05
    verification:
      - kind: other
        ref: "dashboard/app/login/page.tsx + dashboard/lib/auth.ts (code inspection + lint/build)"
        status: pass
      - kind: manual_procedural
        ref: "Human login walkthrough approval, 2026-07-16 (Task 4, 03-04-PLAN.md)"
        status: pass
    human_judgment: true
    rationale: "End-to-end Owner login against the live API and visual D-15 check require a human with seeded credentials."
  - id: D4
    description: "Unauthenticated /schedule redirects to /login; 401 clears the stored token and redirects to /login."
    requirement: DASH-05
    verification:
      - kind: other
        ref: "dashboard/app/schedule/page.tsx requireAuth() + client.ts onResponse 401→handleUnauthorized"
        status: pass
      - kind: manual_procedural
        ref: "Human login walkthrough approval, 2026-07-16 (Task 4, 03-04-PLAN.md)"
        status: pass
    human_judgment: true
    rationale: "Route-guard and session-expiry UX confirmed in the live browser walkthrough."

duration: 5min
completed: 2026-07-16
status: complete
---

# Phase 3 Plan 04: Dashboard Scaffold + Staff Login Summary

**Safe-resume close-out: the staff dashboard Next.js app, typed OpenAPI client, bearer auth, and login/guard flow were already shipped in `5bde199`; this SUMMARY records automated re-verification plus human approval of the login walkthrough.**

## Performance

- **Duration:** ~5 min (close-out only; original implementation on 2026-07-11)
- **Started:** 2026-07-16T08:39:00Z
- **Completed:** 2026-07-16T09:00:00Z
- **Tasks:** 4 (package-legitimacy checkpoint + scaffold/client + auth/login + human login verify)
- **Files modified:** 0 during close-out (verification + docs only)

## Accomplishments

- `dashboard/` mirrors landing-page's Next 15 / React 19 / Tailwind 4 stack on a light, tool-like surface with salon gold/charcoal tokens and Playfair + Inter (D-15).
- Typed client generated from the live OpenAPI document; `openapi-fetch` middleware attaches `Authorization: Bearer` and clears/redirects on 401.
- Staff login stores JWT + expiresAt/displayName/role in `localStorage`, redirects to the protected `/schedule` stub on success, and keeps wrong-password errors inline.
- Human approved the blocking login walkthrough (logged-out redirect, wrong password, Owner login → schedule stub, D-15 surface).

## Task Commits

Implementation was delivered as a single feature commit in the prior session (Tasks 1–3 combined after package-legitimacy approval):

1. **Tasks 1–3: Package legitimacy + scaffold/client + auth/login** - `5bde199` (feat)
2. **Task 4: Human login walkthrough** - no code commit (checkpoint approval 2026-07-16)

**Plan metadata:** commit created below (docs: complete plan)

## Files Created/Modified

- `dashboard/package.json` - Next 15 / React 19 / Tailwind 4 pins + swr/openapi-fetch/openapi-typescript
- `dashboard/app/layout.tsx` / `globals.css` - Staff metadata title + light branded theme tokens
- `dashboard/app/login/page.tsx` - Client login form against `/api/Auth/login`
- `dashboard/app/schedule/page.tsx` - Protected stub until 03-05
- `dashboard/lib/auth.ts` - localStorage session + requireAuth/handleUnauthorized
- `dashboard/lib/api/client.ts` + `schema.d.ts` - openapi-fetch client + generated paths
- `.gitignore` - dashboard build/env ignores

## Decisions Made

- **localStorage JWT session** under `zhs.staff.auth` — D-03 bearer-only; refresh-token hardening deferred to Phase 7/8.
- **`X-Skip-Auth-Redirect` on login** — prevents the global 401 middleware from fighting the inline wrong-password UX.
- **OpenAPI path casing `/api/Auth/login`** — used as generated; runtime routing is case-insensitive.
- **Close-out via safe-resume option 1** — did not re-execute; verified existing `5bde199` artifacts and wrote this SUMMARY after human approval.

## Deviations from Plan

- **Single feature commit instead of per-task commits** — prior executor folded scaffold + auth into `5bde199`. Scope matches the plan; only commit granularity differs.
- **`/schedule` stub page included in 03-04** — plan said the real schedule UI lands in 03-05; the stub + `requireAuth()` guard are required for the Task 4 walkthrough and are in scope.

## Issues Encountered

None during close-out. `next lint` deprecation notice (Next 16 migration) is advisory only.

## User Setup Required

- Dashboard: `NEXT_PUBLIC_API_URL` optional (defaults to `http://localhost:5236`).
- Reuses `Jwt:SigningKey` / `Owner:Email` / `Owner:InitialPassword` user-secrets from 03-01.
- Run dashboard on port 3001: `npm run dev -- -p 3001` (see `dev` skill).

## Next Phase Readiness

- Unblocks **03-05**: day/week schedule UI, detail panel, status actions, polling, and Owner add-staff screen can consume the Wave-2 schedule API behind this login/guard scaffold.

## Known Stubs

- `/schedule` is a placeholder ("Schedule view coming next") until 03-05 delivers the day/week grid and status actions.

---
*Phase: 03-staff-dashboard-schedule*
*Completed: 2026-07-16*

## Self-Check: PASSED

Key files present on disk (`dashboard/package.json`, `login/page.tsx`, `lib/auth.ts`, `lib/api/client.ts`, `lib/api/schema.d.ts`, `schedule/page.tsx`). Feature commit `5bde199` confirmed in `git log`. Build + lint re-verified 2026-07-16.
