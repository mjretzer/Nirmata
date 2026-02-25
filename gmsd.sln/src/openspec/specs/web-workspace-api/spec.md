# web-workspace-api Specification

## Purpose
TBD - created by archiving change add-workspace-management-api. Update Purpose after archive.
## Requirements
### Requirement: Workspace listing is available via API
The system SHALL provide a `GET /api/v1/workspaces` endpoint that returns a list of known workspaces from the local registry.

#### Scenario: Listing workspaces returns metadata
- **GIVEN** one or more workspaces are registered in the local database
- **WHEN** `GET /api/v1/workspaces` is executed
- **THEN** the response is a 200 OK with a list of workspace objects including `id`, `path`, `name`, and `lastOpenedAt`

### Requirement: Active workspace can be set via API
The system SHALL provide a `POST /api/v1/workspaces/open` endpoint to set the active workspace for the current session.

#### Scenario: Opening a valid workspace path succeeds
- **GIVEN** a valid directory path containing a compliant `.aos/` folder
- **WHEN** `POST /api/v1/workspaces/open` is executed with the `path`
- **THEN** the response is 200 OK and the workspace path is stored in the session

#### Scenario: Opening an invalid or unauthorized path fails
- **GIVEN** a path that does not exist or is outside authorized boundaries
- **WHEN** `POST /api/v1/workspaces/open` is executed with the `path`
- **THEN** the response is 400 Bad Request or 403 Forbidden with a detailed error message

### Requirement: Workspace initialization can be triggered via API
The system SHALL provide a `POST /api/v1/workspaces/init` endpoint to bootstrap a new AOS workspace in a specified directory.

#### Scenario: Initializing a new workspace directory
- **GIVEN** an empty directory or a repository root without `.aos/`
- **WHEN** `POST /api/v1/workspaces/init` is executed with the `path`
- **THEN** the response is 201 Created and the `.aos/` structure is created on disk

### Requirement: Workspace validation report is available via API
The system SHALL provide a `GET /api/v1/workspaces/validate` endpoint that returns a structured validation report for a specific workspace.

#### Scenario: Validation report contains schema issues
- **GIVEN** a workspace with a malformed `project.json`
- **WHEN** `GET /api/v1/workspaces/validate` is executed for that workspace
- **THEN** the response is 200 OK with a report containing normalized schema issue objects

### Requirement: Index repair can be triggered via API
The system SHALL provide a `POST /api/v1/workspaces/repair` endpoint to rebuild index artifacts.

#### Scenario: Repairing a workspace with missing indexes
- **GIVEN** a workspace where `spec/tasks/index.json` is missing
- **WHEN** `POST /api/v1/workspaces/repair` is executed
- **THEN** the response is 200 OK and the missing index is rebuilt deterministically

