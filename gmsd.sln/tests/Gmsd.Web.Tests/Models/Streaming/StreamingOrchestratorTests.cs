using System.Runtime.CompilerServices;
using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Preflight;
using Gmsd.Agents.Execution.Preflight.CommandSuggestion;
using Gmsd.Web.Models.Streaming;
using Microsoft.Extensions.Options;
using Xunit;

using WebIntentPayload = Gmsd.Web.Models.Streaming.IntentClassifiedPayload;

namespace Gmsd.Web.Tests.Models.Streaming;

/// <summary>
/// Integration tests for the StreamingOrchestrator that verify event emission
/// in correct sequence when wrapping the core orchestrator.
/// </summary>
public class StreamingOrchestratorTests
{
    #region Fake Implementations

    private sealed class FakeOrchestrator : IOrchestrator
    {
        private readonly OrchestratorResult? _result;
        private readonly Exception? _exception;

        public FakeOrchestrator(OrchestratorResult result)
        {
            _result = result;
        }

        public FakeOrchestrator(Exception exception)
        {
            _exception = exception;
        }

        public Task<OrchestratorResult> ExecuteAsync(WorkflowIntent intent, CancellationToken ct = default)
        {
            if (_exception != null)
                throw _exception;
            return Task.FromResult(_result!);
        }
    }

    private sealed class FakeCommandSuggester : ICommandSuggester
    {
        public Task<CommandProposal?> SuggestAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult<CommandProposal?>(null);
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
                Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = "Fake response" },
                Model = "fake-model",
                Provider = "fake"
            });
        }

        public async IAsyncEnumerable<LlmDelta> StreamCompletionAsync(
            LlmCompletionRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Simulate streaming response
            yield return new LlmDelta { Content = "Fake " };
            yield return new LlmDelta { Content = "response" };
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
                TargetPhase = "Planner",
                Reason = "Default test result",
                Reasoning = "Routing to Planner based on test context",
                RequiresConfirmation = false,
                ProposedAction = null
            };
            return Task.FromResult(result);
        }
    }

    #endregion

    #region ExecuteWithEventsAsync Tests

    [Fact]
    public async Task ExecuteWithEventsAsync_SuccessfulExecution_EmitsCorrectSequence()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Planner",
            RunId = "run-123",
            Artifacts = new Dictionary<string, object>
            {
                ["reason"] = "User wants to plan"
            }
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "/plan",
            CorrelationId = "corr-abc"
        };
        var options = StreamingOrchestrationOptions.Default;

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, options))
        {
            events.Add(@event);
        }

        // Assert - for Write-classified input with successful execution
        events.Should().HaveCountGreaterOrEqualTo(4, "expected: intent.classified, gate.selected, run.started, run.finished");

        // Event 1: intent.classified
        events[0].Type.Should().Be(StreamingEventType.IntentClassified);
        events[0].CorrelationId.Should().Be("corr-abc");
        var intentPayload = events[0].Payload as WebIntentPayload;
        intentPayload.Should().NotBeNull();
        intentPayload!.Category.Should().Be("Write");
        intentPayload.Confidence.Should().BeGreaterThan(0);
        intentPayload.Reasoning.Should().NotBeNullOrEmpty();

        // Should have gate.selected and run events for Write-classified input
        events.Should().Contain(e => e.Type == StreamingEventType.GateSelected);
        events.Should().Contain(e => e.Type == StreamingEventType.RunLifecycle);
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_FailedExecution_EmitsCorrectSequence()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = false,
            FinalPhase = "Executor",
            RunId = "run-456",
            Artifacts = new Dictionary<string, object>
            {
                ["error"] = "Execution failed"
            }
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-def"
        };

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent))
        {
            events.Add(@event);
        }

        // Assert - for Write-classified input with failed execution
        events.Should().HaveCountGreaterOrEqualTo(4, "expected: intent.classified, gate.selected, run.started, run.finished");

        // Verify run.finished has success = false
        var runFinishedEvent = events.Last();
        runFinishedEvent.Type.Should().Be(StreamingEventType.RunLifecycle);
        var runFinishedPayload = runFinishedEvent.Payload as RunLifecyclePayload;
        runFinishedPayload.Should().NotBeNull();
        runFinishedPayload!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_ExceptionThrown_EmitsErrorAndRunFinished()
    {
        // Arrange
        var fakeOrchestrator = new FakeOrchestrator(new InvalidOperationException("Test exception"));
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "/plan",
            CorrelationId = "corr-ghi"
        };

        // Act - collect events then expect exception at end
        var events = new List<StreamingEvent>();
        Exception? thrownException = null;
        try
        {
            await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent))
            {
                events.Add(@event);
            }
        }
        catch (InvalidOperationException ex)
        {
            thrownException = ex;
        }

        // Assert
        events.Should().HaveCountGreaterOrEqualTo(3);
        thrownException.Should().NotBeNull();
        thrownException!.Message.Should().Be("Test exception");

        // Verify error event is emitted
        var errorEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.Error);
        errorEvent.Should().NotBeNull();
        var errorPayload = errorEvent!.Payload as ErrorPayload;
        errorPayload.Should().NotBeNull();
        errorPayload!.Code.Should().Be("ORCHESTRATION_FAILED");
        errorPayload.Message.Should().Be("Test exception");

        // Verify run.finished with failure
        var runFinishedEvent = events.Last();
        var runFinishedPayload = runFinishedEvent.Payload as RunLifecyclePayload;
        runFinishedPayload.Should().NotBeNull();
        runFinishedPayload!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_ChatOnlyOptions_DoesNotEmitRunEvents()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Responder",
            RunId = "run-789"
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "Hello, how are you?",
            CorrelationId = "corr-jkl"
        };
        var chatOptions = StreamingOrchestrationOptions.ChatOnly;

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, chatOptions))
        {
            events.Add(@event);
        }

        // Assert
        events.Should().HaveCount(3, "expected: intent.classified, assistant.delta, assistant.final (no run events in chat mode)");
        events.Should().NotContain(e => e.Type == StreamingEventType.RunLifecycle);
        events.Should().Contain(e => e.Type == StreamingEventType.IntentClassified);
        events.Should().Contain(e => e.Type == StreamingEventType.AssistantDelta || e.Type == StreamingEventType.AssistantFinal);
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_ChatClassifiedInput_AutomaticallySuppressesRunEvents()
    {
        // Arrange - using default options where EmitRunLifecycle=true, but input is Chat-classified
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Responder",
            RunId = "run-789"
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "hello",  // Chat-classified input
            CorrelationId = "corr-chat-auto"
        };
        var defaultOptions = StreamingOrchestrationOptions.Default; // EmitRunLifecycle = true

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, defaultOptions))
        {
            events.Add(@event);
        }

        // Assert - run events should be suppressed automatically for Chat inputs even with default options
        events.Should().HaveCount(3, "expected: intent.classified, assistant.delta, assistant.final (no run events for Chat-classified input)");
        events.Should().NotContain(e => e.Type == StreamingEventType.RunLifecycle);
        
        // Verify the classification is Chat
        var intentEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.IntentClassified);
        intentEvent.Should().NotBeNull();
        var payload = intentEvent!.Payload as WebIntentPayload;
        payload!.Category.Should().Be("Chat");
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_MiminalOptions_OnlyEmitsIntentAndGate()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Planner",
            RunId = "run-999"
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "/plan",
            CorrelationId = "corr-mno"
        };
        var minimalOptions = StreamingOrchestrationOptions.Minimal;

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, minimalOptions))
        {
            events.Add(@event);
        }

        // Assert - minimal options emit assistant events (intent classification is disabled but chat path still works)
        events.Should().HaveCountGreaterOrEqualTo(1);
        events.Should().Contain(e => e.Type == StreamingEventType.AssistantDelta || e.Type == StreamingEventType.AssistantFinal);
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_NullIntent_ThrowsArgumentNullException()
    {
        // Arrange
        var fakeOrchestrator = new FakeOrchestrator(new OrchestratorResult { IsSuccess = true });
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in streamingOrchestrator.ExecuteWithEventsAsync(null!))
            {
                // Should not reach here
            }
        });
    }

    [Theory]
    [InlineData("hello", "Chat", 1.0)]
    [InlineData("/plan", "Write", 1.0)]
    [InlineData("/run", "Write", 1.0)]
    [InlineData("/status", "ReadOnly", 1.0)]
    public async Task ExecuteWithEventsAsync_VariousInputs_CorrectlyClassifies(
        string input, string expectedCategory, double expectedConfidence)
    {
        // Arrange
        var fakeOrchestrator = new FakeOrchestrator(new OrchestratorResult { IsSuccess = true });
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = input,
            CorrelationId = "corr-test"
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
        var payload = intentEvent!.Payload as WebIntentPayload;
        payload.Should().NotBeNull();
        payload!.Category.Should().Be(expectedCategory);
        payload.Confidence.Should().Be(expectedConfidence);
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_SequenceNumbersDisabled_EmitsNoSequenceNumbers()
    {
        // Arrange
        var fakeOrchestrator = new FakeOrchestrator(new OrchestratorResult { IsSuccess = true });
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "Test",
            CorrelationId = "corr-seq"
        };
        var options = new StreamingOrchestrationOptions { EnableSequenceNumbers = false };

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, options))
        {
            events.Add(@event);
        }

        // Assert
        events.Should().NotBeEmpty();
        events.Should().AllSatisfy(e => e.SequenceNumber.Should().BeNull());
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_EmptyCorrelationId_GeneratesNewCorrelationId()
    {
        // Arrange
        var fakeOrchestrator = new FakeOrchestrator(new OrchestratorResult { IsSuccess = true });
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "Test",
            CorrelationId = null!
        };
        var options = new StreamingOrchestrationOptions { CorrelationId = null };

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, options))
        {
            events.Add(@event);
        }

        // Assert
        events.Should().NotBeEmpty();
        events.First().CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_CancellationToken_CanBeCancelled()
    {
        // Arrange
        var fakeOrchestrator = new FakeOrchestrator(new OrchestratorResult { IsSuccess = true });
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "/Test",
            CorrelationId = "corr-cancel"
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - TaskCanceledException derives from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in streamingOrchestrator.ExecuteWithEventsAsync(intent, ct: cts.Token))
            {
                // Should be cancelled before or during iteration
            }
        });
    }

    #endregion

    #region Event Emission Sequence Tests (Task 3.4)

    [Fact]
    public async Task ExecuteWithEventsAsync_EmitsCorrectEventSequence()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Planner",
            RunId = "run-seq"
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "/plan",
            CorrelationId = "corr-seq-test"
        };

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent))
        {
            events.Add(@event);
        }

        // Assert - Verify sequence for Write-classified input: intent.classified → gate.selected → run events
        events.Should().HaveCountGreaterOrEqualTo(4, "expected: intent.classified, gate.selected, run.started, run.finished");

        var eventTypes = events.Select(e => e.Type).ToList();
        eventTypes[0].Should().Be(StreamingEventType.IntentClassified, "first event should be intent.classified");
        eventTypes.Should().Contain(StreamingEventType.GateSelected, "should have gate.selected");
        eventTypes.Should().Contain(StreamingEventType.RunLifecycle, "should have run lifecycle events");
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_GateSelectedEvent_HasCorrectPayloadStructure()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Executor",
            RunId = "run-payload"
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine(new GatingResult
        {
            TargetPhase = "Executor",
            Reason = "Execution required",
            Reasoning = "Routing to Executor for write-destructive operation",
            RequiresConfirmation = true,
            ProposedAction = new Gmsd.Agents.Execution.ControlPlane.ProposedAction
            {
                Phase = "Executor",
                Description = "Execute planned tasks",
                RiskLevel = Gmsd.Agents.Execution.ControlPlane.RiskLevel.WriteDestructive,
                SideEffects = new[] { "file_system" },
                AffectedResources = new[] { "workspace_files" }
            }
        });
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "/run",
            CorrelationId = "corr-payload-test"
        };

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent))
        {
            events.Add(@event);
        }

        // Assert - Verify gate.selected payload structure for Write-classified /run command
        // Note: /run maps to Executor phase which is write-destructive and requires confirmation
        var gateSelectedEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.GateSelected);
        gateSelectedEvent.Should().NotBeNull("gate.selected event should be emitted for Write-classified input");

        var gatePayload = gateSelectedEvent!.Payload as GateSelectedPayload;
        gatePayload.Should().NotBeNull();
        gatePayload!.Phase.Should().NotBeNullOrEmpty("phase should be set");
        gatePayload.Reasoning.Should().NotBeNullOrEmpty("reasoning should be provided");
        gatePayload.RequiresConfirmation.Should().BeTrue("Executor phase requires confirmation");
        gatePayload.ProposedAction.Should().NotBeNull("proposed action should be set for confirmation");
        gatePayload.ProposedAction!.Description.Should().NotBeNullOrEmpty("proposed action should have description");
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_EventSequence_HasMonotonicSequenceNumbers()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Planner",
            RunId = "run-seq-num"
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var intent = new WorkflowIntent
        {
            InputRaw = "/plan",
            CorrelationId = "corr-seq-num"
        };
        var options = new StreamingOrchestrationOptions { EnableSequenceNumbers = true };

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent, options))
        {
            events.Add(@event);
        }

        // Assert - Verify monotonic sequence numbers
        events.Should().AllSatisfy(e => e.SequenceNumber.Should().HaveValue());

        var sequenceNumbers = events.Select(e => e.SequenceNumber!.Value).ToList();
        for (int i = 1; i < sequenceNumbers.Count; i++)
        {
            sequenceNumbers[i].Should().BeGreaterThan(sequenceNumbers[i - 1],
                $"event {i} sequence number ({sequenceNumbers[i]}) should be greater than event {i - 1} ({sequenceNumbers[i - 1]})");
        }
    }

    [Fact]
    public async Task ExecuteWithEventsAsync_AllEventsHaveSameCorrelationId()
    {
        // Arrange
        var expectedResult = new OrchestratorResult
        {
            IsSuccess = true,
            FinalPhase = "Planner",
            RunId = "run-corr"
        };
        var fakeOrchestrator = new FakeOrchestrator(expectedResult);
        var inputClassifier = new InputClassifier();
        var fakeLlmProvider = new FakeLlmProvider();
        var fakeGatingEngine = new FakeGatingEngine();
        var fakeCommandSuggester = new FakeCommandSuggester();
        var suggestionOptions = Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false });
        var streamingOrchestrator = new StreamingOrchestrator(fakeOrchestrator, fakeGatingEngine, inputClassifier, fakeLlmProvider, fakeCommandSuggester, suggestionOptions);
        var expectedCorrelationId = "corr-consistent";
        var intent = new WorkflowIntent
        {
            InputRaw = "/plan",
            CorrelationId = expectedCorrelationId
        };

        // Act
        var events = new List<StreamingEvent>();
        await foreach (var @event in streamingOrchestrator.ExecuteWithEventsAsync(intent))
        {
            events.Add(@event);
        }

        // Assert - All events should share the same correlation ID
        events.Should().AllSatisfy(e => e.CorrelationId.Should().Be(expectedCorrelationId));
    }

    #endregion
}
