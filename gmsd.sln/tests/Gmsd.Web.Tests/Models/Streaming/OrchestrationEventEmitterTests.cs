using FluentAssertions;
using Gmsd.Web.Models.Streaming;
using Xunit;

namespace Gmsd.Web.Tests.Models.Streaming;

public class OrchestrationEventEmitterTests
{
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
        eventSink.Events[0].Type.Should().Be(StreamingEventType.IntentClassified);
        eventSink.Events[0].SequenceNumber.Should().Be(0);
    }

    [Fact]
    public async Task EmitGatingEvent_WithProposedAction_IncludesActionDetails()
    {
        var eventSink = new TestEventSink();
        var emitter = new OrchestrationEventEmitter(eventSink);

        var proposedAction = new ProposedAction
        {
            Description = "Execute task",
            ActionType = "ExecuteTask",
            Parameters = new Dictionary<string, object> { ["taskId"] = "TASK-001" }
        };

        var result = await emitter.EmitGatingEventAsync(
            "Executor",
            "Routing to executor phase",
            true,
            proposedAction);

        result.Should().BeTrue();
        eventSink.Events.Should().HaveCount(1);
        var payload = eventSink.Events[0].Payload as GateSelectedPayload;
        payload.Should().NotBeNull();
        payload!.ProposedAction.Should().NotBeNull();
        payload.RequiresConfirmation.Should().BeTrue();
    }

    [Fact]
    public async Task SequenceNumbers_AreIncremented()
    {
        var eventSink = new TestEventSink();
        var emitter = new OrchestrationEventEmitter(eventSink);

        await emitter.EmitClassificationEventAsync("Chat", 0.95);
        await emitter.EmitGatingEventAsync("Responder");
        await emitter.EmitDispatchStartAsync("Responder");

        eventSink.Events[0].SequenceNumber.Should().Be(0);
        eventSink.Events[1].SequenceNumber.Should().Be(1);
        eventSink.Events[2].SequenceNumber.Should().Be(2);
    }

    [Fact]
    public async Task CorrelationId_IsPropagatedToAllEvents()
    {
        var eventSink = new TestEventSink();
        var emitter = new OrchestrationEventEmitter(eventSink);
        var correlationId = Guid.NewGuid().ToString("N");
        emitter.SetCorrelationId(correlationId);

        await emitter.EmitClassificationEventAsync("Chat", 0.95);
        await emitter.EmitGatingEventAsync("Responder");
        await emitter.EmitDispatchStartAsync("Responder");

        eventSink.Events.Should().AllSatisfy(e => e.CorrelationId.Should().Be(correlationId));
    }

    [Fact]
    public async Task EmitToolCallDetected_WithMultipleTools_IncludesAllToolCalls()
    {
        var eventSink = new TestEventSink();
        var emitter = new OrchestrationEventEmitter(eventSink);

        var toolCalls = new List<ToolCallInfo>
        {
            new ToolCallInfo { ToolCallId = "call-001", ToolName = "read_file", ArgumentsJson = "{\"path\":\"/test.txt\"}" },
            new ToolCallInfo { ToolCallId = "call-002", ToolName = "write_file", ArgumentsJson = "{\"path\":\"/output.txt\"}" }
        };

        var result = await emitter.EmitToolCallDetectedAsync(1, toolCalls);

        result.Should().BeTrue();
        var payload = eventSink.Events[0].Payload as ToolCallDetectedPayload;
        payload!.ToolCalls.Should().HaveCount(2);
    }

    [Fact]
    public async Task EmitToolLoopSequence_MaintainsIterationNumbers()
    {
        var eventSink = new TestEventSink();
        var emitter = new OrchestrationEventEmitter(eventSink);

        // Iteration 1
        await emitter.EmitToolCallDetectedAsync(1, new List<ToolCallInfo>());
        await emitter.EmitToolCallStartedAsync(1, "call-001", "tool1", "{}");
        await emitter.EmitToolCallCompletedAsync(1, "call-001", "tool1", 50, true);

        // Iteration 2
        await emitter.EmitToolCallDetectedAsync(2, new List<ToolCallInfo>());
        await emitter.EmitToolCallStartedAsync(2, "call-002", "tool2", "{}");
        await emitter.EmitToolCallCompletedAsync(2, "call-002", "tool2", 60, true);

        var payload1 = eventSink.Events[0].Payload as ToolCallDetectedPayload;
        var payload2 = eventSink.Events[3].Payload as ToolCallDetectedPayload;

        payload1!.Iteration.Should().Be(1);
        payload2!.Iteration.Should().Be(2);
    }

    [Fact]
    public async Task EmitError_WithRecoveryInfo_IncludesRetryAction()
    {
        var eventSink = new TestEventSink();
        var emitter = new OrchestrationEventEmitter(eventSink);

        var result = await emitter.EmitErrorAsync(
            "error",
            "TOOL_EXECUTION_FAILED",
            "Tool execution failed",
            "Executor",
            true,
            "retry");

        result.Should().BeTrue();
        var payload = eventSink.Events[0].Payload as ErrorPayload;
        payload!.Recoverable.Should().BeTrue();
        payload.RetryAction.Should().Be("retry");
    }

    [Fact]
    public async Task EmitRunLifecycle_StartAndFinish_ContainsCorrectStatus()
    {
        var eventSink = new TestEventSink();
        var emitter = new OrchestrationEventEmitter(eventSink);
        var runId = Guid.NewGuid().ToString("N");

        await emitter.EmitRunLifecycleAsync("started", runId);
        await emitter.EmitRunLifecycleAsync("finished", runId, 1500, true, new List<string> { "/output.txt" });

        var startPayload = eventSink.Events[0].Payload as RunLifecyclePayload;
        var finishPayload = eventSink.Events[1].Payload as RunLifecyclePayload;

        startPayload!.Status.Should().Be("started");
        finishPayload!.Status.Should().Be("finished");
        finishPayload.DurationMs.Should().Be(1500);
        finishPayload.Success.Should().BeTrue();
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
