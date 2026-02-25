# Engine Checkpoint Manager Service

## ADDED Requirements

### Requirement: Checkpoint manager interface exists
The system SHALL define `ICheckpointManager` as a public interface in `Gmsd.Aos/Public/`.

The interface SHALL provide methods to create, restore, and list checkpoints under `.aos/state/checkpoints/`.

#### Scenario: Create checkpoint snapshots state
- **GIVEN** an initialized AOS workspace with valid state
- **WHEN** `ICheckpointManager.CreateCheckpoint()` is called
- **THEN** a new checkpoint folder is created at `.aos/state/checkpoints/<checkpoint-id>/`
- **AND** the folder contains a snapshot of `state.json` and checkpoint metadata

#### Scenario: Create checkpoint returns stable ID
- **GIVEN** an initialized AOS workspace
- **WHEN** `ICheckpointManager.CreateCheckpoint()` is called
- **THEN** a stable checkpoint ID is returned (format: CHK-####)

#### Scenario: Create checkpoint records event
- **GIVEN** an initialized AOS workspace
- **WHEN** `ICheckpointManager.CreateCheckpoint()` is called
- **THEN** a `checkpoint.created` event is appended to `events.ndjson`

#### Scenario: Restore checkpoint replaces state
- **GIVEN** an existing checkpoint
- **WHEN** `ICheckpointManager.RestoreCheckpoint(checkpointId)` is called
- **THEN** `state.json` is replaced with the checkpoint snapshot

#### Scenario: Restore checkpoint records event
- **GIVEN** an existing checkpoint
- **WHEN** `ICheckpointManager.RestoreCheckpoint(checkpointId)` is called
- **THEN** a `checkpoint.restored` event is appended to `events.ndjson`

#### Scenario: List checkpoints returns all checkpoints
- **GIVEN** a workspace with existing checkpoints
- **WHEN** `ICheckpointManager.ListCheckpoints()` is called
- **THEN** all checkpoints are returned with their metadata (checkpointId, createdAt, description)

#### Scenario: Get checkpoint returns specific checkpoint metadata
- **GIVEN** a workspace with an existing checkpoint
- **WHEN** `ICheckpointManager.GetCheckpoint(checkpointId)` is called
- **THEN** the checkpoint metadata is returned

### Requirement: Checkpoint metadata is deterministic JSON
The interface SHALL write checkpoint metadata as deterministic JSON.

#### Scenario: Checkpoint metadata includes required fields
- **GIVEN** a created checkpoint
- **WHEN** the checkpoint metadata file is read
- **THEN** it contains: schemaVersion, checkpointId, createdAtUtc, stateSnapshotPath

### Requirement: Service is registered in DI
The system SHALL register `ICheckpointManager` as a Singleton in `AddGmsdAos()`.

#### Scenario: Plane resolves the service via DI
- **GIVEN** a configured service collection with `AddGmsdAos()` called
- **WHEN** `serviceProvider.GetRequiredService<ICheckpointManager>()` is called
- **THEN** a non-null implementation is returned

## Cross-References
- `aos-checkpoints` - Defines full checkpoint requirements
