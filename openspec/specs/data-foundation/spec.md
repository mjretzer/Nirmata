# data-foundation Specification

## Purpose
TBD - created by archiving change add-foundation-baseline. Update Purpose after archive.
## Requirements
### Requirement: SQLite DbContext Configuration
The data layer SHALL configure `DbContext` to use SQLite for local development.

#### Scenario: Context initialization
- **WHEN** the application starts
- **THEN** the `DbContext` connects using the configured SQLite connection string

### Requirement: Data Project Layout
The data layer SHALL follow the established project layout: `Gmsd.Data/Context` for DbContext, `Gmsd.Data/Entities` for entities, `Gmsd.Data/Mapping` for mappings, and `Gmsd.Data/Migrations` for EF Core migrations.

#### Scenario: File placement
- **WHEN** a new data component is added
- **THEN** it is placed in the correct folder based on its role (Context, Entities, Mapping, Migrations)

### Requirement: Connection String Convention
The API configuration SHALL provide the SQLite connection string under `ConnectionStrings:DefaultConnection` and use a path rooted in the data project (e.g., `../Gmsd.Data/sqllitedb/gmsd.db`).

#### Scenario: Configuration lookup
- **WHEN** the API config is loaded
- **THEN** the DbContext resolves the SQLite connection string from `ConnectionStrings:DefaultConnection`

### Requirement: Migrations Workflow
The data layer SHALL provide a design-time `DbContext` factory so migrations are repeatable.

#### Scenario: Creating a migration
- **WHEN** `dotnet ef migrations add` is executed
- **THEN** the migration targets the SQLite provider without manual overrides

### Requirement: Design-Time DbContext Factory
The data layer SHALL implement `IDesignTimeDbContextFactory<GmsdDbContext>` in `Gmsd.Data/Context` to construct the DbContext for EF Core tooling.

#### Scenario: Tooling context creation
- **WHEN** EF Core tooling runs outside the API host
- **THEN** it can construct `GmsdDbContext` via the design-time factory

### Requirement: Baseline Migration
The data layer SHALL include a baseline migration that can recreate the schema from scratch.

#### Scenario: Reset database
- **WHEN** the local database file is deleted and migrations are applied
- **THEN** the schema is recreated and the API can start successfully

### Requirement: Thin-Slice Entity Definition
The data layer SHALL define a Project entity with `ProjectId` (string) and `Name` (required, max 200), as the thin-slice entity.

#### Scenario: Project schema
- **WHEN** the baseline migration is applied
- **THEN** the Project table includes `ProjectId` and `Name` with the required constraints

### Requirement: Thin-Slice Data Access
The data layer SHALL expose minimal data access needed by the Project thin-slice service.

#### Scenario: Load entity
- **WHEN** the service requests a Project by id
- **THEN** the data layer returns the Project or signals not found

### Requirement: EF Core DbContext Configuration
The system SHALL provide a DbContext (`GmsdDbContext`) configured for SQLite with lazy-loading proxies enabled.

#### Scenario: DbContext initialization with DI
- **GIVEN** the application configures services with `AddDbContext<GmsdDbContext>`
- **WHEN** a service requests `GmsdDbContext` via constructor injection
- **THEN** the context is configured with SQLite and lazy-loading proxies

#### Scenario: Design-time factory
- **GIVEN** EF Core tooling requires a parameterless DbContext constructor
- **WHEN** running migrations or scaffolding
- **THEN** `GmsdDbContextFactory` provides a properly configured context instance

### Requirement: Project Entity
The system SHALL provide a `Project` entity with string primary key, required name, and navigation to related Steps.

#### Scenario: Project creation
- **GIVEN** a new project with ProjectId and Name
- **WHEN** the project is added to the context and saved
- **THEN** it persists with the specified properties and an empty Steps collection

### Requirement: Step Entity with Foreign Key
The system SHALL provide a `Step` entity with string primary key, required name, and required foreign key relationship to Project.

#### Scenario: Step creation with parent Project
- **GIVEN** an existing Project entity
- **WHEN** a Step is created with StepId, Name, and the Project reference
- **THEN** it persists with the foreign key properly linked to the parent Project

### Requirement: Cascade Delete Configuration
The system SHALL configure the Project → Steps relationship with cascade delete behavior.

#### Scenario: Project deletion cascades to Steps
- **GIVEN** a Project with one or more associated Steps
- **WHEN** the Project is deleted and changes are saved
- **THEN** all associated Steps are automatically deleted

### Requirement: EF Core Migrations
The system SHALL include a baseline migration that creates the Project and Step tables with proper schema.

#### Scenario: Initial migration applies successfully
- **GIVEN** a clean SQLite database
- **WHEN** `dotnet ef database update` is executed
- **THEN** the Project and Step tables are created matching the entity configurations

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
- **WHEN** a developer opens `Gmsd.Data/Migrations/Seeds/InitialData.sql`
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

