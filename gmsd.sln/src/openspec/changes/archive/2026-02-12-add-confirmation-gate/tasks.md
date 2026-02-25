## 1. Gating Engine Integration
- [x] 1.1 Create `ConfirmationGateEvaluator` class in `Gmsd.Agents` gating namespace
- [x] 1.2 Integrate evaluator into `GatingEngine.EvaluateAsync` workflow
- [x] 1.3 Add configuration binding for `ConfirmationOptions` (thresholds, timeouts)
- [x] 1.4 Ensure evaluator runs after intent classification but before phase dispatch

## 2. Ambiguous Request Detection
- [x] 2.1 Define ambiguity signals in `IntentClassificationResult` (low confidence + vague verbs + missing context)
- [x] 2.2 Implement `AmbiguityAnalyzer` to detect fuzzy user intent
- [x] 2.3 Add unit tests for ambiguity detection scenarios
- [x] 2.4 Integrate ambiguity check into confirmation gate flow

## 3. Destructive Operation Integration
- [x] 3.1 Wire up `DestructiveOperationDetector` to confirmation gate
- [x] 3.2 Map destructiveness levels to confirmation requirements (`WriteDestructive` = always confirm)
- [x] 3.3 Add support for git operation destructiveness detection
- [x] 3.4 Add file system scope change detection for destructive classification

## 4. Prerequisite Validation with Conversational Recovery
- [x] 4.1 Create `PrerequisiteValidator` that checks `.aos/spec/`, `.aos/state/` requirements
- [x] 4.2 Implement conversational fallback when prerequisites missing (ask don't fail)
- [x] 4.3 Add structured recovery actions to `assistant.final` events
- [x] 4.4 Support bootstrap suggestions for uninitialized workspaces

## 5. Structured ProposedAction Output
- [x] 5.1 Define `ProposedAction` schema with validation attributes
- [x] 5.2 Implement LLM structured output forcing for action proposals
- [x] 5.3 Add server-side validation for `ProposedAction` completeness
- [x] 5.4 Include `AffectedResources`, `RiskLevel`, `Description` in all proposals

## 6. Event Streaming Protocol
- [x] 6.1 Add `confirmation.requested` event type with action details
- [x] 6.2 Add `confirmation.accepted` event type with timestamp
- [x] 6.3 Add `confirmation.rejected` event type with optional user message
- [x] 6.4 Add `confirmation.timeout` event type with cancellation reason
- [x] 6.5 Update `OrchestratorEventEmitter` to emit confirmation lifecycle events

## 7. State Persistence
- [x] 7.1 Create `ConfirmationState` model for `.aos/state/confirmations.json`
- [x] 7.2 Implement `IConfirmationStateStore` with read/write operations
- [x] 7.3 Add duplicate detection logic (same action = same confirmation ID)
- [x] 7.4 Add timeout cleanup for expired confirmations

## 8. UI Integration
- [x] 8.1 Update UI event renderer to display confirmation prompts
- [x] 8.2 Add accept/reject UI controls for pending confirmations
- [x] 8.3 Show `ProposedAction` details (risk, affected resources, description)
- [x] 8.4 Add timeout indicator for pending confirmations

## 9. Testing & Validation
- [x] 9.1 Unit tests for `ConfirmationGateEvaluator` with mocked dependencies
- [x] 9.2 Integration tests for full confirmation flow with fake LLM
- [x] 9.3 E2E tests for destructive operation confirmation in temp workspace
- [x] 9.4 Validate with `openspec validate add-confirmation-gate --strict`

## 10. Documentation
- [x] 10.1 Update streaming-events.md with confirmation event types
- [x] 10.2 Document confirmation gate behavior in AGENTS.md
- [x] 10.3 Add configuration examples for confirmation thresholds
- [x] 10.4 Update Remediation.md to mark confirmation gate items as [x]
