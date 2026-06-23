---
name: openapi-client
description: Regenerate a typed TypeScript API client for the Next.js frontends from the .NET OpenAPI document, keeping OpenAPI as the source of truth.
---

# openapi-client — generate the TS API client

The .NET API exposes an OpenAPI document; the frontends should consume a typed
client generated from it rather than hand-written, drifting `fetch` calls.

## Source of truth

- OpenAPI doc (dev only): `http://localhost:5236/openapi/v1.json`
- The API must be running (use the **dev** skill) before generating.

## Generate

Use a generator already common in the Next.js ecosystem, e.g.
[`openapi-typescript`](https://github.com/openapi-ts/openapi-typescript) for
types + a thin fetch client:

```
# from landing-page/
npx -y openapi-typescript http://localhost:5236/openapi/v1.json \
  -o lib/api/schema.d.ts
```

Add a small typed fetch wrapper in `landing-page/lib/api/client.ts` (e.g. using
`openapi-fetch`) that points at the API base URL. Repeat for `dashboard/` once
it exists.

## Conventions

- Output lives under `lib/api/` in each frontend.
- Treat generated files as build artifacts — regenerate, don't hand-edit.
- Drive the base URL from an env var (e.g. `NEXT_PUBLIC_API_BASE_URL`,
  defaulting to `http://localhost:5236`).

## Verify

- `lib/api/schema.d.ts` regenerates without errors and `tsc`/`next build`
  passes.
- A page using the generated client compiles and fetches real data.
