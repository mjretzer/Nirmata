using FluentAssertions;
using Gmsd.Web.Models.Streaming;
using Xunit;

namespace Gmsd.Web.Tests.Models.Streaming;

public class StreamingProtocolIntegrationTests
{
    [Fact]
    public async Task CompleteWorkflowSequence_EmitsAllRequiredEvents()
    {
        var eventSink = new TestEventSink();
        var emitter = new OrchestrationEventEmitter(eventSink);
        var correlationId = Guid.NewGuid().ToString("N");
        emitter.SetCorrelationId(correlationId);

        // Emit classification event
        await emitter.EmitClassificationEventAsync("Write", 0.95, "User wants to create a file", "Create a new file");

        // Emit gating event
        var proposedAction = new ProposedAction { Description = "Create file in workspace" };
        await emitter.EmitGatingEventAsync("Executor", "Routing to executor phase", false, proposedAction);

        // Emit run lifecycle start
        var runId = Guid.NewGuid().ToString("N");
        await emitter.EmitRunLifecycleAsync("started", runId);

        // Emit phase lifecycle start
        await emitter.EmitDispatchStartAsync("Executor", new Dictionary<string, object> { ["taskId"] = "TASK-001" });

        // Emit tool call
        var callId = Guid.NewGuid().ToString("N");
        await emitter.EmitToolCallAsync(callId, "create_file", new Dictionary<string, object> { ["path"] = "/test.txt" });

        // Emit tool result
        await emitter.EmitToolResultAsync(callId, true, new { path = "/test.txt" }, null, 150);

        // Emit phase lifecycle complete
        var artifacts = new List<PhaseArtifact>
        {
            new PhaseArtifact { Type = "file", Name = "test.txt", Reference = "/test.txt" }
        };
        await emitter.EmitDispatchCompleteAsync("Executor", true, artifacts);

        // Emit run lifecycle finish
        await emitter.EmitRunLifecycleAsync("finished", runId, 1500, true, new List<string> { "/test.txt" });

        // Verify all events were emitted
        eventSink.Events.Should().HaveCount(9);
        eventSink.Events.Should().AllSatisfy(e => e.CorrelationId.Should().Be(correlationId));
        eventSink.Events.Should().AllSatisfy(e => e.SequenceNumber.Should().NotBeNull());
    }

    [Fact]
    public async Task ToolCallingLoopSequence_EmitsAllToolEvents()
    {
        var eventSink = new TestEventSink();
        var emitter = new OrchestrationEventEmitter(eventSink);

        // Emit tool call detected
        var toolCalls = new List<ToolCallInfo>
        {
            new ToolCallInfo { ToolCallId = "call-001", ToolName = "calculator", ArgumentsJson = "{\"op\":\"add\",\"a\":5,\"b\":3}" }
        };
        await emitter.EmitToolCallDetectedAsync(1, toolCalls);

        // Emit tool call started
        await emitter.EmitToolCallStartedAsync(1, "call-001", "calculator", "{\"op\":\"add\",\"a\":5,\"b\":3}");

        // Emit tool call completed
        await emitter.EmitToolCallCompletedAsync(1, "call-001", "calculator", 50, true, "{\"result\":8}");

        // Emit tool results submitted
        var results = new List<ToolResultInfo>
        {
            new ToolResultInfo { ToolCallId = "call-001", ToolName = "calculator", IsSuccess = true }
        };
        await emitter.EmitToolResultsSubmittedAsync(1, results);

        // Emit tool loop iteration completed
        await emitter.EmitToolLoopIterationCompletedAsync(1, false, 1, 100);

        // Emit tool loop completed
        await emitter.EmitToolLoopCompletedAsync(1, 1, "response_generated", 100);

        eventSink.Events.Should().HaveCount(6);
        eventSink.Events[0].Type.Should().Be(StreamingEventType.ToolCallDetected);
        eventSink.Events[1].Type.Should().Be(StreamingEventType.ToolCallStarted);
        eventSink.Events[2].Type.Should().Be(StreamingEventType.ToolCallCompleted);
        eventSink.Events[3].Type.Should().Be(StreamingEventType.ToolResultsSubmitted);
        eventSink.Events[4].Type.Should().Be(StreamingEventType.ToolLoopIterationCompleted);
        eventSink.Events[5].Type.Should().Be(StreamingEventType.ToolLoopCompleted);
    }

    [Fact]
    public async Task ErrorSequence_EmitsErrorAndCleanup()
    {
        var eventSink = new TestEventSink();
        var emitter = new OrchestrationEventEmitter(eventSink);

        // Emit error
        await emitter.EmitErrorAsync("error", "TOOL_EXECUTION_FAILED", "Tool execution failed", "Executor", true, "retry");

        // Emit phase completion with error
        var error = new PhaseError { Code = "TOOL_EXECUTION_FAILED", Message = "Tool execution failed" };
        await emitter.EmitDispatchCompleteAsync("Executor", false, null, error);

        // Emit run finish with failure
        await emitter.EmitRunLifecycleAsync("finished", "RUN-001", 500, false);

        eventSink.Events.Should().HaveCount(3);
        eventSink.Events[0].Type.Should().Be(StreamingEventType.Error);
        eventSink.Events[1].Type.Should().Be(StreamingEventType.PhaseLifecycle);
        eventSink.Events[2].Type.Should().Be(StreamingEventType.RunLifecycle);
    }

    [Fact]
    public void EventVersioning_SupportsV1AndV2()
    {
        EventVersioning.IsVersionSupported(1).Should().BeTrue();
        EventVersioning.IsVersionSupported(2).Should().BeTrue();
        EventVersioning.IsVersionSupported(3).Should().BeFalse();
    }

    [Fact]
    public void BackwardCompatibility_UpgradesV1ToV2()
    {
        var v1Event = new StreamingEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = StreamingEventType.IntentClassified,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new IntentClassifiedPayload { Category = "Chat", Confidence = 0.95 }
        };

        var v2Event = BackwardCompatibilityHandler.UpgradeV1ToV2(v1Event);

        v2Event.CorrelationId.Should().NotBeNullOrEmpty();
        v2Event.SequenceNumber.Should().Be(0);
    }

    [Fact]
    public void BackwardCompatibility_DowngradesV2ToV1()
    {
        var v2Event = new StreamingEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = StreamingEventType.IntentClassified,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N"),
            SequenceNumber = 1,
            Payload = new IntentClassifiedPayload { Category = "Chat", Confidence = 0.95 }
        };

        var v1Event = BackwardCompatibilityHandler.DowngradeV2ToV1(v2Event);

        v1Event.CorrelationId.Should().BeNull();
        v1Event.SequenceNumber.Should().BeNull();
    }

    [Fact]
    public async Task EnhancedEventSink_BuffersAndFiltersEvents()
    {
        var innerSink = new TestEventSink();
        var options = new EventSinkOptions
        {
            EnableBuffering = true,
            BufferSize = 100,
            SamplingRate = 1.0,
            EventTypeFilter = new HashSet<StreamingEventType> { StreamingEventType.IntentClassified, StreamingEventType.Error }
        };
        var enhancedSink = new EnhancedEventSink(innerSink, options);

        var intentEvent = new StreamingEvent
        {
            Type = StreamingEventType.IntentClassified,
            Payload = new IntentClassifiedPayload { Category = "Chat", Confidence = 0.95 }
        };

        var toolEvent = new StreamingEvent
        {
            Type = StreamingEventType.ToolCall,
            Payload = new ToolCallPayload { CallId = "call-001", ToolName = "test" }
        };

        await enhancedSink.EmitAsync(intentEvent);
        await enhancedSink.EmitAsync(toolEvent);

        // Only intent event should pass through the filter
        innerSink.Events.Should().HaveCount(1);
        innerSink.Events[0].Type.Should().Be(StreamingEventType.IntentClassified);

        var stats = enhancedSink.GetStatistics();
        stats.TotalEventsEmitted.Should().Be(2);
        stats.TotalEventsFiltered.Should().Be(1);
    }

    /// <summary>
    /// Test implementation of IEventSink for testing.
    /// </summary>
    private class TestEventSink : IEventSink
    {
        public List<StreamingEvent> Events { get; } = new();
        public bool IsCompleted { get; private set; }

        public ValueTask<bool> EmitAsync(StreamingEvent @event, CancellationToken cancellationToken = default)
        {
            if (@event != null)
            {
                Events.Add(@event);
            }
            return new ValueTask<bool>(true);
        }

        public ValueTask<bool> EmitAsync<TPayload>(
            StreamingEventType type,
            TPayload payload,
            string? correlationId = null,
            long? sequenceNumber = null,
            CancellationToken cancellationToken = default) where TPayload : class
        {
            var @event = StreamingEvent.Create(type, payload, correlationId, sequenceNumber);
            return EmitAsync(@event, cancellationToken);
        }

        public bool TryEmit(StreamingEvent @event)
        {
            if (@event != null)
            {
                Events.Add(@event);
            }
            return true;
        }

        public bool TryEmit<TPayload>(
            StreamingEventType type,
            TPayload payload,
            string? correlationId = null,
            long? sequenceNumber = null) where TPayload : class
        {
            var @event = StreamingEvent.Create(type, payload, correlationId, sequenceNumber);
            return TryEmit(@event);
        }

        public void Complete(Exception? exception = null)
        {
            IsCompleted = true;
        }
    }
}
