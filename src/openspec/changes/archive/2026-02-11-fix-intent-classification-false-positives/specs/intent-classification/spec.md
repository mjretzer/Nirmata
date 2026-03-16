## ADDED Requirements

### Requirement: Explicit Command Grammar
The system SHALL distinguish explicit commands from freeform chat via a structured prefix syntax.

#### Scenario: Command prefix detected
- **GIVEN** a user input starting with `/` followed by a command verb
- **WHEN** the classifier processes the input
- **THEN** it SHALL classify as `IntentKind.WorkflowCommand` with appropriate `SideEffect`

#### Scenario: Freeform chat default
- **GIVEN** a user input without a command prefix
- **WHEN** the classifier processes the input
- **THEN** it SHALL default to `IntentKind.Chat` with `SideEffect.None`

### Requirement: Command Parser Registry
The system SHALL provide a registry of supported commands with their expected side effects.

#### Scenario: Supported command lookup
- **GIVEN** a registry of commands (`/run`, `/plan`, `/status`, `/help`, `/verify`, `/fix`, `/pause`, `/resume`)
- **WHEN** the classifier matches a command prefix
- **THEN** it SHALL return the predefined `SideEffect` for that command

#### Scenario: Unknown command handling
- **GIVEN** an input with `/` prefix that does not match any registered command
- **WHEN** the classifier processes the input
- **THEN** it SHALL classify as `IntentKind.UnknownCommand` with `SideEffect.None` and suggest available commands

### Requirement: Confirmation Gate for Ambiguous Write Operations
The system SHALL require explicit confirmation before executing write operations that were not triggered by explicit commands.

#### Scenario: Ambiguous write requires confirmation
- **GIVEN** a classified intent with `SideEffect.Write` and confidence below threshold
- **WHEN** the orchestrator receives the intent
- **THEN** it SHALL emit a confirmation request event and await user approval before starting the run lifecycle

#### Scenario: Explicit command bypasses confirmation
- **GIVEN** a classified intent with `SideEffect.Write` from an explicit `/command` prefix
- **WHEN** the orchestrator receives the intent
- **THEN** it MAY proceed directly to run lifecycle (subject to destructive operation rules)

### Requirement: Classification Transparency
The system SHALL expose classification reasoning through the streaming event protocol.

#### Scenario: Intent classified event emitted
- **GIVEN** any user input processed by the classifier
- **WHEN** classification completes
- **THEN** the system SHALL emit an `intent.classified` event containing `kind`, `sideEffect`, `confidence`, and `reasoning`

### Requirement: Command Suggestion Mode
The system SHALL provide a mode to suggest explicit commands for freeform inputs that appear workflow-related.

#### Scenario: Natural language triggers suggestion
- **GIVEN** a freeform input containing workflow keywords ("plan the foundation phase")
- **WHEN** the classifier processes the input with suggestion mode enabled
- **THEN** it SHALL respond conversationally with a proposed command and request confirmation rather than auto-executing
