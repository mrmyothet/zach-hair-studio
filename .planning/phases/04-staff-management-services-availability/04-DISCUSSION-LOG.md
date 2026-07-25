# Phase 4: Staff Management (Services & Availability) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-24
**Phase:** 4-Staff Management (Services & Availability)
**Areas discussed:** Service CRUD surface, Availability editor shape, Conflict handling on save, Who edits whose schedule

---

## Service CRUD surface

### Who can manage services?

| Option | Description | Selected |
|--------|-------------|----------|
| Owner only | Catalog changes stay with owner | ✓ |
| Any authenticated staff | Front desk can fix prices/durations | |
| You decide | Claude picks fitting Owner/Staff split | |

**User's choice:** Owner only
**Notes:** Aligns with Phase 3 Owner-only add-staff pattern.

### How does retire work?

| Option | Description | Selected |
|--------|-------------|----------|
| Soft-retire via IsActive | Hidden from public; kept for history | ✓ |
| Hard delete | Remove when unused; block if referenced | |
| Soft-retire + optional purge later | Soft now; hard delete future | |

**User's choice:** Soft-retire via `IsActive = false`
**Notes:** Matches Phase 1 Service model design.

### Service images?

| Option | Description | Selected |
|--------|-------------|----------|
| Paste/URL only | Path or URL into ImageUrl | |
| File upload | Dashboard upload; API stores and sets ImageUrl | ✓ |
| Skip images this phase | No ImageUrl field in form | |

**User's choice:** File upload
**Notes:** Delivers Phase 1 deferred image-management promise.

### Changing duration/price on a live service?

| Option | Description | Selected |
|--------|-------------|----------|
| Edit freely | No block/warn | ✓ |
| Warn when future appointments exist | Allow save with count | |
| Block duration changes if future Confirmed | Price still free | |

**User's choice:** Edit freely
**Notes:** Appointments do not snapshot price/name; slot cells stay as booked. Live Service join updates displayed price/name.

---

## Availability editor shape

### Weekly hours UI?

| Option | Description | Selected |
|--------|-------------|----------|
| Form rows per weekday | Mon–Sun start/end | |
| Visual week strip | Drag/select hour ranges | ✓ |
| Copy-from template | Preset then tweak | |

**User's choice:** Visual week strip

### How do breaks work?

| Option | Description | Selected |
|--------|-------------|----------|
| Breaks = short StylistTimeOff | No new table | ✓ (refined by next Q) |
| Split working-hour segments | Gap between segments | |
| New Break entity | First-class recurring breaks | |

**User's choice:** Short time-off concept, then refined: recurring lunch = gaps in hours

### Recurring lunch vs one-off time off?

| Option | Description | Selected |
|--------|-------------|----------|
| One-off only + gaps for recurring | No recurring TimeOff model | ✓ |
| Extend model for recurring time-off | Weekly break patterns | |
| You decide | Researcher picks lightest approach | |

**User's choice:** One-off/date-range time off only; recurring midday = gaps in week-strip hours

### Time-off entry UX?

| Option | Description | Selected |
|--------|-------------|----------|
| List + form | Upcoming list + date range form | |
| Calendar overlay | Paint blocked ranges on calendar | ✓ |
| From schedule page | Block time on day grid | |

**User's choice:** Calendar overlay

---

## Conflict handling on save

### When availability edit overlaps booking?

| Option | Description | Selected |
|--------|-------------|----------|
| Hard block | Refuse save until conflicts handled | ✓ |
| Warn + override | Owner can force-save | |
| Partial apply | Save non-overlapping slices only | |

**User's choice:** Hard block

### Which appointments count as conflicts?

| Option | Description | Selected |
|--------|-------------|----------|
| Confirmed only | Active bookings owning slots | ✓ |
| Confirmed + Completed | Also flag historical overlap | |
| Any non-cancelled holding slots | Equivalent to Confirmed today | |

**User's choice:** Confirmed only

### How are conflicts shown?

| Option | Description | Selected |
|--------|-------------|----------|
| Inline conflict list | Name, service, stylist, time | ✓ |
| List + deep link | Rows link into schedule | |
| Summary count only | Count only | |

**User's choice:** Inline conflict list

### Which edits run conflict check?

| Option | Description | Selected |
|--------|-------------|----------|
| Both hours and time off | Shrink hours + add time off | ✓ |
| Time off only | | |
| Hours only | | |

**User's choice:** Both

---

## Who edits whose schedule

### Who can edit stylist availability?

| Option | Description | Selected |
|--------|-------------|----------|
| Owner only | Same as services | |
| Any staff any stylist | Front desk manages everyone | ✓ |
| Staff self / Owner anyone | Needs Staff↔Stylist link | |

**User's choice:** Any authenticated staff can edit any stylist

### Stylist roster in this phase?

| Option | Description | Selected |
|--------|-------------|----------|
| Picker only | Existing active stylists | ✓ |
| Owner activate/deactivate | Soft-retire IsActive | |
| Full stylist CRUD | Create/edit name/slug | |

**User's choice:** Picker only

### Include stylist↔service capability matrix?

| Option | Description | Selected |
|--------|-------------|----------|
| Defer | All stylists do all services | ✓ |
| Include now | Capability join + slot filter | |
| You decide | | |

**User's choice:** Defer (backlog)

### Dashboard navigation?

| Option | Description | Selected |
|--------|-------------|----------|
| Two nav items | Services (Owner) + Availability (all staff) | ✓ |
| One Manage hub | Tabs | |
| You decide | | |

**User's choice:** Two nav items

---

## Claude's Discretion

- Image storage backend, MIME/size limits
- Week-strip interaction details and multi-range → row mapping
- Conflict HTTP status / ProblemDetails shape and look-ahead window
- Exact authz attributes and OpenAPI client regen details
- Empty/loading/error chrome consistent with Phase 3 dashboard

## Deferred Ideas

- Stylist↔service capability matrix (Phase 2 D-08; deferred again)
- Stylist create/retire UI
- Owner override on conflicting availability (explicitly rejected)
