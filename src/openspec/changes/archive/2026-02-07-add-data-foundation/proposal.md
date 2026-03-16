# Change: Add EF Core Data Foundation Skeleton

## Why
The product application requires a clean persistence layer for business/domain data (Projects, Steps) separate from the AOS engine workspace artifacts. This establishes the foundational data infrastructure using EF Core with SQLite for development.

## What Changes
- **ADDED** `nirmataDbContext` with Project and Step entities
- **ADDED** Entity configurations with proper relationships (Project → Steps one-to-many with cascade delete)
- **ADDED** EF Core migrations baseline (InitialCreate)
- **ADDED** SQLite database configuration with lazy-loading proxies
- **ADDED** ProjectReference from `nirmata.Data` to `nirmata.Data.Dto`

## Impact
- **Affected specs:** `data-foundation`
- **Affected code:**
  - `nirmata.Data/Context/nirmataDbContext.cs`
  - `nirmata.Data/Context/nirmataDbContextFactory.cs`
  - `nirmata.Data/Entities/Projects/Project.cs`
  - `nirmata.Data/Entities/Projects/Step.cs`
  - `nirmata.Data/Migrations/*`
  - `nirmata.Data/nirmata.Data.csproj`

## Related
- Roadmap item: PH-PRD-0001 — EF Core context + entities + migrations skeleton
