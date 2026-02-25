# web-razor-pages Specification

## Purpose

Defines the current implemented Razor Pages shell for `Gmsd.Web`, including layout/static assets and the baseline product-facing pages (Projects) plus engine-facing operational pages.

- **Lives in:** `Gmsd.Web/Pages/**`, `Gmsd.Web/wwwroot/**`
- **Owns:** Razor Pages composition, layout, and routing for the web UI
- **Does not own:** Engine/agent workflow semantics or AOS contract definitions
## Requirements
### Requirement: Razor Pages Infrastructure
The Gmsd.Web project SHALL provide Razor Pages infrastructure with proper view imports, layout templates, and static file support.

#### Scenario: View imports configured
- **WHEN** the application renders any Razor page
- **THEN** common namespaces (Gmsd.Data.Dto.Models.Projects, Gmsd.Services.Interfaces) are available without explicit @using directives

#### Scenario: Layout template renders
- **WHEN** any page loads
- **THEN** it uses the shared _Layout.cshtml with navigation header and footer

### Requirement: Static Asset Pipeline
The Gmsd.Web project SHALL serve static assets (CSS, JavaScript) from wwwroot with proper organization.

#### Scenario: CSS styling applied
- **WHEN** the homepage renders
- **THEN** site.css styles are applied to layout, navigation, and content areas

#### Scenario: JavaScript available
- **WHEN** pages load
- **THEN** site.js is included and any validation scripts work for future forms

### Requirement: Project List Page
The Gmsd.Web project SHALL provide a read-only list page displaying all projects.

#### Scenario: Projects displayed in table
- **GIVEN** projects exist in the database
- **WHEN** a user navigates to /Projects
- **THEN** the page displays a table with Project Name, Description, and Created Date columns

#### Scenario: Empty state shown
- **GIVEN** no projects exist in the database
- **WHEN** a user navigates to /Projects
- **THEN** the page displays a friendly empty state message

#### Scenario: Links to detail pages
- **GIVEN** the project list is displayed
- **WHEN** the page renders
- **THEN** each project row includes a link to its detail page (/Projects/Details/{id})

### Requirement: Project Detail Page
The Gmsd.Web project SHALL provide a read-only detail page for viewing a single project.

#### Scenario: Project details displayed
- **GIVEN** a project exists with ID "abc-123"
- **WHEN** a user navigates to /Projects/Details/abc-123
- **THEN** the page displays the project's Name, Description, CreatedAt, and UpdatedAt fields

#### Scenario: Not found handling
- **GIVEN** no project exists with ID "nonexistent"
- **WHEN** a user navigates to /Projects/Details/nonexistent
- **THEN** the service throws NotFoundException
- **AND** the page returns HTTP 404 status with a friendly error message

#### Scenario: Navigation back to list
- **GIVEN** a user is viewing a project detail page
- **WHEN** the page renders
- **THEN** a link back to the project list is visible

### Requirement: State & Events Page
The Gmsd.Web project SHALL provide a page for viewing workspace state and events.

#### Scenario: State snapshot displayed
- **GIVEN** a workspace is selected with existing state.json
- **WHEN** a user navigates to /State
- **THEN** the page displays the current state snapshot with JSON viewer

#### Scenario: Cursor signals displayed
- **GIVEN** state is loaded
- **WHEN** the page renders
- **THEN** cursor details (milestone, phase, task, step IDs and statuses) are visible

#### Scenario: Events tail with polling
- **GIVEN** events exist in events.ndjson
- **WHEN** the page loads with auto-refresh enabled
- **THEN** recent events display with HTMX polling or manual refresh

#### Scenario: Event filtering
- **GIVEN** events of multiple types exist
- **WHEN** a user selects a filter from the dropdown
- **THEN** only events matching the selected type are displayed

#### Scenario: History summary
- **GIVEN** multiple runs exist in event history
- **WHEN** the page renders
- **THEN** a summary table grouped by run/task is displayed

### Requirement: Checkpoints Page
The Gmsd.Web project SHALL provide a page for managing checkpoints and execution continuity.

#### Scenario: Checkpoint list displayed
- **GIVEN** checkpoints exist in the workspace
- **WHEN** a user navigates to /Checkpoints
- **THEN** all checkpoints are listed with ID, description, and timestamp

#### Scenario: Pause execution
- **GIVEN** execution is active
- **WHEN** a user clicks "Pause"
- **THEN** handoff.json snapshot is created

#### Scenario: Resume execution
- **GIVEN** a valid handoff.json exists
- **WHEN** a user clicks "Resume"
- **THEN** validation occurs before resuming execution

#### Scenario: Create checkpoint
- **GIVEN** a workspace is selected
- **WHEN** a user enters a description and clicks "Create Checkpoint"
- **THEN** a new checkpoint is created

#### Scenario: Restore checkpoint
- **GIVEN** a checkpoint exists
- **WHEN** a user clicks "Restore" and confirms
- **THEN** the checkpoint is restored

#### Scenario: Lock status displayed
- **GIVEN** a workspace has an active lock
- **WHEN** the page renders
- **THEN** lock owner and timestamp are visible

### Requirement: Validation & Maintenance Page
The Gmsd.Web project SHALL provide a page for workspace validation and maintenance.

#### Scenario: Validation buttons displayed
- **GIVEN** a workspace is selected
- **WHEN** a user navigates to /Validation
- **THEN** buttons for Schemas, Spec, State, Evidence, and Codebase validation are visible

#### Scenario: Run validation
- **GIVEN** validation buttons are visible
- **WHEN** a user clicks any validation button
- **THEN** the corresponding validation runs and results display

#### Scenario: Repair indexes
- **GIVEN** a workspace with missing or corrupted indexes
- **WHEN** a user clicks "Repair Indexes"
- **THEN** missing index files are recreated

#### Scenario: Cache management
- **GIVEN** a workspace with cache entries
- **WHEN** a user clicks "Clear Cache" or "Prune Cache"
- **THEN** cache entries are removed accordingly

#### Scenario: Validation report with issues
- **GIVEN** validation has run with issues
- **WHEN** the results display
- **THEN** collapsible sections show validation results with clickable links to offending files

### Requirement: Navigation Redesign

The application navigation SHALL be redesigned from a traditional top-nav bar to a chat-driven, context-aware navigation model.

#### Scenario: Navigation adapts to context
- **GIVEN** the user is discussing a specific run
- **WHEN** the sidebar navigation renders
- **THEN** relevant actions for runs are prioritized
- **AND** less relevant destinations are minimized or hidden

#### Scenario: Top nav items reduced
- **GIVEN** the new layout is active
- **WHEN** the header renders
- **THEN** top navigation shows: Home, Help (or is removed entirely)
- **AND** other navigation moves to chat or sidebar

#### Scenario: Sidebar quick navigation
- **GIVEN** the left sidebar is expanded
- **WHEN** the navigation section is viewed
- **THEN** common destinations are available as quick actions
- **AND** clicking them issues the appropriate chat command

