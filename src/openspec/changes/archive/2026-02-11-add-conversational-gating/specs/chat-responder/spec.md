## ADDED Requirements

### Requirement: Gate reasoning as conversational content

The chat responder MUST format `gate.selected` events as conversational "thinking" messages that help users understand the agent's decision-making process.

#### Scenario: Format gate reasoning as assistant message

- **GIVEN** a `gate.selected` event with reasoning and proposed action
- **WHEN** the chat responder processes the event
- **THEN** it formats a natural language message explaining:
  - Which phase was selected and why
  - What action is proposed
  - Whether confirmation is required
  - What the user should do next

#### Scenario: Gate explanation for planning phase

- **GIVEN** a `gate.selected` event with `targetPhase: Planner`
- **WHEN** formatting for chat display
- **THEN** message explains: "I've analyzed your request and determined we need to create a plan for the foundation phase. I found your project spec and roadmap, but no detailed task plan exists yet. I can generate one that includes file scopes and verification steps. Would you like me to proceed?"

#### Scenario: Gate explanation for execution phase

- **GIVEN** a `gate.selected` event with `targetPhase: Executor` and file modifications
- **WHEN** formatting for chat display
- **THEN** message includes warning about file modifications: "I'm ready to execute tasks that will modify files in the following scope: [list]. This is a write operation—please confirm you'd like me to proceed."

### Requirement: Confirmation prompt rendering

The chat responder MUST render confirmation prompts when `gate.selected` indicates `requiresConfirmation: true`.

#### Scenario: Render accept/reject buttons for confirmation

- **GIVEN** a `gate.selected` event with `requiresConfirmation: true`
- **WHEN** rendering in the chat UI
- **THEN** the responder displays:
  - The gate reasoning message
  - The proposed action details
  - An "Accept" button to confirm and proceed
  - A "Reject" button to cancel and suggest alternatives

#### Scenario: Handle confirmation timeout

- **GIVEN** a confirmation prompt displayed for more than 5 minutes
- **WHEN** the timeout expires without user response
- **THEN** the chat responder displays a timeout message
- **AND** offers to re-prompt or convert to chat-only mode

#### Scenario: Rejection with alternative suggestions

- **GIVEN** a user clicks "Reject" on a confirmation prompt
- **WHEN** processing the rejection
- **THEN** the chat responder displays alternative options:
  - "Would you like to discuss the plan first?"
  - "I can show you a read-only preview of what would change."
  - "Or we can chat about your goals."

### Requirement: Streaming gate events

The chat responder MUST stream `gate.selected` events as they arrive, treating them as intermediate "thinking" content before the final response.

#### Scenario: Stream gate reasoning tokens

- **GIVEN** an orchestration in progress
- **WHEN** the `gate.selected` event is emitted
- **THEN** the chat responder immediately streams the reasoning content
- **AND** displays a "thinking" indicator while waiting for confirmation or continuation

#### Scenario: No streaming for chat-only interactions

- **GIVEN** a chat-only interaction (no workflow intent)
- **WHEN** processing the response
- **THEN** no `gate.selected` event is emitted
- **AND** the response streams directly as `assistant.delta` events

## ADDED Requirements

### Requirement: Gate decision history

The chat responder SHALL maintain visibility of gate decisions within the conversation context so users can review previous routing decisions.

#### Scenario: Display gate history in conversation

- **GIVEN** multiple workflow interactions in a session
- **WHEN** viewing the conversation history
- **THEN** previous `gate.selected` reasoning is visible as system messages
- **AND** users can expand/collapse gate reasoning details
