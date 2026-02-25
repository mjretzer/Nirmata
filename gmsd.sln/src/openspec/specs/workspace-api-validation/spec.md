# workspace-api-validation Specification

## Purpose
TBD - created by archiving change add-workspace-management-api. Update Purpose after archive.
## Requirements
### Requirement: Workspace validation report is available via API
The system SHALL provide a `GET /api/v1/workspaces/validate` endpoint that returns a structured validation report for a specific workspace.

#### Scenario: Validation report contains deep schema issues
- **GIVEN** a workspace with a malformed `project.json` or invalid artifact version
- **WHEN** `GET /api/v1/workspaces/validate` is executed for that workspace
- **THEN** the response is 200 OK with a report including:
  - `IsCompliant` (bool)
  - `Errors` (list of strings including schema validation failures)
  - `Warnings` (list of strings including deprecated versions)
  - `LockStatus` (info on any active .aos/locks)

