# aos-public-api-surface Specification

## Purpose

Defines canonical AOS workspace contracts and behavioral semantics for $capabilityId.

- **Lives in:** `nirmata.Aos/*`, `.aos/**`
- **Owns:** Engine-level artifact contracts, validation, and deterministic IO semantics for this capability
- **Does not own:** Plane/orchestrator workflows (owned by `agents-*` capabilities)
## Requirements
### Requirement: AOS exposes a stable public compile-against surface
The system SHALL provide a stable compile-against surface for the AOS engine rooted at `nirmata.Aos/Public/**` and `nirmata.Aos.Public.*` namespaces.

The system SHALL treat types in `nirmata.Aos.Public.*` as the only supported compile-time surface for external consumers.

#### Scenario: Consumer compiles against public surface
- **WHEN** a consumer project references `nirmata.Aos` and uses `nirmata.Aos.Public.*` interfaces and catalogs
- **THEN** the consumer compiles without referencing engine implementation namespaces

### Requirement: AOS separates public surface from internal engine core
The system SHALL treat code rooted at `nirmata.Aos/Engine/**` (`nirmata.Aos.Engine.*`) and `nirmata.Aos/_Shared/**` (`nirmata.Aos._Shared.*`) as internal-only implementation details.

The system SHALL NOT expose public types in `nirmata.Aos.Engine.*` or `nirmata.Aos._Shared.*` namespaces.

#### Scenario: Internal namespaces are not public API
- **WHEN** the `nirmata.Aos` assembly is inspected for public types
- **THEN** no public type is declared under `nirmata.Aos.Engine.*` or `nirmata.Aos._Shared.*`

### Requirement: Public services are expressed as interfaces
The system SHALL provide public service interfaces under `nirmata.Aos/Public/Services/**` (`nirmata.Aos.Public.Services.*`) for the engine’s primary subsystems:
- Workspace
- Spec store
- State store
- Evidence store
- Validation
- Command routing

The system SHALL provide a public workspace abstraction in `nirmata.Aos.Public.*` that allows consumers to:
- obtain the `RepositoryRootPath` and `AosRootPath` for the current workspace
- resolve supported artifact IDs to canonical contract paths under `.aos/*`
- resolve contract paths under `.aos/*` to absolute filesystem paths safely

#### Scenario: Public service contracts exist
- **WHEN** a consumer needs to integrate with the engine
- **THEN** it can compile against the subsystem service interfaces without referencing internal implementations

#### Scenario: Consumer resolves canonical paths using only public APIs
- **WHEN** a consumer needs to resolve an artifact id or contract path to a filesystem path
- **THEN** it can do so using only `nirmata.Aos.Public.*` APIs without referencing `nirmata.Aos.Engine.*` namespaces

### Requirement: Public state store contract is usable
The system SHALL provide a public state store contract `nirmata.Aos.Public.IStateStore` that allows consumers to interact with the state layer (`.aos/state/*`) without referencing internal engine types.

The contract MUST support, at minimum:
- reading the current snapshot (`.aos/state/state.json`)
- appending an event to `.aos/state/events.ndjson`
- tailing events from `.aos/state/events.ndjson` in stable file order with filters and paging

Public state store APIs MUST exchange only `nirmata.Aos.Contracts.State.*` types (or primitive CLR types) and MUST NOT expose types in `nirmata.Aos.Engine.*` or `nirmata.Aos._Shared.*`.

#### Scenario: Consumer reads snapshot using public state store
- **GIVEN** a consumer that references only `nirmata.Aos.Public.*` and `nirmata.Aos.Contracts.*`
- **WHEN** it reads the state snapshot via `IStateStore`
- **THEN** it receives a stable snapshot contract type without referencing `nirmata.Aos.Engine.*` namespaces

#### Scenario: Consumer tails events with filters and paging
- **GIVEN** `.aos/state/events.ndjson` contains multiple events
- **WHEN** a consumer tails events with `sinceLine` and `maxItems` and an `eventType` filter
- **THEN** it receives at most `maxItems` events after `sinceLine` in file order and only matching the filter

### Requirement: Stable ID and kind catalogs exist in the public surface
The system SHALL provide stable catalogs in `nirmata.Aos/Public/Catalogs/**` (`nirmata.Aos.Public.Catalogs.*`) for well-known identifiers and kinds, including:
- Schema IDs
- Command IDs
- Artifact kinds

#### Scenario: Consumer references stable IDs
- **WHEN** a consumer needs to reference a known schema/command/artifact kind
- **THEN** it uses the catalog constants/types from `nirmata.Aos.Public.Catalogs.*`

### Requirement: Stable contracts are centralized under `nirmata.Aos.Contracts`
The system SHALL provide stable contract types under `nirmata.Aos/Contracts/**` (`nirmata.Aos.Contracts.*`) to support the public surface without coupling to internal engine types.

#### Scenario: Public APIs use stable contracts
- **WHEN** a public interface requires exchanging a contract type
- **THEN** it uses a type from `nirmata.Aos.Contracts.*` rather than an internal engine type

### Requirement: Stable state contracts exist for snapshot and events
The system SHALL provide stable contract DTOs under `nirmata.Aos/Contracts/State/**` (`nirmata.Aos.Contracts.State.*`) for the public state store surface.

At minimum, the contracts MUST cover:
- a snapshot contract representing `.aos/state/state.json` (including `schemaVersion` and cursor fields)
- an event entry contract representing one parsed NDJSON event line, including the original line number and event payload
- a tail request/response (or equivalent) that expresses `sinceLine`, `maxItems`, and optional filters by `eventType` and legacy `kind`

Contracts MUST be deterministic and serialization-friendly, and MUST NOT depend on internal engine types.

#### Scenario: Consumer implements state tooling using only stable contracts
- **WHEN** a consumer implements tooling around state snapshots and event tailing
- **THEN** it can do so using only `nirmata.Aos.Contracts.State.*` and `nirmata.Aos.Public.*` without referencing internal namespaces

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

