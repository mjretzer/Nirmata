# web-tasks-page Specification

## Purpose
Provides a web interface for viewing and managing tasks, including filtered task lists, detailed task views with spec artifacts, and task execution controls.

## ADDED Requirements

### Requirement: Tasks List Page
The `nirmata.Web` project SHALL provide a `/Tasks` page that displays all tasks with filtering capabilities.

The implementation MUST:
- Read tasks from `.aos/spec/tasks/index.json` and individual task files
- Display tasks in a table with columns: Task ID, Name, Phase, Milestone, Status
- Provide filters for: phase, milestone, status, keyword search
- Show status indicators (pending, running, completed, failed)
- Link each task to its detail page (`/Tasks/Details?id={taskId}`)
- Order tasks by ID (TSK-###### format)
- Support empty state when no tasks exist

#### Scenario: Display tasks list
- **GIVEN** a workspace with tasks TSK-000001, TSK-000002
- **WHEN** a user navigates to `/Tasks`
- **THEN** the page displays all tasks with their phase, milestone, and status

#### Scenario: Filter tasks by phase
- **GIVEN** tasks TSK-000001 belongs to PH-0001, TSK-000002 belongs to PH-0002
- **WHEN** the user filters by phase PH-0001
- **THEN** only TSK-000001 is displayed

#### Scenario: Filter tasks by status
- **GIVEN** tasks with various statuses (pending, completed, failed)
- **WHEN** the user filters by status "failed"
- **THEN** only failed tasks are displayed

### Requirement: Task Detail Page
The `nirmata.Web` project SHALL provide a `/Tasks/Details` page with tabbed interface showing all task spec artifacts.

The implementation MUST:
- Accept task ID via query parameter (`?id={taskId}`)
- Display task metadata: ID, Name, Phase ID, Status, Created Date
- Provide tabs for: task.json, plan.json, uat.json, links.json
- Display raw JSON content in formatted/monospace view
- Provide syntax highlighting or clean formatting for JSON
- Link to associated phase (`/Phases/Details?id={phaseId}`)
- Link to latest run evidence if available
- Return HTTP 404 if task not found

#### Scenario: Display task detail with tabs
- **GIVEN** task TSK-000001 with task.json, plan.json, uat.json, and links.json
- **WHEN** a user navigates to `/Tasks/Details?id=TSK-000001`
- **THEN** the page displays task metadata and tabbed JSON content

#### Scenario: Display task.json tab
- **GIVEN** the task detail page for TSK-000001
- **WHEN** the task.json tab is selected
- **THEN** the formatted content of `.aos/spec/tasks/TSK-000001/task.json` is displayed

#### Scenario: Display plan.json tab
- **GIVEN** the task detail page for TSK-000001
- **WHEN** the plan.json tab is selected
- **THEN** the formatted content of `.aos/spec/tasks/TSK-000001/plan.json` is displayed with file scopes and verification checks

### Requirement: Task Execution Action
The task detail page SHALL provide an "Execute Plan" action to run the task.

The implementation MUST:
- Provide "Execute Plan" button when task status is pending or failed
- Display confirmation with task name and file scopes
- Trigger task execution workflow via `ITaskExecutor`
- Show execution progress indicator
- On completion, display success/failure status
- Update task status in UI
- Create run record in `.aos/evidence/runs/`

#### Scenario: Execute task plan
- **GIVEN** task TSK-000001 with status "pending"
- **WHEN** the user clicks "Execute Plan" and confirms
- **THEN** the task is executed, a run record is created, and the status updates to "completed" or "failed"

#### Scenario: Display execution progress
- **GIVEN** the user clicked "Execute Plan" on TSK-000001
- **WHEN** the execution is running
- **THEN** a progress indicator is displayed with status updates

### Requirement: View Evidence Action
The task detail page SHALL provide a "View Evidence" action linking to the latest run.

The implementation MUST:
- Provide "View Evidence" button when task has been executed
- Read latest run from `.aos/evidence/task-evidence/{taskId}/latest.json`
- Link to `/Runs/Details?id={runId}`
- Display summary of latest run (timestamp, status, files changed)
- Show historical runs in a dropdown or list

#### Scenario: View evidence for latest run
- **GIVEN** task TSK-000001 has been executed with run RUN-000001
- **WHEN** the user clicks "View Evidence"
- **THEN** they navigate to `/Runs/Details?id=RUN-000001`

#### Scenario: Display evidence summary
- **GIVEN** task TSK-000001 has multiple runs
- **WHEN** the task detail page loads
- **THEN** the latest run is summarized with timestamp, status, and diff stats

### Requirement: Mark Status Action
The task detail page SHALL provide functionality to manually mark task status.

The implementation MUST:
- Provide "Mark Status" dropdown with options: pending, completed, failed
- Allow status update with optional notes/reason
- Persist status to `.aos/state/state.json` or task spec
- Append status change event to `.aos/state/events.ndjson`
- Update UI immediately after status change

#### Scenario: Mark task as completed
- **GIVEN** task TSK-000001 with status "pending"
- **WHEN** the user selects "completed" from "Mark Status" and confirms
- **THEN** the task status is updated and displayed

#### Scenario: Mark task as failed with reason
- **GIVEN** task TSK-000001 with status "running"
- **WHEN** the user selects "failed" and enters reason "Scope violation detected"
- **THEN** the task status is updated to failed with the reason recorded
