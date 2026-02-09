# Engine Run Manager Service

## ADDED Requirements

### Requirement: Run manager interface exists
The system SHALL define `IRunManager` as a public interface in `Gmsd.Aos/Public/`.

The interface SHALL provide methods to manage the full run lifecycle: start, finish, and enumerate runs.

#### Scenario: Start run creates evidence folder structure
- **GIVEN** an initialized AOS workspace
- **WHEN** `IRunManager.StartRun(command, args)` is called
- **THEN** a new run folder is created under `.aos/evidence/runs/<run-id>/`
- **AND** the folder contains `run.json`, `packet.json`, and `logs/`, `outputs/` subdirectories

#### Scenario: Start run returns stable run ID
- **GIVEN** an initialized AOS workspace
- **WHEN** `IRunManager.StartRun(command, args)` is called
- **THEN** a stable, unique run ID is returned (32-character hex format)

#### Scenario: Start run adds entry to run index
- **GIVEN** an initialized AOS workspace
- **WHEN** `IRunManager.StartRun(command, args)` is called
- **THEN** the run index at `.aos/evidence/runs/index.json` includes the new run

#### Scenario: Finish run updates metadata
- **GIVEN** an existing started run
- **WHEN** `IRunManager.FinishRun(runId, status, exitCode)` is called
- **THEN** `run.json` reflects the finished status and timestamp
- **AND** `result.json` is created in the artifacts folder

#### Scenario: Finish run updates run index
- **GIVEN** an existing started run
- **WHEN** `IRunManager.FinishRun(runId, status, exitCode)` is called
- **THEN** the run index is updated to reflect the finished status

#### Scenario: List runs returns all runs
- **GIVEN** a workspace with existing runs
- **WHEN** `IRunManager.ListRuns()` is called
- **THEN** all runs are returned with their metadata (runId, status, startedAt, finishedAt)

#### Scenario: Get run returns specific run metadata
- **GIVEN** a workspace with an existing run
- **WHEN** `IRunManager.GetRun(runId)` is called
- **THEN** the run metadata is returned

### Requirement: Run packet is created at start
The interface SHALL ensure a packet artifact is written when a run starts.

#### Scenario: Run packet contains command and args
- **GIVEN** a call to `IRunManager.StartRun("execute-plan", new[] { "--task", "TSK-0001" })`
- **WHEN** the run is started
- **THEN** `artifacts/packet.json` contains the command name and argument array

### Requirement: Service is registered in DI
The system SHALL register `IRunManager` as a Singleton in `AddGmsdAos()`.

#### Scenario: Plane resolves the service via DI
- **GIVEN** a configured service collection with `AddGmsdAos()` called
- **WHEN** `serviceProvider.GetRequiredService<IRunManager>()` is called
- **THEN** a non-null implementation is returned

## Cross-References
- `aos-run-lifecycle` - Defines full run lifecycle requirements
