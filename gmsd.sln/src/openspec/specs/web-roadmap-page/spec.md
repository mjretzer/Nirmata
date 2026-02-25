# web-roadmap-page Specification

## Purpose

Defines the current implemented Roadmap UI in `Gmsd.Web` for displaying and editing the roadmap timeline stored in `.aos/spec/roadmap.json`.

- **Lives in:** `Gmsd.Web/Pages/Roadmap/*`, `.aos/spec/roadmap.json`, `.aos/state/state.json`
- **Owns:** UI for viewing roadmap timeline and applying roadmap edit operations (add/insert/remove phases)
- **Does not own:** Canonical cursor/state semantics or phase/task planning logic
## Requirements
### Requirement: Roadmap List Page
The `Gmsd.Web` project SHALL provide a `/Roadmap` page that displays the current roadmap with milestones and phases in a timeline view.

The implementation MUST:
- Read roadmap data from `.aos/spec/roadmap.json`
- Display milestones with their associated phases in sequence order
- Show phase names, descriptions, and status indicators
- Link each phase to its detail page (`/Phases/Details/{phaseId}`)
- Display empty state when no roadmap exists

#### Scenario: Display roadmap timeline
- **GIVEN** a workspace with roadmap containing milestone MS-0001 and phases PH-0001, PH-0002, PH-0003
- **WHEN** a user navigates to `/Roadmap`
- **THEN** the page displays the timeline with MS-0001 and its three phases in order

#### Scenario: Empty state for no roadmap
- **GIVEN** a workspace with no roadmap.json
- **WHEN** a user navigates to `/Roadmap`
- **THEN** the page displays "No roadmap found" with guidance on generating a roadmap

### Requirement: Phase Management Controls
The roadmap page SHALL provide controls for inserting, removing, and renumbering phases.

The implementation MUST:
- Provide "Add Phase" button to insert new phases
- Support "Insert Before" and "Insert After" for existing phases
- Provide "Remove Phase" button with confirmation
- Auto-renumber phases after insertions or removals
- Validate that roadmap maintains at least one phase

#### Scenario: Insert phase after existing phase
- **GIVEN** the roadmap page is displaying phases PH-0001, PH-0002, PH-0003
- **WHEN** the user clicks "Insert After" on PH-0002
- **THEN** a new phase is inserted, phases are renumbered to PH-0001, PH-0002, PH-0003 (new), PH-0004 (was PH-0003)

#### Scenario: Remove phase with confirmation
- **GIVEN** the roadmap page is displaying multiple phases
- **WHEN** the user clicks "Remove" on a phase and confirms
- **THEN** the phase is removed and remaining phases are renumbered

### Requirement: Phase Entry Points
The roadmap page SHALL provide entry points for phase discussion and planning.

The implementation MUST:
- Provide "Discuss Phase" button linking to phase detail with notes section
- Provide "Plan Phase" button that triggers task generation for the phase
- Pass the phase ID to the target page/action

#### Scenario: Discuss phase entry point
- **GIVEN** the roadmap page displaying phase PH-0001
- **WHEN** the user clicks "Discuss Phase" on PH-0001
- **THEN** they navigate to `/Phases/Details/PH-0001` with notes section visible

#### Scenario: Plan phase entry point
- **GIVEN** the roadmap page displaying phase PH-0002
- **WHEN** the user clicks "Plan Phase" on PH-0002
- **THEN** the system triggers task generation for PH-0002 and redirects to `/Phases/Details/PH-0002`

### Requirement: Cursor Alignment Warnings
The roadmap page SHALL display warnings when the roadmap and state cursor are misaligned.

The implementation MUST:
- Read current cursor position from `.aos/state/state.json`
- Compare cursor phase ID against roadmap phases
- Display warning banner if cursor points to a phase not in roadmap
- Display warning if roadmap phases have been renumbered since cursor was set
- Provide "Sync Cursor" action to realign cursor with current roadmap

#### Scenario: Warning when cursor misaligned
- **GIVEN** cursor at PH-0005 but roadmap only has PH-0001, PH-0002, PH-0003
- **WHEN** the roadmap page loads
- **THEN** a warning banner displays "Cursor points to unknown phase PH-0005"

#### Scenario: Sync cursor action
- **GIVEN** a misaligned cursor warning is displayed
- **WHEN** the user clicks "Sync Cursor"
- **THEN** the cursor is updated to point to the first phase (PH-0001) and the warning disappears

