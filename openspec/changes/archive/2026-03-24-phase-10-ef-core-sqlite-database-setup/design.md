## Context

The Phase 10 work validates the EF Core + SQLite path end to end. The runtime API uses `src/nirmata.Api/appsettings.json`, the design-time factory lives in `src/nirmata.Data/Context/nirmataDbContextFactory.cs`, and startup in `src/nirmata.Api/Program.cs` already calls `Database.MigrateAsync()`. The current database file is intended to live at `src/nirmata.Data/sqllitedb/nirmata.db`, and the migration chain currently includes `InitialCreate`, `AddSeedData`, `AddLastValidatedAtToWorkspace`, and `AddWorkspacesTable`.

## Goals / Non-Goals

**Goals:**
- Use one authoritative SQLite database path for runtime, design-time, and startup bootstrapping.
- Confirm the existing migration chain and model snapshot match the current `nirmataDbContext`.
- Make first boot and schema upgrades repeatable on a clean clone.
- Document exact `dotnet ef` commands so developers can work from any directory.
- Define how local SQLite artifacts are handled in source control.

**Non-Goals:**
- Redesign the database schema or move away from SQLite.
- Change seed semantics unless the audit finds a mismatch.
- Introduce a new persistence layer or a separate local database convention.

## Decisions

- **Standardize on one local SQLite file path**: keep the database centered on `src/nirmata.Data/sqllitedb/nirmata.db` so runtime and design-time tooling do not diverge.
- **Keep runtime and design-time configuration aligned**: if the path is represented relatively, it should resolve to the same file in both contexts; if not, a shared helper should be introduced instead of duplicating path logic.
- **Create the SQLite directory before migration/open**: first boot should not depend on a pre-existing `sqllitedb/` folder.
- **Preserve `Database.MigrateAsync()` on startup**: the app should still apply migrations automatically, but only after the database location is guaranteed.
- **Treat the current migrations as authoritative unless the audit proves otherwise**: add a cleanup or baseline migration only if the model/snapshot/history comparison shows drift.
- **Document explicit EF commands**: prefer `--project src/nirmata.Data --startup-project src/nirmata.Api` examples so tooling works regardless of the current working directory.

## Risks / Trade-offs

- **Relative path drift can create duplicate local databases** → Mitigate by keeping every connection-string reference and design-time fallback on the same resolved file path.
- **Startup migration can fail if the directory is missing** → Mitigate by ensuring the parent folder exists before the database connection is opened.
- **Seed data or snapshot drift may hide schema mismatches** → Mitigate by comparing the model, snapshot, and generated migration history together before making more schema changes.
- **Local artifact files can clutter the repo or confuse contributors** → Mitigate by documenting a clear policy for `.db`, `-wal`, and `-shm` files.

## Migration Plan

1. Audit the migration chain, snapshot, and current model for drift.
2. Normalize SQLite path resolution in runtime and design-time configuration.
3. Add any bootstrap guard needed to create `sqllitedb/` before startup migration.
4. Update developer documentation with explicit EF Core commands and artifact guidance.
5. Verify fresh-clone startup and migration upgrade behavior before closing the change.
