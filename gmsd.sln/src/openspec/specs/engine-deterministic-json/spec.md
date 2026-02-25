# engine-deterministic-json Specification

## Purpose

Defines the public DI/interface surface for deterministic JSON serialization and atomic/no-churn writing in the AOS engine. Canonical deterministic JSON semantics for `.aos/**` artifacts are defined by `aos-deterministic-json-serialization`, and this capability MUST conform.

- **Lives in:** `Gmsd.Aos/Public/IDeterministicJsonSerializer.cs`, `Gmsd.Aos/Public/Composition/*`, `.aos/**` JSON artifacts
- **Owns:** Public interface shape and DI registration for deterministic JSON services
- **Does not own:** Artifact-specific schema/version policies (defined by canonical AOS contract specs)
## Requirements
### Requirement: Deterministic JSON serializer interface exists
The system SHALL define `IDeterministicJsonSerializer` as a public interface in `Gmsd.Aos/Public/`.

The interface SHALL provide methods for deterministic JSON serialization/deserialization.

#### Scenario: Serialize produces deterministic output
- **GIVEN** an object to serialize
- **WHEN** `IDeterministicJsonSerializer.Serialize(obj)` is called twice with the same input
- **THEN** both calls produce byte-identical output

#### Scenario: Serialized output uses UTF-8 without BOM
- **GIVEN** any serializable object
- **WHEN** `IDeterministicJsonSerializer.Serialize(obj)` is called
- **THEN** the output is UTF-8 encoded without a Byte Order Mark

#### Scenario: Serialized output uses LF line endings
- **GIVEN** any serializable object
- **WHEN** `IDeterministicJsonSerializer.Serialize(obj)` is called
- **THEN** the output uses `\n` (LF) line endings, not `\r\n` (CRLF)

#### Scenario: Serialized output has stable key ordering
- **GIVEN** an object with multiple properties
- **WHEN** `IDeterministicJsonSerializer.Serialize(obj)` is called
- **THEN** object keys appear in consistent alphabetical order

### Requirement: Atomic write semantics are provided
The interface SHALL provide atomic write capabilities to prevent partial/corrupt artifacts.

#### Scenario: WriteAtomic completes fully or not at all
- **GIVEN** a path and JSON-serializable object
- **WHEN** `IDeterministicJsonSerializer.WriteAtomic(path, obj)` is called
- **AND** a crash occurs during the write
- **THEN** the target file either contains the complete valid JSON or does not exist (never partial/corrupt)

### Requirement: No-churn semantics are implemented
The interface SHALL skip writing when the canonical bytes are unchanged.

#### Scenario: Unchanged content skips write
- **GIVEN** a file containing canonical JSON at the target path
- **WHEN** `IDeterministicJsonSerializer.WriteAtomic(path, obj)` is called with content that serializes to identical bytes
- **THEN** the file modification time remains unchanged

### Requirement: Deserialize supports all AOS artifact types
The interface SHALL provide generic deserialization for all AOS artifact types.

#### Scenario: Deserialize project.json succeeds
- **GIVEN** valid project.json content
- **WHEN** `IDeterministicJsonSerializer.Deserialize<Project>(content)` is called
- **THEN** a valid Project instance is returned

#### Scenario: Deserialize validates JSON structure
- **GIVEN** invalid JSON content
- **WHEN** `IDeterministicJsonSerializer.Deserialize<T>(content)` is called
- **THEN** a deterministic, actionable exception is thrown

### Requirement: Service is registered in DI
The system SHALL register `IDeterministicJsonSerializer` as a Singleton in `AddGmsdAos()`.

#### Scenario: Plane resolves the service via DI
- **GIVEN** a configured service collection with `AddGmsdAos()` called
- **WHEN** `serviceProvider.GetRequiredService<IDeterministicJsonSerializer>()` is called
- **THEN** a non-null implementation is returned

