using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.ToolCalling;
using Gmsd.Web.AgentRunner;
using Gmsd.Web.Models.Streaming;
using Xunit;

namespace Gmsd.Web.Tests.AgentRunner;

/// <summary>
/// End-to-end tests for tool calling event streaming (Task 8.6).
/// Verifies that Agents layer ToolCallingEvents are correctly converted to Web layer StreamingEvents.
/// </summary>
public class ToolCallingEventStreamingTests
{
    [Fact]
    public void ToStreamingEvent_ToolCallDetectedEvent_ConvertsCorrectly()
    {
        // Arrange
        var detectedEvent = new ToolCallDetectedEvent
        {
            Iteration = 1,
            CorrelationId = "test-correlation",
            ToolCalls = new List<ToolCallDetectedInfo>
            {
                new()
                {
                    ToolCallId = "call-001",
                    ToolName = "write_file",
                    ArgumentsJson = "{\"path\": \"test.txt\", \"content\": \"hello\"}"
                }
            }
        };

        // Act
        var streamingEvent = ToolCallingEventAdapter.ToStreamingEvent(detectedEvent);

        // Assert
        streamingEvent.Type.Should().Be(StreamingEventType.ToolCallDetected);
        streamingEvent.CorrelationId.Should().Be("test-correlation");
        
        var payload = streamingEvent.Payload as ToolCallDetectedPayload;
        payload.Should().NotBeNull();
        payload!.Iteration.Should().Be(1);
        payload.ToolCalls.Should().HaveCount(1);
        payload.ToolCalls[0].ToolCallId.Should().Be("call-001");
        payload.ToolCalls[0].ToolName.Should().Be("write_file");
    }

    [Fact]
    public void ToStreamingEvent_ToolCallStartedEvent_ConvertsCorrectly()
    {
        // Arrange
        var startedEvent = new ToolCallStartedEvent
        {
            Iteration = 1,
            ToolCallId = "call-001",
            ToolName = "write_file",
            ArgumentsJson = "{\"path\": \"test.txt\"}",
            CorrelationId = "test-correlation"
        };

        // Act
        var streamingEvent = ToolCallingEventAdapter.ToStreamingEvent(startedEvent);

        // Assert
        streamingEvent.Type.Should().Be(StreamingEventType.ToolCallStarted);
        
        var payload = streamingEvent.Payload as ToolCallStartedPayload;
        payload.Should().NotBeNull();
        payload!.ToolCallId.Should().Be("call-001");
        payload.ToolName.Should().Be("write_file");
        payload.Iteration.Should().Be(1);
    }

    [Fact]
    public void ToStreamingEvent_ToolCallCompletedEvent_ConvertsCorrectly()
    {
        // Arrange
        var completedEvent = new ToolCallCompletedEvent
        {
            Iteration = 1,
            ToolCallId = "call-001",
            ToolName = "write_file",
            Duration = TimeSpan.FromMilliseconds(150),
            HasResult = true,
            ResultSummary = "File written successfully",
            CorrelationId = "test-correlation"
        };

        // Act
        var streamingEvent = ToolCallingEventAdapter.ToStreamingEvent(completedEvent);

        // Assert
        streamingEvent.Type.Should().Be(StreamingEventType.ToolCallCompleted);
        
        var payload = streamingEvent.Payload as ToolCallCompletedPayload;
        payload.Should().NotBeNull();
        payload!.ToolCallId.Should().Be("call-001");
        payload.DurationMs.Should().Be(150);
        payload.HasResult.Should().BeTrue();
        payload.ResultSummary.Should().Be("File written successfully");
    }

    [Fact]
    public void ToStreamingEvent_ToolCallFailedEvent_ConvertsCorrectly()
    {
        // Arrange
        var failedEvent = new ToolCallFailedEvent
        {
            Iteration = 1,
            ToolCallId = "call-001",
            ToolName = "write_file",
            ErrorCode = "FileAccessDenied",
            ErrorMessage = "Cannot write to protected path",
            Duration = TimeSpan.FromMilliseconds(50),
            CorrelationId = "test-correlation"
        };

        // Act
        var streamingEvent = ToolCallingEventAdapter.ToStreamingEvent(failedEvent);

        // Assert
        streamingEvent.Type.Should().Be(StreamingEventType.ToolCallFailed);
        
        var payload = streamingEvent.Payload as ToolCallFailedPayload;
        payload.Should().NotBeNull();
        payload!.ErrorCode.Should().Be("FileAccessDenied");
        payload.ErrorMessage.Should().Be("Cannot write to protected path");
        payload.DurationMs.Should().Be(50);
    }

    [Fact]
    public void ToStreamingEvent_ToolResultsSubmittedEvent_ConvertsCorrectly()
    {
        // Arrange
        var submittedEvent = new ToolResultsSubmittedEvent
        {
            Iteration = 1,
            ResultCount = 2,
            Results = new List<ToolResultSubmittedInfo>
            {
                new() { ToolCallId = "call-001", ToolName = "write_file", IsSuccess = true },
                new() { ToolCallId = "call-002", ToolName = "read_file", IsSuccess = true }
            },
            CorrelationId = "test-correlation"
        };

        // Act
        var streamingEvent = ToolCallingEventAdapter.ToStreamingEvent(submittedEvent);

        // Assert
        streamingEvent.Type.Should().Be(StreamingEventType.ToolResultsSubmitted);
        
        var payload = streamingEvent.Payload as ToolResultsSubmittedPayload;
        payload.Should().NotBeNull();
        payload!.ResultCount.Should().Be(2);
        payload.Results.Should().HaveCount(2);
    }

    [Fact]
    public void ToStreamingEvent_ToolLoopIterationCompletedEvent_ConvertsCorrectly()
    {
        // Arrange
        var iterationEvent = new ToolLoopIterationCompletedEvent
        {
            Iteration = 2,
            HasMoreToolCalls = true,
            ToolCallCount = 3,
            Duration = TimeSpan.FromSeconds(5),
            CorrelationId = "test-correlation"
        };

        // Act
        var streamingEvent = ToolCallingEventAdapter.ToStreamingEvent(iterationEvent);

        // Assert
        streamingEvent.Type.Should().Be(StreamingEventType.ToolLoopIterationCompleted);
        
        var payload = streamingEvent.Payload as ToolLoopIterationCompletedPayload;
        payload.Should().NotBeNull();
        payload!.Iteration.Should().Be(2);
        payload.HasMoreToolCalls.Should().BeTrue();
        payload.ToolCallCount.Should().Be(3);
        payload.DurationMs.Should().Be(5000);
    }

    [Fact]
    public void ToStreamingEvent_ToolLoopCompletedEvent_ConvertsCorrectly()
    {
        // Arrange
        var completedEvent = new ToolLoopCompletedEvent
        {
            TotalIterations = 3,
            TotalToolCalls = 8,
            CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
            TotalDuration = TimeSpan.FromSeconds(15),
            CorrelationId = "test-correlation"
        };

        // Act
        var streamingEvent = ToolCallingEventAdapter.ToStreamingEvent(completedEvent);

        // Assert
        streamingEvent.Type.Should().Be(StreamingEventType.ToolLoopCompleted);
        
        var payload = streamingEvent.Payload as ToolLoopCompletedPayload;
        payload.Should().NotBeNull();
        payload!.TotalIterations.Should().Be(3);
        payload.TotalToolCalls.Should().Be(8);
        payload.CompletionReason.Should().Be("CompletedNaturally");
        payload.TotalDurationMs.Should().Be(15000);
    }

    [Fact]
    public void ToStreamingEvent_ToolLoopFailedEvent_ConvertsCorrectly()
    {
        // Arrange
        var failedEvent = new ToolLoopFailedEvent
        {
            ErrorCode = "MaxIterationsReached",
            ErrorMessage = "Maximum iterations (20) exceeded",
            Iteration = 20,
            CorrelationId = "test-correlation"
        };

        // Act
        var streamingEvent = ToolCallingEventAdapter.ToStreamingEvent(failedEvent);

        // Assert
        streamingEvent.Type.Should().Be(StreamingEventType.ToolLoopFailed);
        
        var payload = streamingEvent.Payload as ToolLoopFailedPayload;
        payload.Should().NotBeNull();
        payload!.ErrorCode.Should().Be("MaxIterationsReached");
        payload.Iteration.Should().Be(20);
    }

    [Fact]
    public void ToStreamingEvent_UnknownEvent_ReturnsErrorEvent()
    {
        // Arrange - create a custom event type that isn't handled
        var unknownEvent = new UnknownToolCallingEvent();

        // Act
        var streamingEvent = ToolCallingEventAdapter.ToStreamingEvent(unknownEvent);

        // Assert
        streamingEvent.Type.Should().Be(StreamingEventType.Error);
        
        var payload = streamingEvent.Payload as ErrorPayload;
        payload.Should().NotBeNull();
        payload!.Code.Should().Be("UnknownToolEvent");
    }

    [Fact]
    public void StreamingToolEventSink_Emit_WithSequenceNumber_IncrementsSequence()
    {
        // Arrange
        var mockSink = new MockEventSink();
        var streamingSink = new StreamingToolEventSink(mockSink, startingSequence: 100);

        var detectedEvent = new ToolCallDetectedEvent
        {
            Iteration = 1,
            ToolCalls = new List<ToolCallDetectedInfo>()
        };

        // Act
        streamingSink.Emit(detectedEvent);

        // Assert
        mockSink.LastEvent.Should().NotBeNull();
        mockSink.LastEvent!.SequenceNumber.Should().Be(101);
    }

    [Fact]
    public void StreamingEvent_Serialization_RoundTrip()
    {
        // Arrange
        var payload = new ToolCallDetectedPayload
        {
            Iteration = 1,
            ToolCalls = new List<ToolCallInfo>
            {
                new() { ToolCallId = "call-001", ToolName = "write_file", ArgumentsJson = "{}" }
            }
        };

        var streamingEvent = StreamingEvent.Create(StreamingEventType.ToolCallDetected, payload, "correlation-123");

        // Act
        var json = JsonSerializer.Serialize(streamingEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var deserialized = JsonSerializer.Deserialize<StreamingEvent>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Type.Should().Be(StreamingEventType.ToolCallDetected);
        deserialized.CorrelationId.Should().Be("correlation-123");
    }

    /// <summary>
    /// Mock event sink for testing
    /// </summary>
    private class MockEventSink : IEventSink
    {
        public StreamingEvent? LastEvent { get; private set; }

        public bool IsCompleted { get; private set; }

        public ValueTask<bool> EmitAsync(StreamingEvent @event, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastEvent = @event;
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> EmitAsync<TPayload>(
            StreamingEventType type,
            TPayload payload,
            string? correlationId = null,
            long? sequenceNumber = null,
            CancellationToken cancellationToken = default) where TPayload : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastEvent = StreamingEvent.Create(type, payload, correlationId, sequenceNumber);
            return ValueTask.FromResult(true);
        }

        public bool TryEmit(StreamingEvent @event)
        {
            LastEvent = @event;
            return true;
        }

        public bool TryEmit<TPayload>(
            StreamingEventType type,
            TPayload payload,
            string? correlationId = null,
            long? sequenceNumber = null) where TPayload : class
        {
            LastEvent = StreamingEvent.Create(type, payload, correlationId, sequenceNumber);
            return true;
        }

        public void Complete(Exception? exception = null)
        {
            IsCompleted = true;
        }
    }

    /// <summary>
    /// Unknown event type for testing error handling
    /// </summary>
    private class UnknownToolCallingEvent : ToolCallingEvent
    {
        public UnknownToolCallingEvent()
        {
            EventType = (ToolCallingEventType)999; // Unknown type
        }
    }
}
