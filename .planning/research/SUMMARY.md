# Project Research Summary

**Project:** Zach Hair Studio
**Domain:** Salon appointment-booking platform (services-led, with supporting product commerce)
**Researched:** 2026-07-07
**Confidence:** MEDIUM

## Executive Summary

Zach Hair Studio is a services-led salon platform: the core value is friction-free slot-based appointment booking, with a curated product catalog layered in afterward as a stylist-recommended extension of the service relationship, not a general storefront. Experts in this space (Fresha, Vagaro, Boulevard, GlossGenius, etc.) converge on the same shape: a service catalog, staff-scoped real-time availability, write-time double-booking prevention, a staff schedule dashboard, and (once commerce exists) atomic stock decrement and server-authoritative checkout. This project's existing brownfield codebase (feature-folder .NET API + dual Next.js frontends) is a good fit for that shape, provided a service layer is introduced between controllers and BookingDbContext now rather than retrofitted later, and the free-text Booking model is replaced by a real Appointment/Availability domain in Phase 2.

The recommended approach layers ASP.NET Core Identity (shared schema, no second auth store) for both staff and future client auth, FluentValidation for business rules, date-fns/DateTimeOffset+IANA-timezone for slot math, and Stripe (direct) for checkout, all additive to the locked base stack, no replacements. Architecturally, availability should be modeled as staff-authored rules (recurring hours + exceptions) with slots computed on read, never materialized, and reconciled against the same Appointments table used for write-time overlap checks.

The dominant risk across all four research files is correctness under concurrency, not features or tooling: double-booking (read-then-write races), overselling stock, and price-tampering at checkout are the three pitfalls most likely to silently ship as looks done and only fail in front of real customers. Timezone/DST handling is the second-order risk, tightly coupled to the same Phase 2 work. Mitigation is consistent: push these invariants to the database (unique constraints, atomic guarded UPDATEs, server-recomputed totals, webhook-driven fulfillment) rather than trusting application-level checks or client input.

## Key Findings

### Recommended Stack

New, additive layers on top of the locked stack (Next.js 15/React 19/.NET 10/EF Core 10/SQL Server): ASP.NET Core Identity for a single shared identity schema (staff cookie-mode, client bearer-mode via a thin next-auth v5 Credentials wrapper later), FluentValidation for server-side business rules, date-fns v4 + @date-fns/tz for display/formatting only (never the source of truth, that is C# DateTimeOffset/TimeZoneInfo), react-hook-form + Zod for forms, openapi-typescript/openapi-fetch (already the project convention, reconfirmed), and Stripe.net for checkout.

**Core technologies:**
- ASP.NET Core Identity (AddIdentityApiEndpoints/MapIdentityApi) - staff + client auth on one schema, same EF Core migration pipeline already in use - avoids a second schema-owning auth system (rules out Better Auth for this project).
- FluentValidation (manual IValidator<T>.ValidateAsync() in the new service layer) - already the documented target in the codebase anti-pattern fix list.
- Stripe.net (server-side only) - Checkout Session creation, webhook verification; lower fees and deeper first-party ASP.NET/Next.js integration than Paddle/Lemon Squeezy for a single-jurisdiction physical-goods retailer.
- date-fns v4 + @date-fns/tz (display-layer only) - paired with server-side DateTimeOffset + a configured salon IANA timezone as the actual source of truth.
- openapi-typescript + openapi-fetch - reconfirms existing convention; no change recommended over NSwag.

### Expected Features

**Must have (table stakes, matches Phases 1-4):**
- Service catalog (list + detail)
- Real-time, staff-scoped slot availability and slot-based booking (service -> slot -> confirm)
- Booking confirmation (on-screen + email)
- Double-booking prevention (server + DB enforced)
- Staff schedule dashboard (day/week view)
- Appointment status lifecycle including a distinct no-show state
- Staff CRUD for services and staff-managed availability

**Should have (competitive differentiators, matches Phases 5-7):**
- Product recommendations tied to the specific service just booked (stylist-recommended add-ons) - the single most on-brand, cheapest-to-build differentiator
- Preferred-stylist selection in the booking flow
- Loyalty groundwork (simple points-per-completed-visit, not a tiered program)
- Book again rebook shortcut once history exists
- Deposit/cancellation policy shown at booking time (flagged as a near-term add-on once Phase 6 payment provider exists)

**Defer (v2+ / explicitly out of scope):**
- Marketplace/third-party discovery, multi-location/franchise management, native mobile apps, client product reviews, subscription billing, full real-time (websocket) dashboard sync, tiered loyalty, general-purpose recommendation engine, no-show fee capture (until deposits are decided).

### Architecture Approach

Extends the existing layered structure (Frontend -> API -> Shared -> Data) with one addition: a Services layer between Controllers and BookingDbContext, introduced starting Phase 1 so it is not copy-pasted around three more times by Phase 6. Availability (staff-authored rules) and Appointments (derived booking transactions) are separate feature folders that share one computed-on-read slot query - Phase 2 needs only a minimal/seeded availability model, Phase 4 makes the same model staff-editable (not a second system). Cart and Order are modeled as two separate tables (ephemeral vs. immutable snapshot), not one status-flagged table.

**Major components:**
1. Controllers (ZachHairStudio.Api) - thin HTTP binding, [Authorize] gating, no direct DbContext access.
2. Feature service layer (new, per-feature *Service.cs) - validation, business rules, transactions, slot computation.
3. BookingDbContext - persistence only, one shared context across features, grows a DbSet per feature.
4. landing-page/ and dashboard/ - separate Next.js apps as two frontends of one API; dashboard/ is the committed staff surface (the scaffolded ZachHairStudio.Admin MVC project is legacy/conflicting and should be treated as retired).

### Critical Pitfalls

1. **Double-booking via read-then-write slot races** - enforce a DB-level unique constraint/exclusion on (StylistId, SlotStart) plus a same-transaction re-check at write time; app-level checks alone are provably insufficient under concurrency.
2. **Timezone/DST corruption of slots and confirmations** - store all appointment/slot data as DateTimeOffset (never bare DateTime), keep one salon IANA timezone as configuration, convert at the display/input boundary only, test across a DST transition date.
3. **Overselling stock at checkout** - decrement stock as a single atomic, guarded UPDATE (WHERE Stock >= @qty) inside the same transaction as order creation; never split check stock and decrement into separate steps.
4. **Trusting client-submitted prices/totals at checkout** - always recompute the authoritative total server-side from the DB catalog by product ID; fulfill orders only from the payment provider signed webhook, never the client redirect.
5. **Auth boundary confusion between public, staff, and later client-account trust levels** - design three explicit trust levels with named policies from Phase 3 onward, not one blanket [Authorize]; client-account endpoints need explicit ownership checks, not just authentication.

## Implications for Roadmap

The existing specs/roadmap.md phase order (P0 foundation -> P1 services -> P2 booking core -> P3 dashboard -> P4 staff mgmt -> P5 products -> P6 cart/checkout -> P7 accounts -> P8 polish) is already well-aligned with research findings and should be preserved as-is. Research primarily sharpens what each phase must get right, not the ordering itself.

### Phase 1: Service catalog (read-only)
**Rationale:** Foundational - every downstream feature (slots, receipts, recommendations) depends on service duration/price existing first.
**Delivers:** Service entity + list/detail API + public browse/detail pages.
**Addresses:** Service catalog, service detail page (table stakes).
**Avoids:** Anti-Pattern 1 (controllers calling DbContext directly) - introduce the service layer here, first, while surface area is small.

### Phase 2: Booking core
**Rationale:** Highest-correctness-risk phase in the whole roadmap - this is where the free-text Booking model is replaced by real Appointment/Availability entities, and where double-booking, timezone, and no-show groundwork decisions get baked into the schema.
**Delivers:** StylistAvailability + AvailabilityException (seeded/minimal, not yet staff-editable) + Appointment entity, open-slot query, slot-based booking with server + DB-level overlap guard.
**Uses:** DateTimeOffset + TimeZoneInfo + a configured salon IANA timezone; a unique DB constraint on (StylistId, StartAt)/overlap check inside the transaction.
**Implements:** Compute-on-read slots, write-time re-validation.
**Avoids:** Double-booking, timezone/DST corruption - both must be solved here; retrofitting DateTime to DateTimeOffset later requires a full data migration.

### Phase 3: Staff dashboard (schedule)
**Rationale:** Staff need to see what booking produced; also the first point where a real staff/public auth boundary is required, even before full auth lands.
**Delivers:** Day/week appointment view, status updates (confirmed/completed/cancelled/no-show).
**Addresses:** Staff schedule dashboard, appointment status lifecycle (table stakes).
**Implements:** Two auth surfaces on one API - scope staff CORS/auth boundary now even if simplistic, so Phase 7 does not require an emergency rewrite.
**Avoids:** Auth boundary confusion - three trust levels (anonymous/client/staff), not one blanket policy; no-show with no linked policy - model no-show as a distinct terminal status with a cutoff-window field, not just an enum value.

### Phase 4: Staff management of services and availability
**Rationale:** Slot logic (Phase 2) is worthless if staff cannot keep availability accurate; this is the staff-editable UI layered onto the same availability model Phase 2 already reads from.
**Delivers:** Dashboard CRUD for services; dashboard CRUD for stylist availability feeding Phase 2 slot query.
**Addresses:** Staff CRUD for services and staff-managed availability (table stakes, closes out MVP per FEATURES.md).
**Avoids:** Availability/booking drift - editing availability that conflicts with an existing confirmed booking must be flagged/blocked, not silently allowed.

### Phase 5: Product catalog (read-only)
**Rationale:** Sequenced after the service experience is solid, per the services-first guiding rule; functionally independent of Phases 1-4 but intentionally deferred.
**Delivers:** Product entity + list/detail API + public browse, surfaced as stylist-recommended add-ons.
**Addresses:** Product catalog (table stakes); groundwork for the stylist-recommended add-ons differentiator (requires both Service and Product catalogs to exist).

### Phase 6: Cart and checkout
**Rationale:** Highest integration risk after Phase 2 - introduces the payment provider and the second class of read-then-write race (stock), plus the price-authority boundary.
**Delivers:** Cart/CartItem (ephemeral) -> Order/OrderItem (immutable snapshot) via Stripe-backed checkout.
**Uses:** Stripe.net, IPaymentProvider interface, idempotency keys, webhook-driven fulfillment.
**Implements:** Cart vs Order separation.
**Avoids:** Overselling stock (atomic guarded UPDATE, same transaction as order creation), trusting client price (server recomputes from catalog, fulfillment only from verified webhook).

### Phase 7: Accounts and retention
**Rationale:** Client accounts only pay off once there is real booking/order data to attach history to; auth is deferred from Phase 3 staff-only scheme to a shared Identity setup here.
**Delivers:** Client accounts (ASP.NET Core Identity bearer mode plus thin next-auth Credentials wrapper), booking/order history, loyalty groundwork (simple points-per-visit).
**Uses:** Same ASP.NET Core Identity schema as Phase 3 staff auth - one identity source of truth, not two.
**Implements:** Client-account policy tier; ownership checks (not just authentication) on all client-account endpoints.
**Avoids:** IDOR - client accounts must not be able to fetch another client bookings/orders by ID. Guest checkout (Phase 6) must not block on this - Order.ClientId stays nullable so it is additive.

### Phase 8: Polish and launch readiness
**Rationale:** Comes last per the roadmap; research does not change this - CORS/rate-limiting hardening (already flagged in CONCERNS.md) should extend explicitly to the checkout endpoint added in Phase 6.

### Phase Ordering Rationale

- Preserve the existing P0-P8 order; research validates the dependency chain (Service catalog -> Availability/Booking -> Dashboard -> Staff-editable availability -> Products -> Checkout -> Accounts -> Polish) rather than suggesting a different one.
- Phase 2 must ship with a minimal/seeded availability model (not full staff CRUD) so it can precede Phase 4 without building two divergent availability systems - this is the one deliberate looks-circular-but-is-sequential dependency called out by both FEATURES.md and ARCHITECTURE.md.
- The service layer (Controllers -> Service -> DbContext) should be introduced starting Phase 1, not deferred, because retrofitting it after Bookings, Services, Availability, Products, and Orders all directly touch BookingDbContext is markedly more expensive than starting clean.
- No-show is modeled as a first-class terminal status starting Phase 3 (not folded into cancelled) because loyalty (Phase 7) and any future deposit/fee logic (post-Phase 6) both key off it - avoiding a schema migration later.
- Auth is introduced in two increments sharing one schema: a lightweight staff gate in Phase 3, hardened into the full ASP.NET Core Identity plus roles setup shared with client accounts in Phase 7 - not two independent auth systems.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2 (Booking core):** Highest-correctness-risk phase in the roadmap - DB-level uniqueness/overlap constraint design, DateTimeOffset/timezone-config strategy, and the exact seeded-availability-model shape all warrant a focused research pass before planning.
- **Phase 6 (Cart and checkout):** Payment provider integration (Stripe direct vs. alternatives), webhook-driven fulfillment, idempotency, and atomic stock-decrement mechanics are the highest external-integration risk in the project.
- **Phase 7 (Accounts and retention):** Auth provider decision is explicitly open in PROJECT.md; the Auth.js v5 / Better Auth organizational situation was flagged as an actively moving target in mid-2026 and should be re-verified right before this phase is planned.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Service catalog):** Straightforward CRUD/read-model, mirrors an already-established feature-folder convention (Bookings folder).
- **Phase 3 (Staff dashboard):** Well-documented dashboard/list-view pattern; only the auth-boundary shape needs deliberate attention, not deep research.
- **Phase 4 (Staff management):** Standard CRUD once the Phase 2 availability model is settled; the drift-check is a known, well-scoped addition.
- **Phase 5 (Product catalog):** Mirrors the Phase 1 read-only catalog pattern directly.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM | Context7-sourced library docs (ASP.NET Core Identity, Auth.js, Stripe, React Hook Form, FluentValidation, openapi-ts) are MEDIUM confidence first-party sources; web-sourced comparisons (date libraries, MoR platforms, Better Auth vs Auth.js) are LOW and flagged individually. |
| Features | MEDIUM | Cross-verified market synthesis across 10+ salon-software vendors (Fresha, Vagaro, Booksy, Boulevard, Zenoti, GlossGenius, etc.); no single vendor treated as authoritative, but all sources are web-based market content, not primary data. |
| Architecture | MEDIUM | Layering/auth-scheme guidance is grounded in official ASP.NET Core/EF Core docs (MEDIUM via Context7); domain-specific entity modeling (Availability/Appointment/Cart/Order split) is synthesized/directional, cross-checked against this project's own roadmap wording (HIGH-confidence project artifacts). |
| Pitfalls | MEDIUM | Core concurrency/timezone/payment pitfalls are corroborated by official EF Core, ASP.NET Core, and Stripe docs plus multiple independent industry post-mortems; no project-specific incident data exists yet (this is a brownfield codebase without production traffic history). |

**Overall confidence:** MEDIUM

### Gaps to Address

- **Payment provider final decision (Phase 6):** Stripe direct is recommended, but PROJECT.md lists this as an explicit open decision - confirm before Phase 6 planning, and re-check Stripe.net/API version pinning at implementation time.
- **Auth provider/session strategy (Phase 7):** ASP.NET Core Identity (shared schema) is recommended over Better Auth or a fully Auth.js-owned store, but the Auth.js/Better Auth organizational landscape was explicitly flagged as unstable as of mid-2026 - re-verify immediately before Phase 7 planning.
- **ZachHairStudio.Admin scaffolded MVC project:** Conflicts with the dashboard/ decision already logged in PROJECT.md. Treat as legacy/retire; this should be an explicit decision recorded before or during Phase 3, not left ambiguous.
- **Deposit/no-show-fee enforcement timing:** Deferred to if/when decided, but retrofitting it after Phase 2/3 (booking) and Phase 6 (payments) are built independently is costly - the roadmap should at least capture cutoff-window/policy data in Phase 3 even if monetary enforcement is not wired up until later.
- **Guest checkout vs. accounts ordering:** Phase 6 (checkout) precedes Phase 7 (accounts) in the roadmap; confirmed workable only if Order.ClientId is nullable from the start - this must be an explicit modeling decision in Phase 6, not discovered during Phase 7.

## Sources

### Primary (HIGH confidence)
- .planning/codebase/ARCHITECTURE.md, .planning/codebase/STRUCTURE.md, .planning/codebase/CONCERNS.md - ground truth for this repo's current state and known issues.
- .planning/PROJECT.md, specs/roadmap.md, specs/mission.md - ground truth for project scope, priorities, and phase order.

### Secondary (MEDIUM confidence)
- Context7: /dotnet/aspnetcore (Identity, multi-scheme auth), /nextauthjs/next-auth (Auth.js v5), /stripe/stripe-node (Checkout/webhooks), /react-hook-form/documentation, /fluentvalidation/fluentvalidation, /websites/openapi-ts_dev.
- EF Core docs (concurrency/rowversion handling), ASP.NET Core docs (Web API layering, scheme-based authorization) - official docs via Context7.
- Stripe official docs (docs.stripe.com: finalize payments server-side, security guide, server-side integration).
- Cross-verified salon-software market research (Fresha, Vagaro, Booksy, Boulevard, Zenoti, GlossGenius, Mindbody, Mangomint, Square, StyleSeat, Setmore, Salonist, SalonBiz, Studioloop).
- Industry post-mortems on double-booking/inventory race conditions (ITNEXT, Medium, DZone, Sylius GitHub issue, amitavroy.com).

### Tertiary (LOW confidence, flagged for re-verification)
- Web search: NSwag vs. openapi-typescript comparison.
- Web search: date-fns vs. Luxon vs. Temporal comparison.
- Web search: Stripe vs. Paddle vs. Lemon Squeezy pricing/MoR framing.
- Web search: Better Auth vs. Auth.js v5 2026 organizational status - actively moving situation, re-verify before Phase 7.

---
*Research completed: 2026-07-07*
*Ready for roadmap: yes*
