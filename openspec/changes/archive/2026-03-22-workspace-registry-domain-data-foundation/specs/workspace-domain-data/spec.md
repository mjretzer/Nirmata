# Workspace Domain Data Specification
 
## Purpose
Define workspace-scoped domain data surfaces (spec/state reads and filesystem browsing) and their stable DTO shapes.
 
## Requirements

## ADDED Requirements

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

## DTO Schemas

The response payloads for the workspace-scoped spec endpoints SHALL be derived from the AOS artifact files under the workspace `.aos/` directory and SHALL map to stable DTO shapes.

Canonical schema references (see `documents/architecture/schemas.md`):
- `project.schema.json` validates `.aos/spec/project.json`
- `milestone.schema.json` validates `.aos/spec/milestones/MS-####/milestone.json`
- `phase.schema.json` validates `.aos/spec/phases/PH-####/phase.json`
- `task.schema.json` validates `.aos/spec/tasks/TSK-######/task.json`

Response DTO shapes:

`GET /v1/workspaces/{workspaceId}/spec/milestones` → `MilestoneSummary[]`
```json
{
  "id": "MS-0001",
  "title": "...",
  "status": "...",
  "phaseIds": ["PH-0001", "PH-0002"]
}
```

`GET /v1/workspaces/{workspaceId}/spec/phases` → `PhaseSummary[]`
```json
{
  "id": "PH-0002",
  "milestoneId": "MS-0001",
  "title": "...",
  "status": "...",
  "order": 1,
  "taskIds": ["TSK-000013"]
}
```

`GET /v1/workspaces/{workspaceId}/spec/tasks` → `TaskSummary[]`
```json
{
  "id": "TSK-000013",
  "phaseId": "PH-0002",
  "milestoneId": "MS-0001",
  "title": "...",
  "status": "..."
}
```

`GET /v1/workspaces/{workspaceId}/spec/project` → `ProjectSpecResponse`
```json
{
  "name": "...",
  "description": "...",
  "version": "...",
  "owner": "...",
  "repo": "...",
  "milestones": ["MS-0001"],
  "constraints": [],
  "tags": [],
  "createdAt": "...",
  "updatedAt": "..."
}

Filesystem browsing DTO shapes:

`GET /v1/workspaces/{workspaceId}/files/{path}` → directory listing response

`DirectoryEntry`
```json
{
  "name": "...",
  "path": "...",
  "type": "file",
  "sizeBytes": 123,
  "children": []
}
```

Notes:
- `path` is workspace-relative and uses forward slashes (`/`).
- `type` is either `"file"` or `"directory"`.
- `sizeBytes` SHOULD be present for files and SHOULD be omitted (or `null`) for directories.
- `children` SHOULD be present only when `type` is `"directory"`. If present, it represents a nested directory tree.

`DirectoryListingResponse`
```json
{
  "path": "...",
  "entries": [
    {
      "name": "...",
      "path": "...",
      "type": "directory",
      "children": []
    },
    {
      "name": "...",
      "path": "...",
      "type": "file",
      "sizeBytes": 123
    }
  ]
}
```

`GET /v1/workspaces/{workspaceId}/files/{path}` → file content response

For file paths, the system returns raw file bytes with an appropriate `Content-Type` (derived from the file when possible, otherwise `application/octet-stream`).
