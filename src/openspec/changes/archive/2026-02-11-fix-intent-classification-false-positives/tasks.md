## 1. Design and Schema
- [x] 1.1 Define CommandRegistry interface with command-to-side-effect mappings
- [x] 1.2 Define IntentClassificationResult schema for streaming events
- [x] 1.3 Define ConfirmationGate contract for ambiguous write operations

## 2. Refactor InputClassifier
- [x] 2.1 Replace regex keyword matching with CommandParser
- [x] 2.2 Implement prefix-based command detection (`/^\/(\w+)/`)
- [x] 2.3 Implement default-to-chat behavior for non-prefixed input
- [x] 2.4 Add confidence scoring for classification results
- [x] 2.5 Remove legacy regex: `(create|plan|execute|run|verify|fix|pause|resume)`

## 3. Implement Command Registry
- [x] 3.1 Create CommandRegistry class with supported commands
- [x] 3.2 Map commands to side effects:
  - `/run`, `/plan`, `/verify`, `/fix`, `/pause`, `/resume` → `SideEffect.Write`
  - `/status`, `/help` → `SideEffect.ReadOnly`
- [x] 3.3 Add unknown command handler with suggestions

## 4. Implement Confirmation Gate
- [x] 4.1 Add confirmation threshold configuration (default: 0.9 confidence)
- [x] 4.2 Implement ConfirmationGate middleware in orchestrator
- [x] 4.3 Emit `confirmation.required` event when threshold not met
- [x] 4.4 Handle `confirmation.response` event to proceed or cancel

## 5. Streaming Event Integration
- [x] 5.1 Emit `intent.classified` event with classification details
- [x] 5.2 Update ChatStreamingController to handle confirmation flow
- [x] 5.3 Add `confirmation.requested` and `confirmation.responded` event types

## 6. Update Orchestrator Flow
- [x] 6.1 Modify ExecuteAsync to check confirmation status before run lifecycle
- [x] 6.2 Add state machine for awaiting confirmation
- [x] 6.3 Ensure chat path (SideEffect.None) bypasses run lifecycle

## 7. Testing
- [x] 7.1 Add unit tests for CommandParser
- [x] 7.2 Add unit tests for InputClassifier with new behavior
- [x] 7.3 Add integration tests for confirmation gate flow
- [x] 7.4 Verify "create a plan" (chat) vs "/plan" (command) distinction
- [x] 7.5 Verify freeform inputs no longer trigger runs

## 8. Documentation
- [x] 8.1 Update AGENTS.md with command syntax reference (via MIGRATION.md)
- [x] 8.2 Document breaking change in migration notes
- [x] 8.3 Add command help text for `/help` command
