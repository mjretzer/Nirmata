# web-runs-dashboard Specification

## Purpose
TBD - created by archiving change add-direct-agent-runner. Update Purpose after archive.
## Requirements
### Requirement: Runs Dashboard List Page

The `Gmsd.Web` project SHALL provide a `/Runs` page that displays a list of all agent runs with their current status.

The implementation MUST:
- Display runs in a table with columns: Run ID, Status, Start Time, End Time, Current Phase
- Support empty state when no runs exist
- Link each run to its detail page (`/Runs/Details?id={runId}`)
- Read run data from evidence folder (`.aos/evidence/runs/`) or via `IRunRepository`
- Order runs by start time (most recent first)

#### Scenario: Display runs in table
- **GIVEN** multiple runs exist in `.aos/evidence/runs/`
- **WHEN** a user navigates to `/Runs`
- **THEN** a table displays all runs with ID, status, timestamps, and phase

#### Scenario: Empty state for no runs
- **GIVEN** no runs exist in the workspace
- **WHEN** a user navigates to `/Runs`
- **THEN** a friendly message indicates "No runs found" with guidance on starting a run

#### Scenario: Link to run details
- **GIVEN** the runs list is displayed
- **WHEN** a user clicks on a run row
- **THEN** they navigate to `/Runs/Details?id={runId}`

### Requirement: Run Detail Page

The `Gmsd.Web` project SHALL provide a `/Runs/Details` page that displays comprehensive information about a single run.

The implementation MUST:
- Accept run ID via query parameter (`?id={runId}`)
- Display run metadata: ID, status, start/end timestamps, correlation ID
- Display execution logs from `.aos/evidence/runs/{runId}/logs/`
- Display artifact pointers from `summary.json` and `commands.json`
- Provide navigation back to runs list
- Return HTTP 404 if run not found

#### Scenario: Display run metadata
- **GIVEN** a run with ID "RUN-abc-123" exists
- **WHEN** a user navigates to `/Runs/Details?id=RUN-abc-123`
- **THEN** the page displays run ID, status, timestamps, and correlation ID

#### Scenario: Display execution logs
- **GIVEN** a run has log files in `logs/` directory
- **WHEN** the detail page loads
- **THEN** log entries are displayed chronologically

#### Scenario: Display artifacts
- **GIVEN** a completed run with `summary.json` and `commands.json`
- **WHEN** the detail page loads
- **THEN** artifact file names and paths are displayed

#### Scenario: Handle missing run
- **GIVEN** no run exists with ID "nonexistent"
- **WHEN** a user navigates to `/Runs/Details?id=nonexistent`
- **THEN** the page returns HTTP 404 with a friendly error message

#### Scenario: Navigate back to list
- **GIVEN** a user is viewing a run detail page
- **WHEN** they click the "Back to Runs" link
- **THEN** they return to `/Runs` list page

