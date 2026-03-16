# aos-public-api-surface Specification (Delta)

## ADDED Requirements
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

#### Scenario: Public service contracts exist
- **WHEN** a consumer needs to integrate with the engine
- **THEN** it can compile against the subsystem service interfaces without referencing internal implementations

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

