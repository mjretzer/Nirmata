## MODIFIED Requirements

### Requirement: Workspace-scoped filesystem browsing
The system SHALL provide a workspace-scoped filesystem API that can return directory listings and file content, and SHALL support deterministic missing-path handling for plan artifact routes.

#### Scenario: List directory
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/files/{path}` for a directory path
- **THEN** the system returns 200 OK with a directory listing for that workspace

#### Scenario: Read file content
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/files/{path}` for a file path
- **THEN** the system returns 200 OK with the file content for that workspace

#### Scenario: Read missing file path
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/files/{path}` for a path that does not exist in that workspace
- **THEN** the system returns 404 Not Found
- **AND** clients can render missing-artifact behavior for that path
