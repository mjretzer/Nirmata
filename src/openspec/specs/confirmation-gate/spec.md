# confirmation-gate Specification

## Purpose

Defines the durable contract for $capabilityId in the nirmata platform.

- **Lives in:** See repo projects and `.aos/**` artifacts as applicable
- **Owns:** Capability-level contract and scenarios
- **Does not own:** Unrelated domain concerns outside this capability
## Requirements
### Requirement: Confirmation Gate Intercepts All Write Operations
The system SHALL evaluate every workflow transition with `SideEffect.Write` through the confirmation gate before dispatching to the target phase.

#### Scenario: Write operation triggers confirmation evaluation
- **GIVEN** a classified intent with `SideEffect.Write`
- **WHEN** the orchestrator routes to the gating engine
- **THEN** the confirmation gate SHALL evaluate the operation for confirmation requirements
- **AND** the operation SHALL NOT proceed until confirmation is satisfied

#### Scenario: Read-only operations bypass confirmation
- **GIVEN** a classified intent with `SideEffect.ReadOnly` or `SideEffect.None`
- **WHEN** the confirmation gate evaluates the operation
- **THEN** it SHALL return `CanProceed = true` without requiring confirmation

### Requirement: Confidence-Based Confirmation Threshold
The system SHALL require confirmation for write operations when the classification confidence falls below the configured threshold.

#### Scenario: Low confidence requires confirmation
- **GIVEN** a write operation with confidence `0.65` and threshold `0.8`
- **WHEN** the confirmation gate evaluates the operation
- **THEN** it SHALL return `RequiresConfirmation = true` with reason "Confidence below threshold"

#### Scenario: High confidence bypasses confirmation
- **GIVEN** a write operation with confidence `0.95` and threshold `0.8`
- **WHEN** the confirmation gate evaluates the operation
- **THEN** it SHALL return `CanProceed = true` (subject to destructiveness rules)

#### Scenario: Explicit command with low confidence still requires confirmation
- **GIVEN** an explicit `/command` prefix with write side effects and confidence `0.65`
- **WHEN** the confirmation gate evaluates the operation
- **THEN** it SHALL require confirmation despite the explicit command (destructiveness rules still apply)

### Requirement: Confirmation Request Contains Structured Action Description
The system SHALL provide a structured `ProposedAction` object that describes what will happen if confirmed.

#### Scenario: Confirmation request includes action details
- **GIVEN** a confirmation is required for an Executor phase dispatch
- **WHEN** the `confirmation.requested` event is emitted
- **THEN** it SHALL include:
  - `Phase`: The target workflow phase (e.g., "Executor")
  - `Description`: Human-readable description of the action
  - `RiskLevel`: The assessed risk level
  - `AffectedResources`: List of files/resources that will be modified
  - `Metadata`: Optional additional context

#### Scenario: Action description is validated
- **GIVEN** a `ProposedAction` with empty description
- **WHEN** the action is validated
- **THEN** the system SHALL reject the action and emit an error event

### Requirement: Confirmation State Persistence
The system SHALL persist pending confirmation state to support resumability.

#### Scenario: Pending confirmation stored in workspace state
- **GIVEN** a confirmation request with ID `conf-abc123`
- **WHEN** the request is generated
- **THEN** it SHALL be stored in `.aos/state/confirmations.json`
- **AND** include: ID, state, requestedAt, timeout, action details

#### Scenario: Confirmation state survives restart
- **GIVEN** a pending confirmation exists in `.aos/state/confirmations.json`
- **WHEN** the orchestrator restarts and loads state
- **THEN** it SHALL recognize the pending confirmation
- **AND** await the user response before proceeding

#### Scenario: Completed confirmation cleanup
- **GIVEN** a confirmation is accepted or rejected
- **WHEN** the response is processed
- **THEN** the pending confirmation SHALL be removed from state storage

### Requirement: Confirmation Timeout Handling
The system SHALL implement timeout handling for pending confirmations.

#### Scenario: Confirmation expires after timeout
- **GIVEN** a confirmation request with 5-minute timeout
- **WHEN** 5 minutes elapse without user response
- **THEN** the system SHALL:
  - Emit `confirmation.timeout` event
  - Cancel the associated run
  - Remove the pending confirmation from state

#### Scenario: No timeout means indefinite wait
- **GIVEN** a confirmation request with no timeout specified
- **WHEN** the user takes extended time to respond
- **THEN** the confirmation SHALL remain pending indefinitely

### Requirement: Duplicate Confirmation Prevention
The system SHALL prevent duplicate confirmation requests for the same action.

#### Scenario: Same action does not generate duplicate
- **GIVEN** a pending confirmation exists for "Execute plan at cursor X"
- **WHEN** the same action is re-evaluated
- **THEN** the system SHALL return the existing confirmation ID
- **AND** NOT generate a new confirmation request

### Requirement: User Can Reject Confirmation
The system SHALL support explicit rejection of confirmation requests with graceful fallback.

#### Scenario: User rejects confirmation
- **GIVEN** a pending confirmation request
- **WHEN** the user sends rejection response
- **THEN** the system SHALL:
  - Emit `confirmation.rejected` event
  - Cancel the associated run lifecycle
  - Fall back to chat response mode
  - Emit `assistant.final` with helpful message

#### Scenario: Rejection with user explanation
- **GIVEN** a pending confirmation request
- **WHEN** the user rejects with explanatory message
- **THEN** the rejection event SHALL include the user message
- **AND** the assistant response SHALL acknowledge the explanation

### Requirement: Confirmation Gate Complete Evaluation Workflow
The confirmation gate SHALL integrate ambiguous request detection, prerequisite validation, confidence threshold evaluation, destructiveness analysis, and ProposedAction structure validation into a unified confirmation evaluation workflow.

#### Scenario: Complete confirmation evaluation with ambiguity detection
- **GIVEN** the confirmation gate evaluates any write operation
- **WHEN** the evaluation proceeds
- **THEN** it SHALL first check for ambiguous request signals (vague verbs, missing context)
- **AND** validate workspace prerequisites
- **AND** evaluate the confidence threshold against configured values
- **AND** analyze the destructiveness of the proposed operation
- **AND** validate the ProposedAction structure before emitting confirmation request

#### Scenario: Ambiguity takes precedence in evaluation order
- **GIVEN** a write operation with both low confidence AND destructive side effects
- **WHEN** the confirmation gate evaluates
- **THEN** it SHALL prioritize the ambiguity reason in the confirmation request
- **AND** include both ambiguity explanation AND destructiveness warning

#### Scenario: Confirmation request with multiple reasons
- **GIVEN** a request that is both ambiguous AND destructive AND missing prerequisites
- **WHEN** the `confirmation.requested` event is emitted
- **THEN** it SHALL include a `Reasons` array with all applicable reasons
- **AND** present them in priority order: ambiguity, prerequisites, destructiveness, confidence

### Requirement: Ambiguous Request Detection Triggers Confirmation
The system SHALL detect ambiguous user requests and require confirmation before executing write operations, even when intent classification indicates a write operation.

#### Scenario: Low confidence with vague verbs requires confirmation
- **GIVEN** a user input "do something with the project"
- **AND** the intent classifier returns `SideEffect.Write` with confidence `0.72`
- **AND** the input contains vague verbs ("do", "something")
- **WHEN** the confirmation gate evaluates the request
- **THEN** it SHALL classify the request as ambiguous
- **AND** require confirmation with reason "Ambiguous request - please confirm the specific action"

#### Scenario: Missing context parameters creates ambiguity
- **GIVEN** a user input "run the plan"
- **AND** no phase-id or cursor context is provided
- **AND** multiple plans exist in the workspace
- **WHEN** the confirmation gate evaluates the request
- **THEN** it SHALL detect missing context as ambiguity
- **AND** emit `confirmation.requested` with clarification options

#### Scenario: Ambiguous request confirmation includes clarification options
- **GIVEN** an ambiguous request is detected
- **WHEN** the `confirmation.requested` event is emitted
- **THEN** it SHALL include:
  - `AmbiguityReason`: "vague_verbs", "missing_context", "low_confidence", or "multiple_matches"
  - `ClarificationOptions`: Array of possible interpretations
  - `SuggestedCommands`: Specific command alternatives to disambiguate

### Requirement: Confirmation Gate Blocks Run Creation Until Resolved
The system SHALL prevent run lifecycle creation until pending confirmations are resolved, ensuring no write operations execute without explicit user approval.

#### Scenario: Run creation blocked by pending confirmation
- **GIVEN** a write operation requires confirmation
- **AND** the confirmation gate emits `confirmation.requested`
- **WHEN** the orchestrator attempts to create a run
- **THEN** run creation SHALL be blocked
- **AND** the orchestrator SHALL await `confirmation.accepted` or `confirmation.rejected`

#### Scenario: Confirmation resolution allows run to proceed
- **GIVEN** a pending confirmation exists for a write operation
- **WHEN** the user sends acceptance response
- **AND** `confirmation.accepted` event is processed
- **THEN** the orchestrator SHALL proceed with run creation
- **AND** emit `run.started` with the confirmed action details

#### Scenario: Rejection cancels the operation without run creation
- **GIVEN** a pending confirmation exists
- **WHEN** the user sends rejection response
- **AND** `confirmation.rejected` event is processed
- **THEN** no run SHALL be created
- **AND** the system SHALL emit `assistant.final` with helpful alternatives

