using System.Threading.Channels;
using FluentAssertions;
using Gmsd.Web.Models.Streaming;
using Xunit;

namespace Gmsd.Web.Tests.Models.Streaming;

public class EventSinkTests
{
    #region ChannelEventSink Tests

    [Fact]
    public async Task ChannelEventSink_EmitAsync_EventIsReadableFromReader()
    {
        var sink = new ChannelEventSink();
        var payload = new IntentClassifiedPayload
        {
            Category = "Chat",
            Confidence = 0.95,
            Reasoning = "Simple question"
        };
        var @event = StreamingEvent.Create(StreamingEventType.IntentClassified, payload);

        var emitResult = await sink.EmitAsync(@event);

        emitResult.Should().BeTrue();
        var reader = sink.Reader;
        reader.TryRead(out var readEvent).Should().BeTrue();
        readEvent.Should().NotBeNull();
        readEvent!.Type.Should().Be(StreamingEventType.IntentClassified);
        readEvent.Payload.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public async Task ChannelEventSink_EmitAsync_TypedPayload_EventIsReadable()
    {
        var sink = new ChannelEventSink();

        var result = await sink.EmitAsync(
            StreamingEventType.GateSelected,
            new GateSelectedPayload
            {
                Phase = "Planner",
                Reasoning = "User wants to plan"
            }
        );

        result.Should().BeTrue();
        sink.Reader.TryRead(out var readEvent).Should().BeTrue();
        readEvent!.Type.Should().Be(StreamingEventType.GateSelected);
    }

    [Fact]
    public void ChannelEventSink_TryEmit_Synchronous_EventIsReadable()
    {
        var sink = new ChannelEventSink();
        var @event = StreamingEvent.Create(
            StreamingEventType.IntentClassified,
            new IntentClassifiedPayload { Category = "Write", Confidence = 0.8 }
        );

        var tryEmitResult = sink.TryEmit(@event);

        tryEmitResult.Should().BeTrue();
        sink.Reader.TryRead(out var readEvent).Should().BeTrue();
        readEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task ChannelEventSink_EmitAsync_WithCorrelationIdAndSequenceNumber_PreservesMetadata()
    {
        var sink = new ChannelEventSink();

        await sink.EmitAsync(
            StreamingEventType.PhaseLifecycle,
            new PhaseLifecyclePayload { Phase = "Execute", Status = "started" },
            correlationId: "corr-abc",
            sequenceNumber: 42
        );

        sink.Reader.TryRead(out var readEvent).Should().BeTrue();
        readEvent!.CorrelationId.Should().Be("corr-abc");
        readEvent.SequenceNumber.Should().Be(42);
    }

    [Fact]
    public async Task ChannelEventSink_EmitAsync_AfterComplete_ReturnsFalse()
    {
        var sink = new ChannelEventSink();
        sink.Complete();

        var result = await sink.EmitAsync(
            StreamingEventType.IntentClassified,
            new IntentClassifiedPayload { Category = "Chat", Confidence = 0.9 }
        );

        result.Should().BeFalse();
    }

    [Fact]
    public void ChannelEventSink_TryEmit_AfterComplete_ReturnsFalse()
    {
        var sink = new ChannelEventSink();
        sink.Complete();

        var result = sink.TryEmit(
            StreamingEventType.IntentClassified,
            new IntentClassifiedPayload { Category = "Chat", Confidence = 0.9 }
        );

        result.Should().BeFalse();
    }

    [Fact]
    public void ChannelEventSink_IsCompleted_AfterComplete_ReturnsTrue()
    {
        var sink = new ChannelEventSink();

        sink.IsCompleted.Should().BeFalse();
        sink.Complete();
        sink.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void ChannelEventSink_Complete_WithException_CompletesWithError()
    {
        var sink = new ChannelEventSink();
        var exception = new InvalidOperationException("Test error");

        sink.Complete(exception);

        // Wait for completion fault to propagate
        var completionTask = sink.Reader.Completion;
        Assert.Throws<AggregateException>(() => completionTask.Wait(100));
        completionTask.IsFaulted.Should().BeTrue();
        completionTask.Exception!.InnerExceptions[0].Should().Be(exception);
    }

    [Fact]
    public async Task ChannelEventSink_EmitAsync_NullEvent_ThrowsArgumentNullException()
    {
        var sink = new ChannelEventSink();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sink.EmitAsync(null!));
    }

    [Fact]
    public void ChannelEventSink_TryEmit_NullEvent_ThrowsArgumentNullException()
    {
        var sink = new ChannelEventSink();

        Assert.Throws<ArgumentNullException>(() => sink.TryEmit(null!));
    }

    [Fact]
    public async Task ChannelEventSink_MultipleEvents_ReadAllAsyncReturnsAll()
    {
        var sink = new ChannelEventSink();

        await sink.EmitAsync(StreamingEventType.IntentClassified, new IntentClassifiedPayload { Category = "Chat", Confidence = 0.9 });
        await sink.EmitAsync(StreamingEventType.GateSelected, new GateSelectedPayload { Phase = "Planner" });
        await sink.EmitAsync(StreamingEventType.AssistantDelta, new AssistantDeltaPayload { MessageId = "msg-1", Content = "Hello" });
        sink.Complete();

        var events = new List<StreamingEvent>();
        await foreach (var evt in sink.ReadAllAsync())
        {
            events.Add(evt);
        }

        events.Should().HaveCount(3);
        events[0].Type.Should().Be(StreamingEventType.IntentClassified);
        events[1].Type.Should().Be(StreamingEventType.GateSelected);
        events[2].Type.Should().Be(StreamingEventType.AssistantDelta);
    }

    [Fact]
    public async Task ChannelEventSink_BoundedCapacity_WritesBlockWhenFull()
    {
        var sink = new ChannelEventSink(capacity: 1, fullMode: BoundedChannelFullMode.Wait);

        // First write should succeed
        await sink.EmitAsync(StreamingEventType.IntentClassified, new IntentClassifiedPayload { Category = "Chat", Confidence = 0.9 });

        // Second write should block until we read
        var writeTask = sink.EmitAsync(StreamingEventType.GateSelected, new GateSelectedPayload { Phase = "Planner" });

        // Give time for the write to start blocking
        await Task.Delay(10);
        writeTask.IsCompletedSuccessfully.Should().BeFalse();

        // Read to free space
        sink.Reader.TryRead(out _);

        // Now the write should complete
        var result = await writeTask;
        result.Should().BeTrue();
    }

    [Fact]
    public void ChannelEventSink_Dispose_CompletesSink()
    {
        var sink = new ChannelEventSink();

        sink.Dispose();

        sink.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ChannelEventSink_WaitForCompletionAsync_WaitsUntilComplete()
    {
        var sink = new ChannelEventSink();
        await sink.EmitAsync(StreamingEventType.IntentClassified, new IntentClassifiedPayload { Category = "Chat", Confidence = 0.9 });

        var waitTask = sink.WaitForCompletionAsync();
        waitTask.IsCompleted.Should().BeFalse();

        sink.Complete();

        await waitTask;
        waitTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    #endregion

    #region NullEventSink Tests

    [Fact]
    public async Task NullEventSink_EmitAsync_AlwaysReturnsTrue()
    {
        var sink = NullEventSink.Instance;

        var result = await sink.EmitAsync(
            StreamingEventType.IntentClassified,
            new IntentClassifiedPayload { Category = "Chat", Confidence = 0.9 }
        );

        result.Should().BeTrue();
    }

    [Fact]
    public void NullEventSink_TryEmit_AlwaysReturnsTrue()
    {
        var sink = NullEventSink.Instance;

        var result = sink.TryEmit(
            StreamingEventType.IntentClassified,
            new IntentClassifiedPayload { Category = "Chat", Confidence = 0.9 }
        );

        result.Should().BeTrue();
    }

    [Fact]
    public void NullEventSink_IsCompleted_AlwaysReturnsFalse()
    {
        var sink = NullEventSink.Instance;

        sink.IsCompleted.Should().BeFalse();
        sink.Complete();
        sink.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void NullEventSink_Complete_DoesNotThrow()
    {
        var sink = NullEventSink.Instance;

        sink.Complete();
        sink.Complete(new Exception("test"));

        // No exception should be thrown
    }

    [Fact]
    public void NullEventSink_IsSingleton_ReturnsSameInstance()
    {
        var instance1 = NullEventSink.Instance;
        var instance2 = NullEventSink.Instance;

        instance1.Should().BeSameAs(instance2);
    }

    #endregion

    #region EventSinkExtensions Tests

    [Fact]
    public async Task EmitIntentClassifiedAsync_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        await sink.EmitIntentClassifiedAsync(
            category: "Chat",
            confidence: 0.95,
            reasoning: "Simple greeting",
            userInput: "Hello",
            correlationId: "test-corr",
            sequenceNumber: 1
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.IntentClassified);
        evt.CorrelationId.Should().Be("test-corr");
        evt.SequenceNumber.Should().Be(1);

        var payload = evt.Payload as IntentClassifiedPayload;
        payload.Should().NotBeNull();
        payload!.Category.Should().Be("Chat");
        payload.Confidence.Should().Be(0.95);
        payload.Reasoning.Should().Be("Simple greeting");
        payload.UserInput.Should().Be("Hello");
    }

    [Fact]
    public async Task EmitGateSelectedAsync_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        await sink.EmitGateSelectedAsync(
            phase: "Planner",
            reasoning: "User wants to plan",
            requiresConfirmation: true,
            proposedAction: new ProposedAction { Description = "Create plan" },
            correlationId: "test-corr",
            sequenceNumber: 2
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.GateSelected);

        var payload = evt.Payload as GateSelectedPayload;
        payload.Should().NotBeNull();
        payload!.Phase.Should().Be("Planner");
        payload.RequiresConfirmation.Should().BeTrue();
        payload.ProposedAction.Should().NotBeNull();
    }

    [Fact]
    public async Task EmitToolCallAsync_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        await sink.EmitToolCallAsync(
            callId: "call-123",
            toolName: "FileWriter",
            parameters: new Dictionary<string, object> { ["path"] = "/test.txt" },
            phaseContext: "Execute",
            correlationId: "test-corr",
            sequenceNumber: 3
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.ToolCall);

        var payload = evt.Payload as ToolCallPayload;
        payload.Should().NotBeNull();
        payload!.CallId.Should().Be("call-123");
        payload.ToolName.Should().Be("FileWriter");
        payload.PhaseContext.Should().Be("Execute");
    }

    [Fact]
    public async Task EmitToolResultAsync_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        await sink.EmitToolResultAsync(
            callId: "call-123",
            success: true,
            result: "File written",
            durationMs: 150,
            correlationId: "test-corr",
            sequenceNumber: 4
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.ToolResult);

        var payload = evt.Payload as ToolResultPayload;
        payload.Should().NotBeNull();
        payload!.CallId.Should().Be("call-123");
        payload.Success.Should().BeTrue();
        payload.DurationMs.Should().Be(150);
    }

    [Fact]
    public async Task EmitPhaseLifecycleAsync_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        await sink.EmitPhaseLifecycleAsync(
            phase: "Planner",
            status: "started",
            correlationId: "test-corr",
            sequenceNumber: 5
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.PhaseLifecycle);

        var payload = evt.Payload as PhaseLifecyclePayload;
        payload.Should().NotBeNull();
        payload!.Phase.Should().Be("Planner");
        payload.Status.Should().Be("started");
    }

    [Fact]
    public async Task EmitAssistantDeltaAsync_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        await sink.EmitAssistantDeltaAsync(
            messageId: "msg-1",
            content: "Hello ",
            index: 0,
            correlationId: "test-corr",
            sequenceNumber: 6
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.AssistantDelta);

        var payload = evt.Payload as AssistantDeltaPayload;
        payload.Should().NotBeNull();
        payload!.MessageId.Should().Be("msg-1");
        payload.Content.Should().Be("Hello ");
        payload.Index.Should().Be(0);
    }

    [Fact]
    public async Task EmitAssistantFinalAsync_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        await sink.EmitAssistantFinalAsync(
            messageId: "msg-1",
            content: "Hello world!",
            contentType: "text/markdown",
            correlationId: "test-corr",
            sequenceNumber: 7
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.AssistantFinal);

        var payload = evt.Payload as AssistantFinalPayload;
        payload.Should().NotBeNull();
        payload!.MessageId.Should().Be("msg-1");
        payload.ContentType.Should().Be("text/markdown");
        payload.IsFinal.Should().BeTrue();
    }

    [Fact]
    public async Task EmitRunLifecycleAsync_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        await sink.EmitRunLifecycleAsync(
            status: "started",
            runId: "run-abc",
            correlationId: "test-corr",
            sequenceNumber: 8
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.RunLifecycle);

        var payload = evt.Payload as RunLifecyclePayload;
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("started");
        payload.RunId.Should().Be("run-abc");
    }

    [Fact]
    public async Task EmitErrorAsync_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        await sink.EmitErrorAsync(
            severity: "error",
            code: "TOOL_FAILED",
            message: "Tool execution failed",
            context: "ExecutePhase",
            recoverable: true,
            retryAction: "retry",
            correlationId: "test-corr",
            sequenceNumber: 9
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.Error);

        var payload = evt.Payload as ErrorPayload;
        payload.Should().NotBeNull();
        payload!.Severity.Should().Be("error");
        payload.Code.Should().Be("TOOL_FAILED");
        payload.Recoverable.Should().BeTrue();
        payload.RetryAction.Should().Be("retry");
    }

    [Fact]
    public void TryEmitIntentClassified_Synchronous_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        sink.TryEmitIntentClassified(
            category: "Chat",
            confidence: 0.95,
            reasoning: "Simple greeting"
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.IntentClassified);

        var payload = evt.Payload as IntentClassifiedPayload;
        payload!.Category.Should().Be("Chat");
    }

    [Fact]
    public void TryEmitGateSelected_Synchronous_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        sink.TryEmitGateSelected(
            phase: "Planner",
            reasoning: "User wants to plan",
            requiresConfirmation: false
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.GateSelected);

        var payload = evt.Payload as GateSelectedPayload;
        payload!.Phase.Should().Be("Planner");
    }

    [Fact]
    public void TryEmitError_Synchronous_CreatesCorrectEvent()
    {
        var sink = new ChannelEventSink();

        sink.TryEmitError(
            severity: "warning",
            code: "LOW_CONFIDENCE",
            message: "Classification confidence is low"
        );

        sink.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.Type.Should().Be(StreamingEventType.Error);

        var payload = evt.Payload as ErrorPayload;
        payload!.Severity.Should().Be("warning");
        payload.Code.Should().Be("LOW_CONFIDENCE");
    }

    [Fact]
    public async Task ExtensionMethods_WithNullSink_WorkCorrectly()
    {
        var sink = NullEventSink.Instance;

        // All extension methods should work with NullEventSink and return true
        var result1 = await sink.EmitIntentClassifiedAsync("Chat", 0.9);
        var result2 = await sink.EmitGateSelectedAsync("Planner");
        var result3 = await sink.EmitErrorAsync("error", "CODE", "Message");

        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();
    }

    #endregion
}
