## ADDED Requirements

### Requirement: Database Seed Data Configuration
The system SHALL configure EF Core to seed initial Project entities during migration application.

#### Scenario: Fresh database initialization
- **GIVEN** a clean SQLite database with no existing data
- **WHEN** `dotnet ef database update` is executed
- **THEN** the Project table is created and populated with 2-3 seed projects

#### Scenario: Idempotent seed data application
- **GIVEN** a database that already has seed data applied
- **WHEN** migrations are re-run or database is reset
- **THEN** seed data is applied deterministically without duplicates or errors

### Requirement: Seed Data Reference File
The system SHALL provide `InitialData.sql` as a reference for seed data content and structure.

#### Scenario: Manual seed data inspection
- **WHEN** a developer opens `nirmata.Data/Migrations/Seeds/InitialData.sql`
- **THEN** they see the SQL INSERT statements equivalent to the EF Core seed configuration

### Requirement: Repeatable Migration Workflow
The data layer SHALL support a repeatable migration workflow that applies cleanly from baseline to current.

#### Scenario: Full migration cycle
- **GIVEN** a fresh database file
- **WHEN** `dotnet ef database update` is executed
- **THEN** all migrations apply successfully including seed data insertion

#### Scenario: Rollback workflow
- **GIVEN** a database at the latest migration with seed data
- **WHEN** `dotnet ef database update 0` is executed
- **THEN** all tables are removed and the database is reset to empty state

### Requirement: Seed Data Content Standards
The system SHALL provide seed data with meaningful sample content for Projects.

#### Scenario: Sample project content
- **WHEN** seed data is applied
- **THEN** Projects include descriptive names (e.g., "Sample Project Alpha", "Demo Project Beta")
- **AND** each Project has a unique ProjectId following the existing string key pattern
