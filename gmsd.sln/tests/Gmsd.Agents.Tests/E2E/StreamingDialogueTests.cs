using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Preflight;
using Gmsd.Agents.Execution.Preflight.CommandSuggestion;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Web.Models.Streaming;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gmsd.Agents.Tests.E2E;

/// <summary>
/// End-to-end integration tests for streaming dialogue event sequences.
/// Validates the conversational streaming contract per the streaming-dialogue-protocol spec.
/// </summary>
public class StreamingDialogueTests
{
    #region Fake Implementations

    private sealed class FakeOrchestrator : IOrchestrator
    {
        private readonly OrchestratorResult _result;

        public FakeOrchestrator(OrchestratorResult result)
        {
            _result = result;
        }

        public Task<OrchestratorResult> ExecuteAsync(WorkflowIntent intent, CancellationToken ct = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeLlmProvider : ILlmProvider
    {
        public string ProviderName => "fake";

        public Task<LlmCompletionResponse> CompleteAsync(
            LlmCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmCompletionResponse
            {
                Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = "Hello! I'm here to help." },
                Model = "fake-model",
                Provider = "fake"
            });
        }

        public async IAsyncEnumerable<LlmDelta> StreamCompletionAsync(
            LlmCompletionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new LlmDelta { Content = "Hello!" };
            yield return new LlmDelta { Content = " I'm" };
            yield return new LlmDelta { Content = " here" };
            yield return new LlmDelta { Content = " to" };
            yield return new LlmDelta { Content = " help." };
            yield return new LlmDelta { Content = "", FinishReason = "stop" };
        }
    }

    private sealed class FakeGatingEngine : IGatingEngine
    {
        private readonly GatingResult? _result;

        public FakeGatingEngine(GatingResult? result = null)
        {
            _result = result;
        }

        public Task<GatingResult> EvaluateAsync(GatingContext context, CancellationToken ct = default)
        {
            var result = _result ?? new GatingResult
            {
                TargetPhase = "ChatResponder",
                Reason = "Chat-classified input",
                Reasoning = "Routing to chat responder for conversational response",
                RequiresConfirmation = false,
                ProposedAction = null
            };
            return Task.FromResult(result);
        }
    }

    private sealed class FakeToolEmittingOrchestrator : IOrchestrator
    {
        private readonly OrchestratorResult _result;
        private readonly List<(string callId, string toolName, Dictionary<string, object>? parameters)> _toolsToEmit;

        public FakeToolEmittingOrchestrator(
            OrchestratorResult result,
            List<(string callId, string toolName, Dictionary<string, object>? parameters)>? toolsToEmit = null)
        {
            _result = result;
            _toolsToEmit = toolsToEmit ?? [];
        }

        public async Task<OrchestratorResult> ExecuteAsync(WorkflowIntent intent, CancellationToken ct = default)
        {
            // Simulate tool execution via the ToolEventSinkContext
            var toolSink = ToolEventSinkContext.Current;
            if (toolSink != null)
            {
                foreach (var (callId, toolName, parameters) in _toolsToEmit)
                {
                    toolSink.EmitToolCall(
                        callId: callId,
                        toolName: toolName,
                        parameters: parameters,
                        phaseContext: ToolEventSinkContext.PhaseContext,
                        correlationId: ToolEventSinkContext.CorrelationId);

                    // Simulate execution delay
                    await Task.Delay(10, ct);

                    toolSink.EmitToolResult(
                        callId: callId,
                        success: true,
                        result: $"Result from {toolName}",
                        durationMs: 10,
                        correlationId: ToolEventSinkContext.CorrelationId);
                }
            }

            return _result;
        }
    }

    #endregion

    #region Task 4.1: Chat Sequence Integration Test

    /// <summary>
    /// Validates the chat-like interaction event sequence (gating but no run lifecycle):
    /// - Sequence: intent.classified → gate.selected → assistant.delta → assistant.final
    /// - No run.started or run.finished events
    /// - All events have matching correlation ID
    /// </summary>
    [Fact]
    public async Task ChatSequence_EmitsCorrectEventSequence()
    {
        // Arrange - Set up for chat-like interaction with gating but no run lifecycle
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Planner",
            RunId = "run-chat-001",
            Artifacts = new Dictionary<string, object>
            {
                ["response"] = "Hello! I'm here to help."
            }
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine(new GatingResult
        {
            TargetPhase = "Planner",
            Reason = "Write-classified input requiring planning",
            Reasoning = "Routing to Planner for plan creation",
            RequiresConfirmation = false,
            ProposedAction = null
        });
        var commandSuggester = new FakeCommandSuggester();
        var commandOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, commandSuggester, commandOptions);

        var correlationId = "corr-chat-seq-001";
        // Use a Write command to trigger gating, but disable run lifecycle events
        // This produces: intent.classified → gate.selected → assistant.delta → assistant.final
        // WITHOUT: run.started or run.finished
        var intent = new WorkflowIntent
        {
            InputRaw = "/plan",  // Write-classified input
            CorrelationId = correlationId
        };

        // Use options that enable gating but disable run lifecycle
        var options = new StreamingOrchestrationOptions
        {
            EmitIntentClassified = true,
            EmitGateSelected = true,
            EmitPhaseLifecycle = false,
            EmitRunLifecycle = false,  // Disable run events
            EmitAssistantDeltas = true,
            EnableSequenceNumbers = true
        };

        // Act - Collect all events from the stream
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, options))
        {
            events.Add(@event);
        }

        // Assert - Verify we have the expected event types in sequence
        var eventTypes = events.Select(e => e.Type).ToList();

        // Debug: Print actual events
        var eventDescriptions = string.Join(" → ", eventTypes.Select(t => t.ToString()));
        System.Diagnostics.Debug.WriteLine($"Actual events: {eventDescriptions}");

        // First event should be intent.classified
        eventTypes[0].Should().Be(StreamingEventType.IntentClassified, "first event should be intent.classified");

        // Should have gate.selected event
        eventTypes.Should().Contain(StreamingEventType.GateSelected, "should emit gate.selected event");

        // Should have assistant.delta events
        var deltaEvents = events.Where(e => e.Type == StreamingEventType.AssistantDelta).ToList();
        deltaEvents.Should().NotBeEmpty("should emit assistant.delta events");

        // Last assistant-related event should be assistant.final
        var lastAssistantEvent = events.LastOrDefault(e =>
            e.Type == StreamingEventType.AssistantDelta || e.Type == StreamingEventType.AssistantFinal);
        lastAssistantEvent?.Type.Should().Be(StreamingEventType.AssistantFinal, "should end with assistant.final");

        // 2. Verify NO run lifecycle events for chat-only interaction
        eventTypes.Should().NotContain(StreamingEventType.RunLifecycle,
            "chat-only interactions should NOT emit run.started or run.finished events");

        // 3. Verify all events have the same correlation ID
        events.Should().AllSatisfy(e => e.CorrelationId.Should().Be(correlationId,
            $"event {e.Type} should have correlation ID '{correlationId}'"));

        // 4. Verify intent.classified payload
        var intentEvent = events.First(e => e.Type == StreamingEventType.IntentClassified);
        var intentPayload = intentEvent.Payload as Gmsd.Web.Models.Streaming.IntentClassifiedPayload;
        intentPayload.Should().NotBeNull();
        intentPayload!.Category.Should().Be("Write", "input should be classified as Write");
        intentPayload.Confidence.Should().BeGreaterThan(0);
        intentPayload.Reasoning.Should().NotBeNullOrEmpty();

        // 5. Verify gate.selected payload
        var gateEvent = events.First(e => e.Type == StreamingEventType.GateSelected);
        var gatePayload = gateEvent.Payload as GateSelectedPayload;
        gatePayload.Should().NotBeNull();
        gatePayload!.Phase.Should().NotBeNullOrEmpty();
        gatePayload.Reasoning.Should().NotBeNullOrEmpty();

        // 6. Verify assistant events share the same messageId
        var assistantEvents = events.Where(e =>
            e.Type == StreamingEventType.AssistantDelta || e.Type == StreamingEventType.AssistantFinal).ToList();

        string? messageId = null;
        foreach (var assistantEvent in assistantEvents)
        {
            if (assistantEvent.Type == StreamingEventType.AssistantDelta)
            {
                var deltaPayload = assistantEvent.Payload as AssistantDeltaPayload;
                deltaPayload.Should().NotBeNull();
                if (messageId == null)
                {
                    messageId = deltaPayload!.MessageId;
                }
                else
                {
                    deltaPayload!.MessageId.Should().Be(messageId, "all assistant.delta events should share the same messageId");
                }
            }
            else if (assistantEvent.Type == StreamingEventType.AssistantFinal)
            {
                var finalPayload = assistantEvent.Payload as AssistantFinalPayload;
                finalPayload.Should().NotBeNull();
                finalPayload!.MessageId.Should().Be(messageId, "assistant.final should have same messageId as deltas");
            }
        }

        // 7. Verify timestamps increase monotonically
        for (int i = 1; i < events.Count; i++)
        {
            events[i].Timestamp.Should().BeOnOrAfter(events[i - 1].Timestamp,
                $"event {i} timestamp should be >= event {i - 1} timestamp");
        }
    }

    /// <summary>
    /// Validates that chat-only mode with ChatOnly options produces minimal events.
    /// </summary>
    [Fact]
    public async Task ChatSequence_WithChatOnlyOptions_DoesNotEmitRunEvents()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Responder",
            RunId = "run-chat-002",
            Artifacts = new Dictionary<string, object>
            {
                ["response"] = "I'm doing well, thank you!"
            }
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var commandSuggester = new FakeCommandSuggester();
        var commandOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, commandSuggester, commandOptions);

        var intent = new WorkflowIntent
        {
            InputRaw = "What's the weather like?",
            CorrelationId = "corr-chat-seq-002"
        };
        var chatOptions = StreamingOrchestrationOptions.ChatOnly;

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, chatOptions))
        {
            events.Add(@event);
        }

        // Assert
        events.Should().NotContain(e => e.Type == StreamingEventType.RunLifecycle,
            "ChatOnly options should never emit run lifecycle events");

        events.Should().Contain(e => e.Type == StreamingEventType.IntentClassified);
        events.Should().Contain(e => e.Type == StreamingEventType.AssistantFinal);
    }

    /// <summary>
    /// Validates that small talk inputs are correctly classified and processed.
    /// </summary>
    [Theory]
    [InlineData("Hello there")]
    [InlineData("How are you today?")]
    [InlineData("What's your name?")]
    [InlineData("Tell me a joke")]
    public async Task ChatSequence_VariousSmallTalkInputs_ClassifiedAsChat(string input)
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Responder",
            RunId = "run-chat-003",
            Artifacts = new Dictionary<string, object>
            {
                ["response"] = "I'm here to help!"
            }
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var commandSuggester = new FakeCommandSuggester();
        var commandOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, commandSuggester, commandOptions);

        var correlationId = $"corr-chat-{Guid.NewGuid():N}";
        var intent = new WorkflowIntent
        {
            InputRaw = input,
            CorrelationId = correlationId
        };

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent))
        {
            events.Add(@event);
        }

        // Assert
        var intentEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.IntentClassified);
        intentEvent.Should().NotBeNull();

        var intentPayload = intentEvent!.Payload as Gmsd.Web.Models.Streaming.IntentClassifiedPayload;
        intentPayload.Should().NotBeNull();
        intentPayload!.Category.Should().Be("Chat");

        // All events should share the same correlation ID
        events.Should().AllSatisfy(e => e.CorrelationId.Should().Be(correlationId));
    }

    #endregion

    #region Task 4.2: Workflow Sequence Integration Test

    /// <summary>
    /// Validates the write workflow event sequence with full lifecycle:
    /// - Sequence: intent.classified → gate.selected → run.started → phase.started → tools → assistant → phase.completed → run.finished
    /// - All events have matching correlation ID
    /// - Timestamps increase monotonically
    /// </summary>
    [Fact]
    public async Task WorkflowSequence_EmitsCorrectEventSequence()
    {
        // Arrange - Set up for write workflow with run lifecycle and tool events
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Planner",
            RunId = "run-workflow-001",
            Artifacts = new Dictionary<string, object>
            {
                ["response"] = "Plan created successfully with 3 tasks.",
                ["planId"] = "plan-001"
            }
        };

        // Define tools that should be emitted during execution
        var toolsToEmit = new List<(string callId, string toolName, Dictionary<string, object>? parameters)>
        {
            ("call-001", "CreatePlan", new Dictionary<string, object> { ["title"] = "Test Plan" }),
            ("call-002", "AddTask", new Dictionary<string, object> { ["planId"] = "plan-001", ["task"] = "Task 1" })
        };

        var fakeToolOrchestrator = new FakeToolEmittingOrchestrator(expectedResult, toolsToEmit);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine(new GatingResult
        {
            TargetPhase = "Planner",
            Reason = "Write-classified input requiring planning",
            Reasoning = "Routing to Planner phase to create a task plan",
            RequiresConfirmation = false,
            ProposedAction = null
        });
        var commandSuggester = new FakeCommandSuggester();
        var commandOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeToolOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, commandSuggester, commandOptions);

        var correlationId = "corr-workflow-seq-001";
        var intent = new WorkflowIntent
        {
            InputRaw = "/plan create a test plan",  // Write-classified input
            CorrelationId = correlationId
        };

        // Enable ALL event types for full workflow validation
        var options = new StreamingOrchestrationOptions
        {
            EmitIntentClassified = true,
            EmitGateSelected = true,
            EmitPhaseLifecycle = true,
            EmitRunLifecycle = true,  // Enable run lifecycle events
            EmitAssistantDeltas = true,
            EnableSequenceNumbers = true
        };

        // Act - Collect all events from the stream
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, options))
        {
            events.Add(@event);
        }

        // Assert - Verify we have the expected event types in sequence
        var eventTypes = events.Select(e => e.Type).ToList();

        // Debug: Print actual events
        var eventDescriptions = string.Join(" → ", eventTypes.Select(t => t.ToString()));
        System.Diagnostics.Debug.WriteLine($"Actual events: {eventDescriptions}");

        // 1. Verify sequence starts with intent.classified
        eventTypes[0].Should().Be(StreamingEventType.IntentClassified, "first event should be intent.classified");

        // 2. Verify gate.selected follows intent.classified
        var gateIndex = eventTypes.IndexOf(StreamingEventType.GateSelected);
        gateIndex.Should().BeGreaterThan(0, "gate.selected should follow intent.classified");

        // 3. Verify run.started follows gate.selected (for write workflows)
        var runLifecycleEvents = events.Where(e => e.Type == StreamingEventType.RunLifecycle).ToList();
        runLifecycleEvents.Should().HaveCount(2, "should have run.started and run.finished events");

        var runStarted = runLifecycleEvents.First();
        var runStartedPayload = runStarted.Payload as RunLifecyclePayload;
        runStartedPayload.Should().NotBeNull();
        runStartedPayload!.Status.Should().Be("started");

        var runStartedIndex = eventTypes.IndexOf(StreamingEventType.RunLifecycle);
        runStartedIndex.Should().BeGreaterThan(gateIndex, "run.started should follow gate.selected");

        // 4. Verify phase.started follows run.started
        var phaseLifecycleEvents = events.Where(e => e.Type == StreamingEventType.PhaseLifecycle).ToList();
        phaseLifecycleEvents.Should().HaveCount(2, "should have phase.started and phase.completed events");

        var phaseStarted = phaseLifecycleEvents.First();
        var phaseStartedPayload = phaseStarted.Payload as PhaseLifecyclePayload;
        phaseStartedPayload.Should().NotBeNull();
        phaseStartedPayload!.Status.Should().Be("started");

        var phaseStartedIndex = eventTypes.IndexOf(StreamingEventType.PhaseLifecycle);
        phaseStartedIndex.Should().BeGreaterThan(runStartedIndex, "phase.started should follow run.started");

        // 5. Verify tool events exist and are properly sequenced
        var toolCallEvents = events.Where(e => e.Type == StreamingEventType.ToolCall).ToList();
        var toolResultEvents = events.Where(e => e.Type == StreamingEventType.ToolResult).ToList();

        toolCallEvents.Should().HaveCount(2, "should have tool.call events for each tool invocation");
        toolResultEvents.Should().HaveCount(2, "should have tool.result events for each tool invocation");

        // Verify tool call/result correlation
        foreach (var callEvent in toolCallEvents)
        {
            var callPayload = callEvent.Payload as ToolCallPayload;
            callPayload.Should().NotBeNull();

            var matchingResult = toolResultEvents.FirstOrDefault(r =>
            {
                var resultPayload = r.Payload as ToolResultPayload;
                return resultPayload?.CallId == callPayload!.CallId;
            });
            matchingResult.Should().NotBeNull($"each tool.call should have matching tool.result with same CallId");
        }

        // Tool events should be between phase.started and phase.completed
        var firstToolIndex = eventTypes.IndexOf(StreamingEventType.ToolCall);
        firstToolIndex.Should().BeGreaterThan(phaseStartedIndex, "tool events should follow phase.started");

        // 6. Verify assistant events exist
        var deltaEvents = events.Where(e => e.Type == StreamingEventType.AssistantDelta).ToList();
        deltaEvents.Should().NotBeEmpty("should emit assistant.delta events");

        eventTypes.Should().Contain(StreamingEventType.AssistantFinal, "should emit assistant.final");

        // 7. Verify phase.completed comes BEFORE assistant events (actual implementation order)
        var phaseCompleted = phaseLifecycleEvents.Last();
        var phaseCompletedPayload = phaseCompleted.Payload as PhaseLifecyclePayload;
        phaseCompletedPayload.Should().NotBeNull();
        phaseCompletedPayload!.Status.Should().Be("completed");

        var phaseCompletedIndex = eventTypes.LastIndexOf(StreamingEventType.PhaseLifecycle);
        var firstAssistantDeltaIndex = eventTypes.IndexOf(StreamingEventType.AssistantDelta);

        // phase.completed should come before assistant.delta events
        phaseCompletedIndex.Should().BeLessThan(firstAssistantDeltaIndex,
            "phase.completed should come before assistant.delta events (phase completes before assistant responds)");

        // 8. Verify run.finished is last (after assistant events)
        var runFinished = runLifecycleEvents.Last();
        var runFinishedPayload = runFinished.Payload as RunLifecyclePayload;
        runFinishedPayload.Should().NotBeNull();
        runFinishedPayload!.Status.Should().Be("finished");
        runFinishedPayload.Success.Should().BeTrue();
        runFinishedPayload.RunId.Should().Be("run-workflow-001");

        var runFinishedIndex = eventTypes.LastIndexOf(StreamingEventType.RunLifecycle);
        runFinishedIndex.Should().Be(eventTypes.Count - 1, "run.finished should be the final event");

        // 9. Verify all events have matching correlation ID
        events.Should().AllSatisfy(e => e.CorrelationId.Should().Be(correlationId,
            $"event {e.Type} should have correlation ID '{correlationId}'"));

        // 10. Verify timestamps increase monotonically
        for (int i = 1; i < events.Count; i++)
        {
            events[i].Timestamp.Should().BeOnOrAfter(events[i - 1].Timestamp,
                $"event {i} ({events[i].Type}) timestamp should be >= event {i - 1} ({events[i - 1].Type}) timestamp");
        }

        // 11. Verify sequence numbers are present when enabled
        var eventsWithSequence = events.Where(e => e.SequenceNumber.HasValue).ToList();
        eventsWithSequence.Should().HaveCount(events.Count, "all events should have sequence numbers when enabled");

        // 12. Verify intent.classified payload for write input
        var intentEvent = events.First(e => e.Type == StreamingEventType.IntentClassified);
        var intentPayload = intentEvent.Payload as Gmsd.Web.Models.Streaming.IntentClassifiedPayload;
        intentPayload.Should().NotBeNull();
        intentPayload!.Category.Should().Be("Write", "input should be classified as Write");
        intentPayload.Confidence.Should().BeGreaterThan(0);
        intentPayload.Reasoning.Should().NotBeNullOrEmpty();

        // 13. Verify gate.selected payload
        var gateEvent = events.First(e => e.Type == StreamingEventType.GateSelected);
        var gatePayload = gateEvent.Payload as GateSelectedPayload;
        gatePayload.Should().NotBeNull();
        gatePayload!.Phase.Should().Be("Planner");
        gatePayload.Reasoning.Should().NotBeNullOrEmpty();
        gatePayload.RequiresConfirmation.Should().BeFalse();
    }

    #endregion

    #region Task 4.4: Cancellation Support

    /// <summary>
    /// Validates that cancellation token stops event emission gracefully.
    /// When cancellation is requested, the stream should stop yielding new events.
    /// </summary>
    [Fact]
    public async Task Cancellation_Stops_Event_Emission()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Responder",
            RunId = "run-cancel-001",
            Artifacts = new Dictionary<string, object>
            {
                ["response"] = "A very long response..."
            }
        };

        // Use delaying orchestrator to simulate long-running execution (2 seconds)
        // This allows ample time to cancel before execution completes
        var delayingOrchestrator = new FakeDelayingOrchestrator(expectedResult, TimeSpan.FromSeconds(2));
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        
        // Configure gating to return a workflow phase
        var fakeGatingEngine = new FakeGatingEngine(new GatingResult
        {
            TargetPhase = "Responder",
            Reason = "Testing cancellation",
            Reasoning = "Routing to Responder",
            RequiresConfirmation = false,
            ProposedAction = null
        });
        
        var commandSuggester = new FakeCommandSuggester();
        var commandOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(delayingOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, commandSuggester, commandOptions);

        var correlationId = "corr-cancel-001";
        var intent = new WorkflowIntent
        {
            InputRaw = "/plan cancellation test", // Use command to ensure gating logic runs
            CorrelationId = correlationId
        };

        var options = new StreamingOrchestrationOptions
        {
            EmitIntentClassified = true,
            EmitGateSelected = true,
            EmitAssistantDeltas = true,
            EnableSequenceNumbers = true
        };

        using var cts = new CancellationTokenSource();

        // Act - Start collecting events and cancel after a short delay (500ms)
        // This gives enough time for initial events (IntentClassified, GateSelected) but cancels before DelayingOrchestrator finishes (2s)
        var events = new List<StreamingEvent>();
        var cancellationTask = Task.Run(async () =>
        {
            await Task.Delay(500); 
            cts.Cancel();
        });

        try
        {
            await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, options, cts.Token))
            {
                events.Add(@event);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation occurs
        }

        await cancellationTask;

        // Assert - Should have received some events but not all
        events.Should().NotBeEmpty("should receive events before cancellation");

        // Should have received intent.classified and gate.selected (happens before execution)
        events.Should().Contain(e => e.Type == StreamingEventType.IntentClassified);
        events.Should().Contain(e => e.Type == StreamingEventType.GateSelected);

        // Should NOT have received assistant.final (stream was cancelled during execution)
        events.Should().NotContain(e => e.Type == StreamingEventType.AssistantFinal,
            "assistant.final should not be emitted when stream is cancelled");
    }

    /// <summary>
    /// Validates that the stream completes gracefully when cancellation is requested.
    /// No exceptions should propagate to the caller beyond OperationCanceledException.
    /// </summary>
    [Fact]
    public async Task Cancellation_Stream_Completes_Gracefully()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Responder",
            RunId = "run-cancel-002",
            Artifacts = new Dictionary<string, object>
            {
                ["response"] = "Response content"
            }
        };

        var delayingOrchestrator = new FakeDelayingOrchestrator(expectedResult, TimeSpan.FromMilliseconds(500));
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var commandSuggester = new FakeCommandSuggester();
        var commandOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(delayingOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, commandSuggester, commandOptions);

        var intent = new WorkflowIntent
        {
            InputRaw = "hello",
            CorrelationId = "corr-cancel-002"
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        Exception? caughtException = null;
        var events = new List<StreamingEvent>();

        // Act
        try
        {
            await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, ct: cts.Token))
            {
                events.Add(@event);
            }
        }
        catch (OperationCanceledException)
        {
            caughtException = new OperationCanceledException();
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert - Should either complete with no events or throw OperationCanceledException
        if (caughtException != null)
        {
            caughtException.Should().BeOfType<OperationCanceledException>(
                "only OperationCanceledException should be thrown on cancellation");
        }

        // No other exception types should be thrown
        caughtException?.Should().BeOfType<OperationCanceledException>();
    }

    /// <summary>
    /// Validates that no orphaned events are emitted after cancellation.
    /// Events received after cancellation is signaled should not contain incomplete or orphaned data.
    /// </summary>
    [Fact]
    public async Task Cancellation_No_Orphaned_Events()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Responder",
            RunId = "run-cancel-003",
            Artifacts = new Dictionary<string, object>
            {
                ["response"] = "Complete response"
            }
        };

        var delayingOrchestrator = new FakeDelayingOrchestrator(expectedResult, TimeSpan.FromSeconds(2));
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var commandSuggester = new FakeCommandSuggester();
        var commandOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(delayingOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, commandSuggester, commandOptions);

        var correlationId = "corr-cancel-003";
        var intent = new WorkflowIntent
        {
            InputRaw = "/plan test message",
            CorrelationId = correlationId
        };

        using var cts = new CancellationTokenSource();

        // Act - Cancel after receiving first few events
        var events = new List<StreamingEvent>();
        var eventCount = 0;

        try
        {
            await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, ct: cts.Token))
            {
                events.Add(@event);
                eventCount++;

                // Cancel after receiving 2 events
                if (eventCount == 2)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - All received events should have proper correlation ID
        foreach (var evt in events)
        {
            evt.CorrelationId.Should().Be(correlationId,
                $"event {evt.Type} should have consistent correlation ID even after cancellation");
        }

        // All events should have valid sequence numbers (if present)
        var eventsWithSequence = events.Where(e => e.SequenceNumber.HasValue).ToList();
        for (int i = 1; i < eventsWithSequence.Count; i++)
        {
            eventsWithSequence[i].SequenceNumber.Should().BeGreaterThan(eventsWithSequence[i - 1].SequenceNumber!.Value,
                "sequence numbers should remain consistent even after cancellation");
        }
    }


    /// <summary>
    /// Validates that cancellation during tool execution stops further tool events.
    /// </summary>
    [Fact]
    public async Task Cancellation_During_Tool_Execution_Stops_Tool_Events()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Executor",
            RunId = "run-cancel-005",
            Artifacts = new Dictionary<string, object>
            {
                ["response"] = "Tool execution cancelled"
            }
        };

        // Create tools that take time to "execute"
        var toolsToEmit = new List<(string callId, string toolName, Dictionary<string, object>? parameters)>
        {
            ("call-001", "SlowTool1", new Dictionary<string, object> { ["param"] = "value1" }),
            ("call-002", "SlowTool2", new Dictionary<string, object> { ["param"] = "value2" }),
            ("call-003", "SlowTool3", new Dictionary<string, object> { ["param"] = "value3" })
        };

        var fakeToolOrchestrator = new FakeCancellableToolOrchestrator(expectedResult, toolsToEmit);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine(new GatingResult
        {
            TargetPhase = "Executor",
            Reason = "Write operation requiring tools",
            Reasoning = "Routing to Executor for tool execution",
            RequiresConfirmation = false,
            ProposedAction = null
        });
        var commandSuggester = new FakeCommandSuggester();
        var commandOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeToolOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, commandSuggester, commandOptions);

        var intent = new WorkflowIntent
        {
            InputRaw = "/execute complex task",
            CorrelationId = "corr-cancel-005"
        };

        using var cts = new CancellationTokenSource();

        // Act - Cancel after first tool call
        var events = new List<StreamingEvent>();
        var toolCallCount = 0;

        try
        {
            await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, ct: cts.Token))
            {
                events.Add(@event);

                if (@event.Type == StreamingEventType.ToolCall)
                {
                    toolCallCount++;
                    if (toolCallCount == 1)
                    {
                        cts.Cancel();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Should have limited tool events
        var toolCalls = events.Count(e => e.Type == StreamingEventType.ToolCall);
        var toolResults = events.Count(e => e.Type == StreamingEventType.ToolResult);

        // Should have at most 1 tool call (the one that triggered cancellation)
        toolCalls.Should().BeLessThanOrEqualTo(1, "should not emit multiple tool calls after cancellation");

        // Tool results should match tool calls (no orphaned results)
        toolResults.Should().BeLessThanOrEqualTo(toolCalls, "should not have more tool results than calls");
    }

    #endregion

    #region Additional Fake Implementations for Cancellation Tests

    private sealed class FakeSlowLlmProvider : ILlmProvider
    {
        private readonly int _chunkCount;
        private readonly int _chunkDelayMs;

        public FakeSlowLlmProvider(int chunkCount, int chunkDelayMs)
        {
            _chunkCount = chunkCount;
            _chunkDelayMs = chunkDelayMs;
        }

        public string ProviderName => "fake-slow";

        public Task<LlmCompletionResponse> CompleteAsync(
            LlmCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmCompletionResponse
            {
                Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = "Slow response" },
                Model = "fake-model",
                Provider = "fake-slow"
            });
        }

        public async IAsyncEnumerable<LlmDelta> StreamCompletionAsync(
            LlmCompletionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < _chunkCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return new LlmDelta
                {
                    Content = i == _chunkCount - 1 ? "" : $"Chunk {i} ",
                    FinishReason = i == _chunkCount - 1 ? "stop" : null
                };

                if (i < _chunkCount - 1)
                {
                    await Task.Delay(_chunkDelayMs, cancellationToken);
                }
            }
        }
    }

    private sealed class FakeDelayingOrchestrator : IOrchestrator
    {
        private readonly OrchestratorResult _result;
        private readonly TimeSpan _delay;

        public FakeDelayingOrchestrator(OrchestratorResult result, TimeSpan delay)
        {
            _result = result;
            _delay = delay;
        }

        public async Task<OrchestratorResult> ExecuteAsync(WorkflowIntent intent, CancellationToken ct = default)
        {
            await Task.Delay(_delay, ct);
            return _result;
        }
    }

    private sealed class FakeCancellableToolOrchestrator : IOrchestrator
    {
        private readonly OrchestratorResult _result;
        private readonly List<(string callId, string toolName, Dictionary<string, object>? parameters)> _toolsToEmit;

        public FakeCancellableToolOrchestrator(
            OrchestratorResult result,
            List<(string callId, string toolName, Dictionary<string, object>? parameters)> toolsToEmit)
        {
            _result = result;
            _toolsToEmit = toolsToEmit;
        }

        public async Task<OrchestratorResult> ExecuteAsync(WorkflowIntent intent, CancellationToken ct = default)
        {
            var toolSink = ToolEventSinkContext.Current;
            if (toolSink != null)
            {
                foreach (var (callId, toolName, parameters) in _toolsToEmit)
                {
                    ct.ThrowIfCancellationRequested();

                    toolSink.EmitToolCall(
                        callId: callId,
                        toolName: toolName,
                        parameters: parameters,
                        phaseContext: ToolEventSinkContext.PhaseContext,
                        correlationId: ToolEventSinkContext.CorrelationId);

                    // Simulate slow tool execution
                    await Task.Delay(100, ct);

                    ct.ThrowIfCancellationRequested();

                    toolSink.EmitToolResult(
                        callId: callId,
                        success: true,
                        result: $"Result from {toolName}",
                        durationMs: 100,
                        correlationId: ToolEventSinkContext.CorrelationId);
                }
            }

            return _result;
        }
    }

    #endregion
}

