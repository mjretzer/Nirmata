# context-aware-ui — Specification

## Purpose
Enable the UI to automatically display relevant context, entities, and information in side panels based on the current conversation state and user intent.

---

## ADDED Requirements

### Requirement: Workspace Context Display

The left sidebar SHALL display current workspace context and status.

#### Scenario: Workspace status visible
- **GIVEN** a workspace is selected
- **WHEN** the left sidebar renders
- **THEN** workspace name and path are displayed
- **AND** workspace status indicator shows (valid, needs init, error)

#### Scenario: Workspace quick actions
- **GIVEN** a workspace is selected
- **WHEN** the workspace section is viewed
- **THEN** quick actions display: Validate, Clear Cache, Change Workspace
- **AND** clicking them executes the appropriate command

#### Scenario: No workspace selected state
- **GIVEN** no workspace is selected
- **WHEN** the sidebar renders
- **THEN** a prompt to select workspace is displayed
- **AND** a "Select Workspace" button opens the workspace picker

---

### Requirement: Recent Runs Section

The left sidebar SHALL display a list of recent runs for quick access.

#### Scenario: Recent runs listed
- **GIVEN** runs exist in the workspace
- **WHEN** the sidebar renders
- **THEN** the last 5 runs are listed with: ID, status, timestamp
- **AND** runs are sorted by recency (newest first)

#### Scenario: Click run to view details
- **GIVEN** a recent run is listed
- **WHEN** the user clicks on it
- **THEN** the run details open in the right detail panel
- **AND** a chat message acknowledges the selection

#### Scenario: Run status indicators
- **GIVEN** recent runs are displayed
- **WHEN** the list renders
- **THEN** each run shows a status icon/color (pending, running, complete, failed)
- **AND** running/pending runs are visually distinguished (animation/pulse)

---

### Requirement: Active Entity Detection

The system SHALL detect entity references in conversation and update the UI context.

#### Scenario: Detect entity mentions in chat
- **GIVEN** the user or AI mentions "run:abc-123" or "project MyProject"
- **WHEN** the message is processed
- **THEN** the system identifies the entity type and ID
- **AND** the entity is added to "current context"

#### Scenario: Auto-update detail panel
- **GIVEN** an entity is detected in conversation
- **WHEN** the entity is recognized
- **THEN** the detail panel updates to show entity information
- **AND** previous panel content is replaced (or tabbed)

#### Scenario: Multiple entities in context
- **GIVEN** multiple entities are mentioned in conversation
- **WHEN** the most recent entity is detected
- **THEN** the detail panel shows the most recent entity
- **AND** a "context stack" allows switching between recent entities

---

### Requirement: Detail Panel Tabs

The right detail panel SHALL support tabbed views for entity details.

#### Scenario: Properties tab
- **GIVEN** an entity is displayed in the detail panel
- **WHEN** the Properties tab is selected
- **THEN** key entity properties are displayed (name, status, dates, etc.)
- **AND** editable properties show edit controls (when applicable)

#### Scenario: Evidence tab
- **GIVEN** an entity has associated evidence
- **WHEN** the Evidence tab is selected
- **THEN** evidence files are listed with: name, type, timestamp
- **AND** clicking opens evidence in viewer or download

#### Scenario: Raw Data tab
- **GIVEN** an entity is displayed
- **WHEN** the Raw Data tab is selected
- **THEN** the raw JSON/state data is displayed
- **AND** syntax highlighting and copy button are available

#### Scenario: Related tab
- **GIVEN** an entity has related items (e.g., Run has Tasks)
- **WHEN** the Related tab is selected
- **THEN** related entities are listed with links
- **AND** clicking navigates to that entity in the panel

---

### Requirement: Smart Suggestions

The chat interface SHALL provide context-aware command suggestions.

#### Scenario: Suggest based on selected entity
- **GIVEN** a run is selected in the detail panel
- **WHEN** the chat input is focused
- **THEN** suggestions appear: "View tasks for this run", "Re-run", "Show logs"
- **AND** clicking a suggestion inserts the command

#### Scenario: Suggest based on workspace state
- **GIVEN** the workspace has validation issues
- **WHEN** the user opens chat
- **THEN** a suggestion appears: "Run validation to see issues"
- **AND** the suggestion is dismissible

#### Scenario: Empty state suggestions
- **GIVEN** a new conversation (no messages)
- **WHEN** the chat opens
- **THEN** suggested starter commands display
- **AND** suggestions are based on common first-time actions

---

### Requirement: Navigation History

The left sidebar SHALL maintain a navigation history of recently viewed entities.

#### Scenario: History tracks viewed entities
- **GIVEN** the user views multiple entities via chat or panel
- **WHEN** each entity is viewed
- **THEN** it is added to the navigation history
- **AND** history persists for the session

#### Scenario: Click history item
- **GIVEN** items exist in navigation history
- **WHEN** the user clicks a history item
- **THEN** that entity opens in the detail panel
- **AND** the chat shows "Viewing [entity]" context

#### Scenario: History limited size
- **GIVEN** more than 10 entities have been viewed
- **WHEN** the history renders
- **THEN** only the last 10 are shown
- **AND" older items are removed (FIFO)

---

### Requirement: Cross-Reference Highlighting

When an entity is selected, related references throughout the UI SHALL be highlighted.

#### Scenario: Highlight in chat
- **GIVEN** a project "MyProject" is selected in detail panel
- **WHEN** viewing chat history
- **THEN** previous mentions of "MyProject" are visually highlighted
- **AND" clicking a highlighted mention jumps to that message

#### Scenario: Highlight in context sidebar
- **GIVEN** a run is selected
- **WHEN** the sidebar renders recent runs
- **THEN** the selected run is highlighted in the list
- **AND" distinction between selected and non-selected is clear

---

## MODIFIED Requirements

### Requirement: Static Navigation (MODIFIED from web-razor-pages)

**Original**: Fixed navigation menu with all destinations  
**New**: Dynamic, context-aware navigation that adapts to conversation

The left sidebar navigation SHALL adapt dynamically based on conversation context instead of displaying a static, fixed menu.

#### Scenario: Navigation adapts to context
- **GIVEN** the user is discussing a specific run
- **WHEN** the sidebar navigation renders
- **THEN** relevant actions for runs are prioritized
- **AND" less relevant destinations are minimized or hidden

---
