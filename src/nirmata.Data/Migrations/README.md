# Data Migrations and Seeds

This directory contains database migration tooling and seed data for the nirmata application.

## Migration Workflow

There are two equivalent ways to run EF Core tooling. Choose based on where your terminal is:

**From `src/nirmata.Data/` (short form)**

```bash
cd src/nirmata.Data
dotnet ef migrations add <MigrationName>
dotnet ef database update
dotnet ef migrations script 0
```

**From the solution root (explicit flags — works from any directory)**

```bash
dotnet ef migrations add <MigrationName> \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data

dotnet ef database update \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data

dotnet ef migrations script 0 \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data \
  --output migrations.sql
```

> **Note:** The design-time factory (`nirmataDbContextFactory`) uses a relative path
> `Data Source=sqllitedb/nirmata.db`. EF Core resolves this relative to the
> `--project` directory (`src/nirmata.Data/`), so both forms resolve to the same
> file: `src/nirmata.Data/sqllitedb/nirmata.db`.
>
> **Why `--startup-project src/nirmata.Data` (not `src/nirmata.Api`):** The
> `Microsoft.EntityFrameworkCore.Design` package is referenced in `nirmata.Data.csproj`
> with `<PrivateAssets>all</PrivateAssets>`. This makes it a build-time-only dependency
> that does not flow transitively to `nirmata.Api`. EF Core tools require this package to
> be present in the startup project; using `nirmata.Data` as both project and startup
> project satisfies this requirement without modifying the API project.

### Creating a New Migration

From the solution root:
```bash
dotnet ef migrations add <MigrationName> \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data
```

From `src/nirmata.Data/`:
```bash
dotnet ef migrations add <MigrationName>
```

Migration files are generated in `src/nirmata.Data/Migrations/`.

### Applying Migrations

Apply all pending migrations (what the app also does on startup via `MigrateAsync`):

From the solution root:
```bash
dotnet ef database update \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data
```

From `src/nirmata.Data/`:
```bash
dotnet ef database update
```

### Reverting Migrations

Revert to a specific migration:
```bash
dotnet ef database update <MigrationName> \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data
```

Revert all migrations (reset database to empty):
```bash
dotnet ef database update 0 \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data
```

### Generating SQL Scripts

Generate a full idempotent script covering all migrations (safe to run on any database state):
```bash
dotnet ef migrations script 0 \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data \
  --idempotent \
  --output migrations.sql
```

Generate a targeted delta script (e.g., from `AddWorkspacesTable` to the latest migration):
```bash
dotnet ef migrations script AddWorkspacesTable \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data \
  --output delta.sql
```

The `--idempotent` flag wraps each migration in an `IF NOT EXISTS` guard so the script is safe to re-run against a database that is already partially migrated.

## Seed Data

Seed data is configured in `nirmataDbContext.OnModelCreating()` using EF Core's `HasData()` method. The following entities are seeded on fresh database installations:

- **Projects**: 3 sample projects for immediate testing
  - `proj-sample-001`: Sample Web Application
  - `proj-sample-002`: API Migration Project
  - `proj-sample-003`: Database Optimization Initiative

### InitialData.sql

The `InitialData.sql` file in the `Seeds/` directory serves as reference documentation for the baseline seed data that is applied automatically via EF Core migrations.

## Database Provider

This application uses **SQLite** for local development. All three configuration points resolve to the same physical file: `src/nirmata.Data/sqllitedb/nirmata.db`.

| Context | Config source | Connection string | CWD assumed |
|---|---|---|---|
| Runtime API | `nirmata.Api/appsettings.json` | `Data Source='../nirmata.Data/sqllitedb/nirmata.db'` | `src/nirmata.Api/` |
| Design-time (`dotnet ef`) | `nirmataDbContextFactory.CreateDbContext` | `Data Source=sqllitedb/nirmata.db` | `src/nirmata.Data/` |
| DbContext fallback | `nirmataDbContext.OnConfiguring` | `Data Source=sqllitedb/nirmata.db` | `src/nirmata.Data/` |

The single quotes in the runtime connection string are valid — `DbConnectionStringBuilder` strips them during parsing.

## Migration History

| Migration | Description |
|-----------|-------------|
| 20260131211837_InitialCreate | Initial database schema with Project and Step tables |
| 20260207022348_AddSeedData | Adds seed data for Projects |
| 20260222224101_AddWorkspacesTable | Adds Workspaces table for workspace registry |
| 20260222224200_AddLastValidatedAtToWorkspace | Adds `LastValidatedAt` column to Workspaces table |

## Audit (Phase 10)

Comparison of `nirmataDbContext`, `nirmataDbContextModelSnapshot`, and the migration chain against the entity classes revealed two issues that must be fixed before any further schema work:

### Issue 1 — Migration ordering bug (critical)

`AddLastValidatedAtToWorkspace` (timestamp `20260222201500`, 20:15) runs **before** `AddWorkspacesTable` (timestamp `20260222224101`, 22:41) in EF Core's ascending timestamp order. Applying migrations from a clean state fails because the `ALTER TABLE Workspaces ADD COLUMN LastValidatedAt` runs before `CREATE TABLE Workspaces` exists.

The correct logical order is:
1. `InitialCreate` — create Project and Step tables
2. `AddSeedData` — seed project data
3. `AddWorkspacesTable` — create Workspaces table
4. `AddLastValidatedAtToWorkspace` — add LastValidatedAt column

### Issue 2 — Snapshot drift

`nirmataDbContextModelSnapshot.cs` is missing `LastValidatedAt` from the `Workspace` entity. The snapshot represents the state after `AddWorkspacesTable` (which became the "last" in execution order due to its later timestamp) but not the actual final state. Running `dotnet ef migrations add <Next>` would re-generate `LastValidatedAt` as a new column, creating a duplicate.

### Resolution (task 1.2)

No new baseline migration was required. The existing migration content is correct — only the timestamp metadata was wrong. Fixed by:

1. Renamed `20260222201500_AddLastValidatedAtToWorkspace.*` → `20260222224200_AddLastValidatedAtToWorkspace.*` (timestamp now after `AddWorkspacesTable`).
2. Updated `[Migration("...")]` attribute in the designer file to match the new timestamp.
3. Added `LastValidatedAt` to the `Workspace` entity in `nirmataDbContextModelSnapshot.cs` to bring the snapshot in sync with the full migration chain.

The migration history is now authoritative and applies correctly from scratch.

### Observation — Pending migrations on existing database (Phase 10, task 3.2)

Running `dotnet ef migrations list` shows `AddWorkspacesTable` and
`AddLastValidatedAtToWorkspace` as `(Pending)` on an existing database. This means
the local `nirmata.db` predates the timestamp rename (task 1.2 above) or was last
migrated before those two migrations were added. The fix is `dotnet ef database update`
(or simply starting the API, which calls `Database.MigrateAsync()` on boot). A fresh
clone that has never had a database file will apply all four migrations cleanly on first
startup — this is confirmed in task 5.2.

## Local artifact policy

### Source control

SQLite runtime artifacts are **never committed** to the repository:

| Pattern | Rule |
|---|---|
| `*.db` | Ignored via `.gitignore` |
| `*.db-wal` | Ignored via `.gitignore` |
| `*.db-shm` | Ignored via `.gitignore` |
| `sqllitedb/.gitkeep` | **Tracked** — ensures the directory exists on a fresh clone |

The `sqllitedb/` directory is pre-created for you on a fresh clone via `.gitkeep`. You do not need to create it manually.

### Stale artifacts at the wrong path

If you find `*.db`, `*.db-wal`, or `*.db-shm` files inside `src/nirmata.Api/` these are stale artifacts from before the connection string was normalized to `../nirmata.Data/sqllitedb/nirmata.db`. They are untracked and can be deleted safely.

### Clean-room setup

On a fresh clone with no pre-existing database:

1. `sqllitedb/` already exists (via `.gitkeep`).
2. `Program.cs` calls `Directory.CreateDirectory(dbDirectory)` before `MigrateAsync()` as an extra guard.
3. Starting the API (`dotnet run`) applies all four migrations automatically, creates `nirmata.db`, and seeds the three sample projects.

No manual schema steps are needed.

## Verification (Phase 10)

### 5.1 – Fresh clone creates the database without manual steps

Confirmed. A fresh clone has `sqllitedb/` pre-created via `.gitkeep`. `Program.cs` guards against a missing directory with `Directory.CreateDirectory(dbDirectory)` before calling `MigrateAsync()`. Starting the API on a fresh clone creates `nirmata.db` and applies all four migrations in the correct order without any manual intervention.

### 5.2 – Existing migrations apply cleanly; app starts when the database file does not exist

Confirmed. After the timestamp rename (task 1.2), the four migrations apply in the correct order on a clean database:

1. `InitialCreate` — creates `Project` and `Step` tables
2. `AddSeedData` — inserts the three sample projects
3. `AddWorkspacesTable` — creates the `Workspaces` table
4. `AddLastValidatedAtToWorkspace` — adds the `LastValidatedAt` column

When the database file does not yet exist SQLite creates it on first connection; `MigrateAsync()` then brings the schema to the current state.

### 5.3 – Future model changes have a clear workflow and rollback path

**Add a column or table:**
```bash
dotnet ef migrations add <MigrationName> \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data
```
Then start the API or run `dotnet ef database update`.

**Rollback to a specific migration:**
```bash
dotnet ef database update <TargetMigrationName> \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data
```

**Rollback all migrations (reset to empty schema):**
```bash
dotnet ef database update 0 \
  --project src/nirmata.Data \
  --startup-project src/nirmata.Data
```

After rolling back, delete the local `nirmata.db` file and restart the API to get a fresh seeded database.

**Local artifact policy for the generated migration files:** migration `.cs` and `.Designer.cs` files are source-controlled; the `nirmata.db` file is never committed (see Local artifact policy above).
