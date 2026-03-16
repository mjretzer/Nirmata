# web-phases-page Specification

## Purpose

Defines the current implemented Phases UI in `nirmata.Web` for listing phases derived from the roadmap and inspecting phase details with planning entry points.

- **Lives in:** `nirmata.Web/Pages/Phases/*`, `.aos/spec/roadmap.json`, `.aos/state/state.json`
- **Owns:** UI routing/filtering and phase detail rendering
- **Does not own:** Canonical phase planning semantics or task execution behavior
## Requirements
### Requirement: Phases List Page
The `nirmata.Web` project SHALL provide a `/Phases` page that displays all phases with filtering and status.

The implementation MUST:
- Read phases from `.aos/spec/roadmap.json` and apply status/constraints from `.aos/state/state.json`
- Display phases in a table with columns: Phase ID, Name, Status, Milestone, Task Count
- Show status indicators (not_started, in_progress, blocked, done)
- Filter by milestone, status, or keyword search
- Link each phase to its detail page (`/Phases/Details/{phaseId}`)
- Order phases by sequence order within milestone

#### Scenario: Display phases list
- **GIVEN** a workspace with phases PH-0001, PH-0002, PH-0003
- **WHEN** a user navigates to `/Phases`
- **THEN** the page displays all phases with their status and milestone

#### Scenario: Filter phases by milestone
- **GIVEN** phases PH-0001 and PH-0002 belong to MS-0001, PH-0003 belongs to MS-0002
- **WHEN** the user filters by milestone MS-0001
- **THEN** only PH-0001 and PH-0002 are displayed

### Requirement: Phase Detail Page
The `nirmata.Web` project SHALL provide a `/Phases/Details` page with tabbed interface showing comprehensive phase information.

The implementation MUST:
- Accept phase ID via route parameter (`/Phases/Details/{phaseId}`)
- Display phase metadata: ID, Name, Description, Milestone ID, Sequence Order
- Provide tabs for: Overview, Goals/Outcomes, Assumptions, Research, Tasks, Constraints
- Display goals and expected outcomes in the Goals tab
- Display phase constraints pulled from state decisions/blockers
- Link to associated tasks and milestone
- Return HTTP 404 if phase not found

#### Scenario: Display phase details with tabs
- **GIVEN** phase PH-0001 with goals, assumptions, and research defined
- **WHEN** a user navigates to `/Phases/Details/PH-0001`
- **THEN** the page displays phase metadata and tabbed content

#### Scenario: Display phase constraints
- **GIVEN** state has decisions or blockers affecting PH-0001
- **WHEN** the Constraints tab is selected
- **THEN** the page displays relevant constraints from state

### Requirement: Assumptions Management
The phase detail page SHALL provide functionality to list and manage phase assumptions.

The implementation MUST:
- Display current assumptions in the Assumptions tab
- Provide "Add Assumption" form with description and validation status
- Persist assumptions to `.aos/spec/phases/{phaseId}/phase.json`
- Allow marking assumptions as validated or invalidated
- Display assumption validation status

#### Scenario: List assumptions for phase
- **GIVEN** phase PH-0001 has assumptions defined in its spec
- **WHEN** the Assumptions tab is selected
- **THEN** all assumptions are displayed with their validation status

#### Scenario: Add and persist assumption
- **GIVEN** the phase detail page for PH-0001
- **WHEN** the user adds a new assumption "Database schema will not change"
- **THEN** the assumption is persisted to `phase.json` and displayed in the list

### Requirement: Research Tracking
The phase detail page SHALL provide functionality to set and track research topics.

The implementation MUST:
- Display current research topics in the Research tab
- Provide "Set Research" form with topic and findings
- Persist research to `.aos/spec/phases/{phaseId}/phase.json`
- Support marking research as in_progress or completed
- Display research completion status

#### Scenario: Set research for phase
- **GIVEN** the phase detail page for PH-0001
- **WHEN** the user sets research topic "API integration patterns" with findings
- **THEN** the research is persisted to `phase.json` and displayed

#### Scenario: Display completed research
- **GIVEN** phase PH-0001 has completed research on "Database migration strategies"
- **WHEN** the Research tab is selected
- **THEN** the completed research is displayed with findings

### Requirement: Phase Planning Action
The phase detail page SHALL provide a "Plan Phase" action that generates tasks for the phase.

The implementation MUST:
- Provide "Plan Phase" button in the Overview or Tasks tab
- Trigger task generation workflow (creates 2-3 atomic tasks with plans)
- Display progress during task generation
- Show generated tasks after completion
- Link each generated task to its detail page
- Persist tasks to `.aos/spec/tasks/{taskId}/`

#### Scenario: Plan phase generates tasks
- **GIVEN** phase PH-0001 with no tasks defined
- **WHEN** the user clicks "Plan Phase"
- **THEN** 2-3 tasks (TSK-000001, TSK-000002, etc.) are generated with task.json and plan.json files

#### Scenario: Display generated tasks
- **GIVEN** task generation completed for PH-0001
- **WHEN** the Tasks tab is viewed
- **THEN** the newly generated tasks are listed with links to their detail pages

### Requirement: Phase Task Listing
The phase detail page SHALL display all tasks associated with the phase.

The implementation MUST:
- Read tasks from `.aos/spec/tasks/` where task.phaseId matches current phase
- Display tasks in the Tasks tab with status indicators
- Show task name, status, and brief description
- Link each task to `/Tasks/Details/{taskId}`
- Display empty state when no tasks exist with "Plan Phase" prompt

#### Scenario: Display phase tasks
- **GIVEN** phase PH-0001 has tasks TSK-000001 (pending) and TSK-000002 (completed)
- **WHEN** the Tasks tab is selected
- **THEN** both tasks are displayed with their status and links to details

