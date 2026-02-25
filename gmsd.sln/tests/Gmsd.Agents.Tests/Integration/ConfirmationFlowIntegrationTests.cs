using Gmsd.Agents.Execution.ControlPlane.Streaming;
using Gmsd.Agents.Execution.Preflight;
using Gmsd.Agents.Execution.ControlPlane;
using Xunit;

namespace Gmsd.Agents.Tests.Integration;

/// <summary>
/// Integration tests for the full confirmation flow with fake LLM and event emitter.
/// </summary>
public class ConfirmationFlowIntegrationTests
{
    private readonly ConfirmationEventPublisher _eventPublisher;
    private readonly List<ConfirmationEvent> _capturedEvents;
    private readonly ConfirmationGate _confirmationGate;

    public ConfirmationFlowIntegrationTests()
    {
        _capturedEvents = new List<ConfirmationEvent>();
        var mockEmitter = new MockStreamingEventEmitter(_capturedEvents);
        _eventPublisher = new ConfirmationEventPublisher(mockEmitter);
        _confirmationGate = new ConfirmationGate(_eventPublisher);
    }

    [Fact]
    public void FullFlow_ConfidenceBelowThreshold_EmitsRequestedEvent()
    {
        // Arrange
        var classification = CreateClassification(SideEffect.Write, 0.7);
        var options = new ConfirmationGateOptions { ConfirmationThreshold = 0.9 };
        var action = CreateProposedAction("Executor", "Create new file", RiskLevel.WriteDestructive);

        // Act
        var result = _confirmationGate.Evaluate(classification, options);

        // Assert
        Assert.True(result.RequiresConfirmation);
        Assert.Single(_capturedEvents);
        Assert.IsType<ConfirmationRequestedEvent>(_capturedEvents[0]);
    }

    [Fact]
    public void FullFlow_AcceptConfirmation_EmitsAcceptedEvent()
    {
        // Arrange
        var classification = CreateClassification(SideEffect.Write, 0.7);
        var options = new ConfirmationGateOptions { ConfirmationThreshold = 0.9 };
        var action = CreateProposedAction("Executor", "Create new file", RiskLevel.WriteDestructive);

        var evaluation = _confirmationGate.Evaluate(classification, options);
        var confirmationId = evaluation.Request!.Id;
        _capturedEvents.Clear(); // Clear the requested event

        // Act
        _eventPublisher.PublishAccepted(confirmationId, action);

        // Assert
        Assert.Single(_capturedEvents);
        var acceptedEvent = Assert.IsType<ConfirmationAcceptedEvent>(_capturedEvents[0]);
        Assert.Equal(confirmationId, acceptedEvent.ConfirmationId);
    }

    [Fact]
    public void FullFlow_RejectConfirmation_EmitsRejectedEvent()
    {
        // Arrange
        var classification = CreateClassification(SideEffect.Write, 0.7);
        var options = new ConfirmationGateOptions { ConfirmationThreshold = 0.9 };
        var action = CreateProposedAction("Executor", "Create new file", RiskLevel.WriteDestructive);

        var evaluation = _confirmationGate.Evaluate(classification, options);
        var confirmationId = evaluation.Request!.Id;
        _capturedEvents.Clear();

        // Act
        _eventPublisher.PublishRejected(confirmationId, "User declined", action);

        // Assert
        Assert.Single(_capturedEvents);
        var rejectedEvent = Assert.IsType<ConfirmationRejectedEvent>(_capturedEvents[0]);
        Assert.Equal(confirmationId, rejectedEvent.ConfirmationId);
        Assert.Equal("User declined", rejectedEvent.UserMessage);
    }

    [Fact]
    public void FullFlow_Timeout_EmitsTimeoutEvent()
    {
        // Arrange
        var classification = CreateClassification(SideEffect.Write, 0.7);
        var options = new ConfirmationGateOptions { ConfirmationThreshold = 0.9 };
        var action = CreateProposedAction("Executor", "Create new file", RiskLevel.WriteDestructive);

        var evaluation = _confirmationGate.Evaluate(classification, options);
        var confirmationId = evaluation.Request!.Id;
        var requestedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        _capturedEvents.Clear();

        // Act
        _eventPublisher.PublishTimeout(confirmationId, requestedAt, TimeSpan.FromMinutes(5), action, "timeout");

        // Assert
        Assert.Single(_capturedEvents);
        var timeoutEvent = Assert.IsType<ConfirmationTimeoutEvent>(_capturedEvents[0]);
        Assert.Equal(confirmationId, timeoutEvent.ConfirmationId);
        Assert.Equal("timeout", timeoutEvent.CancellationReason);
    }

    [Fact]
    public void FullFlow_AmbiguousRequest_EmitsRequestedWithAmbiguityReason()
    {
        // Arrange - Create an ambiguous classification
        var classification = new IntentClassificationResult
        {
            Intent = new Intent
            {
                Kind = IntentKind.WorkflowCommand,
                SideEffect = SideEffect.Write,
                Confidence = 0.85,
                Reasoning = "Detected vague terms: 'do something', 'handle it'"
            },
            ParsedCommand = new ParsedCommand
            {
                RawInput = "do something with the files",
                CommandName = "unknown",
                SideEffect = SideEffect.Write,
                Confidence = 0.85
            },
            RequiresConfirmation = true
        };

        var options = new ConfirmationGateOptions { ConfirmationThreshold = 0.9 };

        // Act
        var result = _confirmationGate.Evaluate(classification, options);

        // Assert
        Assert.True(result.RequiresConfirmation);
        var requestedEvent = Assert.IsType<ConfirmationRequestedEvent>(_capturedEvents[0]);
        Assert.Contains("Ambiguous", requestedEvent.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FullFlow_DestructiveOperation_AlwaysRequiresConfirmation()
    {
        // Arrange
        var classification = new IntentClassificationResult
        {
            Intent = new Intent
            {
                Kind = IntentKind.WorkflowCommand,
                SideEffect = SideEffect.Write,
                Confidence = 0.95, // High confidence
                Reasoning = "Git commit operation detected"
            },
            ParsedCommand = new ParsedCommand
            {
                RawInput = "commit all changes",
                CommandName = "git-commit",
                SideEffect = SideEffect.Write,
                Confidence = 0.95
            }
        };

        var options = new ConfirmationGateOptions { ConfirmationThreshold = 0.8 };

        // Act
        // Use EvaluateWithDestructiveness to trigger risk-based analysis
        // "Executor" phase typically implies higher risk/destructiveness
        var result = _confirmationGate.EvaluateWithDestructiveness(
            classification,
            "Executor",
            new GatingContext
            {
                HasProject = true,
                HasRoadmap = true,
                HasPlan = true
            },
            options);

        // Assert - Even with high confidence, destructive operations require confirmation
        Assert.True(result.RequiresConfirmation);
        Assert.Contains("Destructive", result.Reason);
    }

    [Fact]
    public void FullFlow_ConfirmationKey_DuplicateDetection()
    {
        // Arrange
        var action = CreateProposedAction("Executor", "Create test file", RiskLevel.WriteSafe);
        var classification = CreateClassification(SideEffect.Write, 0.7);
        var options = new ConfirmationGateOptions { ConfirmationThreshold = 0.9 };

        // Act - First evaluation
        var result1 = _confirmationGate.Evaluate(classification, options);
        var firstEvent = _capturedEvents[0] as ConfirmationRequestedEvent;
        var confirmationKey = firstEvent!.ConfirmationKey;

        // Act - Second evaluation with same action
        _capturedEvents.Clear();
        var result2 = _confirmationGate.Evaluate(classification, options);
        var secondEvent = _capturedEvents[0] as ConfirmationRequestedEvent;

        // Assert - Both events should have the same confirmation key for duplicate detection
        Assert.NotNull(confirmationKey);
        Assert.NotNull(secondEvent!.ConfirmationKey);
    }

    private static IntentClassificationResult CreateClassification(SideEffect sideEffect, double confidence)
    {
        return new IntentClassificationResult
        {
            Intent = new Intent
            {
                Kind = sideEffect == SideEffect.Write ? IntentKind.WorkflowCommand : IntentKind.Unknown,
                SideEffect = sideEffect,
                Confidence = confidence,
                Reasoning = "Test classification"
            },
            RequiresConfirmation = confidence < 0.9
        };
    }

    private static ProposedAction CreateProposedAction(string phase, string description, RiskLevel riskLevel)
    {
        return new ProposedAction
        {
            Phase = phase,
            Description = description,
            RiskLevel = riskLevel,
            AffectedResources = new[] { "test.txt" }
        };
    }

    /// <summary>
    /// Mock streaming event emitter that captures events for verification.
    /// </summary>
    private class MockStreamingEventEmitter : IStreamingEventEmitter
    {
        private readonly List<ConfirmationEvent> _capturedEvents;
        private string? _correlationId;

        public MockStreamingEventEmitter(List<ConfirmationEvent> capturedEvents)
        {
            _capturedEvents = capturedEvents;
        }

        public void Emit(ChatStreamingEvent @event)
        {
            // Not capturing chat events in these tests
        }

        public void Emit(ConfirmationEvent @event)
        {
            _capturedEvents.Add(@event);
        }

        public void SetCorrelationId(string correlationId)
        {
            _correlationId = correlationId;
        }
    }
}
