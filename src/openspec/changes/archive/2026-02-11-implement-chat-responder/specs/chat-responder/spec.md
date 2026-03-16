# Specification: Chat Responder

## Capability

Real LLM-backed chat responses for the orchestrator, replacing stubbed placeholder responses with genuine assistant dialogue.

## Related Capabilities

- `intent-classification` - Provides `SideEffect.None` classification that triggers chat path
- `agents-llm-provider-abstraction` - Provides `ILlmProvider` interface for LLM calls
- `streaming-dialogue-protocol` - SSE streaming protocol for chat responses

## ADDED Requirements

### Requirement: Chat responder interface with blocking and streaming support

The system SHALL define an `IChatResponder` interface that supports both blocking and streaming chat responses.

#### Scenario: Blocking response mode for simple queries

Given the orchestrator has classified input as `SideEffect.None`
When the chat responder is invoked with a user message
Then it SHALL return a complete response after the LLM call finishes

#### Scenario: Streaming response mode for progressive display

Given the UI has requested SSE streaming for a chat message
When the chat responder is invoked in streaming mode
Then it SHALL yield tokens progressively as they arrive from the LLM

---

### Requirement: Context assembly for grounded responses

The chat responder SHALL assemble workspace context to provide grounded, aware responses.

#### Scenario: Project context is included in responses

Given a workspace with a defined `project.json` spec
When the user asks "What project is this?"
Then the chat response SHALL reference the actual project name and description

#### Scenario: Roadmap context is included in responses

Given a workspace with a defined `roadmap.json` and active phases
When the user asks "What's the current roadmap?"
Then the chat response SHALL summarize the roadmap phases and current cursor position

#### Scenario: State context is included in responses

Given a workspace with run history and cursor state
When the user asks "What happened in the last run?"
Then the chat response SHALL reference the most recent run status and artifacts

#### Scenario: Command help context is available

Given any workspace state
When the user asks for help or available commands
Then the chat response SHALL list available `/` commands and their descriptions

---

### Requirement: LLM provider integration

The chat responder SHALL use `ILlmProvider` for all LLM calls, respecting provider capabilities.

#### Scenario: Non-streaming provider fallback

Given a provider that does not support streaming
When a streaming chat request is made
Then the responder SHALL fall back to blocking mode and stream the complete response as a single delta

#### Scenario: Provider error handling

Given the LLM provider returns an error or times out
When the chat responder attempts to generate a response
Then it SHALL return a friendly error message to the user
And the error SHALL be logged with correlation ID for debugging

---

### Requirement: Context token budget enforcement

The chat responder SHALL enforce a context token budget to prevent excessive LLM costs.

#### Scenario: Large spec truncation

Given a workspace with a very large `project.json` that exceeds the token budget
When context is assembled for a chat response
Then the spec SHALL be truncated or summarized to fit within the budget
And the chat response SHALL still be coherent and useful

#### Scenario: Token budget prioritization

Given a configured maximum context size of 2000 tokens
When context assembly would exceed this budget
Then the system SHALL prioritize essential context (state, commands, then spec summaries)
And lower-priority content SHALL be omitted or truncated

---

### Requirement: Orchestrator integration with IChatResponder

The orchestrator SHALL use `IChatResponder` instead of stubbed responses for `SideEffect.None` inputs.

#### Scenario: Chat intent classification uses LLM responder

Given the `InputClassifier` has classified input as `SideEffect.None` with `IntentKind.Unknown`
When the `Orchestrator.ExecuteAsync` processes the intent
Then it SHALL invoke `IChatResponder.RespondAsync` instead of returning a hardcoded string
And the `OrchestratorResult` SHALL contain the LLM-generated response

#### Scenario: Responder phase gating uses LLM responder

Given the `GatingEngine` has selected the "Responder" phase (no workflow triggered)
When the `ResponderHandler` processes the request
Then it SHALL invoke `IChatResponder.RespondAsync` for a conversational response
And the response SHALL reference current workspace state

---

### Requirement: Prompt template stability

The chat responder SHALL use deterministic prompt templates for consistent behavior.

#### Scenario: System prompt contains assistant identity and guidelines

Given any chat request
When the prompt is built
Then the system prompt SHALL include:
  - Assistant identity ("You are nirmata Assistant...")
  - Current workspace state section
  - Available commands section
  - Guidelines for conversational tone

#### Scenario: User prompt is clearly delimited

Given a user input message
When the prompt is built
Then the user message SHALL be clearly delimited in the prompt
And any relevant context SHALL be presented before the user message

---

### Requirement: Response metadata for observability

The chat responder SHALL include metadata in responses for observability.

#### Scenario: Token usage is reported

Given a successful LLM chat completion
When the response is returned
Then the response SHALL include:
  - `model` - The LLM model used
  - `promptTokens` - Tokens in the prompt
  - `completionTokens` - Tokens in the completion
  - `totalTokens` - Total tokens used

#### Scenario: Response timing is tracked

Given any chat request
When the response is returned
Then the response SHALL include `durationMs` indicating elapsed time
And slow responses greater than 3 seconds SHALL trigger a warning log

---

### Requirement: Error recovery and graceful degradation

The chat responder SHALL gracefully handle failures without breaking the orchestration flow.

#### Scenario: LLM provider unavailable fallback

Given the configured LLM provider is unavailable or misconfigured
When a chat request is made
Then the responder SHALL return a friendly fallback message with static help text
And the fallback SHALL include available commands information

#### Scenario: Context assembly failure fallback

Given the workspace state store is unreadable or corrupted
When context assembly fails
Then the responder SHALL fall back to chat without workspace context
And the response SHALL indicate limited context is available

---

## MODIFIED Requirements

None.

## REMOVED Requirements

None.

## Glossary

| Term | Definition |
|------|------------|
| SideEffect.None | Intent classification indicating no side effects (chat intent) |
| Context Budget | Maximum token limit for workspace context assembly |
| Grounded Response | Response that references actual workspace state |
| Streaming Mode | Progressive token delivery via SSE |
