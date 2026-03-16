# Implementation Notes: Refactor Chat Orchestration

## Overview
This document tracks the implementation status of the refactor-chat-orchestration OpenSpec change, including completed work, architectural decisions, and remaining tasks.

## Completed Work

### Section 1: Command Parser and Registry
- ✅ `ICommandParser` interface and `CommandParser` implementation with strict slash-prefix rules
- ✅ Slash command recognition for core set (`/help`, `/status`, `/run`, `/plan`, `/verify`, `/fix`)
- ✅ `CommandRegistry` to manage metadata, help text, and argument schemas
- ✅ Support for command argument parsing with basic type validation
- ✅ Unit tests for command parsing, including edge cases

**Files:**
- `nirmata.Agents/Execution/ControlPlane/Commands/CommandParser.cs`
- `nirmata.Agents/Execution/ControlPlane/Commands/CommandRegistry.cs`
- `tests/nirmata.Agents.Tests/Execution/Commands/CommandParserTests.cs`

### Section 2: Intent Classification and Routing
- ✅ Updated `IInputClassifier` to distinguish between `IntentKind.Chat` and `IntentKind.Command`
- ✅ Enhanced `InputClassifier` to use `ICommandParser` for validation
- ✅ Modified `Orchestrator` to route based on classified intent
- ✅ Implemented `SideEffect.None` for chat intents

**Files:**
- `nirmata.Agents/Execution/Preflight/InputClassifier.cs`
- `nirmata.Agents/Execution/ControlPlane/Orchestrator.cs`

### Section 3: Command Suggestion Mode
- ✅ Enhanced `IChatResponder` to detect command intent
- ✅ Implemented structured command proposal generation
- ✅ Added confirmation flow for suggested commands
- ✅ **NEW:** UI components for command proposal display with accept/reject actions
- ✅ **NEW:** Telemetry tracking for suggestion acceptance rates

**Files:**
- `nirmata.Agents/Execution/ControlPlane/Chat/CommandSuggestionDetector.cs`
- `nirmata.Agents/Execution/ControlPlane/Chat/CommandConfirmationFlow.cs`
- `nirmata.Web/wwwroot/js/command-proposal-renderers.js` (NEW)
- `nirmata.Web/wwwroot/css/command-proposal-cards.css` (NEW)
- `nirmata.Web/wwwroot/js/command-suggestion-telemetry.js` (NEW)

### Section 4: LLM-Backed Chat Responder and Tool Sandbox
- ✅ Replaced stub `ChatResponder` with real `LlmChatResponder` implementation
- ✅ Enhanced `IChatContextAssembly` to include comprehensive workspace facts
- ✅ Implemented `ReadOnlyToolRegistry` and sandbox
- ✅ Exposed `read_file`, `list_dir`, and `inspect_spec` tools
- ✅ **NEW:** `ChatPromptBuilder` with system instructions for command suggestion and tool use
- ✅ **NEW:** `ConversationHistoryManager` with token budget enforcement and sliding-window history

**Files:**
- `nirmata.Agents/Execution/ControlPlane/Chat/LlmChatResponder.cs`
- `nirmata.Agents/Execution/ControlPlane/Chat/ChatContextAssembly.cs`
- `nirmata.Agents/Execution/ControlPlane/Chat/ReadOnlyToolRegistry.cs`
- `nirmata.Agents/Execution/ControlPlane/Chat/ChatPromptBuilder.cs` (ENHANCED)
- `nirmata.Agents/Execution/ControlPlane/Chat/ConversationHistoryManager.cs` (NEW)

### Section 5: Orchestrator Integration
- ✅ Updated `IOrchestrator` to handle unified chat/command flow
- ✅ Modified gating engine to handle chat intents vs command intents
- ✅ Ensured run lifecycle works for both chat and command paths
- ✅ Added proper evidence capture for chat interactions
- ✅ Updated error handling for mixed chat/command scenarios

**Files:**
- `nirmata.Agents/Execution/ControlPlane/Orchestrator.cs`
- `nirmata.Agents/Execution/ControlPlane/GatingEngine.cs`

### Section 6: UI Updates
- ✅ **NEW:** Command proposal display component with accept/reject buttons
- ✅ **NEW:** Command autocomplete with slash hints
- ✅ **NEW:** Visual indicators for chat vs command modes
- ✅ Streaming protocol handles mixed response types
- ✅ Event renderers for command suggestion events

**Files:**
- `nirmata.Web/wwwroot/js/command-proposal-renderers.js` (NEW)
- `nirmata.Web/wwwroot/js/command-autocomplete.js` (NEW)
- `nirmata.Web/wwwroot/css/command-proposal-cards.css` (NEW)
- `nirmata.Web/wwwroot/css/command-autocomplete.css` (NEW)
- `nirmata.Web/Controllers/ChatStreamingController.cs` (ENHANCED)

### Section 7: Testing and Validation
- ✅ Added integration tests for unified chat/command flow
- ✅ Created end-to-end tests for command suggestion workflow
- ✅ Added performance tests for chat response times
- ✅ Validated workspace context inclusion in chat responses
- ✅ Tested error scenarios and fallback behaviors

**Files:**
- `tests/nirmata.Agents.Tests/Execution/ControlPlane/Chat/LlmChatResponderTests.cs`
- `tests/nirmata.Agents.Tests/Execution/ControlPlane/Chat/CommandSuggestionDetectorTests.cs`
- `tests/nirmata.Web.Tests/ChatStreamingControllerTests.cs`

### Section 8: Documentation and Migration
- ✅ **NEW:** This implementation notes document
- ⏳ User documentation for new conversational flow
- ⏳ Developer guide for command extension
- ⏳ Migration path documentation
- ⏳ Troubleshooting guide

## Architectural Decisions

### 1. Conversation-First Approach
- **Decision:** No prefix defaults to chat, slash prefix for explicit commands
- **Rationale:** More natural user experience, reduces cognitive load
- **Implementation:** `InputClassifier` checks for slash prefix to determine intent

### 2. Command Suggestion with Confirmation
- **Decision:** AI suggests commands but requires user confirmation
- **Rationale:** Maintains safety and transparency while being helpful
- **Implementation:** `CommandSuggestionDetector` analyzes chat input and generates proposals

### 3. Schema-Validated Command Proposals
- **Decision:** Use existing command-proposal infrastructure for validation
- **Rationale:** Leverages existing validation, ensures consistency
- **Implementation:** `CommandProposal` class with validation methods

### 4. Read-Only Tool Sandbox
- **Decision:** Only `read_file`, `list_dir`, and `inspect_spec` exposed to chat responder
- **Rationale:** Prevents side-effects during conversation while allowing workspace awareness
- **Implementation:** `ReadOnlyToolRegistry` with restricted tool set

### 5. Token Budget Enforcement
- **Decision:** Sliding-window conversation history with configurable token budget
- **Rationale:** Prevents context explosion while maintaining conversation continuity
- **Implementation:** `ConversationHistoryManager` with automatic pruning of oldest turns

## Integration Points

### Streaming Events
The implementation uses the Dialogue Streaming Protocol v2 with these new event types:
- `command.suggested` - AI proposes a command
- `command.confirmed` - User accepts a command proposal
- `command.rejected` - User rejects a command proposal

### UI Components
- **Command Proposal Renderer:** Displays suggestions with accept/reject controls
- **Command Autocomplete:** Provides slash command hints as user types
- **Telemetry Tracker:** Monitors suggestion effectiveness

### Backend Services
- **ChatPromptBuilder:** Constructs system and user prompts with workspace context
- **ConversationHistoryManager:** Manages conversation state with token budgets
- **CommandSuggestionDetector:** Analyzes user input for command intent

## Testing Strategy

### Unit Tests
- Command parser edge cases (quoted arguments, empty inputs)
- Intent classification accuracy
- Command suggestion detection
- Token budget enforcement

### Integration Tests
- Unified chat/command flow
- Command suggestion workflow
- Streaming event emission
- Error handling and fallbacks

### E2E Tests
- User types chat → receives response
- User types chat with command keywords → receives suggestion
- User accepts/rejects suggestion → appropriate action taken
- Command autocomplete navigation

## Known Limitations

1. **Natural Language Command Parsing:** Still requires slash prefix for explicit commands; no full NL parsing
2. **Concurrent Operations:** Single active run per thread; chat requests during command execution are queued
3. **Context Size:** Token budget may limit very long conversations; oldest turns are pruned
4. **Tool Availability:** Chat responder has read-only tool set; no write operations

## Migration Path

### Phase 1: Backward Compatibility
- Legacy endpoints continue to work
- New v2 endpoints available alongside legacy
- Clients can opt-in to new streaming protocol

### Phase 2: Feature Rollout
- Command suggestions enabled by default
- Can be disabled via feature flags
- Telemetry collected for effectiveness analysis

### Phase 3: Deprecation
- Legacy endpoints marked as deprecated
- Documentation updated to recommend v2
- Timeline for legacy endpoint removal

## Next Steps

1. Complete user documentation
2. Create developer guide for extending commands
3. Write migration guide for existing clients
4. Develop troubleshooting documentation
5. Conduct user acceptance testing
6. Gather feedback on suggestion effectiveness
7. Iterate on UI/UX based on telemetry

## References

- OpenSpec Change: `refactor-chat-orchestration`
- Streaming Protocol: `streaming-dialogue-protocol`
- Chat Interface Spec: `chat-interface`
- Command Proposal Spec: `command-proposal`
