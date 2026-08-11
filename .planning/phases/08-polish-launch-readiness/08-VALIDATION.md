# Phase 8 Validation Checklist (LAUNCH-01)

Human spot-check after responsive polish. Mark each item when verified.

## Breakpoints

| Viewport | Width | Routes | Pass criteria | Status |
|----------|-------|--------|---------------|--------|
| Mobile | 375 | `/`, `/book`, `/account/login`, `/account/bookings` | No horizontal page overflow; primary CTAs ≥44px; nav usable | [ ] |
| Mobile | ~400 | `/cart`, `/checkout` | Form fields usable; no clipped buttons | [ ] |
| Tablet | 768 | landing `/schedule` (or week view) | Schedule chips/controls readable; no overflow | [ ] |
| Desktop | 1280 | landing `/schedule`, landing home | Layout stable; no truncated primary actions | [ ] |
| Wide | 1440+ | landing + landing | No layout collapse; content max-width respected | [ ] |

## Touch targets

| Surface | Control | ≥44px | Status |
|---------|---------|-------|--------|
| Landing Navbar | Account / Log In / Cart | [ ] | |
| Landing Book | Continue / Book CTA | [ ] | |
| Account | Cancel / Reschedule / tabs | [ ] | |
| Dashboard | Login submit, schedule chips | [ ] | |

## Design constraints (must remain true)

- [ ] No purple-on-white / indigo gradient theme introduced
- [ ] Landing charcoal/gold tokens preserved
- [ ] Dashboard surface/gold-dark tokens preserved
- [ ] No new hero cards or marketing sections added for polish

## API launch hardening (automated / smoke)

| Check | How | Status |
|-------|-----|--------|
| Production CORS allowlist | `CorsPolicyTests` + set `Cors:Origins` in Prod | [ ] |
| Admin retired | `ZachHairStudio.Admin` absent from solution | [ ] |
| Prod skips Migrate | Code review / start Prod against migrated DB | [ ] |
| JSON logs in Prod | Start with `ASPNETCORE_ENVIRONMENT=Production` and observe console | [ ] |
| Auth/checkout 429 | `RateLimitTests` | [ ] |

## Sign-off

| Role | Date | Notes |
|------|------|-------|
| | | |
