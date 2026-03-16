# layout-redesign Specification

## Purpose

Defines the durable contract for $capabilityId in the nirmata platform.

- **Lives in:** See repo projects and `.aos/**` artifacts as applicable
- **Owns:** Capability-level contract and scenarios
- **Does not own:** Unrelated domain concerns outside this capability
## Requirements
### Requirement: Three-Panel Layout Structure

The application SHALL use a three-panel layout with a collapsible left sidebar, main chat area, and collapsible right detail panel.

#### Scenario: Layout renders with all panels
- **GIVEN** the application loads
- **WHEN** the main layout renders
- **THEN** three panels are visible: left sidebar, center chat, right detail
- **AND** the chat area occupies remaining space between sidebars

#### Scenario: Left sidebar collapses
- **GIVEN** the left sidebar is expanded
- **WHEN** the user clicks the collapse button
- **THEN** the sidebar collapses to 60px width (icon-only mode)
- **AND** the chat area expands to fill space
- **AND** the collapse state persists across sessions

#### Scenario: Right panel collapses
- **GIVEN** the right detail panel is expanded
- **WHEN** the user clicks the collapse button
- **THEN** the panel collapses to 60px width (icon-only mode)
- **AND** the chat area expands to fill space
- **AND** the collapse state persists across sessions

#### Scenario: Both panels collapsed
- **GIVEN** both sidebars are collapsed
- **WHEN** the layout renders
- **THEN** the chat area occupies maximum width
- **AND** collapse buttons remain accessible

---

### Requirement: Persistent Chat Bar

The layout SHALL include a persistent chat input bar fixed at the bottom of the viewport.

#### Scenario: Chat bar always visible
- **GIVEN** any scroll position in the application
- **WHEN** the user scrolls through content
- **THEN** the chat bar remains fixed at bottom
- **AND** is always accessible for input

#### Scenario: Chat bar styling
- **GIVEN** the chat bar is rendered
- **WHEN** inspected
- **THEN** it has visual separation from content (border/shadow)
- **AND** matches application theme

#### Scenario: Responsive height
- **GIVEN** multi-line input in chat bar
- **WHEN** the user types multiple lines
- **THEN** the chat bar height expands up to max-height (200px)
- **AND** a scrollbar appears for overflow

---

### Requirement: Minimal Header

The layout SHALL have a minimal header bar showing essential information only.

#### Scenario: Header displays essential info
- **GIVEN** the application renders
- **WHEN** the header displays
- **THEN** it shows: Logo, Workspace indicator, User menu
- **AND** traditional navigation items are removed or moved to sidebar

#### Scenario: Workspace indicator in header
- **GIVEN** a workspace is selected
- **WHEN** the header renders
- **THEN** the workspace name/path is displayed
- **AND** clicking it opens workspace selector

---

### Requirement: Panel State Persistence

The layout SHALL remember panel states (collapsed/expanded) across page reloads.

#### Scenario: Sidebar state saved
- **GIVEN** the user collapses the left sidebar
- **WHEN** the page reloads
- **THEN** the sidebar remains collapsed
- **AND** the state is stored in localStorage

#### Scenario: Default state for new users
- **GIVEN** a first-time user (no stored state)
- **WHEN** the layout loads
- **THEN** left sidebar is expanded
- **AND** right panel is collapsed

---

### Requirement: Keyboard Navigation

The layout SHALL support keyboard navigation between panels and components.

#### Scenario: Focus chat input with shortcut
- **GIVEN** any focus position in the application
- **WHEN** the user presses `/` or `Cmd+K`
- **THEN** focus moves to chat input
- **AND** any selected text in chat input is cleared

#### Scenario: Toggle panels with shortcuts
- **GIVEN** the layout is focused
- **WHEN** the user presses `Cmd+1`
- **THEN** the left sidebar toggles (collapse/expand)
- **AND** `Cmd+2` toggles the right panel

#### Scenario: Escape clears focus
- **GIVEN** a panel or element has focus
- **WHEN** the user presses Escape
- **THEN** focus returns to chat input
- **AND** any active selections are cleared

---

### Requirement: CSS Grid Implementation

The layout SHALL use CSS Grid for the main layout structure.

#### Scenario: Grid layout defined
- **GIVEN** the layout CSS is inspected
- **WHEN** the grid properties are checked
- **THEN** the layout uses `display: grid`
- **AND** areas are defined: sidebar, chat, detail, input

#### Scenario: Minimum viewport support
- **GIVEN** the viewport is 1280x768
- **WHEN** the layout renders
- **THEN** all panels are usable without horizontal scroll
- **AND** minimum chat width is 500px

#### Scenario: Panel resize handles
- **GIVEN** panels are expanded
- **WHEN** the user drags resize handles (if implemented)
- **THEN** panel widths adjust within min/max constraints
- **AND** adjusted sizes persist (optional feature)

---

