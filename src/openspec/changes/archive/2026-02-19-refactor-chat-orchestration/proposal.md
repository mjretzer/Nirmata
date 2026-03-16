# Change: Refactor Chat Orchestration

## Why
The current chat implementation separates chat and command execution into different UX paths, creating confusion for users. We need to unify these into a single coherent conversational orchestrator that can handle both freeform chat and explicit commands seamlessly.

## What Changes
- Implement a strict command parser for slash commands (`/help`, `/status`, `/run …`, `/plan …`, `/verify`, `/fix`, etc.)
- Make **no prefix → chat** the default behavior for natural conversation
- Add "command suggestion" mode where the model proposes commands and asks for confirmation
- Replace stub ChatResponder/ResponderHandler with a real LLM-backed responder that includes workspace awareness
- Include a small read-only tool set for the chat responder (e.g., `read_file`, `list_dir`, `inspect_spec`)
- **BREAKING**: Changes the chat interface behavior from command-first to conversation-first
- **BREAKING**: Updates SSE event stream to follow the unified Dialogue Streaming Protocol v2

## Impact
- Affected specs: `chat-interface`, `chat-responder`, `orchestrator-workflow`, `command-proposal`, `intent-classification`
- Affected code: `nirmata.Web` chat controllers, `nirmata.Agents` orchestrator, responder handlers, input classification
- User experience: More natural conversation flow with optional explicit commands and intelligent suggestions
- Client implementation: Web frontend must handle v2 streaming events and command proposal cards
