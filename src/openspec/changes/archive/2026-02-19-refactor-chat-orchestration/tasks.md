## 1. Command Parser and Registry
- [x] 1.1 Create `ICommandParser` interface and `CommandParser` implementation with strict slash-prefix rules
- [x] 1.2 Implement slash command recognition for core set (`/help`, `/status`, `/run`, `/plan`, `/verify`, `/fix`)
- [x] 1.3 Implement `CommandRegistry` to manage metadata, help text, and argument schemas for all commands
- [x] 1.4 Add support for command argument parsing with basic type validation
- [x] 1.5 Add unit tests for command parsing, including edge cases like quoted arguments and empty inputs

## 2. Intent Classification and Routing
- [x] 2.1 Update `IInputClassifier` to distinguish between `IntentKind.Chat` (no prefix) and `IntentKind.Command` (slash prefix)
- [x] 2.2 Enhance `InputClassifier` to use `ICommandParser` for validation during classification
- [x] 2.3 Modify `Orchestrator` to route based on classified intent:
    - `IntentKind.Chat` -> `ChatResponder`
    - `IntentKind.Command` -> Traditional workflow phases
- [x] 2.4 Implement `SideEffect.None` for chat intents to ensure read-only behavior by default

## 3. Command Suggestion Mode
- [x] 3.1 Enhance `IChatResponder` to detect when user likely wants to execute commands
- [x] 3.2 Implement structured command proposal generation with schema validation
- [x] 3.3 Add confirmation flow for suggested commands
- [x] 3.4 Update UI to display command proposals with accept/reject actions
- [x] 3.5 Add telemetry for suggestion acceptance rates

## 4. LLM-Backed Chat Responder and Tool Sandbox
- [x] 4.1 Replace stub `ChatResponder` with real `LlmChatResponder` implementation
- [x] 4.2 Enhance `IChatContextAssembly` to include comprehensive workspace facts (specs, roadmap, current cursor)
- [x] 4.3 Implement `ReadOnlyToolRegistry` and sandbox specifically for the chat responder
- [x] 4.4 Expose `read_file`, `list_dir`, and `inspect_spec` tools to the chat responder
- [x] 4.5 Implement `ChatPromptBuilder` with system instructions for command suggestion and tool use
- [x] 4.6 Add token budget enforcement and sliding-window conversation history

## 5. Orchestrator Integration
- [x] 5.1 Update `IOrchestrator` to handle unified chat/command flow
- [x] 5.2 Modify gating engine to handle chat intents vs command intents
- [x] 5.3 Ensure run lifecycle works for both chat and command paths
- [x] 5.4 Add proper evidence capture for chat interactions
- [x] 5.5 Update error handling for mixed chat/command scenarios

## 6. UI Updates
- [x] 6.1 Update chat interface to handle both chat and command responses
- [x] 6.2 Add command autocomplete with slash hints
- [x] 6.3 Implement command proposal display with confirmation buttons
- [x] 6.4 Add visual indicators for chat vs command modes
- [x] 6.5 Update streaming protocol to handle mixed response types

## 7. Testing and Validation
- [x] 7.1 Add integration tests for unified chat/command flow
- [x] 7.2 Create end-to-end tests for command suggestion workflow
- [x] 7.3 Add performance tests for chat response times
- [x] 7.4 Validate workspace context inclusion in chat responses
- [x] 7.5 Test error scenarios and fallback behaviors

## 8. Documentation and Migration
- [x] 8.1 Update user documentation for new conversational flow
- [x] 8.2 Add developer guide for command extension
- [x] 8.3 Document migration path from command-first to chat-first
- [x] 8.4 Create troubleshooting guide for common issues
- [x] 8.5 Update API documentation for streaming protocol changes
