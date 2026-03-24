# workspace-domain-data Specification

## Purpose
TBD - created by archiving change workspace-registry-domain-data-foundation. Update Purpose after archive.
## Requirements
### Requirement: Workspace-scoped spec read APIs
The system SHALL provide workspace-scoped, read-only APIs to retrieve spec/state slices for milestones, phases, tasks, and project.

#### Scenario: Get milestones
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/spec/milestones`
- **THEN** the system returns 200 OK with a milestones payload for that workspace

#### Scenario: Get phases
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/spec/phases`
- **THEN** the system returns 200 OK with a phases payload for that workspace

#### Scenario: Get tasks
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/spec/tasks`
- **THEN** the system returns 200 OK with a tasks payload for that workspace

#### Scenario: Get project
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/spec/project`
- **THEN** the system returns 200 OK with a project payload for that workspace

### Requirement: Workspace-scoped filesystem browsing
The system SHALL provide a workspace-scoped filesystem API that can return directory listings and file content.

#### Scenario: List directory
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/files/{path}` for a directory path
- **THEN** the system returns 200 OK with a directory listing for that workspace

#### Scenario: Read file content
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/files/{path}` for a file path
- **THEN** the system returns 200 OK with the file content for that workspace

### Requirement: Filesystem requests are gated to workspace root
The system MUST ensure that filesystem requests can only access paths under the registered workspace root.

#### Scenario: Path escapes workspace root
- **WHEN** a client requests a filesystem path that resolves outside the registered workspace root
- **THEN** the system rejects the request

### Requirement: Workspace ID is required and validated for domain data endpoints
The system MUST validate that workspace-scoped requests reference an existing registered workspace.

#### Scenario: Unknown workspace ID
- **WHEN** a client requests any workspace-scoped endpoint with an unknown workspace ID
- **THEN** the system returns a 404 Not Found

