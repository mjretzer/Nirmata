# web-razor-pages — Specification

## ADDED Requirements

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
