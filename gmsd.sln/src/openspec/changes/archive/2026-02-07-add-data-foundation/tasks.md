## 1. Implementation
- [x] 1.1 Create GmsdDbContext with Project and Step DbSets
- [x] 1.2 Configure Project → Steps relationship with cascade delete
- [x] 1.3 Create Project entity with [Table], [Key], [Required], [MaxLength] attributes
- [x] 1.4 Create Step entity with foreign key to Project
- [x] 1.5 Add EF Core SQLite + Proxies + Design package references
- [x] 1.6 Generate InitialCreate migration
- [x] 1.7 Configure SQLite connection string (Data Source=sqllitedb/gmsd.db)

## 2. Validation
- [x] 2.1 Verify `dotnet build` succeeds
- [x] 2.2 Verify migration files generated in `Gmsd.Data/Migrations/`
- [x] 2.3 Verify database schema matches entity configuration

## 3. Verification
- [x] 3.1 Run `dotnet build` to confirm compilation
- [x] 3.2 Confirm migrations apply cleanly to SQLite
