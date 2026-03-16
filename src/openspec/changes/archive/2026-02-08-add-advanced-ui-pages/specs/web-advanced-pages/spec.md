# web-advanced-pages Specification (Delta)

## ADDED Requirements

### Requirement: Fix Planning Page (Repair Loop)
The nirmata.Web project SHALL provide a `/Fix` page displaying the repair loop state and fix planning actions.

The implementation MUST:
- Display generated fix plan tasks (limited to a small, manageable set)
- Provide actions: Plan Fix, Execute Fix, Re-verify
- Show loop state clearly (verified-pass vs verified-fail)
- Link to related run and issue details

#### Scenario: Display fix plan tasks
- **GIVEN** a repair loop exists with generated fix tasks
- **WHEN** a user navigates to `/Fix`
- **THEN** the page displays the fix plan tasks with title, description, and status

#### Scenario: Trigger plan fix action
- **GIVEN** a failed verification exists
- **WHEN** a user clicks "Plan Fix"
- **THEN** the system generates a fix plan and displays the tasks

#### Scenario: Execute fix action
- **GIVEN** a fix plan exists
- **WHEN** a user clicks "Execute Fix"
- **THEN** the system executes the fix plan and updates task statuses

#### Scenario: Re-verify action
- **GIVEN** fixes have been executed
- **WHEN** a user clicks "Re-verify"
- **THEN** the system runs verification and updates loop state (pass/fail)

---

### Requirement: Codebase Intelligence Page
The nirmata.Web project SHALL provide a `/Codebase` page for viewing and triggering codebase intelligence operations.

The implementation MUST:
- Provide trigger buttons: Scan, Map Build, Symbols Build, Graph Build
- Display viewers for all intelligence artifacts: map, stack, architecture, structure, conventions, testing, integrations, concerns
- Show last built timestamp for each artifact
- Display validation status for the codebase pack

#### Scenario: Trigger codebase scan
- **GIVEN** the codebase page is loaded
- **WHEN** a user clicks "Scan"
- **THEN** the codebase mapper workflow executes and artifacts are generated

#### Scenario: View map artifact
- **GIVEN** a codebase pack exists with `map.json`
- **WHEN** a user navigates to `/Codebase` and clicks "View Map"
- **THEN** the map.json content is displayed in a formatted JSON viewer

#### Scenario: View stack artifact
- **GIVEN** a codebase pack exists with `stack.json`
- **WHEN** a user clicks "View Stack"
- **THEN** the stack.json content is displayed showing detected technology stack

#### Scenario: View architecture artifact
- **GIVEN** a codebase pack exists with `architecture.json`
- **WHEN** a user clicks "View Architecture"
- **THEN** the architecture.json content is displayed showing patterns and boundaries

#### Scenario: View symbols cache
- **GIVEN** a codebase pack exists with `cache/symbols.json`
- **WHEN** a user clicks "View Symbols"
- **THEN** the symbols.json content is displayed with searchable/filterable symbol list

#### Scenario: View file graph
- **GIVEN** a codebase pack exists with `cache/file-graph.json`
- **WHEN** a user clicks "View Graph"
- **THEN** the file-graph.json content is displayed showing project/file dependencies

#### Scenario: Display build timestamps
- **GIVEN** codebase artifacts exist
- **WHEN** the codebase page loads
- **THEN** each artifact shows its last modified timestamp

---

### Requirement: Context Packs Page
The nirmata.Web project SHALL provide a `/Context` page for managing context packs.

The implementation MUST:
- List context packs grouped by task/phase with budget size
- Provide actions: Build Pack, Show Pack, Diff Pack since RUN
- Display pack metadata (size, created, related task/phase)

#### Scenario: List context packs
- **GIVEN** context packs exist in `.aos/context/`
- **WHEN** a user navigates to `/Context`
- **THEN** packs are listed grouped by associated task or phase with size info

#### Scenario: Build context pack
- **GIVEN** a task or phase is selected
- **WHEN** a user clicks "Build Pack"
- **THEN** a context pack is generated and added to the list

#### Scenario: Show pack contents
- **GIVEN** a context pack exists
- **WHEN** a user clicks "Show Pack"
- **THEN** the pack contents are displayed in a structured viewer

#### Scenario: Diff pack since run
- **GIVEN** a context pack and a run ID
- **WHEN** a user clicks "Diff Pack"
- **THEN** differences between the pack and the run's state are displayed

---

### Requirement: State, Events & History Page
The nirmata.Web project SHALL provide a `/State` page for viewing operational state and event history.

The implementation MUST:
- Provide a `state.json` viewer showing cursor, decisions, blockers, gating signals
- Provide an events tail from `events.ndjson` with filtering by event type
- Provide a history summary viewer with run/task keyed entries

#### Scenario: View state.json
- **GIVEN** a workspace with `.aos/state/state.json`
- **WHEN** a user navigates to `/State`
- **THEN** the state.json content is displayed showing cursor, decisions, blockers, gating signals

#### Scenario: Tail events with filter
- **GIVEN** events exist in `.aos/state/events.ndjson`
- **WHEN** a user selects an event type filter and clicks "Tail Events"
- **THEN** matching events are displayed with timestamp, type, and payload summary

#### Scenario: View history summary
- **GIVEN** history entries exist for runs/tasks
- **WHEN** a user clicks "History"
- **THEN** a summary view shows run/task keyed entries with key milestones

---

### Requirement: Pause, Resume & Checkpoints Page
The nirmata.Web project SHALL provide a `/Checkpoints` page for managing run continuity.

The implementation MUST:
- Support pausing a run (creates `handoff.json` snapshot)
- Support resuming a run (validates alignment and continues)
- Provide checkpoint list/create/show/restore operations
- Display lock status and allow lock release

#### Scenario: Pause run creates handoff
- **GIVEN** a run is in progress
- **WHEN** a user clicks "Pause"
- **THEN** a `handoff.json` snapshot is created and the run pauses

#### Scenario: Resume run validates alignment
- **GIVEN** a paused run with handoff.json
- **WHEN** a user clicks "Resume"
- **THEN** alignment is validated and the run continues from the handoff point

#### Scenario: List checkpoints
- **GIVEN** checkpoints exist in `.aos/state/checkpoints/`
- **WHEN** a user navigates to `/Checkpoints`
- **THEN** all checkpoints are listed with ID, created date, and description

#### Scenario: Create checkpoint
- **GIVEN** a workspace with valid state
- **WHEN** a user clicks "Create Checkpoint"
- **THEN** a new checkpoint is created and appears in the list

#### Scenario: Show checkpoint
- **GIVEN** a checkpoint exists
- **WHEN** a user clicks "Show" on a checkpoint
- **THEN** the checkpoint metadata and state snapshot are displayed

#### Scenario: Restore checkpoint
- **GIVEN** a checkpoint exists
- **WHEN** a user clicks "Restore" on a checkpoint
- **THEN** the workspace state is restored from the checkpoint

#### Scenario: View lock status
- **GIVEN** a workspace with lock files
- **WHEN** the checkpoints page loads
- **THEN** current lock status is displayed with owner and timestamp

#### Scenario: Release lock
- **GIVEN** a lock is held
- **WHEN** a user clicks "Release Lock"
- **THEN** the lock is released with appropriate safety checks

---

### Requirement: Validation & Maintenance Page
The nirmata.Web project SHALL provide a `/Validation` page for workspace maintenance operations.

The implementation MUST:
- Provide buttons for: validate schemas, validate spec, validate state, validate evidence, validate codebase
- Provide "Repair Indexes" action for index corruption
- Provide cache clear/prune operations
- Display validation report artifacts with links to failing files

#### Scenario: Validate schemas
- **GIVEN** the validation page is loaded
- **WHEN** a user clicks "Validate Schemas"
- **THEN** all JSON schemas are validated and results displayed

#### Scenario: Validate spec
- **GIVEN** spec files exist in `.aos/spec/`
- **WHEN** a user clicks "Validate Spec"
- **THEN** spec integrity is validated and results displayed

#### Scenario: Validate state
- **GIVEN** state files exist in `.aos/state/`
- **WHEN** a user clicks "Validate State"
- **THEN** state.json and events.ndjson are validated and results displayed

#### Scenario: Validate evidence
- **GIVEN** evidence files exist in `.aos/evidence/`
- **WHEN** a user clicks "Validate Evidence"
- **THEN** evidence artifacts are validated and results displayed

#### Scenario: Validate codebase
- **GIVEN** codebase pack exists in `.aos/codebase/`
- **WHEN** a user clicks "Validate Codebase"
- **THEN** codebase intelligence pack is validated and results displayed

#### Scenario: Repair indexes
- **GIVEN** index corruption is detected
- **WHEN** a user clicks "Repair Indexes"
- **THEN** indexes are rebuilt and validation re-run

#### Scenario: Clear cache
- **GIVEN** cache exists in `.aos/cache/`
- **WHEN** a user clicks "Clear Cache"
- **THEN** cache entries are cleared with confirmation

#### Scenario: Prune cache
- **GIVEN** old cache entries exist
- **WHEN** a user clicks "Prune Cache"
- **THEN** stale cache entries are removed based on age criteria

#### Scenario: View validation report with file links
- **GIVEN** validation has been run with failures
- **WHEN** a user views the report
- **THEN** failing files are listed with clickable links to inspect

---

### Requirement: Navigation Integration
The nirmata.Web project SHALL update the shared layout to include navigation for all new advanced pages.

The implementation MUST:
- Add navigation links for: Codebase, Context, State, Checkpoints, Validation, Fix
- Group related pages logically in the navigation
- Highlight current page in navigation

#### Scenario: Navigation displays new pages
- **GIVEN** the shared layout renders
- **WHEN** any page loads
- **THEN** navigation includes links to all new advanced pages

#### Scenario: Current page highlighted
- **GIVEN** a user is on `/Codebase`
- **WHEN** the page renders
- **THEN** the Codebase navigation item is visually highlighted
