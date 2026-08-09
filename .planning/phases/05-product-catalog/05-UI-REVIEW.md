# Phase 5 — UI Review

**Audited:** 2026-08-09
**Baseline:** 05-UI-SPEC.md (approved design contract)
**Screenshots:** captured (desktop 1440x900, tablet 768x1024, mobile 375x812 of `/products`; desktop of `/services`) via headless Chrome at `.planning/ui-reviews/05-20260809/`. Note: API was down during capture, so `/products/[slug]` and `/services/[slug]` returned 404 (warm-cache expiry, ISR 60s revalidate). Visual verification of those two routes is DOM- and class-level only, not pixel-level.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 4/4 | Every CTA/empty/error string matches the contract verbatim |
| 2. Visuals | 3/4 | Icon-only "Z" fallback is `aria-hidden` and never decorated with a tooltip; async load renders no intermediate state (whole-page blank until RSC resolves) |
| 3. Color | 4/4 | Gold confined to the 5 declared accent roles; zero hardcoded colors; 60/30/10 split preserved |
| 4. Typography | 4/4 | Exactly the declared 5 sizes + 3 weights (400/600/700); no third weight introduced |
| 5. Spacing | 3/4 | 4px scale honored; but card grid renders 12 identical `p-7` cards with 0 `line-clamp` — two-seed-product cards fall back to the "Z" monogram while 10 siblings show 16:9 imagery, so card heights collide in a row |
| 6. Experience Design | 3/4 | Empty state, 404, and stock states are contract-covered; but there is no `loading.tsx`/`error.tsx`/`ErrorBoundary` anywhere in the app and no `line-clamp-3` (the spec's own backstop) |

**Overall: 21/24**

---

## Top 3 Priority Fixes

1. **Card grid row alignment when only some products have images** — 10 of 12 seeded products render an `aspect-video` image; the 2 without render a `w-14 h-14` Z-monogram, so the two image-less cards are ~200px shorter than their row siblings and their internal spacing collapses against the fixed-height grid. User impact: misaligned grid row on the catalog page, the exact defect the spec's 🧪 backstop predicted. Concrete fix: add `line-clamp-3` to the card description `p` (both `ProductCard` in `app/products/page.tsx:71` and `RecommendedProductCard` in `app/services/[slug]/page.tsx:148`) and give the image slot a fixed `aspect-video` container even when the image is absent (e.g. render an empty `aspect-video w-full rounded-xl mb-5 bg-white/5` placeholder instead of the short Z badge, or clamp `shortDescription` to a fixed line count so 12 cards stay equal height).
2. **No loading or error boundary for the async detail routes** — `/products/[slug]`, `/services/[slug]`, and `/products` are async RSC pages with no `loading.tsx`, `error.tsx`, or `ErrorBoundary` anywhere under `landing-page/app/`. On a cold start / ISR revalidate miss, the page blocks (blank) until the fetch resolves, and any unexpected render-time exception surfaces Next.js's default error overlay. User impact: perceived blank page; degraded failure mode that hides the nice empty-state copy. Concrete fix: add `landing-page/app/products/loading.tsx` + `error.tsx` (and `services/loading.tsx` + `error.tsx`) using the existing `bg-charcoal`/`border-white/5` skeleton-panel pattern; cheap, no new dependency.
3. **Gold gradient in `<p>` is non-selectable/copy-paste-invisible text** — the Recommended Products heading highlight relies on `SectionHeading`'s `gold-gradient` background-clip (inherited from Phase 1, `globals.css:30`). The gold-highlight "Products" span is invisible when the page is text-selected or rendered with `background-clip: text` unsupported. User impact: low (decorative), but the style is inherited from the baseline, not new this phase — defer unless the catalog header is being touched anyway. Concrete fix if addressed: add `select-text` or a `text-gold` fallback on the span; otherwise accept as a Phase-1 legacy inherited defect.

---

## Detailed Findings

### Pillar 1: Copywriting (4/4)

Every copy string in the phase's new UI matches the UI-SPEC Copywriting Contract verbatim:

- Empty-state heading "Products Are Being Curated" and body "Our product recommendations are temporarily unavailable. Please check back soon, or ask your stylist during your next visit." — exact match, `app/products/page.tsx:111-118`.
- Catalog eyebrow/title: "Stylist Picks" / "Recommended" + gold "Products" with subtitle matching the contract, `app/products/page.tsx:102-107`.
- Out of Stock badge: "Out of Stock" — exact, `app/products/page.tsx:80`, `app/products/[slug]/page.tsx:81`, `app/services/[slug]/page.tsx:156`.
- Recommended Products section eyebrow/heading: "Stylist Picks for This Service" / "Recommended" + "Products", `app/services/[slug]/page.tsx:99-103`.
- Detail breadcrumb: "&larr; Back to products" mirrors "Back to services", `app/products/[slug]/page.tsx:37`.
- `subtitle` passed as `""` on the recommendations heading (contract-declared pattern) — no stray text renders, `app/services/[slug]/page.tsx:104`.
- Grep for generic strings ("Submit", "Click Here", "OK", "No data", "went wrong", "try again") across the three phase files: zero hits.

No defects found. The section-omission behavior (zero recommended products renders no heading, no empty box) is implemented as `service.recommendedProducts && ...length > 0` and matches the contract, `app/services/[slug]/page.tsx:97`.

### Pillar 2: Visuals (3/4)

- Clear focal hierarchy per page: catalog title via `SectionHeading` (eyebrow → serif display → gold-highlight), then category `h2` groups, then cards with eyebrow → name → description → price/badge footer. Consistent with `/services` baseline.
- Card is a whole-card `<Link>` with hover lift (`card-hover` in `globals.css:37-44`) and `hover:border-gold/30`; the card itself is the affordance, matching `ServiceCard`.
- The "Z" monogram fallback badge (no `imageUrl`) carries `aria-hidden="true"` on its text (`app/products/page.tsx:60`), so screen readers skip it; category eyebrow and product name are the accessible text. Good.
- WARNING: the Z badge is effectively an icon-only decorative element with no tooltip, and it appears alongside real 16:9 product imagery. Because no seeded product has an image, this phase's own catalog shows 12 monograms — visually uniform but short — while the designed 16:9-image variant is also possible in the same grid (see Pillar 5). Mixed grids will look inconsistent.
- WARNING: no `loading.tsx`/`error.tsx` (see Pillar 6) — the visual experience during async fetch and on failure is a blank page, not a skeleton.

### Pillar 3: Color (4/4)

- Zero hardcoded hex/rgb values in the three phase files (grep: none). All color through `@theme` tokens or Tailwind utilities.
- Gold confined to the contract's declared accent roles:
  - Category eyebrows / "Back to products" link / card price / aside price — `text-gold` (12 instances).
  - `border-gold/20` on the detail aside (matches the inherited ServiceDetailPage aside, `app/products/[slug]/page.tsx:62`, `app/services/[slug]/page.tsx:65`).
  - `hover:border-gold/30` card hover (2 instances).
  - `bg-gold/10`, `bg-gold/20` — the "Z" monogram tile, exact reuse of `ServiceCard`'s badge pattern (`app/products/page.tsx:59`, `app/services/[slug]/page.tsx:136`).
  - `bg-gold-dark` on the aside "Book This Service" button — inherited Phase-1 element, unchanged this phase.
- The one instance of solid `bg-gold` (the Book button) is a Phase-1 inherited element, not a new misuse.
- Out of Stock badge uses the exact contract classes `bg-white/5 border border-white/10 text-gray-400 text-xs uppercase tracking-wider px-3 py-1 rounded-full` — muted neutral, not destructive red. Verified in rendered HTML (2 badges present on `/products`).
- Dominant/secondary split: cards `bg-charcoal`, main wrapper `bg-charcoal-light`, section headings white — 60/30/10 preserved.

### Pillar 4: Typography (4/4)

- Sizes in the three phase files: `text-xs`, `text-sm`, `text-lg`, `text-xl`, `text-2xl`, `text-3xl`, `text-4xl`, `text-6xl`. All eight are declared contract roles (Label 12px / Body 14px / card title 18px / section sub-label 20px / display 36px + 48-60px). `text-3xl` and `text-6xl` appear only in inherited elements (category `h2` from the `/services` group header pattern; the detail `h1` `md:text-6xl` from `ServiceDetailPage`'s exact class). No new size introduced.
- Weights: `font-semibold` (5), `font-bold` (5), `font-medium` (2), `font-serif` (5). Contract allows 400 (default) + 600 (titles/prices). `font-bold` (700) and `font-medium` (500) are inherited baseline elements (`text-gold font-bold` price treatment, `text-gray-400 font-medium` stock value) — no third *new* weight introduced by this phase.
- Display headings use `font-serif` (Playfair) consistently via `SectionHeading` and `h1`s; body/labels `font-sans` (default). Matches the Font contract.

### Pillar 5: Spacing (3/4)

- All values are multiples of 4 and match the declared scale: `p-7` card padding (lg 24px, matches `ServiceCard`), `gap-6` grids (lg), `gap-8` detail columns (xl 32px), `space-y-5` dl rows (lg), `mb-6`/`mb-8` in-detail gaps, `py-16` section padding (3xl 64px), `pt-32` main offset, `px-6` container. The only bracketed arbitrary value is `lg:grid-cols-[1fr_320px]`, which the contract itself declares ("mirrors `app/services/[slug]/page.tsx`'s two-column `grid lg:grid-cols-[1fr_320px] gap-8` layout").
- WARNING (backstop failure): the spec's 🧪 backstop — "verify at execution that a very long `shortDescription` doesn't break card grid alignment (add `line-clamp-3` if needed)" — was not resolved. The card description uses `flex-1` (`app/products/page.tsx:71`) but no `line-clamp`. With 12 cards in a row, any product whose `shortDescription` wraps to a different line count than its neighbors creates unequal card heights within a fixed-height grid. Combined with the image/monogram asymmetry below, rows can visibly misalign.
- WARNING (layout asymmetry): `ProductCard` renders either a 16:9 `Image` (`aspect-video w-full ... mb-5`) or a `w-14 h-14` monogram tile. 10/12 seeded products have `imageUrl`; 2 do not. In a 3-col grid these two cards are ~170-200px shorter than their siblings, so the footer row (price/badge) sits higher within the same grid cell. This is a real visual defect for the seeded catalog, not hypothetical.
- Touch targets: every new interactive element is a full-card Link with `p-7` padding, ≥44px tall — contract satisfied.

### Pillar 6: Experience Design (3/4)

Contract-covered states — all verified in code:

- Empty state (zero products): renders the "Products Are Being Curated" box, `app/products/page.tsx:109-119`.
- Out-of-stock (zero-value): `stock === 0` shows the neutral badge, card stays browsable, `app/products/page.tsx:78-82`.
- Recommended-products none-mapped: section omitted entirely, `app/services/[slug]/page.tsx:97-111`.
- Fetch failure: catch-to-empty/null mirrors `lib/services.ts`, `lib/products.ts:36-38,51-53` — failures render the same empty-state copy, no separate error UI (contract-declared).
- Unknown slug: `notFound()` on `!product`, `app/products/[slug]/page.tsx:23-25` (verified: `/products/does-not-exist` returns 404).
- No destructive actions this phase; no disabled/confirmation states needed.

Deficits:

- No `loading.tsx`, `error.tsx`, or `ErrorBoundary` anywhere under `landing-page/app/` (glob search: none). Async RSC routes block on a blank page during fetch/revalidate and have no graceful render-failure path. This predates Phase 5 (Phase-1 baseline has the same gap), so it is scored as inherited debt, but the spec's UI Considerations table did not cover it.
- No `line-clamp` resolution for long `shortDescription` (the spec's own flagged backstop) — see Pillar 5.
- No disabled state needed, but there is also no in-flight affordance (spinner/skeleton) during the `fetchProducts`/`fetchProductBySlug` server-side wait — consistent with the Phase-1 baseline.

---

## Registry Safety

`components.json` absent in `landing-page/` — shadcn not initialized. No third-party registries in play (UI-SPEC Registry Safety table: none). All phase components are hand-written Tailwind + semantic HTML. Skip: no Registry Safety flags.

---

## Files Audited

- `landing-page/app/products/page.tsx` — catalog page, local `ProductCard`, `groupProductsByCategory`
- `landing-page/app/products/[slug]/page.tsx` — detail page, `notFound()` guard
- `landing-page/app/services/[slug]/page.tsx` — Recommended Products section + `RecommendedProductCard`
- `landing-page/lib/products.ts` — `ProductSchema`, `fetchProducts`/`fetchProductBySlug`
- `landing-page/lib/services.ts` — `ServiceSchema.recommendedProducts` optional field
- `landing-page/lib/data.ts` — `navLinks` "Products" entry
- `landing-page/app/services/page.tsx` — Phase-1 baseline reference
- `landing-page/components/SectionHeading.tsx` — shared heading component
- `landing-page/app/globals.css` — `@theme` tokens, `gold-gradient`, `card-hover`
- Rendered HTML: `/products` (desktop/mobile/tablet), `/services`, `/products/leave-in-repair-serum`, `/products/does-not-exist`

Screenshots: `.planning/ui-reviews/05-20260809/products-desktop.png`, `products-mobile.png`, `products-tablet.png`, `services-desktop.png` (git-ignored).
