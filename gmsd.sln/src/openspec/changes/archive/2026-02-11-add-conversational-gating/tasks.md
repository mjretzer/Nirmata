## 1. Gating Engine Enhancements
- [x] 1.1 Extend `GatingResult` to include `Reasoning`, `ProposedAction`, and `RequiresConfirmation` fields
- [x] 1.2 Implement `ProposedAction` structured output schema with validation
- [x] 1.3 Add `IDestructivenessAnalyzer` to classify operation risk level
- [x] 1.4 Update `IGatingEngine.EvaluateAsync` to emit reasoning and confirmation requirements
- [x] 1.5 Write unit tests for gating engine confirmation logic

## 2. Orchestrator Integration
- [x] 2.1 Modify orchestrator to emit `gate.selected` event before phase dispatch
- [x] 2.2 Add confirmation wait loop in orchestrator when `requiresConfirmation` is true
- [x] 2.3 Ensure `run.started` only emits after confirmation for write operations
- [x] 2.4 Add cancellation path when user rejects proposed action
- [x] 2.5 Write integration tests for confirmation flow

## 3. Streaming Protocol Implementation
- [x] 3.1 Implement `IEventEmitter.EmitGateSelectedAsync` with full reasoning payload
- [x] 3.2 Add `GateSelectedEvent` type to streaming dialogue protocol
- [x] 3.3 Ensure event ordering: `intent.classified` → `gate.selected` → (`run.started` if confirmed)
- [x] 3.4 Write tests for event emission sequence

## 4. Chat Responder Updates
- [x] 4.1 Update chat responder to handle `gate.selected` events
- [x] 4.2 Format gate reasoning as conversational "thinking" message
- [x] 4.3 Add UI support for confirmation prompts (accept/reject proposed action)
- [x] 4.4 Write E2E tests for conversational gating flow

## 5. Validation
- [x] 5.1 Run `openspec validate add-conversational-gating --strict`
- [x] 5.2 Verify all existing orchestrator tests still pass
- [x] 5.3 Validate streaming events match protocol specification
