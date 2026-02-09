# Change: Add EF Core Data Foundation Skeleton

## Why
The product application requires a clean persistence layer for business/domain data (Projects, Steps) separate from the AOS engine workspace artifacts. This establishes the foundational data infrastructure using EF Core with SQLite for development.

## What Changes
- **ADDED** `GmsdDbContext` with Project and Step entities
- **ADDED** Entity configurations with proper relationships (Project → Steps one-to-many with cascade delete)
- **ADDED** EF Core migrations baseline (InitialCreate)
- **ADDED** SQLite database configuration with lazy-loading proxies
- **ADDED** ProjectReference from `Gmsd.Data` to `Gmsd.Data.Dto`

## Impact
- **Affected specs:** `data-foundation`
- **Affected code:**
  - `Gmsd.Data/Context/GmsdDbContext.cs`
  - `Gmsd.Data/Context/GmsdDbContextFactory.cs`
  - `Gmsd.Data/Entities/Projects/Project.cs`
  - `Gmsd.Data/Entities/Projects/Step.cs`
  - `Gmsd.Data/Migrations/*`
  - `Gmsd.Data/Gmsd.Data.csproj`

## Related
- Roadmap item: PH-PRD-0001 — EF Core context + entities + migrations skeleton
