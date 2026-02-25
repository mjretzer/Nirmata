## Context
The current GMSD platform implements chat and commands as separate concerns - users must either use explicit slash commands or get basic stub responses. This creates a disjointed experience where conversational interaction doesn't integrate with the powerful workflow engine.

## Goals / Non-Goals
- Goals: 
  - Unified conversational interface that seamlessly handles both chat and commands
  - Intelligent command suggestions when chat seems to request workflow actions
  - Real LLM-backed responses with full workspace awareness
  - Backward compatibility with existing slash commands
- Non-Goals:
  - Complete natural language command parsing (keep slash commands explicit)
  - Full workflow execution from chat (use command suggestions instead)
  - Breaking existing command APIs

## Decisions
- Decision: **Conversation-first approach** - no prefix defaults to chat, slash prefix for explicit commands
  - Alternatives considered: Command-first (current), hybrid detection
  - Rationale: More natural user experience, reduces cognitive load
- Decision: **Command suggestion with confirmation** rather than direct execution from chat
  - Alternatives considered: Direct NL command execution, no suggestions
  - Rationale: Maintains safety and transparency while being helpful
- Decision: **Schema-validated command proposals** using existing command-proposal infrastructure
  - Alternatives considered: Ad-hoc suggestions, free-text proposals
  - Rationale: Leverages existing validation, ensures consistency
- Decision: **Read-only tool sandbox for Chat Responder**
  - Rationale: Prevents side-effects during conversation while allowing workspace awareness. Only `read_file`, `list_dir`, and `inspect_spec` are exposed.
- Decision: **Dialogue Streaming Protocol v2** for all interactions
  - Rationale: Provides structured events (intent, gating, tool calls, assistant deltas) that allow the UI to render rich components like command cards.

## Architecture
```
User Input → CommandParser → Intent Classification → Orchestrator
                                    ↓
                              Chat Mode (default) → LLM Responder → Response
                                    ↓
                              Command Mode → Workflow Execution → Result
                                    ↓
                              Suggestion Mode → Command Proposal → Confirmation → Execution
```

## Risks / Trade-offs
- **Risk**: Chat responses may be slower than current stub responses
  - Mitigation: Implement streaming, optimize context assembly, set timeouts
- **Risk**: Users may confuse when to use chat vs commands
  - Mitigation: Clear UI indicators, help text, smart suggestions
- **Trade-off**: Increased complexity in orchestrator routing
  - Mitigation: Clear separation of concerns, comprehensive testing

## Migration Plan
1. Phase 1: Implement command parser alongside existing system
2. Phase 2: Add LLM responder with read-only tools
3. Phase 3: Integrate command suggestions with confirmation
4. Phase 4: Update UI and remove old stub responders
5. Phase 5: Documentation and user communication

## Open Questions
- Should we maintain conversation history across command executions? (Decision: Yes, history should be persisted in the state store and re-injected as context for continuity).
- How should we handle very long chat contexts that affect token budgets? (Mitigation: Implement a sliding window with priority for recent turns and key workspace facts).
- Should command suggestions be optional/configurable? (Decision: Default on, but add a user preference in Phase 5).
- What's the fallback behavior if LLM provider is unavailable? (Decision: Fall back to basic command help and status information).
- How to handle concurrent chat/command runs in the same thread? (Decision: Single active run per thread; chat requests while a command is running should be queued or rejected).
