## MODIFIED Requirements

### Requirement: Issue Tracking
The API SHALL provide workspace-scoped endpoints to list, create, update, delete, and status-transition issues backed by the workspace spec store.

#### Scenario: List issues with filters
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/issues` with optional `status`, `severity`, `taskId`, `phaseId`, or `milestoneId` filters
- **THEN** the API returns the matching `Issue` records for that workspace

#### Scenario: Create an issue
- **WHEN** a client requests `POST /v1/workspaces/{workspaceId}/issues` with a valid issue payload
- **THEN** the API persists the issue under `.aos/spec/issues/` and returns the created record

#### Scenario: Update an issue
- **WHEN** a client requests `PUT /v1/workspaces/{workspaceId}/issues/{issueId}` with updated issue fields
- **THEN** the API updates the persisted issue record and returns the updated record

#### Scenario: Delete an issue
- **WHEN** a client requests `DELETE /v1/workspaces/{workspaceId}/issues/{issueId}`
- **THEN** the API removes the issue record from the workspace issue store

#### Scenario: Update issue status
- **WHEN** a client requests `PATCH /v1/workspaces/{workspaceId}/issues/{issueId}/status` with a new status value
- **THEN** the API updates only the issue status field and returns the updated record

## ADDED Requirements

### Requirement: Workspace UAT summaries
The API SHALL provide a workspace-scoped UAT summary endpoint that returns UAT records and derived pass/fail state per task and phase.

#### Scenario: Retrieve UAT summaries
- **WHEN** a client requests `GET /v1/workspaces/{workspaceId}/uat`
- **THEN** the API returns UAT records from `.aos/spec/uat/`
- **AND** the response includes derived pass/fail summaries for the current task and phase context
