## 1. Core Protocol Implementation

- [x] 1.1 Create `Gmsd.Agents.Execution.ToolCalling` namespace structure
- [x] 1.2 Define `IToolCallingLoop` interface with `ExecuteAsync` method
- [x] 1.3 Define `ToolCallingRequest` model (messages, tools, options, context)
- [x] 1.4 Define `ToolCallingResult` model (final message, conversation history, usage stats)
- [x] 1.5 Define `ToolCallingOptions` model (max iterations, timeout, token limits)
- [x] 1.6 Define `ToolCallExecutionResult` model (correlates tool call to result/error)
- [x] 1.7 Implement `ToolCallingLoop` class with 5-step conversation protocol
- [x] 1.8 Implement parallel tool call execution within a single turn
- [x] 1.9 Implement iteration tracking and budget enforcement
- [x] 1.10 Write unit tests for core loop logic

## 2. Event System Integration

- [x] 2.1 Define `ToolCallDetectedEvent` (when LLM requests tool calls)
- [x] 2.2 Define `ToolCallStartedEvent` (when individual tool execution begins)
- [x] 2.3 Define `ToolCallCompletedEvent` (when tool execution succeeds)
- [x] 2.4 Define `ToolCallFailedEvent` (when tool execution fails)
- [x] 2.5 Define `ToolResultsSubmittedEvent` (when results sent back to LLM)
- [x] 2.6 Define `ToolLoopIterationCompletedEvent` (one full cycle completed)
- [x] 2.7 Define `ToolLoopCompletedEvent` (loop finished normally)
- [x] 2.8 Integrate event emission into `ToolCallingLoop`
- [x] 2.9 Write unit tests for event emission

## 3. LLM Provider Integration

- [x] 3.1 Update `LlmCompletionRequest` builder to support incremental message accumulation
- [x] 3.2 Implement tool call detection from `LlmCompletionResponse` (finish_reason = tool_calls)
- [x] 3.3 Implement tool result message formatting for `LlmMessage`
- [x] 3.4 Add streaming support detection for tool arguments (if provider supports)
- [x] 3.5 Write integration tests with `SemanticKernelLlmProvider`

## 4. Tool Registry Integration

- [x] 4.1 Integrate `IToolRegistry` resolution in `ToolCallingLoop`
- [x] 4.2 Implement tool call parameter deserialization from JSON
- [x] 4.3 Implement tool result serialization to string for LLM consumption
- [x] 4.4 Handle missing/unregistered tool errors gracefully
- [x] 4.5 Write integration tests with `ToolRegistry`

## 5. Evidence Capture

- [x] 5.1 Define `ToolCallingConversationEvidence` model
- [x] 5.2 Capture complete message history (user, assistant, tool) per loop
- [x] 5.3 Capture tool call executions with timing and results
- [x] 5.4 Integrate with `IAosEvidenceWriter` for evidence persistence
- [x] 5.5 Write to `.aos/evidence/runs/<run-id>/tool-calling/<call-id>.json`
- [x] 5.6 Write unit tests for evidence capture

## 6. Subagent Orchestrator Integration

- [x] 6.1 Refactor `ISubagentOrchestrator` to use `IToolCallingLoop` for execution
- [x] 6.2 Wire up `ToolCallingOptions` from `SubagentRunRequest` configuration
- [x] 6.3 Pass context pack tools to the loop's available tools
- [x] 6.4 Handle loop completion/failure in subagent result translation
- [x] 6.5 Write integration tests for subagent with tool calling

## 7. Task Executor Integration

- [x] 7.1 Update `TaskExecutor` to delegate tool execution through the loop
- [x] 7.2 Map task file scopes to tool calling context
- [x] 7.3 Handle tool call failures in task execution result
- [x] 7.4 Write integration tests for task execution with tools

## 8. Web Layer (SSE Events)

- [x] 8.1 Add client-side event types for tool lifecycle in `ChatStreamingController`
- [x] 8.2 Implement server-side event conversion from loop events to SSE
- [x] 8.3 Add UI message rendering for tool call detection
- [x] 8.4 Add UI message rendering for tool execution progress
- [x] 8.5 Add UI message rendering for tool results
- [x] 8.6 Write end-to-end tests for streaming tool events

## 9. Validation & Hardening

- [x] 9.1 Run `openspec validate implement-tool-calling-protocol --strict`
- [x] 9.2 Add load tests for parallel tool execution (32 concurrent calls)
- [x] 9.3 Add stress tests for max iterations enforcement
- [x] 9.4 Add timeout handling tests
- [x] 9.5 Verify backward compatibility with existing subagent workflows
- [x] 9.6 Document API changes in `docs/tool-calling-protocol.md`

## 10. Documentation & Examples

- [x] 10.1 Update `Gmsd.Agents` README with tool calling documentation
- [x] 10.2 Add code example: simple calculator tool calling loop
- [x] 10.3 Add code example: file system operations with loop
- [x] 10.4 Document event types and SSE contract for frontend developers
