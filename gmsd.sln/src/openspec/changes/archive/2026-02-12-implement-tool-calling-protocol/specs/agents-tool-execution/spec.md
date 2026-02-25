## ADDED Requirements

### Requirement: Tool calling loop interface is defined
The system SHALL define an `IToolCallingLoop` interface as the primary contract for multi-step tool calling conversations.

The interface MUST specify:
- `Task<ToolCallingResult> ExecuteAsync(ToolCallingRequest request, CancellationToken cancellationToken)` - executes the conversation loop

`ToolCallingRequest` MUST include:
- `IReadOnlyList<LlmMessage> InitialMessages` - starting conversation messages
- `IReadOnlyList<LlmToolDefinition> AvailableTools` - tools the LLM can invoke
- `ToolCallingOptions Options` - loop behavior configuration
- `string RunId` - correlation ID for evidence and events
- `string? ParentCallId` - optional parent call identifier for nested loops

`ToolCallingResult` MUST include:
- `LlmMessage FinalMessage` - the assistant's final response (may be null if loop failed)
- `IReadOnlyList<LlmMessage> CompleteConversation` - full message history including tool results
- `int IterationCount` - number of conversation turns (LLM requests)
- `int TotalToolCallsExecuted` - total number of tool calls executed
- `bool IsSuccess` - whether the loop completed successfully
- `string? ErrorMessage` - error details if loop failed
- `LlmUsage? TokenUsage` - cumulative token usage across all LLM calls

#### Scenario: Simple tool calling conversation succeeds
- **GIVEN** a `ToolCallingRequest` with one user message, one available tool, and default options
- **WHEN** `ExecuteAsync` is called and the LLM requests the tool
- **THEN** the tool is executed, result is sent back, LLM responds, and `ToolCallingResult` contains the final assistant message

#### Scenario: Multi-turn tool calling with iterative reasoning
- **GIVEN** a `ToolCallingRequest` where the LLM requires two sequential tool calls to answer
- **WHEN** `ExecuteAsync` runs
- **THEN** first tool is called and result submitted, LLM requests second tool, second tool is called and result submitted, LLM provides final answer
- **AND** `ToolCallingResult.IterationCount` equals 2

### Requirement: Tool calling options control loop behavior
The system SHALL define `ToolCallingOptions` to configure conversation loop constraints.

`ToolCallingOptions` MUST include:
- `int MaxIterations` - maximum conversation turns (default: 10, minimum: 1)
- `int MaxToolCallsPerTurn` - maximum parallel tool calls per turn (default: 32)
- `int TimeoutSeconds` - wall-clock timeout for entire loop (default: 300)
- `int? MaxTokens` - maximum total tokens across all LLM calls (null = unlimited)
- `bool AllowParallelToolCalls` - whether to execute multiple tool calls in parallel (default: true)
- `bool RequireConfirmationForDestructive` - whether to prompt before destructive tools (default: false)

The loop MUST enforce all constraints and terminate with appropriate error if exceeded.

#### Scenario: Max iterations prevents infinite loops
- **GIVEN** a `ToolCallingOptions` with `MaxIterations = 3`
- **WHEN** the LLM continues requesting tool calls beyond the limit
- **THEN** the loop terminates after the 3rd iteration with `IsSuccess = false` and error message indicating iteration limit exceeded

#### Scenario: Timeout terminates long-running loops
- **GIVEN** a `ToolCallingOptions` with `TimeoutSeconds = 5`
- **WHEN** tool execution takes longer than 5 seconds
- **THEN** the loop terminates with `IsSuccess = false` and error message indicating timeout

#### Scenario: Parallel tool calls execute concurrently
- **GIVEN** a `ToolCallingOptions` with `AllowParallelToolCalls = true`
- **WHEN** the LLM requests 5 tool calls in a single turn
- **THEN** all 5 tools execute concurrently and results are submitted together

### Requirement: Tool call execution results are captured
The system SHALL define `ToolCallExecutionResult` to capture the outcome of individual tool invocations within the loop.

`ToolCallExecutionResult` MUST include:
- `string ToolCallId` - identifier matching the original `LlmToolCall.Id`
- `string ToolName` - name of the tool that was invoked
- `bool IsSuccess` - whether the tool execution succeeded
- `string? ResultContent` - stringified result for LLM consumption (null if failed)
- `string? ErrorCode` - error classification when failed
- `string? ErrorMessage` - human-readable error when failed
- `TimeSpan ExecutionDuration` - time taken to execute the tool
- `DateTimeOffset ExecutedAt` - timestamp of execution

#### Scenario: Successful tool call result is captured
- **GIVEN** a tool call `read_file` with ID `call_123` that succeeds
- **WHEN** the tool executes and returns file content
- **THEN** `ToolCallExecutionResult` has `IsSuccess = true`, `ResultContent` contains file content, and `ToolCallId = "call_123"`

#### Scenario: Failed tool call result captures error details
- **GIVEN** a tool call that throws an exception
- **WHEN** the tool fails
- **THEN** `ToolCallExecutionResult` has `IsSuccess = false`, `ErrorCode = "ToolExecutionError"`, and `ErrorMessage` contains exception details

### Requirement: Tool calling loop implements 5-step conversation protocol
The system SHALL implement `ToolCallingLoop` that executes the canonical 5-step conversation cycle.

The 5-step protocol:
1. **Send** conversation + available tools to LLM via `ILlmProvider`
2. **Detect** if LLM response contains `ToolCalls` (finish_reason = "tool_calls")
3. **Execute** each tool call via `IToolRegistry` resolution and `ITool.InvokeAsync`
4. **Submit** tool results as `LlmMessage` with `Role = Tool` back to the conversation
5. **Repeat** from step 1 until LLM responds without tool calls, max iterations, or error

The loop MUST:
- Maintain message history across iterations
- Execute parallel tool calls concurrently (when `AllowParallelToolCalls = true`)
- Convert tool results to `LlmMessage` format with proper `ToolCallId` correlation
- Emit events at each state transition
- Capture evidence of the complete conversation

#### Scenario: Complete 5-step cycle executes once
- **GIVEN** a user asks "What files are in /docs?"
- **WHEN** the loop executes
- **THEN** Step 1: Send messages + `list_directory` tool to LLM
- **AND** Step 2: LLM requests `list_directory` tool call
- **AND** Step 3: Tool executes and returns file list
- **AND** Step 4: Result submitted as Tool message
- **AND** Step 5: LLM provides final answer without additional tool calls, loop completes

#### Scenario: Multi-turn cycle with context accumulation
- **GIVEN** a user asks "Read file A, then read file B that A references"
- **WHEN** the loop executes
- **THEN** First turn: `read_file(A)` → result submitted → LLM requests `read_file(B)`
- **AND** Second turn: `read_file(B)` → result submitted → LLM provides final answer
- **AND** Final conversation contains 4 messages: user, assistant (tool call), tool result, assistant (final)

### Requirement: Tool calling loop emits observable events
The system SHALL emit typed events for each significant state transition in the conversation loop.

Required events:
- `ToolCallDetectedEvent` - LLM requested one or more tool calls (contains `IReadOnlyList<LlmToolCall>`)
- `ToolCallStartedEvent` - Individual tool execution began (contains `ToolCallId`, `ToolName`)
- `ToolCallCompletedEvent` - Tool execution succeeded (contains `ToolCallExecutionResult`)
- `ToolCallFailedEvent` - Tool execution failed (contains `ToolCallExecutionResult` with error)
- `ToolResultsSubmittedEvent` - All tool results sent back to LLM (contains count, total size)
- `ToolLoopIterationCompletedEvent` - One full cycle completed (contains iteration number)
- `ToolLoopCompletedEvent` - Loop finished normally (contains final message, iteration count, token usage)
- `ToolLoopFailedEvent` - Loop terminated abnormally (contains failure reason, partial results)

Events MUST be emitted via `IEventEmitter` or equivalent event infrastructure.

#### Scenario: Single tool call emits full event sequence
- **GIVEN** a conversation where LLM requests one tool
- **WHEN** the loop executes
- **THEN** Events fire in order: `ToolCallDetectedEvent` → `ToolCallStartedEvent` → `ToolCallCompletedEvent` → `ToolResultsSubmittedEvent` → `ToolLoopCompletedEvent`

#### Scenario: Parallel tool calls emit individual started/completed events
- **GIVEN** a conversation where LLM requests 3 tools simultaneously
- **WHEN** the loop executes with parallel execution enabled
- **THEN** One `ToolCallDetectedEvent` with 3 calls
- **AND** Three `ToolCallStartedEvent` (one per tool)
- **AND** Three `ToolCallCompletedEvent` (as each finishes)
- **AND** One `ToolResultsSubmittedEvent` with all 3 results

### Requirement: Tool calling conversation evidence is captured
The system SHALL capture complete evidence of tool calling conversations for auditability.

Evidence MUST be written to `.aos/evidence/runs/<run-id>/tool-calling/<call-id>.json` containing:
- `schemaVersion: 1`
- `runId` - correlation ID
- `callId` - unique identifier for this tool calling loop
- `parentCallId` - if nested within another loop
- `startTimeUtc` - loop start timestamp
- `endTimeUtc` - loop end timestamp
- `durationMs` - total milliseconds
- `requestSummary` - initial messages count, tools available
- `iterations` - array of iteration records:
  - `iterationNumber` - 1-based iteration index
  - `llmRequestSummary` - messages sent to LLM
  - `llmResponseSummary` - finish reason, tool calls requested
  - `toolExecutions` - array of `ToolCallExecutionResult`
- `finalMessage` - assistant's final response content
- `cumulativeTokenUsage` - total tokens consumed
- `isSuccess` - whether loop completed successfully
- `errorMessage` - if failed

#### Scenario: Evidence file is written on successful completion
- **GIVEN** a successful tool calling loop with run ID `RUN-abc123`
- **WHEN** the loop completes
- **THEN** a JSON file exists at `.aos/evidence/runs/RUN-abc123/tool-calling/toolcall-<guid>.json`
- **AND** the file contains complete conversation history and execution results

#### Scenario: Evidence is written even on failure
- **GIVEN** a tool calling loop that hits max iterations
- **WHEN** the loop fails
- **THEN** evidence file is still written with `isSuccess = false`, partial iterations recorded, and error details
