## 1. Migration history audit

- [x] 1.1 Compare the current `nirmataDbContext` model, the generated snapshot, and the migration chain in `src/nirmata.Data/Migrations/`.
- [x] 1.2 Confirm whether the existing migration history is authoritative or whether a cleanup/baseline migration is needed before more schema work.

## 2. SQLite path and bootstrap alignment

- [x] 2.1 Verify the runtime connection string, the design-time factory, and startup configuration resolve the same local SQLite database file.
- [x] 2.2 Ensure `sqllitedb/` is created or otherwise guaranteed before first boot so SQLite can create or open `nirmata.db` cleanly.
- [x] 2.3 Confirm `Database.MigrateAsync()` succeeds on first boot and on schema upgrade without manual file setup.

## 3. Developer EF tooling

- [x] 3.1 Document the exact `dotnet ef migrations add`, `dotnet ef database update`, and `dotnet ef migrations script` commands for this repository.
- [x] 3.2 Verify the documented commands work from `src/nirmata.Data` and with explicit `--project` / `--startup-project` flags.

## 4. Seeds, snapshots, and local artifacts

- [x] 4.1 Review `HasData()` entries and confirm they still match the intended baseline data.
- [x] 4.2 Check that the model snapshot stays in sync with `nirmataDbContext`.
- [x] 4.3 Decide how local `.db`, `-wal`, and `-shm` files are handled for source control and clean-room setup.

## 5. Verification

- [x] 5.1 Confirm a fresh clone can create or update the SQLite database without manual schema steps.
- [x] 5.2 Confirm existing migrations apply cleanly and the app starts when the database file does not yet exist.
- [x] 5.3 Confirm future model changes have a clear migration workflow, rollback path, and local artifact policy.
