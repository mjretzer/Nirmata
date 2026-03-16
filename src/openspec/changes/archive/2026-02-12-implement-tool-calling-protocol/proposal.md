# Change: Implement Tool Calling as First-Class Multi-Step Protocol

## Why

The current system has tool definitions (`ITool`, `ToolDescriptor`) and LLM provider abstraction (`ILlmProvider`, `LlmToolCall`), but lacks a first-class protocol for the conversation loop required for modern agentic tool use. Tool calling is currently treated as a "helper function" rather than a multi-step conversation protocol.

In modern agentic systems, tool calling is a conversation loop:
1. Send tools + messages to the model
2. Model emits a tool call
3. Application executes the tool call
4. Application sends tool results back to the model
5. Model produces next response (or additional tool calls)

Without this protocol, the orchestrator cannot support iterative reasoning with tools, and the "reasoning gates" remain invisible to users.

## What Changes

- **ADDED**: `IToolCallingLoop` interface defining the multi-step conversation protocol
- **ADDED**: `ToolCallingLoop` implementation managing the 5-step conversation cycle
- **ADDED**: `ToolCallExecutionResult` capturing tool execution outcomes for LLM consumption
- **ADDED**: `ToolCallingOptions` for controlling loop behavior (max iterations, timeout, etc.)
- **ADDED**: Event types for tool call lifecycle (`tool.call.requested`, `tool.call.executed`, `tool.call.completed`, `tool.call.failed`)
- **MODIFIED**: `ILlmProvider` interaction pattern to support iterative message building with tool results
- **ADDED**: Evidence capture for the complete tool calling conversation loop

## Impact

- **Affected specs**:
  - `agents-llm-provider-abstraction` - needs ADDED requirements for iterative tool calling support
  - `agents-tool-registry` - no changes (tool descriptors remain valid)
  - `agents-subagent-orchestration` - ADDED requirements for tool calling loop integration
  - `orchestrator-event-emitter` - ADDED event types for tool lifecycle
  - `aos-evidence-store` - ADDED evidence types for tool calling conversations

- **Affected code**:
  - `nirmata.Agents` - new `Execution.ToolCalling` namespace
  - `nirmata.Agents` - integration with `ISubagentOrchestrator`
  - `nirmata.Web` - SSE event handling for tool call events

- **Breaking changes**: None. This is purely additive to existing LLM provider and tool contracts.

## References

- Remediation.md section: "Tool calling must be treated as a first-class, multi-step protocol"
- Related existing specs: `agents-tool-registry`, `agents-llm-provider-abstraction`, `aos-tool-contracts`
