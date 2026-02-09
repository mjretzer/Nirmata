# Engine Artifact Path Resolution Service

## ADDED Requirements

### Requirement: Artifact path resolver interface exists
The system SHALL define `IArtifactPathResolver` as a public interface in `Gmsd.Aos/Public/`.

The interface SHALL provide methods to resolve artifact IDs to canonical contract paths under `.aos/`.

#### Scenario: Milestone ID resolves to canonical path
- **GIVEN** a milestone ID `MS-0001`
- **WHEN** `IArtifactPathResolver.ResolveMilestonePath("MS-0001")` is called
- **THEN** the result is `.aos/spec/milestones/MS-0001/milestone.json`

#### Scenario: Phase ID resolves to canonical path
- **GIVEN** a phase ID `PH-0001`
- **WHEN** `IArtifactPathResolver.ResolvePhasePath("PH-0001")` is called
- **THEN** the result is `.aos/spec/phases/PH-0001/phase.json`

#### Scenario: Task ID resolves to canonical path
- **GIVEN** a task ID `TSK-000001`
- **WHEN** `IArtifactPathResolver.ResolveTaskPath("TSK-000001")` is called
- **THEN** the result is `.aos/spec/tasks/TSK-000001/task.json`

#### Scenario: Issue ID resolves to canonical path
- **GIVEN** an issue ID `ISS-0001`
- **WHEN** `IArtifactPathResolver.ResolveIssuePath("ISS-0001")` is called
- **THEN** the result is `.aos/spec/issues/ISS-0001.json`

#### Scenario: UAT ID resolves to canonical path
- **GIVEN** a UAT ID `UAT-0001`
- **WHEN** `IArtifactPathResolver.ResolveUatPath("UAT-0001")` is called
- **THEN** the result is `.aos/spec/uat/UAT-0001.json`

#### Scenario: Context pack ID resolves to canonical path
- **GIVEN** a context pack ID `PCK-0001`
- **WHEN** `IArtifactPathResolver.ResolveContextPackPath("PCK-0001")` is called
- **THEN** the result is `.aos/context/packs/PCK-0001.json`

#### Scenario: Run ID resolves to evidence root path
- **GIVEN** a run ID in 32-character hex format
- **WHEN** `IArtifactPathResolver.ResolveRunPath(runId)` is called
- **THEN** the result is `.aos/evidence/runs/<run-id>/`

### Requirement: Well-known contract paths are centralized
The interface SHALL expose well-known paths that do not depend on IDs.

#### Scenario: Workspace lock path is available
- **WHEN** `IArtifactPathResolver.GetWorkspaceLockPath()` is called
- **THEN** the result is `.aos/locks/workspace.lock.json`

#### Scenario: State file path is available
- **WHEN** `IArtifactPathResolver.GetStatePath()` is called
- **THEN** the result is `.aos/state/state.json`

#### Scenario: Events file path is available
- **WHEN** `IArtifactPathResolver.GetEventsPath()` is called
- **THEN** the result is `.aos/state/events.ndjson`

#### Scenario: Run index path is available
- **WHEN** `IArtifactPathResolver.GetRunIndexPath()` is called
- **THEN** the result is `.aos/evidence/runs/index.json`

### Requirement: Service is registered in DI
The system SHALL register `IArtifactPathResolver` as a Singleton in `AddGmsdAos()`.

#### Scenario: Plane resolves the service via DI
- **GIVEN** a configured service collection with `AddGmsdAos()` called
- **WHEN** `serviceProvider.GetRequiredService<IArtifactPathResolver>()` is called
- **THEN** a non-null implementation is returned

## Cross-References
- `aos-path-routing` - Defines ID format and canonical path rules
