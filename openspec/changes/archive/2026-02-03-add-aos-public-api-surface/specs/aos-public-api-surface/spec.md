# aos-public-api-surface Specification (Delta)

## ADDED Requirements
### Requirement: AOS exposes a stable public compile-against surface
The system SHALL provide a stable compile-against surface for the AOS engine rooted at `Gmsd.Aos/Public/**` and `Gmsd.Aos.Public.*` namespaces.

The system SHALL treat types in `Gmsd.Aos.Public.*` as the only supported compile-time surface for external consumers.

#### Scenario: Consumer compiles against public surface
- **WHEN** a consumer project references `Gmsd.Aos` and uses `Gmsd.Aos.Public.*` interfaces and catalogs
- **THEN** the consumer compiles without referencing engine implementation namespaces

### Requirement: AOS separates public surface from internal engine core
The system SHALL treat code rooted at `Gmsd.Aos/Engine/**` (`Gmsd.Aos.Engine.*`) and `Gmsd.Aos/_Shared/**` (`Gmsd.Aos._Shared.*`) as internal-only implementation details.

The system SHALL NOT expose public types in `Gmsd.Aos.Engine.*` or `Gmsd.Aos._Shared.*` namespaces.

#### Scenario: Internal namespaces are not public API
- **WHEN** the `Gmsd.Aos` assembly is inspected for public types
- **THEN** no public type is declared under `Gmsd.Aos.Engine.*` or `Gmsd.Aos._Shared.*`

### Requirement: Public services are expressed as interfaces
The system SHALL provide public service interfaces under `Gmsd.Aos/Public/Services/**` (`Gmsd.Aos.Public.Services.*`) for the engine’s primary subsystems:
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
The system SHALL provide stable catalogs in `Gmsd.Aos/Public/Catalogs/**` (`Gmsd.Aos.Public.Catalogs.*`) for well-known identifiers and kinds, including:
- Schema IDs
- Command IDs
- Artifact kinds

#### Scenario: Consumer references stable IDs
- **WHEN** a consumer needs to reference a known schema/command/artifact kind
- **THEN** it uses the catalog constants/types from `Gmsd.Aos.Public.Catalogs.*`

### Requirement: Stable contracts are centralized under `Gmsd.Aos.Contracts`
The system SHALL provide stable contract types under `Gmsd.Aos/Contracts/**` (`Gmsd.Aos.Contracts.*`) to support the public surface without coupling to internal engine types.

#### Scenario: Public APIs use stable contracts
- **WHEN** a public interface requires exchanging a contract type
- **THEN** it uses a type from `Gmsd.Aos.Contracts.*` rather than an internal engine type

