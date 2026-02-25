using System.Text.Json;
using FluentAssertions;
using Gmsd.Web.Models.Streaming;
using Xunit;

namespace Gmsd.Web.Tests.Models.Streaming;

public class StreamingEventSerializationTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void StreamingEvent_Type_RoundTripsCorrectly()
    {
        var original = StreamingEvent.Create(
            StreamingEventType.IntentClassified,
            new IntentClassifiedPayload
            {
                Category = "Chat",
                Confidence = 0.95,
                Reasoning = "User is asking a simple question",
                UserInput = "Hello"
            },
            correlationId: "corr-123",
            sequenceNumber: 1
        );

        var json = JsonSerializer.Serialize(original, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<StreamingEvent>(json, _jsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(original.Id);
        deserialized.Type.Should().Be(StreamingEventType.IntentClassified);
        deserialized.CorrelationId.Should().Be("corr-123");
        deserialized.SequenceNumber.Should().Be(1);
        deserialized.Timestamp.Should().BeCloseTo(original.Timestamp, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void IntentClassifiedPayload_RoundTripsCorrectly()
    {
        var payload = new IntentClassifiedPayload
        {
            Category = "Write",
            Confidence = 0.87,
            Reasoning = "User wants to create a file",
            UserInput = "create a new class"
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<IntentClassifiedPayload>(json, _jsonOptions);

        deserialized.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void IntentClassifiedPayload_WithEnvelope_RoundTripsCorrectly()
    {
        var payload = new IntentClassifiedPayload
        {
            Category = "Plan",
            Confidence = 0.92,
            Reasoning = "Multi-step task detected",
            UserInput = "plan my project"
        };

        var envelope = StreamingEvent.Create(StreamingEventType.IntentClassified, payload);
        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<StreamingEvent>(json, _jsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Type.Should().Be(StreamingEventType.IntentClassified);

        var deserializedPayload = deserialized.Payload as JsonElement?;
        deserializedPayload.Should().NotBeNull();

        var extractedPayload = deserializedPayload!.Value.Deserialize<IntentClassifiedPayload>(_jsonOptions);
        extractedPayload.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void GateSelectedPayload_RoundTripsCorrectly()
    {
        var payload = new GateSelectedPayload
        {
            Phase = "Planner",
            Reasoning = "Intent requires planning phase",
            RequiresConfirmation = true,
            ProposedAction = new ProposedAction
            {
                Description = "Create project structure",
                ActionType = "plan"
            }
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<GateSelectedPayload>(json, _jsonOptions);

        deserialized.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void GateSelectedPayload_WithoutConfirmation_RoundTripsCorrectly()
    {
        var payload = new GateSelectedPayload
        {
            Phase = "Chat",
            Reasoning = "Simple chat intent",
            RequiresConfirmation = false,
            ProposedAction = null
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<GateSelectedPayload>(json, _jsonOptions);

        deserialized.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void ToolCallPayload_RoundTripsCorrectly()
    {
        var payload = new ToolCallPayload
        {
            CallId = "call-123",
            ToolName = "FileReader",
            Parameters = new Dictionary<string, object>
            {
                ["path"] = "/test/file.txt",
                ["encoding"] = "utf-8"
            },
            PhaseContext = "Planner"
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ToolCallPayload>(json, _jsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.CallId.Should().Be("call-123");
        deserialized.ToolName.Should().Be("FileReader");
        deserialized.PhaseContext.Should().Be("Planner");
    }

    [Fact]
    public void ToolResultPayload_Success_RoundTripsCorrectly()
    {
        var payload = new ToolResultPayload
        {
            CallId = "call-123",
            Success = true,
            Result = new { content = "file contents" },
            Error = null,
            DurationMs = 150
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ToolResultPayload>(json, _jsonOptions);

        deserialized!.CallId.Should().Be(payload.CallId);
        deserialized.Success.Should().Be(payload.Success);
        deserialized.Error.Should().Be(payload.Error);
        deserialized.DurationMs.Should().Be(payload.DurationMs);
    }

    [Fact]
    public void ToolResultPayload_Failure_RoundTripsCorrectly()
    {
        var payload = new ToolResultPayload
        {
            CallId = "call-456",
            Success = false,
            Result = null,
            Error = "File not found",
            DurationMs = 25
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ToolResultPayload>(json, _jsonOptions);

        deserialized!.CallId.Should().Be(payload.CallId);
        deserialized.Success.Should().Be(payload.Success);
        deserialized.Error.Should().Be(payload.Error);
        deserialized.DurationMs.Should().Be(payload.DurationMs);
    }

    [Fact]
    public void PhaseLifecyclePayload_Started_RoundTripsCorrectly()
    {
        var payload = new PhaseLifecyclePayload
        {
            Phase = "Executor",
            Status = "started",
            Context = new Dictionary<string, object>
            {
                ["runId"] = "run-001"
            },
            Artifacts = null,
            Error = null
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<PhaseLifecyclePayload>(json, _jsonOptions);

        deserialized!.Phase.Should().Be(payload.Phase);
        deserialized.Status.Should().Be(payload.Status);
        deserialized.Artifacts.Should().BeNull();
        deserialized.Error.Should().BeNull();
    }

    [Fact]
    public void PhaseLifecyclePayload_Completed_RoundTripsCorrectly()
    {
        var payload = new PhaseLifecyclePayload
        {
            Phase = "Planner",
            Status = "completed",
            Context = new Dictionary<string, object>
            {
                ["inputTokens"] = 150
            },
            Artifacts = new List<PhaseArtifact>
            {
                new()
                {
                    Type = "plan",
                    Name = "execution-plan.json",
                    Reference = "/plans/exec-001.json",
                    Summary = "Contains 5 execution steps"
                }
            },
            Error = null
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<PhaseLifecyclePayload>(json, _jsonOptions);

        deserialized!.Phase.Should().Be(payload.Phase);
        deserialized.Status.Should().Be(payload.Status);
        deserialized.Artifacts.Should().HaveCount(1);
        deserialized.Artifacts![0].Name.Should().Be("execution-plan.json");
    }

    [Fact]
    public void PhaseLifecyclePayload_WithError_RoundTripsCorrectly()
    {
        var payload = new PhaseLifecyclePayload
        {
            Phase = "Executor",
            Status = "completed",
            Context = null,
            Artifacts = null,
            Error = new PhaseError
            {
                Code = "EXECUTION_FAILED",
                Message = "Command returned non-zero exit code",
                Details = "Exit code: 1"
            }
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<PhaseLifecyclePayload>(json, _jsonOptions);

        deserialized!.Phase.Should().Be(payload.Phase);
        deserialized.Status.Should().Be(payload.Status);
        deserialized.Error.Should().NotBeNull();
        deserialized.Error!.Code.Should().Be("EXECUTION_FAILED");
        deserialized.Error.Message.Should().Be("Command returned non-zero exit code");
        deserialized.Error.Details.Should().Be("Exit code: 1");
    }

    [Fact]
    public void AssistantDeltaPayload_RoundTripsCorrectly()
    {
        var payload = new AssistantDeltaPayload
        {
            MessageId = "msg-789",
            Content = "Hello",
            Index = 0
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AssistantDeltaPayload>(json, _jsonOptions);

        deserialized.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void AssistantFinalPayload_RoundTripsCorrectly()
    {
        var payload = new AssistantFinalPayload
        {
            MessageId = "msg-789",
            Content = "Hello! How can I help you today?",
            StructuredData = new { type = "text" },
            ContentType = "text/plain",
            IsFinal = true
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AssistantFinalPayload>(json, _jsonOptions);

        deserialized!.MessageId.Should().Be(payload.MessageId);
        deserialized.Content.Should().Be(payload.Content);
        deserialized.ContentType.Should().Be(payload.ContentType);
        deserialized.IsFinal.Should().Be(payload.IsFinal);
    }

    [Fact]
    public void AssistantFinalPayload_WithoutOptionalFields_RoundTripsCorrectly()
    {
        var payload = new AssistantFinalPayload
        {
            MessageId = "msg-abc",
            Content = null,
            StructuredData = null,
            ContentType = null,
            IsFinal = true
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AssistantFinalPayload>(json, _jsonOptions);

        deserialized!.MessageId.Should().Be(payload.MessageId);
        deserialized.Content.Should().BeNull();
        deserialized.ContentType.Should().BeNull();
        deserialized.IsFinal.Should().BeTrue();
    }

    [Fact]
    public void RunLifecyclePayload_Started_RoundTripsCorrectly()
    {
        var payload = new RunLifecyclePayload
        {
            Status = "started",
            RunId = "run-001",
            DurationMs = null,
            Success = null,
            ArtifactReferences = null
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<RunLifecyclePayload>(json, _jsonOptions);

        deserialized.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void RunLifecyclePayload_Finished_RoundTripsCorrectly()
    {
        var payload = new RunLifecyclePayload
        {
            Status = "finished",
            RunId = "run-001",
            DurationMs = 5000,
            Success = true,
            ArtifactReferences = new List<string> { "/outputs/result.txt" }
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<RunLifecyclePayload>(json, _jsonOptions);

        deserialized!.Status.Should().Be(payload.Status);
        deserialized.RunId.Should().Be(payload.RunId);
        deserialized.DurationMs.Should().Be(payload.DurationMs);
        deserialized.Success.Should().Be(payload.Success);
        deserialized.ArtifactReferences.Should().BeEquivalentTo(payload.ArtifactReferences);
    }

    [Fact]
    public void ErrorPayload_RoundTripsCorrectly()
    {
        var payload = new ErrorPayload
        {
            Severity = "error",
            Code = "ORCHESTRATION_FAILED",
            Message = "Failed to execute command",
            Context = "Executor",
            Recoverable = true,
            RetryAction = "retry"
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ErrorPayload>(json, _jsonOptions);

        deserialized.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void ErrorPayload_Warning_RoundTripsCorrectly()
    {
        var payload = new ErrorPayload
        {
            Severity = "warning",
            Code = "SLOW_OPERATION",
            Message = "Operation took longer than expected",
            Context = "Planner",
            Recoverable = false,
            RetryAction = null
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ErrorPayload>(json, _jsonOptions);

        deserialized.Should().BeEquivalentTo(payload);
    }

    [Theory]
    [InlineData(StreamingEventType.IntentClassified)]
    [InlineData(StreamingEventType.GateSelected)]
    [InlineData(StreamingEventType.ToolCall)]
    [InlineData(StreamingEventType.ToolResult)]
    [InlineData(StreamingEventType.PhaseLifecycle)]
    [InlineData(StreamingEventType.AssistantDelta)]
    [InlineData(StreamingEventType.AssistantFinal)]
    [InlineData(StreamingEventType.RunLifecycle)]
    [InlineData(StreamingEventType.Error)]
    public void AllEventTypes_SerializeToInteger(StreamingEventType eventType)
    {
        var envelope = new StreamingEvent { Type = eventType };

        var json = JsonSerializer.Serialize(envelope, _jsonOptions);

        json.Should().Contain($"\"type\":{(int)eventType}");
    }

    [Fact]
    public void ProposedAction_WithParameters_RoundTripsCorrectly()
    {
        var action = new ProposedAction
        {
            Description = "Create file",
            ActionType = "file.create",
            Parameters = new Dictionary<string, object>
            {
                ["path"] = "/test.txt",
                ["content"] = "Hello World"
            }
        };

        var json = JsonSerializer.Serialize(action, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ProposedAction>(json, _jsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Description.Should().Be("Create file");
        deserialized.ActionType.Should().Be("file.create");
    }

    [Fact]
    public void PhaseArtifact_RoundTripsCorrectly()
    {
        var artifact = new PhaseArtifact
        {
            Type = "code",
            Name = "Program.cs",
            Reference = "/src/Program.cs",
            Summary = "Main entry point"
        };

        var json = JsonSerializer.Serialize(artifact, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<PhaseArtifact>(json, _jsonOptions);

        deserialized.Should().BeEquivalentTo(artifact);
    }

    [Fact]
    public void PhaseError_RoundTripsCorrectly()
    {
        var error = new PhaseError
        {
            Code = "COMPILATION_ERROR",
            Message = "Syntax error on line 42",
            Details = "Missing semicolon"
        };

        var json = JsonSerializer.Serialize(error, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<PhaseError>(json, _jsonOptions);

        deserialized.Should().BeEquivalentTo(error);
    }

    [Fact]
    public void StreamingEvent_Create_SetsRequiredFields()
    {
        var payload = new IntentClassifiedPayload
        {
            Category = "Chat",
            Confidence = 1.0
        };

        var envelope = StreamingEvent.Create(
            StreamingEventType.IntentClassified,
            payload,
            correlationId: "test-corr",
            sequenceNumber: 42
        );

        envelope.Id.Should().NotBeNullOrEmpty();
        envelope.Type.Should().Be(StreamingEventType.IntentClassified);
        envelope.Payload.Should().BeSameAs(payload);
        envelope.CorrelationId.Should().Be("test-corr");
        envelope.SequenceNumber.Should().Be(42);
        envelope.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
            return value;

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
