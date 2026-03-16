# Spec Delta: Command Suggestion Mode

Parent spec: `openspec/specs/intent-classification/spec.md`

## ADDED Requirements

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

## MODIFIED Requirements

### MODIFIED: Command Suggestion Mode (spec.md Requirement 5)

Original requirement text:
> The system SHALL provide a mode to suggest explicit commands for freeform inputs that appear workflow-related.

**MODIFIED to**:
> The system SHALL provide an opt-in mode (disabled by default) to suggest explicit commands for freeform inputs. When enabled, the system uses LLM analysis to propose commands with structured arguments, emits `command.suggested` events, and requires explicit user confirmation before execution. The feature gracefully degrades to normal chat behavior if the LLM is unavailable or produces low-confidence results.

#### Scenario: Natural language to command mapping
- **GIVEN** freeform input: "plan the foundation phase"
- **WHEN** the suggester analyzes with available phase context
- **THEN** it SHALL propose: `/plan --phase-id PH-0001` with reasoning and confidence

#### Scenario: Complex argument extraction
- **GIVEN** freeform input: "run the tests but skip the slow ones"
- **WHEN** the suggester analyzes
- **THEN** it SHALL propose: `/run --filter "!Slow"` or similar mapped arguments

## REMOVED Requirements

None.

## Cross-References

- Related to `streaming-dialogue-protocol` spec: `command.suggested` event type must align with streaming event schema
- Related to `orchestrator-event-emitter` spec: events must follow event emitter contracts
- Related to `orchestrator-workflow` spec: confirmation gate must handle suggested commands
- Depends on: LLM provider implementation (see Remediation.md Phase 4)

## Implementation Notes

1. **Performance**: LLM calls for suggestion should be fast (< 500ms). Consider caching common patterns.
2. **Privacy**: Input sent to LLM for suggestion should not include sensitive workspace data
3. **Extensibility**: `ICommandSuggester` allows alternative implementations (rule-based, local model, etc.)
4. **Telemetry**: Track suggestion acceptance rate to tune confidence thresholds
