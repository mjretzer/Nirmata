# Streaming Protocol Implementation Guide

## Overview

The GMSD streaming protocol provides real-time visibility into orchestration workflows through Server-Sent Events (SSE). This guide covers the implementation of the v2 protocol with comprehensive observability and tracing capabilities.

## Protocol Versions

### Version 1 (Legacy)
- Basic event envelope with type, timestamp, and payload
- Limited event types (9 core types)
- No correlation ID or sequence number support
- Backward compatible with v2

### Version 2 (Current)
- Enhanced envelope with correlation ID and sequence number
- Expanded event types (20+ types including tool calling loop events)
- Full request tracing support
- Backward compatible with v1 clients

## Core Components

### 1. Event Validation (StreamingEventValidator)

Validates all streaming events against schema and type constraints.

```csharp
var validator = new StreamingEventValidator();
var result = validator.ValidateEvent(@event);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Validation error: {error}");
    }
}
```

### 2. Orchestration Event Emitter (OrchestrationEventEmitter)

Emits events with automatic correlation ID and sequence number management.

```csharp
var emitter = new OrchestrationEventEmitter(eventSink);
emitter.SetCorrelationId(correlationId);

// Emit classification event
await emitter.EmitClassificationEventAsync(
    "Write",
    0.95,
    "User wants to create a file",
    "Create a new file");

// Emit gating event
await emitter.EmitGatingEventAsync(
    "Executor",
    "Routing to executor phase",
    false,
    proposedAction);
```

### 3. Tracing Infrastructure (ITracingProvider)

Provides distributed tracing with correlation ID and run ID tracking.

```csharp
var tracingProvider = new TracingProvider();

// Start a trace context
using (var traceContext = tracingProvider.StartTrace(correlationId))
{
    // Create a span
    using (var span = tracingProvider.CreateSpan("ProcessRequest"))
    {
        span.SetAttribute("user_id", userId);
        tracingProvider.RecordEvent("processing_started");
        
        // Do work...
        
        tracingProvider.RecordEvent("processing_completed");
    }
}
```

### 4. LLM Interceptors

Monitor and control LLM calls at the provider boundary.

```csharp
// Create interceptors
var loggingInterceptor = new LlmLoggingInterceptor(logger);
var safetyInterceptor = new LlmSafetyInterceptor(logger);

// Use in LLM provider
var context = new LlmRequestContext
{
    Model = "gpt-4",
    Prompt = userPrompt,
    CorrelationId = correlationId
};

// Interceptors are called before/after LLM requests
await loggingInterceptor.OnBeforeRequestAsync(context);
// ... LLM call ...
await loggingInterceptor.OnAfterResponseAsync(responseContext);
```

### 5. Enhanced Event Sink

Provides buffering, filtering, and sampling capabilities.

```csharp
var options = new EventSinkOptions
{
    EnableBuffering = true,
    BufferSize = 1000,
    SamplingRate = 1.0,
    EventTypeFilter = new HashSet<StreamingEventType>
    {
        StreamingEventType.IntentClassified,
        StreamingEventType.Error
    }
};

var enhancedSink = new EnhancedEventSink(innerSink, options);

// Events are automatically buffered and filtered
await enhancedSink.EmitAsync(@event);

// Get statistics
var stats = enhancedSink.GetStatistics();
Console.WriteLine($"Emitted: {stats.TotalEventsEmitted}, Filtered: {stats.TotalEventsFiltered}");
```

## Event Sequences

### Classification → Gating → Dispatch → Execution

```
┌─────────────────────┐
│ intent.classified   │
│ (category: Write)   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  gate.selected      │
│ (phase: Executor)   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  run.started        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ phase.started       │
│ (phase: Executor)   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  tool.call          │
│  tool.result        │
│  (pairs)            │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ phase.completed     │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  run.finished       │
└─────────────────────┘
```

### Tool Calling Loop

```
┌──────────────────────────┐
│ tool.call.detected       │
│ (iteration: 1)           │
└──────────┬───────────────┘
           │
           ▼
┌──────────────────────────┐
│ tool.call.started        │
│ tool.call.completed      │
│ (per tool)               │
└──────────┬───────────────┘
           │
           ▼
┌──────────────────────────┐
│ tool.results.submitted   │
└──────────┬───────────────┘
           │
           ▼
┌──────────────────────────┐
│ tool.loop.iteration      │
│ .completed               │
└──────────┬───────────────┘
           │
           ▼
┌──────────────────────────┐
│ tool.loop.completed      │
│ (or tool.loop.failed)    │
└──────────────────────────┘
```

## Backward Compatibility

### Upgrading V1 Events to V2

```csharp
var v1Event = new StreamingEvent { /* ... */ };
var v2Event = BackwardCompatibilityHandler.UpgradeV1ToV2(v1Event);
// Automatically adds correlationId and sequenceNumber
```

### Downgrading V2 Events to V1

```csharp
var v2Event = new StreamingEvent { /* ... */ };
var v1Event = BackwardCompatibilityHandler.DowngradeV2ToV1(v2Event);
// Removes V2-specific fields for legacy client compatibility
```

## Configuration

### Tracing Configuration

```json
{
  "Observability": {
    "Tracing": {
      "Enabled": true,
      "SamplingRate": 1.0,
      "IncludeStackTraces": true
    }
  }
}
```

### Event Sink Configuration

```json
{
  "Streaming": {
    "EventSink": {
      "BufferingEnabled": true,
      "BufferSize": 1000,
      "SamplingRate": 1.0,
      "FilteredEventTypes": []
    }
  }
}
```

## Best Practices

1. **Always set correlation ID** - Ensures request tracing across all components
2. **Use sequence numbers** - Enables proper event ordering on the client
3. **Validate events** - Use StreamingEventValidator before emission
4. **Monitor LLM calls** - Use interceptors for safety and performance monitoring
5. **Buffer events** - Use EnhancedEventSink for production reliability
6. **Handle errors gracefully** - Always emit error events with recovery information

## Testing

### Unit Tests

```csharp
[Fact]
public async Task EmitClassificationEvent_WithValidData_SuccessfullyEmits()
{
    var eventSink = new TestEventSink();
    var emitter = new OrchestrationEventEmitter(eventSink);

    var result = await emitter.EmitClassificationEventAsync(
        "Write",
        0.92,
        "User input contains write operation keywords",
        "Create a new file");

    result.Should().BeTrue();
    eventSink.Events.Should().HaveCount(1);
}
```

### Integration Tests

See `StreamingProtocolIntegrationTests.cs` for complete workflow examples.

## Troubleshooting

### Events Not Being Emitted

1. Check that IEventSink is properly registered in DI
2. Verify correlation ID is set
3. Validate event payload matches expected type
4. Check event sink is not completed

### Validation Failures

1. Use StreamingEventValidator to identify issues
2. Ensure all required fields are populated
3. Verify enum values are correct (e.g., status must be "started" or "completed")
4. Check confidence scores are between 0.0 and 1.0

### Tracing Not Working

1. Verify TracingProvider is registered in DI
2. Check that correlation ID is being set
3. Ensure AsyncLocal context is not being cleared
4. Verify tracing configuration is enabled

## References

- [Streaming Events Documentation](./streaming-events.md)
- [Tool Calling Protocol](./tool-calling-protocol.md)
- [UI Contract Command Suggestion](./ui-contract-command-suggestion.md)
