# Tool Calling Protocol

This document describes the tool calling protocol implemented in GMSD, which enables multi-step conversational tool use with LLMs.

## Overview

The tool calling protocol is a first-class, multi-step conversation protocol that manages the iterative cycle of:

1. **Send** tools + messages to the model
2. **Model emits** a tool call request
3. **Application executes** the tool call
4. **Application sends** tool results back to the model
5. **Model produces** next response (or additional tool calls)

This continues until the conversation completes naturally or hits configured limits.

## Core Components

### `IToolCallingLoop` Interface

The main entry point for tool calling execution:

```csharp
public interface IToolCallingLoop
{
    Task<ToolCallingResult> ExecuteAsync(
        ToolCallingRequest request,
        CancellationToken cancellationToken = default);
}
```

### `ToolCallingRequest`

Defines the input for a tool calling session:

```csharp
public sealed record ToolCallingRequest
{
    public required IReadOnlyList<ToolCallingMessage> Messages { get; init; }
    public IReadOnlyList<ToolCallingToolDefinition> Tools { get; init; } = Array.Empty<ToolCallingToolDefinition>();
    public ToolCallingOptions Options { get; init; } = new();
    public IReadOnlyDictionary<string, object?> Context { get; init; } = new Dictionary<string, object?>();
    public string? CorrelationId { get; init; }
}
```

### `ToolCallingResult`

Contains the outcome of a tool calling session:

```csharp
public sealed record ToolCallingResult
{
    public required ToolCallingMessage FinalMessage { get; init; }
    public required IReadOnlyList<ToolCallingMessage> ConversationHistory { get; init; }
    public int IterationCount { get; init; }
    public ToolCallingCompletionReason CompletionReason { get; init; }
    public ToolCallingError? Error { get; init; }
    public ToolCallingUsageStats? Usage { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
```

### `ToolCallingOptions`

Controls the behavior of the tool calling loop:

| Property | Default | Description |
|----------|---------|-------------|
| `MaxIterations` | 10 | Maximum number of LLM calls allowed |
| `Timeout` | 5 minutes | Maximum time for the entire loop |
| `MaxTotalTokens` | null | Total token budget across all iterations |
| `Temperature` | null | LLM temperature setting |
| `MaxTokensPerCompletion` | null | Max tokens per LLM response |
| `Model` | null | Specific model to use |
| `ToolChoice` | null | Force specific tool usage |
| `EnableParallelToolExecution` | true | Execute multiple tools in parallel |
| `MaxParallelToolExecutions` | 32 | Maximum concurrent tool executions |

## Completion Reasons

The loop can complete for several reasons:

| Reason | Description |
|--------|-------------|
| `CompletedNaturally` | LLM responded without requesting more tools |
| `MaxIterationsReached` | Hit the iteration limit |
| `Timeout` | Exceeded the configured timeout |
| `Cancelled` | Operation was cancelled via CancellationToken |
| `Error` | An error occurred during execution |

## Event System

The tool calling loop emits events for observability:

### Event Types

- **`ToolCallDetectedEvent`** - LLM requested tool calls
- **`ToolCallStartedEvent`** - Individual tool execution began
- **`ToolCallCompletedEvent`** - Tool execution succeeded
- **`ToolCallFailedEvent`** - Tool execution failed
- **`ToolResultsSubmittedEvent`** - Results sent back to LLM
- **`ToolLoopIterationCompletedEvent`** - One full iteration completed
- **`ToolLoopCompletedEvent`** - Loop finished normally
- **`ToolLoopFailedEvent`** - Loop terminated due to error

### Event Structure

All events implement `ToolCallingEvent`:

```csharp
public abstract record ToolCallingEvent
{
    public required string CorrelationId { get; init; }
    public int Iteration { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
```

## Parallel Tool Execution

When `EnableParallelToolExecution` is true (default), multiple tool calls requested in a single turn are executed concurrently with a semaphore-controlled limit (`MaxParallelToolExecutions`, default 32).

Example: If the LLM requests 50 tool calls and `MaxParallelToolExecutions` is 32, the first 32 will execute in parallel, then the remaining 18 will execute as slots become available.

## Timeout Handling

The loop supports two timeout mechanisms:

1. **Per-loop timeout** - Configured via `ToolCallingOptions.Timeout`
2. **External cancellation** - Via `CancellationToken`

When a timeout occurs:
- The loop returns with `CompletionReason = Timeout`
- A `ToolCallingError` with code "Timeout" is included
- A `ToolLoopFailedEvent` is emitted
- Partial results may be available in `ConversationHistory`

## Max Iterations Enforcement

The loop enforces the `MaxIterations` limit strictly:

- Iteration counter increments before each LLM call
- Loop exits immediately when `iteration >= MaxIterations`
- Returns with `CompletionReason = MaxIterationsReached`

Setting `MaxIterations = 0` will cause the loop to fail immediately without calling the LLM.

## Integration with Subagent Orchestrator

The tool calling loop is integrated with `ISubagentOrchestrator`:

```csharp
// Budget mapping
var toolCallingOptions = new ToolCallingOptions
{
    MaxIterations = request.Budget.MaxIterations,
    Timeout = TimeSpan.FromSeconds(request.Budget.MaxExecutionTimeSeconds),
    MaxTotalTokens = request.Budget.MaxTokens,
    EnableParallelToolExecution = true,
    MaxParallelToolExecutions = 32
};
```

### Backward Compatibility

Existing subagent workflows remain compatible:
- Subagent configurations work without changes
- Budget settings map correctly to loop options
- Context data passes through unchanged
- Result structure maintains expected fields
- Error handling preserves existing behavior

## Usage Examples

### Basic Tool Calling

```csharp
var request = new ToolCallingRequest
{
    Messages = new[]
    {
        ToolCallingMessage.System("You are a helpful assistant."),
        ToolCallingMessage.User("What's the weather in New York?")
    },
    Tools = new[]
    {
        new ToolCallingToolDefinition
        {
            Name = "get_weather",
            Description = "Get weather for a location",
            ParametersSchema = new
            {
                type = "object",
                properties = new
                {
                    location = new { type = "string" },
                    unit = new { type = "string", @enum = new[] { "celsius", "fahrenheit" } }
                },
                required = new[] { "location" }
            }
        }
    },
    Options = new ToolCallingOptions
    {
        MaxIterations = 5,
        Timeout = TimeSpan.FromSeconds(30)
    }
};

var result = await toolCallingLoop.ExecuteAsync(request);
```

### Parallel Tool Execution

```csharp
var request = new ToolCallingRequest
{
    Messages = new[] { ToolCallingMessage.User("Get data from 50 sources") },
    Tools = dataSourceTools,
    Options = new ToolCallingOptions
    {
        EnableParallelToolExecution = true,
        MaxParallelToolExecutions = 32
    }
};

// All 50 tools will execute with max 32 concurrent
var result = await toolCallingLoop.ExecuteAsync(request);
```

### With Event Monitoring

```csharp
public class LoggingEventEmitter : IToolCallingEventEmitter
{
    private readonly ILogger _logger;

    public void Emit(ToolCallingEvent @event)
    {
        switch (@event)
        {
            case ToolCallDetectedEvent detected:
                _logger.LogInformation("Tool call detected: {ToolName}", 
                    detected.ToolCalls.First().ToolName);
                break;
            case ToolCallCompletedEvent completed:
                _logger.LogInformation("Tool completed: {ToolName} in {DurationMs}ms",
                    completed.ToolName, completed.Duration.TotalMilliseconds);
                break;
            case ToolLoopCompletedEvent loopCompleted:
                _logger.LogInformation("Loop completed in {Iterations} iterations",
                    loopCompleted.TotalIterations);
                break;
        }
    }
}
```

## Evidence Capture

The tool calling conversation is captured as evidence:

- Location: `.aos/evidence/runs/{run-id}/tool-calling/`
- Format: JSON with full message history
- Includes: All iterations, tool calls, results, timing, errors

## API Changes

### New Types

- `IToolCallingLoop` - Main interface
- `ToolCallingLoop` - Implementation
- `ToolCallingRequest` - Input model
- `ToolCallingResult` - Output model
- `ToolCallingOptions` - Configuration
- `ToolCallingMessage` - Conversation message
- `ToolCallingToolDefinition` - Tool definition
- `ToolCallingEvent` (and derived types) - Events

### Modified Types

- `SubagentOrchestrator` - Now uses `IToolCallingLoop`
- `ISubagentOrchestrator` - No breaking changes
- `SubagentRunResult` - No breaking changes

## Configuration

### DI Registration

```csharp
services.AddSingleton<IToolCallingLoop>(sp => new ToolCallingLoop(
    sp.GetRequiredService<IChatCompletionService>(),
    sp.GetRequiredService<IToolRegistry>(),
    sp.GetService<IToolCallingEventEmitter>())); // Optional
```

### Options Pattern

```csharp
services.Configure<ToolCallingOptions>(options =>
{
    options.MaxIterations = 15;
    options.Timeout = TimeSpan.FromMinutes(3);
    options.MaxParallelToolExecutions = 16;
});
```

## Testing

### Unit Tests

- `ToolCallingLoopTests.cs` - Core loop logic
- `ToolCallingStressTests.cs` - Load and stress tests

### Integration Tests

- `SubagentOrchestratorToolCallingTests.cs` - Orchestrator integration
- `SubagentOrchestratorBackwardCompatibilityTests.cs` - Compatibility tests

### Key Test Scenarios

- 32 concurrent tool calls
- Max iterations enforcement (1, 3, 5, 10, 50)
- Timeout during LLM calls
- Timeout during tool execution
- Timeout across multiple iterations
- Tool failures and recovery
- Parallel execution with throttling

## Performance Considerations

- Parallel execution reduces total time for multiple tools
- Semaphore prevents resource exhaustion
- Token budget enforcement prevents runaway costs
- Timeout prevents hung operations
- Iteration limits prevent infinite loops

## Error Handling

### Tool Not Found

When a tool is requested but not in the registry:
- Error is recorded in conversation history
- LLM receives error message as tool result
- Loop continues (LLM may retry or complete)

### Tool Execution Failure

When a tool throws or returns `ToolResult.Failure`:
- Error details sent to LLM
- Loop continues (LLM may handle gracefully)

### LLM Provider Errors

When the LLM provider fails:
- `ToolLoopFailedEvent` emitted
- Returns with `CompletionReason = Error`
- `ToolCallingError` contains exception details

## See Also

- `Gmsd.Agents.Execution.ToolCalling` namespace
- `IToolRegistry` - Tool resolution
- `IAosEvidenceWriter` - Evidence persistence
- `ISubagentOrchestrator` - Subagent integration
