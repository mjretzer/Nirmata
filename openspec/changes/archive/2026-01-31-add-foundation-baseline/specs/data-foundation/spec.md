## ADDED Requirements
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
