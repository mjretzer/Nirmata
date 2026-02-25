# web-milestones-page Specification

## Purpose

Defines the current implemented Milestones UI in `Gmsd.Web` for viewing and creating milestones via the roadmap artifact and for showing milestone progress derived from state.

- **Lives in:** `Gmsd.Web/Pages/Milestones/*`, `.aos/spec/roadmap.json`, `.aos/state/state.json`
- **Owns:** UI routing and rendering for milestones + milestone creation UX
- **Does not own:** Canonical milestone/phase/task semantics (owned by engine planning artifacts)
## Requirements
### Requirement: Milestones List Page
The `Gmsd.Web` project SHALL provide a `/Milestones` page that displays all milestones with their status and phases.

The implementation MUST:
- Read milestones from `.aos/spec/roadmap.json`
- Display milestones in a table with columns: Milestone ID, Name, Status, Phase Count, Completion %
- Show status indicators (not_started, in_progress, blocked, done)
- Link each milestone to its detail page (`/Milestones/Details/{milestoneId}`)
- Support empty state when no milestones exist
- Order milestones by sequence order

#### Scenario: Display milestones list
- **GIVEN** a workspace with milestones MS-0001, MS-0002
- **WHEN** a user navigates to `/Milestones`
- **THEN** the page displays a table with both milestones, their names, status, and phase counts

#### Scenario: Empty state for no milestones
- **GIVEN** a workspace with no milestones defined
- **WHEN** a user navigates to `/Milestones`
- **THEN** the page displays "No milestones found" with guidance

### Requirement: Milestone Detail Page
The `Gmsd.Web` project SHALL provide a `/Milestones/Details` page that displays comprehensive information about a single milestone.

The implementation MUST:
- Accept milestone ID via route parameter (`/Milestones/Details/{milestoneId}`)
- Display milestone metadata: ID, Name, Description, Sequence Order
- Display completion criteria list
- Display all associated phases with their status
- Link each phase to its detail page (`/Phases/Details/{phaseId}`)
- Display overall milestone completion percentage
- Provide navigation back to milestones list
- Return HTTP 404 if milestone not found

#### Scenario: Display milestone details
- **GIVEN** a milestone MS-0001 with name "Initial Delivery" and phases PH-0001, PH-0002
- **WHEN** a user navigates to `/Milestones/Details/MS-0001`
- **THEN** the page displays the milestone name, description, completion criteria, and associated phases

#### Scenario: Handle missing milestone
- **GIVEN** no milestone exists with ID "nonexistent"
- **WHEN** a user navigates to `/Milestones/Details/nonexistent`
- **THEN** the page returns HTTP 404 with a friendly error message

### Requirement: Milestone Creation
The milestones page SHALL provide functionality to create new milestones.

The implementation MUST:
- Provide "New Milestone" button on the list page
- Open a form for milestone name, description, and completion criteria
- Write the new milestone into `.aos/spec/roadmap.json`
- Redirect to the new milestone's detail page

#### Scenario: Create new milestone
- **GIVEN** the milestones list page with existing MS-0001
- **WHEN** the user clicks "New Milestone", enters name "Phase 2 Delivery" and description
- **THEN** a new milestone is added to `roadmap.json` and the user is redirected to `/Milestones/Details/{milestoneId}`

### Requirement: Milestone Completion
The milestone detail page SHALL provide functionality to mark a milestone as complete.

The implementation MUST:
- Provide "Complete Milestone" button when milestone is in_progress
- Validate all completion criteria are met
- Validate all associated phases are done
- Display confirmation dialog with completion gate summary
- Update milestone status to "done" in spec
- Append `milestone.completed` event to `.aos/state/events.ndjson`
- Return to milestones list on success

#### Scenario: Complete milestone with validation
- **GIVEN** milestone MS-0001 with all phases completed and criteria met
- **WHEN** the user clicks "Complete Milestone"
- **THEN** a confirmation dialog shows the completion summary, and on confirm, the milestone is marked done

#### Scenario: Block completion when criteria not met
- **GIVEN** milestone MS-0001 with incomplete phases
- **WHEN** the user clicks "Complete Milestone"
- **THEN** an error message displays listing the unmet criteria and incomplete phases

### Requirement: Milestone Status Display
The milestones page SHALL display accurate status indicators based on phase completion.

The implementation MUST:
- Calculate status as "done" when all phases are done
- Calculate status as "in_progress" when at least one phase is in_progress or done
- Calculate status as "blocked" when current phase is blocked
- Calculate status as "not_started" when no phases have started
- Calculate completion percentage based on done phases / total phases

#### Scenario: Status reflects phase completion
- **GIVEN** milestone MS-0001 with phases PH-0001 (done), PH-0002 (in_progress), PH-0003 (not_started)
- **WHEN** the milestones list page displays
- **THEN** MS-0001 shows status "in_progress" and completion "33%"

