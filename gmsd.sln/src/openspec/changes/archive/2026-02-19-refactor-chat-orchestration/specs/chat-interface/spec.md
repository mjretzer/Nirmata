## MODIFIED Requirements
### Requirement: Command Autocomplete

The chat input SHALL provide intelligent command suggestions as the user types, with conversation-first behavior.

#### Scenario: Slash command hints
- **GIVEN** the user types "/"
- **WHEN** the autocomplete triggers
- **THEN** a dropdown displays available commands with descriptions
- **AND** commands are filterable by typed text
- **AND** a tooltip indicates "Use / for commands, or just type to chat"

#### Scenario: Default chat behavior
- **GIVEN** the user types text without "/"
- **WHEN** the input is processed
- **THEN** the system treats it as conversational chat by default
- **AND** no command suggestions appear unless explicitly requested

#### Scenario: Command selection via keyboard
- **GIVEN** autocomplete dropdown is visible
- **WHEN** the user presses Arrow Down/Up
- **THEN** selection moves through suggestions
- **AND** Enter inserts the selected command

#### Scenario: Context-aware suggestions
- **GIVEN** a command requires arguments
- **WHEN** the user types the command
- **THEN** argument hints appear (e.g., available project names)
- **AND** hints are based on current workspace context

---

## ADDED Requirements
### Requirement: Command Proposal Display

The chat interface SHALL display structured command proposals from the AI assistant with clear confirmation controls.

#### Scenario: Command proposal card appears
- **GIVEN** the AI responder suggests a command action
- **WHEN** the response renders
- **THEN** a structured proposal card displays with command, rationale, and expected outcome
- **AND** Accept/Reject buttons are clearly visible
- **AND** the proposal includes schema validation indicator

#### Scenario: Command proposal acceptance
- **GIVEN** a command proposal is displayed
- **WHEN** the user clicks Accept
- **THEN** the proposed command is executed
- **AND** the execution results appear in the chat thread
- **AND** a system message confirms the action was taken

#### Scenario: Command proposal rejection
- **GIVEN** a command proposal is displayed
- **WHEN** the user clicks Reject
- **THEN** the proposal is dismissed
- **AND** the AI may provide an alternative suggestion or continue conversation
- **AND** the rejection is logged for telemetry

#### Scenario: Multiple command proposals
- **GIVEN** the AI suggests multiple possible actions
- **WHEN** the response renders
- **THEN** proposals appear as separate, clearly labeled cards
- **AND** the user can accept any single proposal
- **AND** accepting one proposal dismisses the others

---

### Requirement: Conversation Mode Indicators

The chat interface SHALL clearly indicate whether the current interaction is in chat or command mode.

#### Scenario: Mode indicator visible
- **GIVEN** any chat interaction
- **WHEN** the interface renders
- **THEN** a mode indicator shows "Chat" or "Command" prominently
- **AND** the indicator updates dynamically based on input type

#### Scenario: Chat mode visual styling
- **GIVEN** the system is in chat mode (no slash prefix)
- **WHEN** messages are displayed
- **THEN** chat messages use conversational styling
- **AND** the input field shows "Type to chat or use / for commands" placeholder

#### Scenario: Command mode visual styling
- **GIVEN** the user has typed a slash command
- **WHEN** the interface processes the command
- **THEN** command mode is indicated with distinct styling
- **AND** command execution results show with technical formatting

---

### Requirement: Mixed Conversation History

The chat interface SHALL maintain a single conversation thread that seamlessly mixes chat messages and command executions.

#### Scenario: Chat and command messages interleaved
- **GIVEN** a conversation with both chat and command interactions
- **WHEN** the message thread renders
- **THEN** both types appear in chronological order
- **AND** each message type has appropriate visual distinction
- **AND** the flow reads naturally as a single conversation

#### Scenario: Command execution in conversation context
- **GIVEN** an ongoing chat conversation
- **WHEN** a command is executed via proposal or direct input
- **THEN** the command result appears with context from the previous chat
- **AND** subsequent chat responses acknowledge the command outcome

#### Scenario: Conversation state persistence
- **GIVEN** a mixed conversation with chat and commands
- **WHEN** the page reloads or session restores
- **THEN** the entire conversation history is preserved
- **AND** both chat messages and command results are maintained
