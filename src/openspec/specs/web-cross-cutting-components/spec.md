# Web Cross-Cutting Components Specification

## Purpose

The Web Cross-Cutting Components capability provides global UI components that deliver consistent UX patterns across all nirmata Web pages, including command palette navigation, workspace status display, artifact linking, and toast notifications.

## Requirements

### Requirement: Global Command Palette

The system SHALL provide a keyboard-driven command palette accessible via Ctrl+K (or Cmd+K on Mac) that enables quick navigation to any page, artifact, or command.

The command palette MUST:
- Open when the user presses Ctrl+K (or Cmd+K)
- Display searchable commands across categories: Pages, Commands, Artifacts, Runs
- Support arrow key navigation (Up/Down) through results
- Execute selected command on Enter key
- Close on Escape key or when clicking outside
- Filter results as the user types

#### Scenario: User opens command palette with keyboard shortcut
- **GIVEN** the user is on any nirmata Web page
- **WHEN** they press Ctrl+K
- **THEN** the command palette overlay appears with a search input

#### Scenario: User searches and executes a command
- **GIVEN** the command palette is open
- **WHEN** the user types "status" and presses Enter on the "Show Status" command
- **THEN** the palette closes and the status page/action is executed

#### Scenario: User navigates with arrow keys
- **GIVEN** the command palette is open with multiple results
- **WHEN** the user presses Arrow Down twice then Enter
- **THEN** the third item in the list is executed

### Requirement: Persistent Workspace Badge

The system SHALL display the current workspace path and .aos health status in the page header.

The workspace badge MUST:
- Show the repository path from workspace configuration
- Display a health indicator: valid (green), invalid (red), or missing (gray)
- Provide a tooltip with full path and health details
- Truncate long paths for display
- Hide on mobile viewports (< 768px)

#### Scenario: Valid workspace displays green indicator
- **GIVEN** a workspace is selected with a valid .aos directory
- **WHEN** the page header renders
- **THEN** the workspace badge shows the path with a green health indicator

#### Scenario: Missing workspace displays gray indicator
- **GIVEN** no workspace is selected
- **WHEN** the page header renders
- **THEN** the workspace badge shows a gray indicator and prompts to select workspace

#### Scenario: Invalid workspace displays red indicator
- **GIVEN** a workspace with .aos directory but missing required subdirectories
- **WHEN** the page header renders
- **THEN** the workspace badge shows the path with a red health indicator

### Requirement: Unified Artifact Link System

The system SHALL automatically convert artifact references (TSK-123, PH-5, MS-1, UAT-1, RUN-abc) to clickable links.

The artifact link system MUST:
- Detect patterns: TSK-*, PH-*, MS-*, UAT-*, RUN-* using regex `\b(TSK|PH|MS|UAT|RUN)-([\w-]+)\b`
- Render color-coded prefix badges for each artifact type
- Link to the appropriate Details pages (/Tasks/Details/{id}, /Phases/Details/{id}, etc.)
- Process dynamic content via MutationObserver
- Skip code blocks and preformatted text

#### Scenario: Artifact reference renders as clickable link
- **GIVEN** page content contains "TSK-123"
- **WHEN** the page renders
- **THEN** "TSK-123" appears as a blue clickable badge linking to /Tasks/Details/TSK-123

#### Scenario: Multiple artifact types have distinct colors
- **GIVEN** content contains "TSK-1 PH-1 MS-1 UAT-1 RUN-abc"
- **WHEN** the page renders
- **THEN** each artifact has a distinct color: TSK blue, PH purple, MS orange, UAT green, RUN red

#### Scenario: Artifact links in dynamic content are processed
- **GIVEN** new content is added to the page via AJAX containing "PH-5"
- **WHEN** the MutationObserver detects the change
- **THEN** "PH-5" is automatically converted to a clickable link

### Requirement: Toast Notification System

The system SHALL provide toast notifications for user feedback on validation failures, run completion, and other events.

The toast system MUST:
- Support types: success, error, warning, info
- Auto-dismiss after a configurable timeout (default 5 seconds)
- Provide manual dismiss via X button
- Accept server-side triggers via TempData
- Provide client-side JavaScript API
- Display in top-right position (full width on mobile, max 400px on desktop)

#### Scenario: Server-side toast displays on page load
- **GIVEN** a PageModel calls `ToastSuccess("Operation complete")`
- **WHEN** the page renders
- **THEN** a success toast appears in the top-right corner

#### Scenario: Client-side toast displays via API
- **GIVEN** JavaScript calls `window.nirmataToasts.error("Connection failed")`
- **WHEN** the function executes
- **THEN** an error toast appears with the message

#### Scenario: Toast auto-dismisses after timeout
- **GIVEN** a toast is displayed with default settings
- **WHEN** 5 seconds elapse
- **THEN** the toast automatically fades out and removes itself

#### Scenario: User manually dismisses toast
- **GIVEN** a toast is visible with an X button
- **WHEN** the user clicks the X button
- **THEN** the toast immediately disappears

## Implementation Notes

### Files
- `nirmata.Web/wwwroot/js/command-palette.js`
- `nirmata.Web/Pages/Shared/_WorkspaceBadge.cshtml`
- `nirmata.Web/wwwroot/js/artifact-links.js`
- `nirmata.Web/wwwroot/js/toasts.js`
- `nirmata.Web/Helpers/ToastHelper.cs`
- `nirmata.Web/wwwroot/css/site.css` (component styles)

### CSS Variables
```css
:root {
  --color-primary: #3498db;
  --color-success: #27ae60;
  --color-error: #e74c3c;
  --color-warning: #f39c12;
  --color-info: #3498db;
  --artifact-tsk: #3498db;
  --artifact-ph: #9b59b6;
  --artifact-ms: #e67e22;
  --artifact-uat: #27ae60;
  --artifact-run: #e74c3c;
  --z-palette: 1000;
  --z-toast: 1100;
}
```

### Integration
All components auto-initialize in `_Layout.cshtml`.

### Responsive Behavior
- **Command Palette**: Full width on mobile, max 600px on desktop
- **Workspace Badge**: Hidden on mobile (< 768px)
- **Toasts**: Full width on mobile, max 400px on desktop
