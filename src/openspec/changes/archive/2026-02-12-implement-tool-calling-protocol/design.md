# Design: Tool Calling Multi-Step Protocol

## Context

The orchestrator architecture (phases, dispatchers, evidence) is well-suited to capture tool calling as a conversation loop—but only if the LLM layer can actually support it. Currently, tool calling is handled ad-hoc: the LLM provider returns tool calls, and each workflow implements its own (often incomplete) execution logic.

This design establishes a reusable, observable, and evidence-capturing protocol for the complete tool calling conversation loop.

## Goals / Non-Goals

**Goals:**
- Provide a first-class protocol for the 5-step tool calling conversation loop
- Integrate with existing `ILlmProvider` and `IToolRegistry` abstractions
- Emit events for each step of the loop (observability)
- Capture evidence of the complete conversation (auditability)
- Support multiple tool calls in a single turn (parallel execution)
- Support iterative tool calling (model can request more tools after receiving results)

**Non-Goals:**
- Replace existing tool definitions or registry (additive only)
- Implement specific tool functionality (file system, process, git)
- Change LLM provider abstraction layer (works within existing `ILlmProvider`)
- Handle tool UI rendering (web layer responsibility)

## Decisions

### Decision: Single-loop implementation vs. workflow-embedded loops
**Decision**: Implement a standalone `ToolCallingLoop` service that workflows invoke.

**Rationale**:
- Eliminates duplicate loop implementations across workflows
- Centralizes loop control logic (budgets, timeouts, iteration limits)
- Single point for evidence capture and event emission
- Easier to test and reason about

**Alternative considered**: Each workflow (interviewer, planner, executor) implements its own loop. Rejected due to code duplication and inconsistent behavior.

### Decision: Synchronous loop execution with async steps
**Decision**: The loop runs sequentially (step 1 → 2 → 3 → 4 → 5 → repeat), but tool execution can be parallel within a single turn.

**Rationale**:
- LLMs typically expect tool results before generating next response
- Parallel execution for multiple tool calls in one turn is supported
- Simpler mental model for debugging and evidence capture

### Decision: Event-driven observability
**Decision**: Emit typed events for each significant state transition in the loop.

**Events emitted**:
- `tool.call.detected` - LLM requested tool calls
- `tool.call.started` - Individual tool call execution began
- `tool.call.completed` - Tool call succeeded with result
- `tool.call.failed` - Tool call execution failed
- `tool.results.submitted` - All results sent back to LLM
- `tool.loop.iteration` - Completed one full iteration
- `tool.loop.completed` - Loop finished (assistant message or max iterations)

### Decision: Budget controls at loop level
**Decision**: Enforce limits at the loop boundary, not per-workflow.

**Controls**:
- `MaxIterations` - maximum conversation turns (default: 10)
- `MaxToolCallsPerTurn` - maximum parallel calls (default: 32)
- `TimeoutSeconds` - wall-clock timeout (default: 300)
- `MaxTokens` - total token budget for the conversation

**Rationale**:
- Prevents runaway tool calling conversations
- Consistent protection across all workflows
- Can be tuned per-workflow via `ToolCallingOptions`

### Decision: Tool results are normalized to `LlmMessage`
**Decision**: After executing tool calls, results are formatted as `LlmMessage` with `Role = Tool` for submission back to the LLM.

**Rationale**:
- Follows existing `agents-llm-provider-abstraction` patterns
- Compatible with existing provider adapters (OpenAI, Anthropic, etc.)
- Clear correlation via `ToolCallId` matching

## Risks / Trade-offs

**Risk**: Long-running tool calls block the loop
→ Mitigation: Tool implementations should handle their own timeouts; loop has global timeout

**Risk**: Token usage grows unbounded in multi-turn conversations
→ Mitigation: `MaxTokens` budget + loop tracks cumulative usage + can truncate/summarize history

**Risk**: Tool call failures leave LLM without context
→ Mitigation: Failed calls still submit error as tool result; LLM can retry or respond with error explanation

**Risk**: Infinite loops with circular tool dependencies
→ Mitigation: `MaxIterations` hard limit + option for human confirmation after N iterations

## Migration Plan

**Steps**:
1. Implement `IToolCallingLoop` and `ToolCallingLoop` in `nirmata.Agents.Execution.ToolCalling`
2. Add event types to `orchestrator-event-emitter` spec
3. Integrate into `SubagentOrchestrator` as the execution backend
4. Update `TaskExecutor` to use the loop instead of direct tool execution
5. Add SSE event handling in `nirmata.Web` for tool lifecycle events

**Rollback**: Can disable by reverting `SubagentOrchestrator` to previous direct execution pattern. Evidence captured in `.aos/evidence/` is backward-compatible.

## Open Questions

- Should we support "human-in-the-loop" confirmation for expensive/destructive tool calls within the loop?
- How should we handle tool call streaming (partial arguments) from providers that support it?
- Should there be a "resume" capability if the loop is interrupted mid-iteration?
