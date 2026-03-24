## MODIFIED Requirements

### Requirement: Codebase Intelligence
The API MUST provide workspace-scoped codebase intelligence data.

#### Scenario: Get workspace codebase intel
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/codebase`
- **THEN** the API returns the workspace's codebase artifact inventory, language breakdown, and stack info

#### Scenario: Get codebase artifact detail
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/codebase/{artifactId}`
- **THEN** the API returns the selected artifact's payload

### Requirement: Orchestrator State
The API MUST provide workspace-scoped orchestrator gate data and timeline snapshots.

#### Scenario: Get orchestrator gate
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/orchestrator/gate`
- **THEN** the API returns the current next-task gate, including checks and runnable status

#### Scenario: Get orchestrator timeline
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/orchestrator/timeline`
- **THEN** the API returns the ordered orchestrator timeline for that workspace
