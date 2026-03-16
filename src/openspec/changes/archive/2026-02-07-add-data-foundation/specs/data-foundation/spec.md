## ADDED Requirements

### Requirement: EF Core DbContext Configuration
The system SHALL provide a DbContext (`nirmataDbContext`) configured for SQLite with lazy-loading proxies enabled.

#### Scenario: DbContext initialization with DI
- **GIVEN** the application configures services with `AddDbContext<nirmataDbContext>`
- **WHEN** a service requests `nirmataDbContext` via constructor injection
- **THEN** the context is configured with SQLite and lazy-loading proxies

#### Scenario: Design-time factory
- **GIVEN** EF Core tooling requires a parameterless DbContext constructor
- **WHEN** running migrations or scaffolding
- **THEN** `nirmataDbContextFactory` provides a properly configured context instance

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
