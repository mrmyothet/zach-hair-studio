---
name: ef-migrations
description: Add and apply EF Core migrations against BookingDbContext, including the one-time switch off EnsureCreated() so migrations own the schema.
---

# ef-migrations — manage the database schema

Manages EF Core 10 migrations for `BookingDbContext`
(`API/ZachHairStudio.Shared/Db/BookingDbContext.cs`), with the API project
(`API/ZachHairStudio.Api`) as the startup project.

## Prerequisites (check first)

- The `dotnet-ef` tool must match EF Core **10**. The environment currently has
  9.0.15, which is too old. Update once:
  `dotnet tool update --global dotnet-ef --version "10.*"`
  (or add a local tool manifest: `dotnet new tool-manifest` +
  `dotnet tool install dotnet-ef --version "10.*"`).

## One-time: adopt migrations

The API currently builds the schema with `db.Database.EnsureCreated()` in
`API/ZachHairStudio.Api/Program.cs`. Migrations and `EnsureCreated()` don't mix.
Before the first migration:

1. Replace `db.Database.EnsureCreated();` with `db.Database.Migrate();`.
2. Delete any existing `Data/bookings.db` created by `EnsureCreated()` (dev data
   only) so the initial migration applies cleanly.

## Add a migration

From the repo root (Shared holds the context, Api is the startup project):

```
dotnet ef migrations add <Name> \
  --project API/ZachHairStudio.Shared \
  --startup-project API/ZachHairStudio.Api
```

## Apply migrations

```
dotnet ef database update \
  --project API/ZachHairStudio.Shared \
  --startup-project API/ZachHairStudio.Api
```

(`dotnet run` also applies them automatically once `Program.cs` uses
`Migrate()`.)

## Verify

- A `Migrations/` folder appears under `API/ZachHairStudio.Shared`.
- `dotnet ef database update` succeeds and `bookings.db` has the new schema
  (inspect with the `sqlite` MCP server).
