# web-cross-cutting-components Specification

## Purpose
Define cross-cutting UI components that provide consistent UX patterns across all nirmata Web pages: global command palette, workspace context visibility, artifact linking, and toast notifications.

## ADDED Requirements

### Requirement: Global Command Palette
The nirmata.Web project SHALL provide a global command palette accessible via keyboard shortcut.

The implementation MUST:
- Open with Ctrl+K keyboard shortcut
- Display searchable list of commands: pages, artifacts, runs, and actions
- Support filtering by typing in the search box
- Navigate to selected item on click or Enter key
- Close on Escape key or clicking outside

#### Scenario: Open command palette with keyboard shortcut
- **GIVEN** any nirmata Web page is loaded
- **WHEN** a user presses Ctrl+K
- **THEN** the command palette overlay appears centered on screen

#### Scenario: Search commands
- **GIVEN** the command palette is open
- **WHEN** a user types "fix" in the search box
- **THEN** commands are filtered to show only those matching "fix"

#### Scenario: Navigate via command palette
- **GIVEN** the command palette is open and filtered
- **WHEN** a user clicks on "Fix Planning" or presses Enter with it selected
- **THEN** the browser navigates to /Fix and the palette closes

#### Scenario: Close command palette
- **GIVEN** the command palette is open
- **WHEN** a user presses Escape
- **THEN** the palette closes without navigation

---

### Requirement: Persistent Workspace Badge
The nirmata.Web project SHALL display a persistent workspace badge in the header showing current context.

The implementation MUST:
- Show the current workspace/repository path
- Display .aos health status (valid/invalid/missing)
- Update when workspace changes
- Show detailed info on hover

#### Scenario: Display workspace path
- **GIVEN** a workspace is selected
- **WHEN** any page loads
- **THEN** the header shows the workspace path (e.g., "nirmata")

#### Scenario: Show health status
- **GIVEN** the workspace has a valid .aos directory
- **WHEN** the badge renders
- **THEN** a green indicator shows "Healthy" status

#### Scenario: Show invalid status
- **GIVEN** the workspace .aos directory is corrupted
- **WHEN** the badge renders
- **THEN** a red indicator shows "Invalid" status

#### Scenario: Show missing status
- **GIVEN** no .aos directory exists
- **WHEN** the badge renders
- **THEN** a gray indicator shows "Not Initialized" status

#### Scenario: Hover for details
- **GIVEN** the workspace badge is displayed
- **WHEN** a user hovers over it
- **THEN** a tooltip shows the full workspace path and detailed status

---

### Requirement: Unified Artifact Link System
The nirmata.Web project SHALL automatically convert artifact references to clickable links.

The implementation MUST:
- Detect artifact prefixes: TSK (Task), PH (Phase), MS (Milestone), UAT (UAT item), RUN (Run)
- Convert plain text references to anchor tags linking to detail pages
- Maintain prefix badge styling (colored label + ID)

#### Scenario: Link TSK references
- **GIVEN** page content contains "TSK-123"
- **WHEN** the content renders
- **THEN** it appears as a clickable link to /Tasks/Details/123

#### Scenario: Link PH references
- **GIVEN** page content contains "PH-456"
- **WHEN** the content renders
- **THEN** it appears as a clickable link to /Phases/Details/456

#### Scenario: Link MS references
- **GIVEN** page content contains "MS-789"
- **WHEN** the content renders
- **THEN** it appears as a clickable link to /Milestones/Details/789

#### Scenario: Link UAT references
- **GIVEN** page content contains "UAT-001"
- **WHEN** the content renders
- **THEN** it appears as a clickable link to /Uat/Details/001

#### Scenario: Link RUN references
- **GIVEN** page content contains "RUN-abc123"
- **WHEN** the content renders
- **THEN** it appears as a clickable link to /Runs/Details/abc123

#### Scenario: Hover preview
- **GIVEN** an artifact link is displayed
- **WHEN** a user hovers over it
- **THEN** a tooltip shows the artifact title/name

---

### Requirement: Toast/Notification System
The nirmata.Web project SHALL provide a toast notification system for user feedback.

The implementation MUST:
- Support toast types: success (green), error (red), warning (yellow), info (blue)
- Display toasts in a fixed position (top-right corner)
- Auto-dismiss after a configurable duration (default 5 seconds)
- Allow manual dismiss via X button
- Support multiple simultaneous toasts
- Animate in/out smoothly

#### Scenario: Show success toast
- **GIVEN** a run completes successfully
- **WHEN** the system triggers a success notification
- **THEN** a green toast appears saying "Run completed successfully"

#### Scenario: Show error toast
- **GIVEN** a validation fails
- **WHEN** the system triggers an error notification
- **THEN** a red toast appears with the error message

#### Scenario: Show warning toast
- **GIVEN** a lock conflict is detected
- **WHEN** the system triggers a warning notification
- **THEN** a yellow toast appears saying "Workspace is locked by another process"

#### Scenario: Show info toast
- **GIVEN** a cache operation completes
- **WHEN** the system triggers an info notification
- **THEN** a blue toast appears saying "Cache cleared: 42 items removed"

#### Scenario: Auto-dismiss toast
- **GIVEN** a toast is displayed
- **WHEN** 5 seconds pass without interaction
- **THEN** the toast fades out and disappears

#### Scenario: Manual dismiss toast
- **GIVEN** a toast is displayed
- **WHEN** a user clicks the X button
- **THEN** the toast immediately disappears

#### Scenario: Multiple toasts
- **GIVEN** multiple events occur in quick succession
- **WHEN** toasts are triggered
- **THEN** they stack vertically with newest at top

#### Scenario: Server-side toast trigger
- **GIVEN** a Razor Page action completes
- **WHEN** the page redirects with a success message
- **THEN** a toast displays that message on the target page

---

### Requirement: Shared Styling Consistency
The nirmata.Web project SHALL ensure all cross-cutting components share consistent styling.

The implementation MUST:
- Use CSS variables for colors, spacing, and animations
- Follow existing site.css conventions
- Be responsive on mobile devices
- Respect dark mode preferences (if supported)

#### Scenario: Consistent theming
- **GIVEN** command palette, badge, links, and toasts are rendered
- **WHEN** viewed together
- **THEN** they share consistent colors, borders, and shadows

#### Scenario: Mobile responsive
- **GIVEN** the page is viewed on a mobile device
- **WHEN** components render
- **THEN** they adapt to smaller screens appropriately

