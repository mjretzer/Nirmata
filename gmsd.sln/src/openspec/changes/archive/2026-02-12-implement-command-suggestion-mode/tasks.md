# Tasks: Implement Command Suggestion Mode

## Phase 1: Core Data Contracts

1. **Create CommandProposal model**
   - Add `CommandProposal.cs` with properties: CommandName, Arguments, Confidence, Reasoning, FormattedCommand
   - Add validation: Confidence must be 0.0-1.0, CommandName must be non-empty
   - Add to `Gmsd.Agents/Execution/Preflight/CommandSuggestion/`
   - **Validation**: Unit tests for validation rules **- [x]**

2. **Create ICommandSuggester interface**
   - Define `ICommandSuggester` with method `Task<CommandProposal?> SuggestAsync(string input, CancellationToken)`
   - Define `CommandSuggestionOptions` for confidence threshold (default 0.7)
   - Add to `Gmsd.Agents/Execution/Preflight/CommandSuggestion/`
   - **Validation**: Interface compiles, options have sensible defaults **- [x]**

## Phase 2: LLM Integration

3. **Create LLM-based CommandSuggester implementation**
   - Implement `LlmCommandSuggester : ICommandSuggester`
   - Create prompt template for command suggestion (system + user messages)
   - Use structured output (JSON schema) for reliable parsing
   - Inject `ILlmProvider` dependency
   - **Validation**: Unit tests with mock LLM provider **- [x]**

4. **Add prompt template for command suggestion**
   - Create `Prompts/CommandSuggestion.txt` or embedded resource
   - Include available commands with descriptions and examples
   - Include response schema definition
   - **Validation**: Prompt renders correctly with all command descriptions **- [x]**

## Phase 3: Integration with Classifier

5. **Add DI registration for ICommandSuggester**
   - Register `CommandSuggestionOptions` with configuration binding
   - Register `LlmCommandSuggester` as singleton
   - Wire up in both `AddGmsdAgents` overloads
   - **Validation**: Service provider can resolve `ICommandSuggester` **- [x]**

6. **Update IntentClassificationResult for proposals**
   - Add `CommandProposal? SuggestedCommand` property
   - Add helper method `HasSuggestion()` for convenience
   - Ensure serialization works for streaming events
   - **Validation**: JSON round-trip tests **- [x]**

## Phase 4: Streaming Events

7. **Add command.suggested event type**
   - Add `CommandSuggested` event class extending `StreamingEventBase`
   - Properties: Proposal details, original input, timestamp
   - Add to `Gmsd.Agents/Observability/Streaming/Events/`
   - **Validation**: Event serialization tests **- [x]**

8. **Wire up event emission in classifier**
   - Inject `IStreamingEventEmitter` into classifier or orchestrator
   - Emit `intent.classified` event (existing)
   - Emit `command.suggested` event when proposal is generated
   - **Validation**: Integration test verifies events are emitted **- [x]**

## Phase 5: Confirmation Gate

9. **Update ConfirmationGate for suggested commands**
   - Modify to recognize `IntentClassificationResult` with `SuggestedCommand`
   - Pre-fill confirmation dialog with proposed command details
   - Track suggestion source for telemetry
   - **Validation**: Unit tests for confirmation flow with proposals **- [x]**

10. **Add confirmation response handling**
    - Create `SuggestedCommandConfirmed` / `SuggestedCommandRejected` event types
    - Wire events to execute command or continue as chat
    - **Validation**: End-to-end confirmation flow tests **- [x]**

## Phase 6: UI Integration

11. **Update ChatStreamingController**
    - Ensure controller forwards `command.suggested` events to SSE stream
    - Add endpoint for confirming/dismissing suggestions
    - **Validation**: Controller returns correct event stream **- [x]**

12. **Add UI contract documentation**
    - Document event schema for frontend consumption
    - Provide example UI flow: display suggestion card → user action → execute
    - **Validation**: Documentation review **- [x]**

## Phase 7: Testing & Hardening

13. **Add integration tests**
    - Test: Freeform input "plan the foundation phase" → suggestion emitted
    - Test: User confirms suggestion → command executed
    - Test: User rejects suggestion → chat continues
    - Test: Ambiguous input → no suggestion (falls back to chat)
    - **Validation**: All integration tests pass **- [x]**

14. **Add edge case tests**
    - LLM returns malformed JSON → graceful fallback
    - LLM unavailable → normal chat behavior
    - Multiple possible commands → suggest highest confidence only
    - Invalid arguments in proposal → reject and explain
    - **Validation**: Edge case tests pass **- [x]**

15. **Add performance tests**
    - Measure LLM call latency for suggestion (should be < 500ms)
    - Ensure suggestion mode doesn't block chat responsiveness
    - **Validation**: Benchmarks meet targets **- [x]**

## Phase 8: Documentation

16. **Update user documentation**
    - Document new suggestion mode feature
    - Provide examples of natural language → command mapping
    - Explain confirmation flow
    - **Validation**: Documentation reviewed for clarity **- [x]**

17. **Update developer documentation**
    - Document `ICommandSuggester` extension point
    - Explain how to add new command suggestions
    - Document event flow
    - **Validation**: Other developers can extend the system **- [x]**

## Dependencies

- Requires `ILlmProvider` implementation (Direction A or B from Remediation.md)
- Builds on existing `InputClassifier` and `CommandRegistry`
- Uses existing streaming event infrastructure

## Rollback Plan

If issues arise:
1. Set `EnableSuggestionMode = false` in configuration (immediate)
2. Remove `ICommandSuggester` registration from DI (if critical)
3. System gracefully falls back to strict command grammar behavior
