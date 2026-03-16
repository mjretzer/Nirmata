## ADDED Requirements

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

## MODIFIED Requirements

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
