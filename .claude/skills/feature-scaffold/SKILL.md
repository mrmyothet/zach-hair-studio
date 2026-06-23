---
name: feature-scaffold
description: Scaffold a new backend feature mirroring the Features/Bookings pattern (entity, DTOs, mappers, DbSet, controller) plus a starter Next.js page.
---

# feature-scaffold — add a new feature end to end

Creates a new feature following the established convention. Use the existing
**Bookings** feature as the canonical template — read those files first and
mirror their structure, naming, and namespaces.

## Backend template (`API/ZachHairStudio.Shared/Features/Bookings/`)

For a new feature `Xyz`, create `API/ZachHairStudio.Shared/Features/Xyz/`:

| File | Mirrors | Purpose |
|---|---|---|
| `Xyz.cs` | `Booking.cs` | EF entity (the persisted model). |
| `XyzCreateDto.cs` | `BookingCreateDto.cs` | Inbound create payload. |
| `XyzResponseDto.cs` | `BookingResponseDto.cs` | Outbound shape. |
| `XyzStatus.cs` | `BookingStatus.cs` | Enum, only if the feature has states. |
| `XyzExtensions.cs` | `BookingExtensions.cs` | Entity ⇄ DTO mapping helpers. |

Then:
1. Add `public DbSet<Xyz> Xyzs => Set<Xyz>();` to `BookingDbContext.cs` and any
   `OnModelCreating` property config (max lengths, enum-as-string conversion —
   follow the Booking config).
2. Add `API/ZachHairStudio.Api/Controllers/XyzsController.cs`, mirroring
   `BookingsController.cs` (routing, DI of `BookingDbContext`, async actions).
3. Create the migration with the **ef-migrations** skill.

## Frontend starter

Add a route under `landing-page/app/<xyz>/page.tsx` (a Server Component that
lists items). Once the typed client exists, prefer the **openapi-client** skill
output over hand-written fetches.

## Verify

- Solution builds: `dotnet build API/ZachHairStudio.slnx`.
- New endpoints appear in `http://localhost:5236/openapi/v1.json`.
- The new page renders and reads from the API.
