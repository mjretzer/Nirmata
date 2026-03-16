# intent-classification Specification

## Purpose

Defines the durable contract for $capabilityId in the nirmata platform.

- **Lives in:** See repo projects and `.aos/**` artifacts as applicable
- **Owns:** Capability-level contract and scenarios
- **Does not own:** Unrelated domain concerns outside this capability
## Requirements
### Requirement: Intent classification with chat/command detection

The system SHALL provide an `IInputClassifier` that distinguishes between chat conversations and explicit commands, routing each to appropriate handlers.

#### Scenario: Chat intent detection for natural conversation

- **GIVEN** user input without slash prefix (e.g., "What should I work on next?")
- **WHEN** `ClassifyAsync` is called
- **THEN** it SHALL return `IntentKind.Chat` with `SideEffect.None`
- **AND** the classification SHALL include confidence level for chat detection
- **AND** the result SHALL route to the chat responder

#### Scenario: Command intent detection for slash commands

- **GIVEN** user input with slash prefix (e.g., "/run authentication-phase")
- **WHEN** `ClassifyAsync` is called
- **THEN** it SHALL return appropriate `IntentKind` based on command type
- **AND** the `SideEffect` SHALL reflect the command's expected impact
- **AND** the result SHALL route to traditional workflow phases

#### Scenario: Ambiguous input handling

- **GIVEN** user input that could be either chat or command-like (e.g., "help")
- **WHEN** `ClassifyAsync` is called
- **THEN** it SHALL default to `IntentKind.Chat` for safety
- **AND** the chat responder SHALL provide help and suggest explicit commands
- **AND** the classification SHALL include ambiguity flag for UI hints

---

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

### Requirement: Enhanced classification context awareness

The input classifier SHALL use workspace context and conversation history to improve classification accuracy.

#### Scenario: Context-aware chat detection

- **GIVEN** a conversation history showing previous chat interactions
- **WHEN** new input is classified
- **THEN** the classifier SHALL consider conversation continuity
- **AND** it SHALL favor chat classification when following conversational patterns
- **AND** it SHALL maintain consistent interaction mode within conversations

#### Scenario: Workspace state influence on classification

- **GIVEN** a workspace state where specific actions are clearly needed
- **WHEN** ambiguous input is classified
- **THEN** the classifier SHALL consider workspace context in routing decisions
- **AND** it MAY suggest command proposals even for chat-classified input
- **AND** it SHALL provide reasoning for classification decisions

#### Scenario: Learning from classification corrections

- **GIVEN** user feedback indicating incorrect classification
- **WHEN** similar input is encountered in future
- **THEN** the classifier SHALL adjust its patterns based on corrections
- **AND** it SHALL maintain user preference profiles for classification tendencies
- **AND** it SHALL improve accuracy over time through feedback

---

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

### Requirement: Command Suggestion Service Contract

The system SHALL provide a service contract for analyzing freeform input and suggesting explicit commands.

#### Scenario: Service interface defined
- **GIVEN** the need to extend classification with natural language understanding
- **WHEN** developers implement command suggestion
- **THEN** they SHALL implement `ICommandSuggester` with `SuggestAsync(string input)` returning `CommandProposal?`

#### Scenario: Structured proposal output
- **GIVEN** a valid command suggestion
- **WHEN** the suggester analyzes input
- **THEN** it SHALL return `CommandProposal` containing:
  - `CommandName`: the suggested command (e.g., "plan")
  - `Arguments`: mapped arguments as key-value pairs
  - `Confidence`: 0.0 to 1.0 score
  - `Reasoning`: explanation of why this command fits
  - `FormattedCommand`: full command string ready for execution (e.g., "/plan --phase-id PH-0001")

### Requirement: LLM-Based Command Analysis

The system SHALL use an LLM to perform natural language analysis for command suggestion.

#### Scenario: Prompt includes available commands
- **GIVEN** the LLM suggestion prompt
- **WHEN** sent to the provider
- **THEN** it SHALL include:
  - All registered commands with descriptions and examples
  - Response schema definition (structured output)
  - Confidence threshold guidance
  - Instructions to return null/empty for non-command input

#### Scenario: Structured output parsing
- **GIVEN** an LLM response for command suggestion
- **WHEN** the suggester processes it
- **THEN** it SHALL parse JSON matching the defined schema and map to `CommandProposal`

#### Scenario: Low confidence handling
- **GIVEN** an LLM response with confidence below threshold (default 0.7)
- **WHEN** the suggester evaluates it
- **THEN** it SHALL return null (no suggestion) rather than a low-confidence proposal

### Requirement: Suggestion Mode Integration

The system SHALL integrate suggestion mode into the classification pipeline as an optional second stage.

#### Scenario: Opt-in via configuration
- **GIVEN** the `InputClassifier` configuration
- **WHEN** `EnableSuggestionMode` is set to true
- **THEN** the classifier SHALL invoke `ICommandSuggester` for freeform chat inputs

#### Scenario: Suggestion bypasses on explicit commands
- **GIVEN** user input with explicit `/command` prefix
- **WHEN** the classifier processes it
- **THEN** it SHALL skip suggestion mode and use direct command parsing

#### Scenario: Suggestion bypasses on small talk
- **GIVEN** input classified as `IntentKind.SmallTalk` (greetings, thanks, etc.)
- **WHEN** the classifier evaluates suggestion mode
- **THEN** it SHALL skip suggestion to avoid annoying users in casual conversation

### Requirement: Command Suggested Event

The system SHALL emit a streaming event when a command suggestion is generated.

#### Scenario: Event emitted on suggestion
- **GIVEN** a successful command suggestion (confidence >= threshold)
- **WHEN** the suggester returns a proposal
- **THEN** the system SHALL emit `command.suggested` event containing:
  - Full `CommandProposal` details
  - Original user input
  - Timestamp
  - Suggestion source identifier

#### Scenario: Event not emitted for chat
- **GIVEN** freeform input that does not trigger a suggestion
- **WHEN** classification completes as chat
- **THEN** no `command.suggested` event SHALL be emitted

### Requirement: Suggestion Confirmation Flow

The system SHALL require explicit user confirmation before executing a suggested command.

#### Scenario: Confirmation requested for suggestion
- **GIVEN** a `CommandProposal` from suggestion mode
- **WHEN** the orchestrator routes the intent
- **THEN** it SHALL emit `confirmation.requested` event with:
  - Proposal details displayed to user
  - Accept/Reject options
  - Warning that this originated from natural language interpretation

#### Scenario: Explicit commands bypass suggestion confirmation
- **GIVEN** an explicit `/command` with `SideEffect.Write`
- **WHEN** the orchestrator evaluates confirmation
- **THEN** it SHALL use standard confirmation rules (not suggestion-specific flow)

#### Scenario: User accepts suggestion
- **GIVEN** a pending command suggestion confirmation
- **WHEN** the user confirms
- **THEN** the system SHALL:
  - Emit `confirmation.accepted` event
  - Execute the suggested command with parsed arguments
  - Proceed through normal run lifecycle

#### Scenario: User rejects suggestion
- **GIVEN** a pending command suggestion confirmation
- **WHEN** the user rejects
- **THEN** the system SHALL:
  - Emit `confirmation.rejected` event
  - Fall back to chat response (treat original input as freeform)
  - Emit `assistant.final` with helpful response

### Requirement: Suggestion Mode Safety

The system SHALL implement safety measures for LLM-based command suggestion.

#### Scenario: LLM unavailable fallback
- **GIVEN** suggestion mode is enabled but LLM provider is unavailable
- **WHEN** the suggester attempts to analyze input
- **THEN** it SHALL gracefully fall back to normal chat intent (no error to user)

#### Scenario: Malformed proposal rejection
- **GIVEN** an LLM response that does not match expected schema
- **WHEN** the suggester attempts to parse it
- **THEN** it SHALL log the issue and return null (no suggestion)

#### Scenario: Unknown command in proposal
- **GIVEN** a proposal suggesting a command not in `CommandRegistry`
- **WHEN** the suggester validates the proposal
- **THEN** it SHALL reject the proposal and return null

#### Scenario: Maximum suggestion rate limiting
- **GIVEN** rapid successive chat inputs
- **WHEN** the suggester is invoked
- **THEN** it SHALL skip LLM calls for inputs that are clearly conversational (short, no keywords)

### Requirement: Command parsing integration for precise classification

The input classifier SHALL integrate with the command parser to provide precise command detection and validation.

#### Scenario: Command validation during classification

- **GIVEN** input identified as potential command (slash prefix)
- **WHEN** classification is performed
- **THEN** the command SHALL be validated against the command registry
- **AND** valid commands SHALL be classified with specific intent kinds
- **AND** invalid commands SHALL be classified as chat with help suggestions

#### Scenario: Command argument parsing for intent refinement

- **GIVEN** a valid command with arguments (e.g., "/run --phase authentication")
- **WHEN** classification occurs
- **THEN** the arguments SHALL be parsed and validated
- **AND** the intent SHALL include the parsed argument structure
- **AND** the side effects SHALL be calculated based on command and arguments

#### Scenario: Command help and suggestion integration

- **GIVEN** input classified as chat but containing command-like keywords
- **WHEN** classification is complete
- **THEN** the result SHALL include suggested commands
- **AND** the suggestions SHALL be contextually relevant to the input
- **AND** the suggestions SHALL be formatted for easy user selection

---

### Requirement: Conversation flow classification

The input classifier SHALL maintain awareness of conversation flow to provide consistent interaction modes.

#### Scenario: Conversation mode persistence

- **GIVEN** an ongoing conversation in chat mode
- **WHEN** new input is received
- **THEN** the classifier SHALL prefer maintaining chat mode
- **AND** it SHALL only switch to command mode for explicit slash commands
- **AND** it SHALL provide smooth transitions when mode changes are needed

#### Scenario: Mode switching detection

- **GIVEN** a conversation currently in chat mode
- **WHEN** the user types an explicit slash command
- **THEN** the classifier SHALL detect the intentional mode switch
- **AND** it SHALL route to command execution while maintaining conversation context
- **AND** it SHALL enable smooth return to chat mode after command completion

#### Scenario: Mixed interaction support

- **GIVEN** a conversation that includes both chat and command interactions
- **WHEN** classifying new input
- **THEN** the classifier SHALL consider the mixed interaction history
- **AND** it SHALL provide appropriate routing for each input type
- **AND** it SHALL maintain conversation coherence across mode switches

