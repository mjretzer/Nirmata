## 1. Implementation

- [x] 1.1 Create `nirmata.Data/Migrations/Seeds/` directory structure
- [x] 1.2 Add seed data configuration in `nirmataDbContext.OnModelCreating`
- [x] 1.3 Create `InitialData.sql` with reference seed data for Projects
- [x] 1.4 Add `HasData()` call for Project entity with 2-3 sample projects
- [x] 1.5 Generate new migration `AddSeedData` with seeded entities
- [x] 1.6 Verify migration applies cleanly on fresh database
- [x] 1.7 Verify migration rolls back cleanly
- [x] 1.8 Document migration workflow in README or project docs

## 2. Validation

- [x] 2.1 Run `dotnet ef database update` on fresh SQLite database
- [x] 2.2 Confirm seed Projects exist in database after migration
- [x] 2.3 Run `dotnet ef migrations script` to verify SQL output includes seed data
- [x] 2.4 Verify rollback works: `dotnet ef database update 0` then re-apply
