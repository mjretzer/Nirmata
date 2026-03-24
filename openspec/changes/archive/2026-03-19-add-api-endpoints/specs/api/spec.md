## ADDED Requirements

### Requirement: Workspace Retrieval
The API MUST provide endpoints to retrieve workspace summaries and details.

#### Scenario: List workspaces
- **WHEN** a client requests `GET /api/v1/workspaces`
- **THEN** the API returns a list of `WorkspaceSummary` objects

#### Scenario: Get workspace details
- **WHEN** a client requests `GET /api/v1/workspaces/{workspaceId}`
- **THEN** the API returns the full `Workspace` object for that ID

### Requirement: Task Retrieval
The API MUST provide an endpoint to retrieve tasks with optional filtering.

#### Scenario: List tasks
- **WHEN** a client requests `GET /api/v1/tasks`
- **THEN** the API returns a list of `Task` objects

#### Scenario: Filter tasks
- **WHEN** a client requests `GET /api/v1/tasks?phaseId={id}`
- **THEN** the API returns only tasks belonging to that phase

### Requirement: Run History
The API MUST provide an endpoint to retrieve run history.

#### Scenario: List runs
- **WHEN** a client requests `GET /api/v1/runs`
- **THEN** the API returns a list of `Run` objects

### Requirement: Issue Tracking
The API MUST provide an endpoint to retrieve issues.

#### Scenario: List issues
- **WHEN** a client requests `GET /api/v1/issues`
- **THEN** the API returns a list of `Issue` objects

### Requirement: Project Checkpoints
The API MUST provide an endpoint to retrieve checkpoints.

#### Scenario: List checkpoints
- **WHEN** a client requests `GET /api/v1/checkpoints`
- **THEN** the API returns a list of `Checkpoint` objects

### Requirement: Continuity State
The API MUST provide endpoints to retrieve continuity state, events, and context packs.

#### Scenario: Get continuity state
- **WHEN** a client requests `GET /api/v1/continuity`
- **THEN** the API returns `ContinuityState`, `HandoffSnapshot`, `Event[]`, and `ContextPack[]`

### Requirement: Virtual File System
The API MUST provide access to the virtual file system representation.

#### Scenario: Get file system
- **WHEN** a client requests `GET /api/v1/filesystem`
- **THEN** the API returns the `FileSystemNode` tree

### Requirement: Host Console Data
The API MUST provide host logs and API surface status.

#### Scenario: Get host logs
- **WHEN** a client requests `GET /api/v1/host/logs`
- **THEN** the API returns `HostLogLine[]`

#### Scenario: Get API surfaces
- **WHEN** a client requests `GET /api/v1/host/surfaces`
- **THEN** the API returns `ApiSurface[]`

### Requirement: Diagnostics
The API MUST provide diagnostic data including logs, artifacts, locks, and cache entries.

#### Scenario: Get diagnostics
- **WHEN** a client requests `GET /api/v1/diagnostics`
- **THEN** the API returns diagnostic logs, artifacts, locks, and cache entries

### Requirement: Codebase Intelligence
The API MUST provide codebase intelligence data.

#### Scenario: Get codebase intel
- **WHEN** a client requests `GET /api/v1/codebase/intel`
- **THEN** the API returns artifacts, language breakdown, and stack info

### Requirement: Orchestrator State
The API MUST provide the current orchestrator and gate state.

#### Scenario: Get orchestrator state
- **WHEN** a client requests `GET /api/v1/orchestrator/state`
- **THEN** the API returns gates, metadata, and timeline templates

### Requirement: Chat Integration
The API MUST provide chat messages and suggestions.

#### Scenario: Get chat data
- **WHEN** a client requests `GET /api/v1/chat`
- **THEN** the API returns messages, command suggestions, and quick actions
