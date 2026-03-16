## ADDED Requirements

### Requirement: Dashboard Overview Page

The `nirmata.Web` project SHALL provide a `/Dashboard` page that displays the current operational state of the AOS engine.

The implementation MUST:
- Display current cursor position from `state.json` (milestone, phase, task, step)
- Show "next recommended action" based on state machine rules
- List blockers and open issues summary from `spec/issues/` and state
- Provide quick action buttons: Validate, Checkpoint, Pause, Resume, Tail events
- Display latest run card(s) with pass/fail status and linked evidence artifacts
- Update content dynamically via HTMX polling (every 5 seconds when active)

#### Scenario: Display current cursor position
- **GIVEN** `state.json` contains `{ "milestone": "M1", "phase": "P1", "task": "T3", "step": 2 }`
- **WHEN** a user navigates to `/Dashboard`
- **THEN** the cursor card shows: "Milestone: Foundation, Phase: Setup, Task: Configure CI, Step: 2 of 5"

#### Scenario: Show next recommended action
- **GIVEN** current state has next action "execute"
- **WHEN** the dashboard loads
- **THEN** the "Next Recommended Action" section displays: "Execute task T3 — Ready to run"
- **AND** a "Go to Command" button links to `/Command`

#### Scenario: Display blockers summary
- **GIVEN** `spec/issues/open/` contains 3 blocker files
- **WHEN** the dashboard loads
- **THEN** the blockers section shows: "3 Open Blockers" with severity counts (1 critical, 2 warnings)
- **AND** each blocker title links to `/Specs/Issues?id={issueId}`

#### Scenario: Quick action buttons trigger operations
- **GIVEN** a workspace is loaded
- **WHEN** a user clicks the "Validate" button
- **THEN** an HTMX request validates the workspace
- **AND** the validation result appears inline without page reload

#### Scenario: Display latest run status
- **GIVEN** a completed run exists in `.aos/evidence/runs/RUN-001/`
- **WHEN** the dashboard loads
- **THEN** the latest run card shows: Run ID, Status (✓ Passed), Start/End times, Duration
- **AND** links to `/Runs/Details?id=RUN-001` and artifact files
