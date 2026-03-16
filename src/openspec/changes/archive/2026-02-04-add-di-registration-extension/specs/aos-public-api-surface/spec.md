## ADDED Requirements

### Requirement: DI registration extension method

The system SHALL provide an extension method `AddnirmataAos(this IServiceCollection services)` in `nirmata.Aos/Composition/ServiceCollectionExtensions.cs` that registers all public AOS services with the .NET dependency injection container.

The extension method MUST accept an optional configuration callback or `IConfiguration` parameter for binding `AosOptions`.

#### Scenario: Consumer registers AOS services
- **GIVEN** a consumer project (e.g., `nirmata.Agents`) references `nirmata.Aos`
- **WHEN** the consumer calls `services.AddnirmataAos()` in their composition root
- **THEN** all public AOS services are registered and resolvable from the container

#### Scenario: Configuration flows from appsettings
- **GIVEN** an `appsettings.json` with an `Aos` section containing `RepositoryRootPath`
- **WHEN** `services.AddnirmataAos(configuration)` is called
- **THEN** `IOptions<AosOptions>` is bound and available for injection

### Requirement: Service lifetime conventions

The system SHALL define and enforce deterministic service lifetimes:

- **Singleton** (shared state, thread-safe):
  - `IWorkspace` and its implementation
  - `ISpecStore` and its implementation (`AosSpecStore`)
  - `IStateStore` and its implementation (`AosStateStore`)
  - `IEvidenceStore` and its implementation (`AosEvidenceStore`)
  - `IValidator` and its implementation
  - `CommandCatalog`

- **Scoped** or **Transient** (per-invocation isolation):
  - `ICommandRouter` and its implementation (`CommandRouter`)
  - Individual command handlers (transient per resolution)

#### Scenario: Multiple command executions don't share mutable state
- **GIVEN** a singleton `CommandCatalog` and scoped `ICommandRouter`
- **WHEN** two concurrent scopes execute commands via separate `ICommandRouter` instances
- **THEN** each router has its own handler instances and execution context, while the catalog is shared

#### Scenario: Stores maintain singleton state across resolutions
- **GIVEN** `ISpecStore` registered as singleton
- **WHEN** multiple components resolve `ISpecStore` in different scopes
- **THEN** all components receive the same instance, ensuring consistent file system view

### Requirement: Public services are resolvable via DI

The system SHALL ensure the following public interfaces can be resolved from the DI container after calling `AddnirmataAos()`:

- `ICommandRouter` from `nirmata.Aos.Public.Services`
- `IWorkspace` from `nirmata.Aos.Public`
- `ISpecStore` from `nirmata.Aos.Public`
- `IStateStore` from `nirmata.Aos.Public`
- `IEvidenceStore` from `nirmata.Aos.Public`
- `IValidator` from `nirmata.Aos.Public`

#### Scenario: nirmata.Agents resolves engine services
- **GIVEN** `nirmata.Agents` calls `services.AddnirmataAos()`
- **WHEN** the Plane layer requests `ICommandRouter`, `IWorkspace`, `ISpecStore`, `IStateStore`, `IEvidenceStore`, or `IValidator`
- **THEN** all services resolve successfully with correct implementations

### Requirement: Configuration options class

The system SHALL provide an `AosOptions` class in `nirmata.Aos/Configuration/` for engine configuration binding.

At minimum, the options class MUST include:
- `RepositoryRootPath` (string): absolute path to the repository root where `.aos/` resides

#### Scenario: Options bound from configuration
- **GIVEN** a configuration section with `Aos:RepositoryRootPath = "/path/to/repo"`
- **WHEN** `AddnirmataAos(configuration)` is called
- **THEN** `IOptions<AosOptions>.Value.RepositoryRootPath` returns `/path/to/repo`

#### Scenario: Options validation
- **GIVEN** `AosOptions` with null or empty `RepositoryRootPath`
- **WHEN** the options are validated
- **THEN** validation fails with an actionable error indicating the path is required
