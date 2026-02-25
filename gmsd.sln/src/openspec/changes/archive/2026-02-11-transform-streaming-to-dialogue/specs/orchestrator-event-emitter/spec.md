# orchestrator-event-emitter — Specification

## Overview

Specifies the modifications to the GMSD Agents orchestrator to emit typed streaming events during classification, gating, and phase execution. This enables transparent observation of the orchestrator's decision-making process.

## ADDED Requirements

### Requirement: IEventSink Interface

The system MUST provide an event sink interface for emitting streaming events.

#### Scenario: Event Sink Creation
**Given** a streaming orchestration session
**When** the orchestrator begins execution
**Then** an `IEventSink` is available to emit events

**Interface Definition**:
```csharp
public interface IEventSink
{
    void Emit(StreamingEvent @event);
    void Emit(string eventType, object payload);
    bool IsEnabled { get; }
}
```

#### Scenario: Null Event Sink
**Given** a non-streaming orchestration context
**When** no event sink is provided
**Then** a null/no-op event sink is used
**And** orchestrator logic executes without event emission

### Requirement: Event-Aware Orchestrator Interface

The system MUST provide a streaming-capable orchestrator interface.

#### Scenario: Streaming Orchestration
**Given** a user request with streaming enabled
**When** `ExecuteWithEventsAsync` is called
**Then** the orchestrator returns `IAsyncEnumerable<StreamingEvent>`

**Interface Definition**:
```csharp
public interface IStreamingOrchestrator
{
    IAsyncEnumerable<StreamingEvent> ExecuteWithEventsAsync(
        WorkflowIntent intent,
        CancellationToken ct);
}
```

### Requirement: Intent Classification Events

The orchestrator MUST emit `intent.classified` events after classification completes.

#### Scenario: Classification Emission
**Given** a user input to classify
**When** the classifier determines intent
**Then** an `intent.classified` event is emitted with:
- Classification category (Chat, ReadOnly, Write)
- Confidence score (0.0 - 1.0)
- Reasoning explanation

#### Scenario: Chat Classification
**Given** user input "hello, how are you?"
**When** classification completes
**Then** event payload contains:
```json
{
  "classification": "Chat",
  "confidence": 0.98,
  "reasoning": "Greeting with no workflow-related keywords or intent indicators"
}
```

#### Scenario: Write Classification
**Given** user input "plan the foundation phase"
**When** classification completes
**Then** event payload contains:
```json
{
  "classification": "Write",
  "confidence": 0.91,
  "reasoning": "Contains planning verb and references phase creation"
}
```

### Requirement: Gate Selection Events

The orchestrator MUST emit `gate.selected` events when the gating engine selects a phase.

#### Scenario: Gating Emission
**Given** classified intent indicating workflow operation
**When** the gating engine selects target phase
**Then** a `gate.selected` event is emitted with:
- Target phase name
- Selection reasoning
- Confirmation requirement flag
- Proposed action details (if applicable)

#### Scenario: Planner Gate Selection
**Given** user input "create a plan for phase PH-001"
**When** gating engine selects Planner phase
**Then** event payload contains:
```json
{
  "targetPhase": "Planner",
  "reasoning": "Input matches planning intent with explicit phase reference",
  "requiresConfirmation": true,
  "proposedAction": {
    "type": "CreatePhasePlan",
    "phaseId": "PH-001",
    "description": "Generate task plan for phase PH-001"
  }
}
```

#### Scenario: Responder Gate Selection
**Given** user input "what is the status of my runs?"
**When** gating engine selects Responder phase
**Then** event payload contains:
```json
{
  "targetPhase": "Responder",
  "reasoning": "Informational query about run status",
  "requiresConfirmation": false,
  "proposedAction": null
}
```

### Requirement: Run Lifecycle Events

The orchestrator MUST emit run lifecycle events for write operations.

#### Scenario: Run Started Emission
**Given** a write workflow is initiated
**When** run execution begins
**Then** a `run.started` event is emitted with run identifier

#### Scenario: Run Finished Emission
**Given** a write workflow completes
**When** run execution ends (success or failure)
**Then** a `run.finished` event is emitted with:
- Run identifier
- Completion status
- Duration
- Artifact references

#### Scenario: Chat-Only Suppression
**Given** classification indicates Chat intent
**When** orchestrator responds conversationally
**Then** NO `run.started` or `run.finished` events are emitted

### Requirement: Phase Lifecycle Events

The orchestrator MUST emit phase lifecycle events during workflow execution.

#### Scenario: Phase Start Emission
**Given** a phase begins execution
**When** phase execution starts
**Then** a `phase.started` event is emitted with:
- Phase name
- Run identifier (if applicable)
- Context data

#### Scenario: Phase Complete Emission
**Given** a phase completes execution
**When** phase execution ends
**Then** a `phase.completed` event is emitted with:
- Phase name
- Completion status
- Output artifacts

### Requirement: Tool Call Events

The orchestrator MUST emit events when tools are invoked.

#### Scenario: Tool Call Emission
**Given** a phase invokes a tool
**When** tool execution is initiated
**Then** a `tool.call` event is emitted with:
- Unique call identifier
- Tool name
- Invocation arguments

#### Scenario: Tool Result Emission
**Given** a tool execution completes
**When** result is available
**Then** a `tool.result` event is emitted with:
- Call identifier (matching call event)
- Success status
- Result data or error
- Execution duration

#### Scenario: Tool Sequence
**Given** a phase uses multiple tools
**When** tools execute sequentially
**Then** `tool.call` and `tool.result` events are emitted in pairs
**And** call identifiers enable correlation

### Requirement: Assistant Dialogue Events

The orchestrator MUST emit assistant dialogue events for conversational responses.

#### Scenario: Assistant Delta Emission
**Given** an LLM generates a streaming response
**When** tokens are produced
**Then** `assistant.delta` events are emitted for each token chunk
**And** all deltas share the same `messageId`

#### Scenario: Assistant Final Emission
**Given** an LLM completes response generation
**When** the final token is received
**Then** an `assistant.final` event is emitted with:
- Complete message content
- Message identifier (matching deltas)
- Optional structured data

#### Scenario: Non-Streaming Assistant Response
**Given** an LLM response without streaming
**When** complete response is available
**Then** a single `assistant.final` event is emitted
**And** no `assistant.delta` events are emitted

### Requirement: Event Ordering

Events MUST be emitted in a sequence that reflects actual orchestration flow.

#### Scenario: Chat Response Sequence
**Given** a chat-only interaction
**When** orchestration completes
**Then** event order is:
1. `intent.classified` (Chat)
2. `gate.selected` (Responder)
3. `assistant.delta` (zero or more)
4. `assistant.final`

#### Scenario: Workflow Execution Sequence
**Given** a confirmed write workflow
**When** orchestration completes
**Then** event order is:
1. `intent.classified`
2. `gate.selected`
3. `run.started`
4. `phase.started`
5. Zero or more (`tool.call`, `tool.result`) pairs
6. `assistant.delta` (zero or more)
7. `assistant.final`
8. `phase.completed`
9. `run.finished`

### Requirement: Error Event Emission

The orchestrator MUST emit error events when failures occur.

#### Scenario: Classification Error
**Given** an error during intent classification
**When** exception is caught
**Then** an `error` event is emitted with:
- Error code
- Error message
- Recoverable flag
- Context (phase where error occurred)

#### Scenario: Phase Execution Error
**Given** an error during phase execution
**When** exception is caught
**Then** an `error` event is emitted
**And** `phase.completed` is emitted with success=false
**And** `run.finished` is emitted (if write operation)

## MODIFIED Requirements

None.

## REMOVED Requirements

None.

---

## Implementation Guidelines

### Event Channel Pattern

Use `Channel<StreamingEvent>` for async event buffering:

```csharp
public class StreamingOrchestrator : IStreamingOrchestrator
{
    private readonly Channel<StreamingEvent> _eventChannel;
    
    public async IAsyncEnumerable<StreamingEvent> ExecuteWithEventsAsync(...)
    {
        _ = Task.Run(async () => {
            await ExecuteAndEmitAsync(intent);
            _eventChannel.Writer.Complete();
        });
        
        await foreach (var evt in _eventChannel.Reader.ReadAllAsync())
        {
            yield return evt;
        }
    }
}
```

### Event Emission Timing

Events SHOULD be emitted synchronously at decision points:

| Decision Point | Event to Emit |
|---------------|---------------|
| Classification complete | `intent.classified` |
| Gate selection complete | `gate.selected` |
| User confirms action | `run.started` |
| Phase execution begins | `phase.started` |
| Tool invocation | `tool.call` |
| Tool execution complete | `tool.result` |
| LLM token available | `assistant.delta` |
| LLM response complete | `assistant.final` |
| Phase execution ends | `phase.completed` |
| Run execution ends | `run.finished` |
