# Feature Research

**Domain:** Salon appointment-booking platform (services-led, with supporting product commerce)
**Researched:** 2026-07-07
**Confidence:** MEDIUM (cross-verified market-research synthesis from Fresha, Vagaro, Booksy, Square Appointments, Boulevard, Mindbody, GlossGenius, Zenoti, Phorest, Mangomint; no single vendor source treated as authoritative — see Sources)

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = product feels incomplete or broken relative to any competing salon (including the phone-and-paper-book status quo this platform is replacing).

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Service catalog (name, description, duration, price) | Clients need to know what they're booking and what it costs before committing | LOW | Phase 1. Read-only list + detail is enough for v1; duration and price are the two fields every downstream feature (slots, receipts) depends on. |
| Service detail page | Clients research a specific service (what's included, how long, price) before booking, especially first-time clients | LOW | Phase 1. Can be a simple templated page; imagery matters more than functionality here. |
| Real-time availability / open-slot lookup | Clients expect to see actual bookable times, not a "we'll call you back" form — this is the #1 gap in the current free-text booking form | MEDIUM | Phase 2. Must reflect staff working hours, existing bookings, and service duration simultaneously — the single hardest piece of the whole roadmap. |
| Slot-based booking (pick service → pick slot → confirm) | Directly the "Core Value" from PROJECT.md — friction-free reservation, not a request-and-wait | MEDIUM | Phase 2. Depends on availability model existing first. |
| Booking confirmation (on-screen + email/SMS) | Clients need proof the slot is theirs, not just submitted | LOW-MEDIUM | Phase 2. Email is sufficient for v1; SMS is a fast-follow, not blocking. |
| Automated appointment reminders | Reduces no-shows industry-wide; absence is a top client complaint ("no one reminded me") | LOW-MEDIUM | Requires a scheduled job/notification service; can ship after Phase 2 core booking works, doesn't have to be day-one. |
| Double-booking prevention | A stylist double-booked is the single fastest way to lose trust in "effortless booking" | MEDIUM | Enforced server-side at booking-creation time against the same availability model that powers slot lookup. |
| Staff schedule dashboard (day/week view) | Staff need one screen to see "what's my day" — replaces the paper book | MEDIUM | Phase 3. This is the internal mirror of the client-facing booking flow. |
| Appointment status lifecycle (confirmed → completed / cancelled / no-show) | Staff need to track what actually happened, not just what was booked, for accountability and reporting | LOW-MEDIUM | Phase 3. Already partially validated in the existing codebase (Pending→Confirmed→Completed→Cancelled) — extend with a distinct "No-show" terminal state, since it behaves differently downstream (no-show fee logic, retention signal) than a client-initiated cancellation. |
| Staff CRUD for services | Owner/staff must be able to add a new color technique or retire a discontinued one without a code change | LOW-MEDIUM | Phase 4. Straightforward CRUD once the Phase 1 catalog schema is settled. |
| Staff-managed availability (hours, breaks, time off) | Slot logic is worthless if staff can't keep it accurate as their week changes | MEDIUM | Phase 4. Feeds directly into the Phase 2 slot-query engine — this is a hard two-way dependency (see Feature Dependencies). |
| Product catalog (name, description, price, image, stock) | Baseline expectation once a "shop" exists at all | LOW | Phase 5. Read-only, mirrors the service catalog pattern already established. |
| Cart | Standard e-commerce expectation; anything less feels broken | LOW-MEDIUM | Phase 6. Client-side cart is fine for a single-salon catalog size; no need for a distributed cart service. |
| Checkout with a real payment provider | Clients won't complete a purchase without a trusted, standard checkout (Stripe/Square-class UX) | MEDIUM-HIGH | Phase 6. Provider choice is an explicit open decision in PROJECT.md; this is the single highest-integration-risk item in the roadmap. |
| Stock decrement on order | Prevents overselling a physical product; expected the moment stock is modeled at all | LOW | Phase 6. Simple transactional decrement; watch for race conditions on concurrent checkout (see PITFALLS). |
| Client accounts (auth, profile) | Once booking/order history is promised, clients expect a place to log in and see it | MEDIUM | Phase 7. Auth provider is an explicit open decision in PROJECT.md. |
| Booking & order history per client | Table stakes the moment accounts exist — "why do I have an account if I can't see my past visits" | LOW-MEDIUM | Phase 7. Mostly a query/view over data that already exists from Phases 2 and 6. |
| Cancellation / rescheduling by the client | Clients expect self-service change of plans, not "call the salon" | LOW-MEDIUM | Natural extension of Phase 2/7; not explicitly named in the roadmap phases but strongly expected once accounts exist — flag as a near-term add-on to Phase 7. |

### Differentiators (Competitive Advantage)

Features that set the product apart from a generic booking form, and align with the "services-led, products as stylist-recommended extension" positioning in `specs/mission.md`.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Product recommendations tied to the specific service just booked/completed ("stylist-recommended add-ons") | Directly reflects the mission's core differentiation: products reinforce the service relationship rather than being a separate storefront. Cross-industry data shows service-linked upsell prompts lift revenue 10-30% and CLV ~20%. | LOW-MEDIUM | Phase 5/6. Implement as a simple mapping (service → recommended product ids) surfaced on the service detail page and at checkout — not a generic recommendation engine. This is the single most on-brand differentiator available and is cheap to build. |
| Deposit / cancellation policy shown at time of booking | Protects revenue on high-value color/chemical services and sets expectations up front, which is explicitly called out by multiple vendors as reducing disputes | LOW-MEDIUM | Not in current roadmap phases — worth flagging as a Phase 2 or Phase 7 add-on once payment provider (Phase 6) exists, since deposits require the same payment integration. |
| Loyalty / rewards (points-per-visit, punch-card, or tier) tied to booking + order history | Turns the accounts phase from "just a login" into a retention lever; PROJECT.md already names "loyalty/rewards groundwork" as part of Phase 7 | MEDIUM | Phase 7 as scoped ("groundwork") — start with the simplest model (e.g., points-per-completed-appointment, redeemable as a discount) rather than a full tiered program; tiering can come later once there's usage data. |
| Preferred-stylist selection in the booking flow | Personal relationship with "my stylist" is a strong retention driver in the salon industry specifically (more so than most service verticals) | LOW-MEDIUM | Natural extension of Phase 2 slot selection (filter slots by staff member first, then time) — cheap to add once availability is staff-scoped, which it already needs to be. |
| Real-time dashboard status updates across staff/front-desk | Nice-to-have polish on top of the Phase 3 dashboard; matters more as staff count grows | LOW-MEDIUM | Single-salon, likely small staff count — full real-time push (SignalR/websockets) is probably over-engineering for v1; a page refresh / short polling interval is enough. Revisit if staff report stale views in practice. |
| "Book again" / rebook-last-service shortcut from booking history | Reduces friction for repeat clients, directly serving the "effortless booking" core value for the platform's most valuable segment (returning clients) | LOW | Cheap add-on once Phase 7 history exists; high leverage for retention. |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but conflict with the stated scope, priorities, or the services-first sequencing in PROJECT.md/roadmap.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|------------------|-------------|
| Marketplace / third-party seller listings (à la Fresha/Booksy/Vagaro's own client-acquisition marketplaces) | These are the biggest visible differentiator among SaaS vendors, and it's tempting to copy "what the big platforms do" | Explicitly out of scope in PROJECT.md — this is a single salon's own storefront, not a multi-tenant marketplace; building marketplace-style discovery adds enormous scope for zero benefit to one salon | Rely on the salon's own marketing/SEO/social presence for discovery; the platform only needs to convert visitors who already found the salon |
| Multi-location scheduling / franchise management | Vendors sell this as a headline feature and it looks "enterprise-ready" | Explicitly out of scope; adds a location dimension to every model (services, staff, availability, inventory) that this salon doesn't need and that would ripple through every phase | Single-location assumption baked into schema; revisit only if the business actually opens a second location |
| Native mobile apps | Competing platforms all have iOS/Android apps and it feels like table stakes at first glance | Explicitly out of scope — a responsive web app covers mobile; native apps roughly double the maintenance surface (two more codebases, app-store review cycles) for a single-location business | Responsive, mobile-first web design in `landing-page/`/`dashboard/`; PWA-style "add to home screen" if app-like feel is wanted later |
| Client-facing product reviews / ratings | Common on e-commerce sites and looks like an obvious retention/trust feature | Explicitly out of scope for v1 — introduces moderation burden, spam/abuse surface, and a whole new data model, for a catalog that's secondary to services anyway | Let staff verbally recommend products in-person (already the salon's natural sales channel); revisit post-v1 if product sales become significant |
| Subscription/membership billing for recurring services | Looks like a natural loyalty upgrade ("subscribe and save") | Explicitly out of scope for v1 and adds recurring-billing complexity (proration, failed payments, cancellation windows) on top of an already-deferred one-time payment provider decision | Ship simple points/discount loyalty (Phase 7 "groundwork") first; reconsider subscriptions only after checkout and loyalty basics are proven |
| Full real-time (websocket) sync across every dashboard view | Big platforms (Boulevard, Zenoti) advertise real-time scheduling as a headline feature, and it's tempting to build it because "that's what real booking software does" | For a single small salon's staff dashboard, the complexity of a live-sync layer (connection management, conflict resolution) is disproportionate to the actual concurrency (a handful of staff, not hundreds) | Server-driven booking creation with conflict checks at write time, plus simple polling/refresh on the dashboard; add push-based sync only if staff actually report stale-data problems |
| General-purpose recommendation engine for products | "Amazon-style" recommendations sound impressive and are a common ask once a product catalog exists | Massive overkill for a small, curated product catalog tied to a handful of services — no meaningful data volume to train on, and it works against the "stylist-recommended" framing that's the actual differentiator | Simple, explicit service→product mapping curated by staff/owner (see Differentiators row above) |

## Feature Dependencies

```
Service catalog (Phase 1)
    └──requires──> (nothing upstream; foundational)

Staff availability model (Phase 4, staff-managed)
    └──requires──> Staff/stylist identity existing (implicit in Phase 3/4)

Open-slot query (Phase 2)
    └──requires──> Service catalog (needs service duration)
    └──requires──> Staff availability model (needs working hours/time off)
       [NOTE: Phase 2 ships before Phase 4 in the roadmap, so Phase 2 needs
        at least a minimal/seeded availability model to query against —
        the full staff-editable CRUD comes later in Phase 4. This is a
        real ordering tension to flag for the roadmap/planning phase.]

Slot-based booking (Phase 2)
    └──requires──> Open-slot query
    └──requires──> Double-booking prevention (write-time conflict check)

Booking confirmation + reminders
    └──requires──> Slot-based booking
    └──enhances──> Slot-based booking (reduces no-shows, builds trust)

Staff schedule dashboard (Phase 3)
    └──requires──> Slot-based booking (needs real appointments to display)

Appointment status lifecycle (confirmed/completed/cancelled/no-show)
    └──requires──> Staff schedule dashboard (status changes happen there)
    └──enhances──> Loyalty/retention (completed-appointment count drives points)
    └──enhances──> Deposit/cancellation-fee policy (no-show triggers fee capture)

Staff CRUD for services (Phase 4)
    └──requires──> Service catalog schema (Phase 1)

Staff-managed availability (Phase 4)
    └──requires──> Open-slot query existing to feed (Phase 2)
    [circular-looking but sequential: Phase 2 reads a minimal availability
     model; Phase 4 makes that model staff-editable]

Product catalog (Phase 5)
    └──requires──> (nothing upstream functionally, but sequenced after
                    services per the services-first priority)

Product recommendations tied to service ("stylist-recommended add-ons")
    └──requires──> Service catalog (Phase 1) AND Product catalog (Phase 5)
    └──enhances──> Cart & checkout (surfaces at the point of purchase)

Cart & checkout (Phase 6)
    └──requires──> Product catalog (needs price/stock)
    └──requires──> Payment provider decision (explicit open decision)

Stock decrement
    └──requires──> Checkout (order creation triggers decrement)

Client accounts (Phase 7)
    └──requires──> (nothing upstream functionally, but needs an auth
                    provider decision — explicit open decision)

Booking & order history per client
    └──requires──> Client accounts (Phase 7)
    └──requires──> Slot-based booking (Phase 2, source of booking records)
    └──requires──> Checkout (Phase 6, source of order records)

Loyalty / rewards groundwork
    └──requires──> Client accounts (need an identity to attach points to)
    └──requires──> Appointment status lifecycle (completed = point-earning event)
    └──requires──> Booking & order history (surfaces balance/progress)

Deposit / cancellation policy ──requires──> Payment provider (Phase 6)
Preferred-stylist selection ──enhances──> Slot-based booking (Phase 2)
"Book again" shortcut ──requires──> Booking & order history (Phase 7)

Marketplace/discovery ──conflicts──> Single-salon scope (Out of Scope)
Multi-location management ──conflicts──> Single-location schema assumption (Out of Scope)
```

### Dependency Notes

- **Open-slot query requires Staff availability model, but the roadmap ships slot-querying in Phase 2 and staff-editable availability in Phase 4:** this is the most important sequencing detail for the roadmap to get right. Phase 2 needs *some* availability data to query against (even if seeded/hardcoded per stylist) before Phase 4 gives staff a CRUD UI to manage it. Treat Phase 2's availability model as "data model + minimal seed/admin path" and Phase 4 as "staff self-service UI on top of the same model" — do not model them as two separate availability systems.
- **Appointment status lifecycle enhances both Loyalty and Deposit/cancellation-fee logic:** a "completed" status is the trigger for awarding loyalty points; a "no-show" status (distinct from "cancelled") is the trigger for capturing a no-show fee. Modeling no-show as its own terminal state (not folded into "cancelled") now, even before deposits/loyalty are built, avoids a schema migration later.
- **Product recommendations enhance Cart & checkout:** the differentiator only pays off if it's surfaced at the moment of highest intent — on the service detail page and again at checkout — not buried in a separate "shop" tab disconnected from the booking flow.
- **Marketplace/discovery conflicts with the single-salon scope:** any feature request that reads like "help clients discover us" (vs. "help clients who found us book/buy") is out of scope by definition; route that need to marketing/SEO, not the product.

## MVP Definition

### Launch With (v1)

Minimum viable product — matches Phases 1-4 of the existing roadmap, which is itself already scoped tightly around the stated Core Value.

- [ ] Service catalog (list + detail) — nothing else works without it
- [ ] Real-time availability + slot-based booking (service → slot → confirm) — this *is* the Core Value
- [ ] Booking confirmation (on-screen + email) — clients need proof of the reservation
- [ ] Double-booking prevention — a single double-booked slot destroys trust in the whole system
- [ ] Staff schedule dashboard (day/week view) — staff need to see what booking actually produced
- [ ] Appointment status lifecycle (confirmed/completed/cancelled/no-show) — required for the dashboard to be useful, not just a viewer
- [ ] Staff CRUD for services + staff-managed availability — without this, every service/schedule change needs a code deploy, which is unsustainable past week one

### Add After Validation (v1.x)

Features to add once the core booking loop (Phases 1-4) is proven working end to end — matches Phases 5-7.

- [ ] Automated reminders (email/SMS) — add once bookings are flowing and no-shows become a measurable problem
- [ ] Product catalog + stylist-recommended add-ons — add once the service/booking experience is solid, per the explicit services-first sequencing
- [ ] Cart & checkout with payment provider — add once the product catalog exists and is worth transacting on
- [ ] Client accounts + booking/order history — add once there's enough repeat-visit volume to make an account valuable
- [ ] Loyalty groundwork (simple points-per-visit) — add once accounts exist and there's a base of returning clients to retain
- [ ] Preferred-stylist selection — cheap add-on to slot selection once staff-scoped availability (Phase 4) exists
- [ ] Client self-service cancel/reschedule — natural companion to accounts (Phase 7)

### Future Consideration (v2+)

Features to defer until the core services-led + supporting-commerce product has product-market fit.

- [ ] Deposit / no-show-fee capture — defer until a payment provider exists (Phase 6) and no-shows are demonstrated to be a real revenue problem, not a hypothetical one
- [ ] Tiered loyalty program (beyond simple points) — defer until points-per-visit data shows what tiers would actually reward
- [ ] "Book again" rebook shortcut — defer until booking history (Phase 7) has real repeat-client data to act on
- [ ] Push-based real-time dashboard sync — defer indefinitely unless staff actually report stale-data problems with a small team
- [ ] Any marketplace/multi-location/native-app work — permanently out of scope per PROJECT.md unless the business itself changes shape

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|----------------------|----------|
| Service catalog (list + detail) | HIGH | LOW | P1 |
| Availability model + slot query | HIGH | MEDIUM | P1 |
| Slot-based booking + confirmation | HIGH | MEDIUM | P1 |
| Double-booking prevention | HIGH | MEDIUM | P1 |
| Staff schedule dashboard | HIGH | MEDIUM | P1 |
| Appointment status lifecycle (incl. no-show) | HIGH | LOW-MEDIUM | P1 |
| Staff CRUD services + availability | HIGH | MEDIUM | P1 |
| Automated reminders | MEDIUM | LOW-MEDIUM | P2 |
| Product catalog | MEDIUM | LOW | P2 |
| Stylist-recommended product add-ons | MEDIUM-HIGH | LOW-MEDIUM | P2 |
| Cart & checkout + payment provider | MEDIUM | MEDIUM-HIGH | P2 |
| Client accounts + auth | MEDIUM | MEDIUM | P2 |
| Booking & order history | MEDIUM | LOW-MEDIUM | P2 |
| Loyalty groundwork (points) | MEDIUM | MEDIUM | P2 |
| Preferred-stylist selection | MEDIUM | LOW-MEDIUM | P3 |
| Deposit / no-show fee | LOW-MEDIUM | MEDIUM | P3 |
| "Book again" shortcut | LOW-MEDIUM | LOW | P3 |
| Real-time dashboard push sync | LOW | MEDIUM-HIGH | P3 |
| Marketplace/discovery, multi-location, native apps | N/A (out of scope) | HIGH | Not planned |

**Priority key:**
- P1: Must have for launch (Phases 1-4 of the current roadmap)
- P2: Should have, add when possible (Phases 5-7)
- P3: Nice to have, future consideration (post-v1 polish, gated on real usage signals)

## Competitor Feature Analysis

| Feature | Fresha / Vagaro / Booksy (SaaS multi-tenant) | Boulevard / Zenoti (premium salon suites) | Our Approach |
|---------|-----------------------------------------------|---------------------------------------------|--------------|
| Service catalog & booking | Rich, but generalized across thousands of salons; often cluttered with categories not relevant to a single salon | Similar, tuned for high-end multi-location chains | Keep it simple and curated: one salon's actual service list, styled to match the brand — no generic category taxonomy |
| Slot availability | Real-time, staff-scoped, accounts for buffer time and variable service duration | "Precision Scheduling" — intelligently packs appointments to minimize gaps | Match the baseline (staff-scoped real-time slots with service-duration awareness); defer intelligent auto-packing as premature optimization for a single small salon |
| Client acquisition / marketplace | Core value prop — clients discover the salon through the platform's own directory (with a commission model, e.g. Fresha's ~20% new-client fee) | Less marketplace-driven, more chain-internal | Explicitly not building this — out of scope; discovery is the salon's own marketing responsibility |
| Product upsell | Present but generic (POS-integrated upsell prompts across any product) | Present, chain-wide inventory-aware upsell | Narrower and more intentional: curated service→product mapping framed as "your stylist recommends," matching the mission's positioning rather than a general storefront |
| Loyalty | Points, tiers, punch cards, referral rewards — full programs, often a paid add-on module | Full CRM-grade loyalty and membership tiers | Start minimal (points-per-completed-visit, redeemable discount) as explicit "groundwork," not a full program — matches PROJECT.md's Phase 7 scoping |
| Staff dashboard | Full CRM: notes, client history, marketing tools, reporting | Enterprise-grade multi-location reporting | Focused schedule + status dashboard only — no CRM/marketing/reporting layer unless a future milestone calls for it |

## Sources

- [Best Salon Booking Software 2026: Top 10 Compared | Zenoti](https://www.zenoti.com/thecheckin/best-salon-booking-software)
- [The Best Salon Software for Small Businesses: Feature Integration Matrix | Booksy](https://biz.booksy.com/en-us/blog/how-to-find-the-best-salon-software-for-small-businesses-features-to-consider)
- [Best Salon Software 2026: The Ultimate Comparison Guide | Fresha](https://www.fresha.com/for-business/salon/best-salon-software)
- [Best Salon Booking & Scheduling Software (2026) | Studioloop](https://www.studioloop.app/best-salon-software)
- [Salon booking software: 9 apps for booking and payments | GlossGenius](https://glossgenius.com/blog/appointment-booking-apps)
- [Booking + Payment Software Solutions for Salons | Booksy](https://biz.booksy.com/en-us/blog/booking-payment-software-solutions-for-salons)
- [Vagaro vs Booksy: Which Platform Powers Your Business Better? | GoodCall](https://www.goodcall.com/appointment-scheduling-software/vagaro-vs-booksy)
- [Booking and Scheduling Software for Salons | Mindbody](https://www.mindbodyonline.com/business/education/blog/booking-scheduling-salon)
- [Hair Salon Scheduling Software | Salonist](https://salonist.io/hair-salon-scheduling-software)
- [Salon Booking System Software | SalonBiz](https://salonbizsoftware.com/product/scheduling-and-appointment-book/)
- [Salon Management Software & Appointment Book | Zenoti](https://www.zenoti.com/salon-management-software)
- [Boost Salon's Revenue with Upselling & Cross-Selling | Reservio](https://www.reservio.com/blog/tips/what-is-upselling-cross-selling-and-how-to-use-them-in-your-salon)
- [7 ways to upsell retail and services in your salon | Kitomba](https://www.kitomba.com/blog/7-ideas-to-help-you-successfully-upsell/)
- [How to unlock upsell potential to boost salon and spa revenue | Zenoti](https://www.zenoti.com/thecheckin/how-to-unlock-upsell-potential-strategies-to-boost-salon-and-spa-revenue)
- [Tips For Upselling Your Salon Services | Square](https://squareup.com/us/en/the-bottom-line/selling-anywhere/tips-for-upselling-your-salon-services)
- [The Ultimate Guide to Creating a Salon Loyalty Program | Booksy](https://biz.booksy.com/en-us/blog/the-ultimate-guide-to-creating-a-salon-loyalty-program-that-keeps-clients-coming-back)
- [10 salon loyalty program examples to boost client retention | Zenoti](https://www.zenoti.com/thecheckin/salon-loyalty-programs)
- [Guide To Creating a Salon Loyalty Program | StyleSeat](https://www.styleseat.com/blog/salon-loyalty-program/)
- [Salon Loyalty Programs: Examples, Ideas & How to Start | Vagaro](https://www.vagaro.com/learn/customer-loyalty-programs-for-small-businesses-salons)
- [Salon Booking Software | Management & POS | Vagaro](https://www.vagaro.com/pro/salon-software)
- [Give your scheduling a makeover with free salon booking software | Setmore](https://www.setmore.com/industries/salon)
- [Salon Booking and Cancellation Policy Templates and Examples | Square](https://squareup.com/us/en/the-bottom-line/operating-your-business/salon-booking-cancellation-policy-templates)
- [Salon Policies 101: No-Shows, Cancellations & Payment Rules | Vagaro](https://www.vagaro.com/learn/policies-procedures-for-clients-in-salons-examples)
- [Salon Cancellation Policy Guide | Mangomint](https://www.mangomint.com/blog/salon-cancellation-policy-guide/)
- Internal: `.planning/PROJECT.md`, `specs/mission.md`, `specs/roadmap.md` (in-repo scope, priorities, and phase order — not external market research, used to frame what applies)

---
*Feature research for: salon appointment-booking + supporting product commerce platform*
*Researched: 2026-07-07*
