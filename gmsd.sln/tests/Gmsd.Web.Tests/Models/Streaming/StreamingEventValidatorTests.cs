using FluentAssertions;
using Gmsd.Web.Models.Streaming;
using Xunit;

namespace Gmsd.Web.Tests.Models.Streaming;

public class StreamingEventValidatorTests
{
    private readonly StreamingEventValidator _validator = new();

    [Fact]
    public void ValidateEvent_WithValidIntentClassifiedEvent_ReturnsValid()
    {
        var @event = new StreamingEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = StreamingEventType.IntentClassified,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new IntentClassifiedPayload
            {
                Category = "Chat",
                Confidence = 0.95,
                Reasoning = "User is asking a question",
                UserInput = "What is the status?"
            }
        };

        var result = _validator.ValidateEvent(@event);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateEvent_WithMissingId_ReturnsInvalid()
    {
        var @event = new StreamingEvent
        {
            Id = string.Empty,
            Type = StreamingEventType.IntentClassified,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new IntentClassifiedPayload { Category = "Chat", Confidence = 0.95 }
        };

        var result = _validator.ValidateEvent(@event);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Event ID is required"));
    }

    [Fact]
    public void ValidateEvent_WithInvalidConfidence_ReturnsInvalid()
    {
        var @event = new StreamingEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = StreamingEventType.IntentClassified,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new IntentClassifiedPayload
            {
                Category = "Chat",
                Confidence = 1.5 // Invalid: > 1.0
            }
        };

        var result = _validator.ValidateEvent(@event);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("confidence must be between 0.0 and 1.0"));
    }

    [Fact]
    public void ValidateEvent_WithValidToolCallEvent_ReturnsValid()
    {
        var @event = new StreamingEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = StreamingEventType.ToolCall,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N"),
            SequenceNumber = 1,
            Payload = new ToolCallPayload
            {
                CallId = "call-001",
                ToolName = "read_file",
                Parameters = new Dictionary<string, object> { ["path"] = "/test.txt" }
            }
        };

        var result = _validator.ValidateEvent(@event);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateEvent_WithMissingToolCallId_ReturnsInvalid()
    {
        var @event = new StreamingEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = StreamingEventType.ToolCall,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new ToolCallPayload
            {
                CallId = string.Empty,
                ToolName = "read_file"
            }
        };

        var result = _validator.ValidateEvent(@event);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("CallId"));
    }

    [Fact]
    public void ValidateEvent_WithValidPhaseLifecycleEvent_ReturnsValid()
    {
        var @event = new StreamingEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = StreamingEventType.PhaseLifecycle,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new PhaseLifecyclePayload
            {
                Phase = "Planner",
                Status = "started",
                Context = new Dictionary<string, object> { ["phaseId"] = "PH-001" }
            }
        };

        var result = _validator.ValidateEvent(@event);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateEvent_WithInvalidPhaseStatus_ReturnsInvalid()
    {
        var @event = new StreamingEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = StreamingEventType.PhaseLifecycle,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new PhaseLifecyclePayload
            {
                Phase = "Planner",
                Status = "invalid_status"
            }
        };

        var result = _validator.ValidateEvent(@event);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("'started' or 'completed'"));
    }

    [Fact]
    public void ValidateEvent_WithValidErrorEvent_ReturnsValid()
    {
        var @event = new StreamingEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = StreamingEventType.Error,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new ErrorPayload
            {
                Severity = "error",
                Code = "TOOL_EXECUTION_FAILED",
                Message = "Tool execution failed",
                Context = "Planner",
                Recoverable = true,
                RetryAction = "retry"
            }
        };

        var result = _validator.ValidateEvent(@event);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateEvent_WithInvalidErrorSeverity_ReturnsInvalid()
    {
        var @event = new StreamingEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = StreamingEventType.Error,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new ErrorPayload
            {
                Severity = "critical",
                Code = "ERROR",
                Message = "An error occurred"
            }
        };

        var result = _validator.ValidateEvent(@event);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("'error', 'warning', or 'info'"));
    }

    [Fact]
    public void ValidateEventJson_WithValidJson_ReturnsValid()
    {
        var json = @"{
            ""id"": ""evt-001"",
            ""type"": 3,
            ""timestamp"": ""2026-02-10T12:00:00Z"",
            ""correlationId"": ""corr-001"",
            ""sequenceNumber"": 1,
            ""payload"": {
                ""category"": ""Chat"",
                ""confidence"": 0.95,
                ""reasoning"": ""User is asking a question"",
                ""userInput"": ""What is the status?""
            }
        }";

        var result = _validator.ValidateEventJson(json);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateEventJson_WithInvalidJson_ReturnsInvalid()
    {
        var json = "{ invalid json }";

        var result = _validator.ValidateEventJson(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("JSON parsing error"));
    }
}
