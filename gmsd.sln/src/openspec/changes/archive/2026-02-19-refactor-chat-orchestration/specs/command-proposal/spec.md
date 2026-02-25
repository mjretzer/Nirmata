## MODIFIED Requirements
### Requirement: Command Proposal Output Schema with Enhanced Chat Integration

The `LlmCommandSuggester` MUST produce a JSON object following a strict schema for every suggested next action, with enhanced fields for chat context and user experience.

#### Scenario: Valid Command Proposal Generation with Chat Context
- **Given** a conversation context and current workspace state
- **When** the chat responder proposes a next action
- **Then** it MUST return a JSON object containing:
    - `intent`: An object describing the proposed action
    - `command`: The suggested command string (e.g., `/execute`)
    - `group`: The command group (e.g., `run`)
    - `rationale`: A short explanation of why this action is chosen, referencing the conversation
    - `expectedOutcome`: What the agent expects to happen after execution
    - `conversationContext`: Brief reference to what the user was asking about
    - `confidenceLevel`: How confident the agent is this is the right action (high/medium/low)

#### Scenario: Multi-Option Command Proposals

- **GIVEN** a situation where multiple valid actions could be taken
- **WHEN** the chat responder generates suggestions
- **THEN** it MUST return an array of command proposals
- **AND** each proposal MUST include the enhanced schema fields
- **AND** proposals MUST be ordered by confidence level

#### Scenario: Fallback to Chat on Command Parsing Failure

- **GIVEN** input with a slash prefix (e.g., `/unknown-command`)
- **WHEN** the command parser fails to recognize it
- **THEN** the orchestrator SHALL route to the chat responder
- **AND** the responder SHALL inform the user the command was not recognized
- **AND** it SHALL suggest valid alternatives from the registry

---

### Requirement: Schema Validation for Enhanced Proposals

The engine MUST validate every command proposal against the enhanced `CommandProposal` schema before presenting it to the user or executing it.

#### Scenario: Enhanced Proposal Validation
- **Given** a command proposal with the new enhanced fields
- **When** schema validation is performed
- **Then** all required fields MUST be present and valid
- **And** optional fields MUST conform to their constraints (confidenceLevel enum, etc.)
- **And** conversationContext MUST be a non-empty string when provided

#### Scenario: Invalid Enhanced Proposal Rejection
- **Given** a command proposal that violates the enhanced schema
- **When** the orchestrator receives the proposal
- **Then** it MUST reject the proposal and log a system error
- **And** the chat responder MUST be notified to generate a new proposal

---

## ADDED Requirements
### Requirement: Command Proposal Confirmation Flow

The system SHALL implement a structured confirmation flow for command proposals generated from chat conversations.

#### Scenario: Proposal Presentation to User

- **GIVEN** a command proposal generated from chat
- **WHEN** the proposal is presented to the user
- **THEN** the proposal SHALL display with clear Accept/Reject controls
- **AND** the rationale SHALL be prominently featured
- **AND** the expected outcome SHALL be clearly explained
- **AND** the confidence level SHALL be indicated visually

#### Scenario: User Accepts Command Proposal

- **GIVEN** a displayed command proposal
- **WHEN** the user clicks Accept
- **THEN** the proposed command SHALL be executed immediately
- **AND** the execution SHALL follow traditional workflow routing
- **AND** the results SHALL be displayed in the conversation thread
- **AND** the proposal acceptance SHALL be logged for telemetry

#### Scenario: User Rejects Command Proposal

- **GIVEN** a displayed command proposal
- **WHEN** the user clicks Reject
- **THEN** the proposal SHALL be dismissed without execution
- **AND** the chat responder MAY provide an alternative suggestion
- **AND** the rejection SHALL be logged for improving future suggestions

#### Scenario: Proposal Timeout and Fallback

- **GIVEN** a command proposal displayed to the user
- **WHEN** no action is taken within a timeout period (e.g., 5 minutes)
- **THEN** the proposal SHALL be automatically dismissed
- **AND** the conversation SHALL continue without executing the command
- **AND** the timeout SHALL be logged as a non-action

---

### Requirement: Context-Aware Command Suggestion Logic

The chat responder SHALL use conversation context and workspace state to generate relevant command suggestions.

#### Scenario: Contextual Action Detection

- **GIVEN** a user message like "I think we need to update the authentication flow"
- **WHEN** the chat responder analyzes the message
- **THEN** it SHALL detect the implied need for planning or execution
- **AND** it SHALL suggest commands relevant to authentication work
- **AND** the suggestion SHALL reference the specific context (authentication)

#### Scenario: State-Based Suggestion Enhancement

- **GIVEN** a workspace where a phase plan is missing
- **WHEN** the user asks about next steps
- **THEN** command suggestions SHALL prioritize planning commands
- **AND** the rationale SHALL explain why planning is needed first
- **AND** alternative suggestions SHALL be provided if planning is not desired

#### Scenario: Learning from User Preferences

- **GIVEN** a history of accepted and rejected command proposals
- **WHEN** generating new suggestions
- **THEN** the system SHALL weight suggestions based on user patterns
- **AND** frequently rejected command types SHALL be deprioritized
- **AND** frequently accepted command types SHALL be prioritized

---

### Requirement: Command Proposal Telemetry and Improvement

The system SHALL collect telemetry on command proposal performance to improve suggestion quality over time.

#### Scenario: Proposal Effectiveness Tracking

- **GIVEN** command proposals are generated and presented
- **WHEN** users accept or reject proposals
- **THEN** acceptance rates SHALL be tracked per command type and context
- **AND** proposal-to-execution time SHALL be measured
- **AND** user satisfaction indicators SHALL be collected

#### Scenario: Suggestion Quality Metrics

- **GIVEN** collected telemetry data
- **WHEN** analyzing proposal performance
- **THEN** the system SHALL calculate quality metrics:
  - Acceptance rate by command type
  - Context relevance score
  - User feedback correlation
  - Execution success rate of accepted proposals

#### Scenario: Automated Suggestion Improvement

- **GIVEN** quality metrics showing poor performance for certain suggestion types
- **WHEN** the system detects patterns of low acceptance
- **THEN** suggestion logic SHALL be automatically adjusted
- **AND** low-performing suggestion patterns SHALL be deprioritized
- **AND** new patterns SHALL be tested based on successful interactions

#### Scenario: Handling Conflicting Commands in Suggestions

- **GIVEN** multiple suggested commands that perform mutually exclusive actions
- **WHEN** the user accepts one proposal
- **THEN** all other conflicting proposals in that set MUST be invalidated
- **AND** the UI MUST disable the action buttons for the invalidated proposals
