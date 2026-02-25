## MODIFIED Requirements
### Requirement: Chat responder interface with blocking and streaming support

The system SHALL define an `IChatResponder` interface that supports both blocking and streaming chat responses with workspace awareness and command suggestion capabilities.

#### Scenario: Blocking response mode for simple queries

Given the orchestrator has classified input as conversational (no slash prefix)
When the chat responder is invoked with a user message
Then it SHALL return a complete response after the LLM call finishes
And the response SHALL include relevant workspace context

#### Scenario: Streaming response mode for progressive display

Given the UI has requested SSE streaming for a chat message
When the chat responder is invoked in streaming mode
Then it SHALL yield tokens progressively as they arrive from the LLM
And the response SHALL maintain conversation context throughout

---

### Requirement: Context assembly for grounded responses

The chat responder SHALL assemble workspace context to provide grounded, aware responses with read-only tool access.

#### Scenario: Project context is included in responses

Given a workspace with a defined `project.json` spec
When the user asks "What project is this?"
Then the chat response SHALL reference the actual project name and description
And the response SHALL be generated using read-only access to spec files

#### Scenario: Roadmap context is included in responses

Given a workspace with a defined `roadmap.json` and active phases
When the user asks "What's the current roadmap?"
Then the chat response SHALL summarize the roadmap phases and current cursor position
And the response SHALL use the state store for current cursor information

#### Scenario: State context is included in responses

Given a workspace with run history and cursor state
When the user asks "What happened in the last run?"
Then the chat response SHALL reference the most recent run status and artifacts
And the response SHALL access evidence store for run information

#### Scenario: Command help context is available

Given any workspace state
When the user asks for help or available commands
Then the chat response SHALL list available `/` commands and their descriptions
And the response SHALL include usage examples and current workspace applicability

#### Scenario: Read-only tool access for context gathering

Given the chat responder needs workspace information
When assembling context for a response
Then it SHALL use read-only tools (file reading, spec inspection, state queries)
And it SHALL NOT modify any workspace state during chat operations

---

### Requirement: Command suggestion generation

The chat responder SHALL detect when user conversations suggest workflow actions and propose structured commands with confirmation requirements.

#### Scenario: Action detection in conversation

Given a user message like "I want to start working on the authentication phase"
When the chat responder processes the message
Then it SHALL detect the implied workflow action
And it SHALL generate a command proposal for the appropriate next step

#### Scenario: Structured command proposal generation

Given the chat responder has identified a potential workflow action
When generating a suggestion
Then it SHALL create a structured `CommandProposal` object with all required fields
And the proposal SHALL include intent, command, rationale, and expected outcome
And the proposal SHALL pass schema validation before being returned

#### Scenario: Context-aware command suggestions

Given a workspace with specific current state (e.g., missing phase plan)
When the user asks about next steps
Then the chat responder SHALL suggest the most relevant command for the current situation
And the suggestion SHALL reference the specific workspace state that informs the recommendation

#### Scenario: Multiple option suggestions

Given a situation where multiple valid next steps exist
When the chat responder generates suggestions
Then it SHALL propose multiple command options with clear differentiation
And each option SHALL include rationale for why it's appropriate

---

### Requirement: LLM provider integration with enhanced capabilities

The chat responder SHALL use `ILlmProvider` for all LLM calls, with enhanced prompt templates for command suggestion and workspace awareness.

#### Scenario: Non-streaming provider fallback

Given a provider that does not support streaming
When a streaming chat request is made
Then the responder SHALL fall back to blocking mode and stream the complete response as a single delta

#### Scenario: Provider error handling

Given the LLM provider returns an error or times out
When the chat responder attempts to generate a response
Then it SHALL return a friendly error message to the user
And the error SHALL be logged with correlation ID for debugging

#### Scenario: Enhanced prompt templates for command detection

Given any chat request
When the prompt is built
Then the system prompt SHALL include:
  - Assistant identity with command suggestion capabilities
  - Current workspace state section
  - Available commands section with descriptions
  - Guidelines for detecting when to suggest commands
  - Instructions for generating structured proposals

---

## ADDED Requirements
### Requirement: Workspace-aware conversation context

The chat responder SHALL maintain and utilize comprehensive workspace context throughout conversations to provide relevant, grounded responses.

#### Scenario: Persistent workspace context across conversation

Given a multi-turn conversation about the workspace
When subsequent messages are processed
Then the chat responder SHALL maintain workspace context from previous turns
And it SHALL reference earlier context when relevant to new questions

#### Scenario: Dynamic context updates

Given a workspace state change during a conversation (e.g., command execution)
When the next chat message is processed
Then the chat responder SHALL use the updated workspace state
And it SHALL acknowledge recent changes in responses when relevant

#### Scenario: Context-efficient token management

Given a long conversation with large workspace context
When assembling context for a new response
Then the chat responder SHALL prioritize recent and relevant context
And it SHALL summarize or truncate less critical information to stay within token limits

---

### Requirement: Tool integration for read-only operations

The chat responder SHALL integrate with the tool registry to provide read-only access to workspace information during conversations.

#### Scenario: File reading tool access

Given a user asks about specific file contents
When the chat responder processes the request
Then it SHALL use the file reading tool to access the requested file
And it SHALL present the information in a conversational format

#### Scenario: Spec inspection tool access

Given a user asks about project specifications
When the chat responder processes the request
Then it SHALL use spec inspection tools to read relevant spec files
And it SHALL summarize the specifications in natural language

#### Scenario: State query tool access

Given a user asks about current progress or state
When the chat responder processes the request
Then it SHALL use state query tools to access current cursor and run information
And it SHALL present the state information conversationally

#### Scenario: Chat responder tool access enforcement

- **GIVEN** the chat responder is operating in conversation mode
- **WHEN** any tool is invoked
- **THEN** only read-only tools SHALL be permitted
- **AND** any attempt to use write/modification tools SHALL be blocked with error logging
- **AND** the user SHALL receive a message explaining that write operations require explicit commands

#### Scenario: Tool timeout and fallback handling

- **GIVEN** a read-only tool call exceeds its timeout limit
- **WHEN** the timeout occurs
- **THEN** the chat responder SHALL gracefully handle the failure
- **AND** it SHALL inform the user about the tool access issue
- **AND** it SHALL continue the conversation without the tool data

#### Scenario: Tool access restrictions enforced

Given the chat responder is operating in conversation mode
When any tool is invoked
Then only read-only tools SHALL be permitted
And any attempt to use write/modification tools SHALL be blocked with error logging
