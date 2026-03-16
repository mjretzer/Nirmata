# Tasks for implement-confirmation-gate

## Phase 1: Core Confirmation Gate Logic

1. **[x] Extend ConfirmationGate for Prerequisite Validation**
   - [x] Add `EvaluatePrerequisites` method to check workspace state
   - [x] Return `MissingPrerequisite` result with conversational recovery option
   - [x] Validate `.aos/spec`, `.aos/state`, `.aos/context` existence
   - Priority: High

2. **[x] Implement Destructiveness Analysis for Git Operations**
   - [x] Extend `DestructivenessAnalyzer` to detect git commit/push operations
   - [x] Add `RiskLevel.WriteDestructiveGit` category
   - [x] Require confirmation for any git mutating operations
   - Priority: High

3. **[x] Add Confidence Threshold Configuration**
   - [x] Implement configurable thresholds per operation type
   - [x] Support workspace-level overrides via `.aos/config`
   - [x] Add default thresholds: destructive=0.95, write=0.8, ambiguous=0.7
   - Priority: Medium

## Phase 2: Streaming Event Protocol

4. **[x] Define Confirmation Event Contracts**
   - [x] Create `ConfirmationRequestedEvent` with action description, risk level, timeout
   - [x] Create `ConfirmationRespondedEvent` with accept/reject status
   - [x] Add `ConfirmationTimeoutEvent` for expired confirmations
   - Priority: High

5. **[x] Integrate Events with EventEmitter**
   - [x] Wire confirmation gate to `IStreamingEventEmitter`
   - [x] Emit events at each confirmation state transition
   - [x] Include correlation ID for tracing
   - Priority: High

## Phase 3: Orchestrator Integration

6. **[x] Wire Confirmation Gate into Orchestrator Workflow**
   - [x] Add confirmation check before `run.started` event
   - [x] Pause execution loop when confirmation pending
   - [x] Resume on `confirmation.accepted` event
   - Priority: High

7. **[x] Implement Confirmation State Persistence**
   - [x] Store pending confirmations in `.aos/state/confirmations.json`
   - [x] Support resumability after orchestrator restart
   - [x] Clean up completed confirmations
   - Priority: Medium

8. **[x] Add Structured ProposedAction Output**
   - [x] Define `ProposedAction` schema for model output
   - [x] Validate action before confirmation request
   - [x] Include affected resources, estimated impact
   - Priority: Medium

## Phase 4: Testing & Validation

9. **[x] Unit Tests for Confirmation Gate**
   - [x] Test confidence threshold evaluation
   - [x] Test destructiveness analysis
   - [x] Test prerequisite validation
   - Priority: High

10. **[x] Integration Tests for Orchestrator Flow**
    - [x] Test end-to-end confirmation flow
    - [x] Test timeout handling
    - [x] Test resumability with pending confirmations
    - Priority: High

11. **[x] Validate with openspec validate**
    - [x] Run strict validation on all spec deltas
    - [x] Fix any schema or requirement issues
    - Priority: High

## Dependencies

- Requires: `streaming-dialogue-protocol` (for event contracts)
- Requires: `intent-classification` (for confidence scores)
- Related to: `agents-orchestrator-workflow` (for integration point)

## Parallel Work

- Tasks 1-3 can proceed in parallel (core logic)
- Tasks 4-5 can proceed in parallel (events)
- Task 6 depends on 1-5
- Task 7 depends on 6
- Task 8 can proceed in parallel with 6-7
- Tasks 9-11 depend on all implementation tasks
