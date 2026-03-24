## MODIFIED Requirements

### Requirement: Workspace Retrieval
The API MUST provide endpoints to retrieve workspace summaries and details.

#### Scenario: List workspaces
- **WHEN** a client requests `GET /v1/workspaces`
- **THEN** the API returns a list of `WorkspaceSummary` objects

#### Scenario: Get workspace details
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}`
- **THEN** the API returns the full `Workspace` object for that ID

## ADDED Requirements

### Requirement: Workspace CRUD
The API MUST provide endpoints to create, update, and delete workspaces.

#### Scenario: Create workspace
- **WHEN** a client requests `POST /v1/workspaces` with a valid workspace registration payload
- **THEN** the API returns 201 Created with the created workspace

#### Scenario: Update workspace
- **WHEN** a client requests `PUT /v1/workspaces/{workspaceId}` with a valid workspace update payload
- **THEN** the API returns 200 OK with the updated workspace

#### Scenario: Delete workspace
- **WHEN** a client requests `DELETE /v1/workspaces/{workspaceId}`
- **THEN** the API returns 204 No Content

### Requirement: Workspace spec read endpoints
The API MUST provide workspace-scoped endpoints to retrieve spec/state slices.

#### Scenario: Get milestones
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/spec/milestones`
- **THEN** the API returns 200 OK with milestones data

#### Scenario: Get phases
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/spec/phases`
- **THEN** the API returns 200 OK with phases data

#### Scenario: Get tasks
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/spec/tasks`
- **THEN** the API returns 200 OK with tasks data

#### Scenario: Get project
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/spec/project`
- **THEN** the API returns 200 OK with project data

### Requirement: Workspace filesystem endpoints
The API MUST provide workspace-scoped endpoints to browse directories and retrieve file content.

#### Scenario: Get file tree or file content
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/files/{path}`
- **THEN** the API returns directory tree information for directory paths
- **AND** returns raw file content for file paths

Directory paths MUST return a stable directory listing DTO shape (`DirectoryListingResponse` containing `DirectoryEntry` items) as defined in `specs/workspace-domain-data/spec.md`.

File paths MUST return raw file bytes with an appropriate `Content-Type` (derived from the file when possible, otherwise `application/octet-stream`).
