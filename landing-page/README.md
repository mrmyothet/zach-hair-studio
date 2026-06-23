# Zach Hair Studio — Landing Page

The Zach Hair Studio landing page, built with [Next.js](https://nextjs.org)
(App Router), TypeScript, and Tailwind CSS v4.

## Getting started

```bash
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

## Scripts

- `npm run dev` — start the dev server
- `npm run build` — production build
- `npm start` — serve the production build
- `npm run lint` — run ESLint

## Structure

```
app/
  layout.tsx      Root layout + metadata
  page.tsx        Page composition
  globals.css     Tailwind import, theme tokens, custom styles
components/        Section components (Navbar, Hero, Services, …)
lib/data.ts        Content data (services, gallery, team, reviews, branches)
public/            Images (logo + gallery photos)
```

Content lives in `lib/data.ts`, so copy and pricing can be edited without
touching markup. The theme colors (`gold`, `charcoal`) are defined as Tailwind
tokens in `app/globals.css`.
