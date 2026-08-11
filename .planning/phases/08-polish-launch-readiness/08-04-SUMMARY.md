---
phase: 08-polish-launch-readiness
plan: 04
subsystem: api
tags: [rate-limiting, launch]

provides:
  - Fixed-window per-IP RateLimiter policies auth (10/min) and checkout (20/min)
  - 429 Too Many Attempts JSON body
affects: [auth, checkout]

key-files:
  created:
    - API/ZachHairStudio.Api.Tests/Features/Launch/RateLimitTests.cs
  modified:
    - API/ZachHairStudio.Api/Program.cs
    - API/ZachHairStudio.Api/Controllers/AuthController.cs
    - API/ZachHairStudio.Api/Controllers/OrdersController.cs

key-decisions:
  - "D-08: Auth + checkout/quote only; not global"

requirements-completed: [LAUNCH-05]
---

# Plan 08-04 Summary — Rate limiting

`AddRateLimiter` / `UseRateLimiter` with named policies. AuthController class-level `auth` policy; checkout + quote use `checkout`. `RateLimitTests` bursts login past the window and expects 429.
