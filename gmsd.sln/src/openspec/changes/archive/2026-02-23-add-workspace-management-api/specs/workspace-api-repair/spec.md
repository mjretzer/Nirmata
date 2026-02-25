# Spec Delta: workspace-api-repair

## MODIFIED Requirements
### Requirement: Index repair can be triggered via API
The system SHALL provide a `POST /api/v1/workspaces/repair` endpoint to rebuild index artifacts and ensure baseline consistency.

#### Scenario: Repairing a workspace with missing indexes
- **GIVEN** a workspace where `spec/tasks/index.json` is missing
- **WHEN** `POST /api/v1/workspaces/repair` is executed
- **THEN** the response is 200 OK and the missing index is rebuilt deterministically by scanning the directory

#### Scenario: Repairing a workspace with schema drift
- **GIVEN** a workspace where an artifact's `SchemaVersion` is outdated or invalid
- **WHEN** `POST /api/v1/workspaces/repair` is executed
- **THEN** the response is 200 OK and the system attempts to normalize the artifact or reports it in the health status
