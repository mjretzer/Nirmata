## ADDED Requirements

### Requirement: Workspace-scoped gate summary read API
The system SHALL provide a workspace-scoped read API that returns the current workflow gate summary derived from canonical artifacts.

#### Scenario: Read workspace gate summary
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/status`
- **THEN** the system returns 200 OK with the current gate, the next required step, and the blocking reason for that workspace

#### Scenario: Read gate summary for brownfield preflight blocker
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/status`
- **AND** canonical workspace state indicates brownfield codebase preflight is blocking progression
- **THEN** the response includes codebase readiness details that distinguish whether the map is missing or stale

#### Scenario: Read gate summary after workflow state advances
- **WHEN** canonical artifacts advance the workspace from one gate to another
- **THEN** a subsequent request to `GET /v1/workspaces/{workspaceId}/status` returns the updated current gate and next required step