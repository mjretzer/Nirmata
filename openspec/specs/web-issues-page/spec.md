# web-issues-page Specification

## Purpose
TBD - created by archiving change add-execution-verification-ui-pages. Update Purpose after archive.
## Requirements
### Requirement: Issues List Page
The `Gmsd.Web` project SHALL provide an `/Issues` page that displays all issues with comprehensive filtering capabilities.

The implementation MUST:
- Read issues from `.aos/spec/issues/index.json` and individual issue files
- Display issues in a table with columns: Issue ID, Title, Severity, Status, Type, Task, Phase, Milestone
- Provide filters for: status (open, resolved, deferred), type (bug, task, feature), severity (urgent, high, medium, low), task, phase, milestone
- Show status indicators with color coding: open (red), resolved (green), deferred (gray)
- Show severity indicators: urgent (critical), high (warning), medium (info), low (muted)
- Link each issue to its detail page (`/Issues/Details?id={issueId}`)
- Order issues by severity (urgent first) then by ID
- Support empty state when no issues exist
- Display issue count by status in summary header

#### Scenario: Display issues list
- **GIVEN** a workspace with issues ISS-0001, ISS-0002, ISS-0003
- **WHEN** a user navigates to `/Issues`
- **THEN** the page displays all issues with their severity, status, and associated task/phase

#### Scenario: Filter issues by status
- **GIVEN** issues with statuses open (ISS-0001, ISS-0002) and resolved (ISS-0003)
- **WHEN** the user filters by status "open"
- **THEN** only ISS-0001 and ISS-0002 are displayed

#### Scenario: Filter issues by severity
- **GIVEN** issues with various severities (urgent, high, medium)
- **WHEN** the user filters by severity "urgent"
- **THEN** only urgent issues are displayed

#### Scenario: Filter issues by task association
- **GIVEN** issues ISS-0001 and ISS-0002 associated with TSK-000001, ISS-0003 with TSK-000002
- **WHEN** the user filters by task TSK-000001
- **THEN** only ISS-0001 and ISS-0002 are displayed

### Requirement: Issue Detail Page
The `Gmsd.Web` project SHALL provide an `/Issues/Details` page that displays comprehensive information about a single issue.

The implementation MUST:
- Accept issue ID via query parameter (`?id={issueId}`)
- Display issue metadata: ID, Title, Severity, Status, Type, Created At, Updated At
- Display scope/impacted area (files, components, or areas affected)
- Display reproduction steps in a formatted section
- Display expected behavior vs actual behavior comparison
- Display parent UAT reference if issue originated from failed verification
- Link to associated task (`/Tasks/Details?id={taskId}`)
- Link to associated phase (`/Phases/Details?id={phaseId}`)
- Link to associated milestone (`/Milestones/Details?id={milestoneId}`)
- Link to parent UAT if applicable (`/Uat/Verify?id={uatId}`)
- Return HTTP 404 if issue not found

#### Scenario: Display issue details
- **GIVEN** issue ISS-0001 with repro steps, expected/actual behavior, and scope
- **WHEN** a user navigates to `/Issues/Details?id=ISS-0001`
- **THEN** the page displays all issue metadata, repro steps, and comparison

#### Scenario: Display issue from failed UAT
- **GIVEN** issue ISS-0001 was created from failed UAT verification
- **WHEN** the issue detail page displays
- **THEN** a link to the parent UAT is shown

#### Scenario: Navigate to associated task
- **GIVEN** the issue detail page for ISS-0001 associated with TSK-000001
- **WHEN** the user clicks the task link
- **THEN** they navigate to `/Tasks/Details?id=TSK-000001`

### Requirement: Route to Fix Plan Action
The issue detail page SHALL provide a "Route to Fix Plan" action to create a fix plan for the issue.

The implementation MUST:
- Provide "Route to Fix Plan" button for open issues
- Display confirmation dialog with issue summary
- Trigger fix planning workflow (creates task plan to resolve the issue)
- Create new task or update existing task with fix plan
- Link the issue to the fix task
- Update issue status to "in_fix_planning"
- Redirect to the fix task detail page on completion

#### Scenario: Route issue to fix plan
- **GIVEN** open issue ISS-0001 with reproduction steps
- **WHEN** the user clicks "Route to Fix Plan" and confirms
- **THEN** a fix task is created/updated, the issue is linked to it, and the user is redirected to the task

#### Scenario: Issue linked to fix task
- **GIVEN** issue ISS-0001 was routed to fix plan
- **WHEN** the issue detail page displays
- **THEN** the fix task is shown with a link to its detail page

### Requirement: Mark Resolved Action
The issue detail page SHALL provide a "Mark Resolved" action to close an issue.

The implementation MUST:
- Provide "Mark Resolved" button for open or in_fix_planning issues
- Display resolution form with: resolution type (fixed, wont_fix, duplicate, not_reproducible), resolution notes, optional commit/reference
- Validate resolution notes are provided
- Update issue status to "resolved"
- Append resolution event to `.aos/state/events.ndjson`
- Record resolution timestamp and user
- Update linked task status if applicable

#### Scenario: Mark issue as resolved fixed
- **GIVEN** open issue ISS-0001 where the bug has been fixed
- **WHEN** the user selects "fixed" resolution, enters notes "Fixed in commit abc123", and confirms
- **THEN** the issue status is updated to "resolved" with resolution details

#### Scenario: Mark issue as wont_fix
- **GIVEN** open issue ISS-0001 that is a feature request that won't be implemented
- **WHEN** the user selects "wont_fix" with explanation
- **THEN** the issue is marked resolved with wont_fix resolution type

### Requirement: Mark Deferred Action
The issue detail page SHALL provide a "Mark Deferred" action to defer an issue for later.

The implementation MUST:
- Provide "Mark Deferred" button for open issues
- Display deferral form with: deferral reason, target milestone/phase for reconsideration
- Update issue status to "deferred"
- Append deferral event to `.aos/state/events.ndjson`
- Record deferral timestamp
- Allow setting a reminder date for reconsideration

#### Scenario: Defer issue to later milestone
- **GIVEN** open issue ISS-0001 that should be addressed in Phase 2
- **WHEN** the user clicks "Mark Deferred", selects target milestone MS-0002, and confirms
- **THEN** the issue status is updated to "deferred" with target milestone reference

#### Scenario: Defer issue with reason
- **GIVEN** open issue ISS-0001 that is lower priority
- **WHEN** the user defers with reason "Low priority, address after core features"
- **THEN** the issue is marked deferred with the reason recorded

### Requirement: Issue Linking
The issue detail page SHALL support linking issues to tasks, phases, and milestones.

The implementation MUST:
- Provide "Link to Task" selector to associate issue with a task
- Provide "Link to Phase" selector to associate issue with a phase
- Provide "Link to Milestone" selector to associate issue with a milestone
- Update issue scope field with selected associations
- Display linked artifacts in the issue detail
- Allow unlinking from previously linked artifacts
- Persist link updates to `.aos/spec/issues/{issueId}.json`

#### Scenario: Link issue to task
- **GIVEN** issue ISS-0001 currently unlinked
- **WHEN** the user selects "Link to Task" and chooses TSK-000001
- **THEN** the issue is updated with taskId reference and displays the link

#### Scenario: Link issue to phase and milestone
- **GIVEN** issue ISS-0001
- **WHEN** the user links to phase PH-0001 and milestone MS-0001
- **THEN** the issue displays both phase and milestone links

### Requirement: Issue Status History
The issue detail page SHALL display the status history of an issue.

The implementation MUST:
- Read status change events from `.aos/state/events.ndjson` for the issue
- Display chronological list of status changes
- Show timestamp, old status, new status, and user for each change
- Display comments/notes associated with status changes
- Show current status prominently at top of page

#### Scenario: Display status history
- **GIVEN** issue ISS-0001 with history: created (open) → in_fix_planning → resolved
- **WHEN** the issue detail page displays the History section
- **THEN** all three status changes are shown chronologically with timestamps

