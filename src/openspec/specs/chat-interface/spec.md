# chat-interface Specification

## Purpose

Defines the durable contract for $capabilityId in the nirmata platform.

- **Lives in:** See repo projects and `.aos/**` artifacts as applicable
- **Owns:** Capability-level contract and scenarios
- **Does not own:** Unrelated domain concerns outside this capability
## Requirements
### Requirement: Persistent Chat Input

The layout SHALL provide a chat input area that is always visible and accessible at the bottom of the screen.

#### Scenario: Chat input visible on all views
- **GIVEN** any page in the application
- **WHEN** the page renders
- **THEN** the chat input bar is visible at the bottom
- **AND** the input has focus indicator and placeholder text

#### Scenario: Chat input accepts text entry
- **GIVEN** the chat input is visible
- **WHEN** a user types text
- **THEN** the text appears in the input field
- **AND** the submit button enables when text is present

#### Scenario: Submit on Enter
- **GIVEN** text exists in the chat input
- **WHEN** the user presses Enter
- **THEN** the message is submitted
- **AND** the input clears

#### Scenario: Newline on Shift+Enter
- **GIVEN** the chat input has focus
- **WHEN** the user presses Shift+Enter
- **THEN** a newline is inserted at cursor position
- **AND** the message is NOT submitted

---

### Requirement: Message Thread Display

The chat interface SHALL display a scrollable message thread showing conversation history between the user and the AI assistant.

#### Scenario: Messages displayed with avatars
- **GIVEN** messages exist in the conversation
- **WHEN** the chat thread renders
- **THEN** each message displays with an avatar (User = "👤" or initials, AI = "🤖" or logo)
- **AND** user messages align to the right, AI messages to the left

#### Scenario: Message metadata visible
- **GIVEN** a message in the thread
- **WHEN** the message renders
- **THEN** the timestamp is displayed
- **AND** the author name ("You" or "nirmata") is visible

#### Scenario: Message actions available
- **GIVEN** a rendered message
- **WHEN** the user hovers over it
- **THEN** action buttons appear: Copy, Provide Feedback
- **AND** clicking Copy copies message text to clipboard

#### Scenario: Virtual scrolling for large histories
- **GIVEN** 100+ messages exist
- **WHEN** the chat thread renders
- **THEN** only visible messages are rendered (virtual scrolling)
- **AND** smooth scrolling performance is maintained

---

### Requirement: Rich Content Rendering

Chat messages SHALL support rich content types beyond plain text, rendered inline within the message thread.

#### Scenario: Table rendering in chat
- **GIVEN** an AI response contains tabular data
- **WHEN** the message renders
- **THEN** a styled table displays with headers and rows
- **AND** columns are sortable by clicking headers
- **AND** rows support selection and click actions

#### Scenario: Code block rendering
- **GIVEN** an AI response contains code
- **WHEN** the message renders
- **THEN** syntax-highlighted code block displays
- **AND** a "Copy" button is available
- **AND** language is auto-detected or specified

#### Scenario: Entity card rendering
- **GIVEN** an AI response references a Project, Run, or Task
- **WHEN** the message renders
- **THEN** an entity card displays with key properties
- **AND** the card links to detailed view in side panel

#### Scenario: Inline form rendering
- **GIVEN** an AI response requires user input
- **WHEN** the message renders
- **THEN** inline form inputs display (text, select, checkbox)
- **AND** submit button processes the form response

---

### Requirement: Streaming Response Support

The chat interface SHALL support streaming AI responses for real-time feedback during command execution.

#### Scenario: Streaming indicator displays
- **GIVEN** an AI command is in progress
- **WHEN** the response begins streaming
- **THEN** a "thinking" indicator appears
- **AND** content appears progressively as it streams

#### Scenario: Cancel in-flight request
- **GIVEN** a streaming response is active
- **WHEN** the user clicks the Cancel button
- **THEN** the request is aborted
- **AND** a partial response marker appears

#### Scenario: Complete streamed response
- **GIVEN** a streaming response completes
- **WHEN** the final chunk arrives
- **THEN** the thinking indicator disappears
- **AND** the full message is persisted to history

---

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

### Requirement: Conversation State Management

The chat interface SHALL maintain conversation state across interactions.

#### Scenario: History persists during session
- **GIVEN** messages were exchanged
- **WHEN** the user navigates between views
- **THEN** the conversation history is preserved
- **AND** scroll position is maintained

#### Scenario: Local storage backup
- **GIVEN** messages exist in the thread
- **WHEN** the page unloads
- **THEN** conversation is saved to localStorage
- **AND** restored on page reload (within same session)

#### Scenario: Clear conversation
- **GIVEN** a conversation with messages
- **WHEN** the user issues `/clear` or clicks Clear button
- **THEN** all messages are removed
- **AND** localStorage is cleared

---

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

