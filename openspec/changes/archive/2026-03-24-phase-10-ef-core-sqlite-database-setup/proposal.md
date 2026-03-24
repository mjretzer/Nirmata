## Why

Phase 10 needs the EF Core and SQLite setup locked down so fresh clones can bootstrap the local database without manual path fixes, migration drift, or one-off setup steps. The current runtime and design-time configuration already point at the local `sqllitedb` database, but the path resolution, directory creation, and migration workflow still need to be verified and documented as authoritative before any new schema work lands.

## What Changes

- Verify the EF Core migration history in `src/nirmata.Data/Migrations/` against the current model and decide whether any baseline cleanup is needed.
- Align the runtime connection string, the design-time factory, and startup bootstrap so they all resolve the same SQLite database file.
- Ensure the `sqllitedb/` directory exists or is created before first boot so SQLite can create or open the database cleanly.
- Standardize local `dotnet ef` workflows for migration add, update, and script commands.
- Confirm application startup safely runs `Database.MigrateAsync()` when the database file is missing or the schema needs upgrading.
- Review seed data, model snapshots, and source-control handling for local `.db`, `-wal`, and `-shm` artifacts.

## Impact

- `nirmata.Api` startup and configuration become deterministic for local SQLite bootstrapping.
- `nirmata.Data` migration history and tooling guidance become the source of truth for developers.
- Fresh clone setup should no longer require manual database file creation or path adjustments.
