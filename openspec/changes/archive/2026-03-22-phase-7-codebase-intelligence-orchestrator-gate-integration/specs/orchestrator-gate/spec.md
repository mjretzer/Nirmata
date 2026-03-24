# orchestrator-gate Specification

## Purpose
Define workspace-scoped orchestrator gate computation and timeline snapshots.

## ADDED Requirements
### Requirement: Next task gate derivation
The system SHALL derive the next task gate from the workspace cursor, task plan, UAT state, and evidence state.

#### Scenario: Next task is runnable
- **WHEN** the current task's dependencies are satisfied, UAT has passed, and evidence exists for the workspace
- **THEN** the system returns a gate with `runnable` set to `true`

#### Scenario: Next task is blocked
- **WHEN** the current task has missing dependencies, failing UAT, or missing evidence
- **THEN** the system returns a gate with `runnable` set to `false`
- **AND** the gate identifies the blocking conditions

### Requirement: Gate check reporting
The system SHALL expose dependency, UAT, and evidence checks in the gate response.

#### Scenario: Return gate checks
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/orchestrator/gate`
- **THEN** the system returns a gate response containing dependency, UAT, and evidence checks
- **AND** each check includes an id, kind, label, detail, and status

#### Scenario: Include recommended action
- **WHEN** the gate is not runnable
- **THEN** the response includes a recommended action describing the next step required to unblock the task

### Requirement: Orchestrator timeline snapshot
The system SHALL return an ordered orchestrator timeline for the workspace.

#### Scenario: Return timeline
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/orchestrator/timeline`
- **THEN** the system returns the workspace's timeline steps in order
- **AND** each step includes a stable id, label, and status

#### Scenario: Timeline reflects current state
- **WHEN** the workspace cursor advances
- **THEN** the returned timeline updates to reflect the current workspace progression

### Requirement: Workspace-scoped orchestrator access
The system SHALL reject orchestrator gate requests for unknown workspaces.

#### Scenario: Unknown workspace
- **WHEN** a client requests the orchestrator gate or timeline for an unknown workspace
- **THEN** the system returns `404 Not Found`
