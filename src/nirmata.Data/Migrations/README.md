# Data Migrations and Seeds

This directory contains database migration tooling and seed data for the nirmata application.

## Migration Workflow

### Creating a New Migration

```bash
cd nirmata.Data
dotnet ef migrations add <MigrationName>
```

### Applying Migrations

```bash
cd nirmata.Data
dotnet ef database update
```

### Reverting Migrations

Revert to a specific migration:
```bash
cd nirmata.Data
dotnet ef database update <MigrationName>
```

Revert all migrations (reset database):
```bash
cd nirmata.Data
dotnet ef database update 0
```

### Generating SQL Scripts

Generate full migration script from baseline:
```bash
cd nirmata.Data
dotnet ef migrations script 0
```

## Seed Data

Seed data is configured in `nirmataDbContext.OnModelCreating()` using EF Core's `HasData()` method. The following entities are seeded on fresh database installations:

- **Projects**: 3 sample projects for immediate testing
  - `proj-sample-001`: Sample Web Application
  - `proj-sample-002`: API Migration Project
  - `proj-sample-003`: Database Optimization Initiative

### InitialData.sql

The `InitialData.sql` file in the `Seeds/` directory serves as reference documentation for the baseline seed data that is applied automatically via EF Core migrations.

## Database Provider

This application uses **SQLite** for local development. The connection string is configured in:
- `nirmataDbContext.OnConfiguring()` (design-time fallback)
- `appsettings.json` in the startup project (runtime)

## Migration History

| Migration | Description |
|-----------|-------------|
| 20260131211837_InitialCreate | Initial database schema with Project and Step tables |
| 20260207022348_AddSeedData | Adds seed data for Projects |
