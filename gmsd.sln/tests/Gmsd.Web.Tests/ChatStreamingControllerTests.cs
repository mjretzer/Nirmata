using System.Net;
using System.Text.Json;
using FluentAssertions;
using Gmsd.Web.Controllers;
using Gmsd.Web.Models;
using Gmsd.Web.Models.Streaming;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Preflight;
using Xunit;

namespace Gmsd.Web.Tests;

/// <summary>
/// Integration tests for legacy streaming endpoint backward compatibility.
/// Validates that legacy endpoints continue to function and return legacy StreamingChatEvent format.
/// </summary>
public class ChatStreamingControllerTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public ChatStreamingControllerTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Mock ILlmProvider
                    var llmProviderMock = new Mock<ILlmProvider>();
                    llmProviderMock.Setup(p => p.ProviderName).Returns("test");
                    llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new LlmCompletionResponse
                        {
                            Message = LlmMessage.Assistant("Test LLM response"),
                            Model = "test-model",
                            Provider = "test"
                        });

                    // Mock IOrchestrator with proper artifacts for streaming events
                    var orchestratorMock = new Mock<IOrchestrator>();
                    orchestratorMock.Setup(o => o.ExecuteAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((WorkflowIntent intent, CancellationToken ct) => new OrchestratorResult
                        {
                            IsSuccess = true,
                            FinalPhase = "Responder",
                            RunId = "test-run-123",
                            Artifacts = new Dictionary<string, object>
                            {
                                ["response"] = $"Hello! I received your message: '{intent.InputRaw}'",
                                ["reason"] = "Input classified as chat intent"
                            }
                        });

                    // Mock IGatingEngine for streaming orchestrator
                    var gatingEngineMock = new Mock<IGatingEngine>();
                    gatingEngineMock.Setup(g => g.EvaluateAsync(It.IsAny<GatingContext>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((GatingContext ctx, CancellationToken ct) => new GatingResult
                        {
                            TargetPhase = "Responder",
                            Reason = "Chat-classified input",
                            Reasoning = "Routing to chat responder for conversational response",
                            RequiresConfirmation = false,
                            ProposedAction = null
                        });

                    // Remove existing registrations
                    var llmDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILlmProvider));
                    if (llmDescriptor != null) services.Remove(llmDescriptor);

                    var orchDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IOrchestrator));
                    if (orchDescriptor != null) services.Remove(orchDescriptor);

                    var gatingDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IGatingEngine));
                    if (gatingDescriptor != null) services.Remove(gatingDescriptor);

                    services.AddSingleton<ILlmProvider>(llmProviderMock.Object);
                    services.AddSingleton<IOrchestrator>(orchestratorMock.Object);
                    services.AddSingleton<IGatingEngine>(gatingEngineMock.Object);
                });
            });

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    #region Task 4.3: Legacy Endpoint Backward Compatibility

    /// <summary>
    /// Validates that the legacy endpoint returns StreamingChatEvent format
    /// with required properties (Type, MessageId, Content, Timestamp).
    /// </summary>
    [Fact]
    public async Task LegacyEndpoint_Returns_StreamingChatEvent_Format()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var events = ParseLegacyEventStream(responseBody);
        events.Should().NotBeEmpty("legacy endpoint should return events");

        // Verify each event has required StreamingChatEvent properties
        foreach (var evt in events)
        {
            evt.Type.Should().NotBeNullOrEmpty("each event should have a Type");
            evt.MessageId.Should().NotBeNullOrEmpty("each event should have a MessageId");
            evt.Timestamp.Should().NotBe(default, "each event should have a valid Timestamp");
        }
    }

    /// <summary>
    /// Validates that the legacy endpoint starts with a message_start event type.
    /// </summary>
    [Fact]
    public async Task LegacyEndpoint_Starts_With_MessageStart()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "test command")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        var events = ParseLegacyEventStream(responseBody);
        events.Should().NotBeEmpty();

        // First event should be message_start
        var firstEvent = events.First();
        firstEvent.Type.Should().Be("message_start", "first event should be message_start");
        firstEvent.MessageId.Should().NotBeNullOrEmpty();
        firstEvent.Content.Should().BeEmpty("message_start should have empty content");
    }

    /// <summary>
    /// Validates that the legacy endpoint transforms events correctly:
    /// - intent.classified → thinking
    /// - gate.selected → thinking
    /// - assistant.delta → content_chunk
    /// - assistant.final → message_complete
    /// </summary>
    [Fact]
    public async Task LegacyEndpoint_Transforms_Events_Correctly()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "plan the project")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        var events = ParseLegacyEventStream(responseBody);
        events.Should().NotBeEmpty();

        // Legacy format should have transformed event types
        var eventTypes = events.Select(e => e.Type).ToList();

        // Should have message_start
        eventTypes.Should().Contain("message_start");

        // Should have thinking events (transformed from intent.classified, gate.selected, etc.)
        eventTypes.Should().Contain("thinking", "reasoning events should be transformed to thinking");

        // Should have content_chunk events (transformed from assistant.delta)
        eventTypes.Should().Contain("content_chunk", "assistant delta events should be transformed to content_chunk");

        // Should end with message_complete (transformed from assistant.final)
        eventTypes.Last().Should().Be("message_complete", "final event should be message_complete");
    }

    /// <summary>
    /// Validates that new typed events (intent.classified, gate.selected, etc.) are NOT
    /// directly emitted to legacy clients - they should be transformed.
    /// </summary>
    [Fact]
    public async Task LegacyEndpoint_DoesNot_Emit_NewTyped_Events()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "create a test plan")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        var events = ParseLegacyEventStream(responseBody);

        // Verify no new typed event types are present
        var disallowedTypes = new[]
        {
            "intent.classified",
            "gate.selected",
            "assistant.delta",
            "assistant.final",
            "tool.call",
            "tool.result",
            "phase.started",
            "phase.completed",
            "run.started",
            "run.finished"
        };

        foreach (var evt in events)
        {
            disallowedTypes.Should().NotContain(evt.Type,
                $"event type '{evt.Type}' should not appear in legacy format - it should be transformed");
        }
    }

    /// <summary>
    /// Validates that legacy events include metadata with the original event type
    /// for debugging and client identification purposes.
    /// </summary>
    [Fact]
    public async Task LegacyEndpoint_Events_Include_OriginalEventType_In_Metadata()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "execute task")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        var events = ParseLegacyEventStream(responseBody);

        // Events should have metadata with original eventType
        var eventsWithMetadata = events.Where(e => e.Metadata != null && e.Metadata.ContainsKey("eventType")).ToList();
        eventsWithMetadata.Should().NotBeEmpty("legacy events should include original eventType in metadata");

        // Verify metadata contains expected original event types
        var originalEventTypes = eventsWithMetadata
            .Select(e => e.Metadata!["eventType"]?.ToString())
            .Where(t => t != null)
            .ToList();

        originalEventTypes.Should().Contain("intent.classified");
        originalEventTypes.Should().Contain("gate.selected");
    }

    /// <summary>
    /// Validates that the v2 endpoint with legacy Accept header returns legacy format.
    /// </summary>
    [Fact]
    public async Task StreamV2_With_LegacyAcceptHeader_Returns_LegacyFormat()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello")
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream-v2")
        {
            Content = content,
            Headers = { { "Accept", "application/vnd.gmsd.legacy+json" } }
        };

        // Act
        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Should return legacy format (StreamingChatEvent)
        var events = ParseLegacyEventStream(responseBody);
        events.Should().NotBeEmpty();

        // Verify legacy format
        foreach (var evt in events)
        {
            evt.Type.Should().NotBeNullOrEmpty();
            evt.MessageId.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>
    /// Validates that content chunks are properly accumulated and delivered.
    /// </summary>
    [Fact]
    public async Task LegacyEndpoint_Content_Chunks_Are_Accumulated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "tell me a story")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        var events = ParseLegacyEventStream(responseBody);

        // Get content chunks
        var contentChunks = events.Where(e => e.Type == "content_chunk").ToList();

        // Content chunks should have content
        foreach (var chunk in contentChunks)
        {
            chunk.Content.Should().NotBeNull();
        }

        // Final message_complete should have IsFinal=true
        var finalEvent = events.LastOrDefault(e => e.Type == "message_complete");
        finalEvent.Should().NotBeNull("should have message_complete event");
        finalEvent!.IsFinal.Should().BeTrue("message_complete should have IsFinal=true");
    }

    /// <summary>
    /// Validates that the legacy endpoint handles empty commands correctly.
    /// </summary>
    [Fact]
    public async Task LegacyEndpoint_EmptyCommand_Returns_Error()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = ParseLegacyEventStream(responseBody);
        events.Should().NotBeEmpty();

        // Should have error event
        events.Should().Contain(e => e.Type == "error", "empty command should return error event");
    }

    /// <summary>
    /// Validates that thinking events include reasoning content.
    /// </summary>
    [Fact]
    public async Task LegacyEndpoint_Thinking_Events_Include_Reasoning()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "plan the architecture")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        var events = ParseLegacyEventStream(responseBody);

        // Thinking events should have content
        var thinkingEvents = events.Where(e => e.Type == "thinking").ToList();

        foreach (var thinking in thinkingEvents)
        {
            thinking.Content.Should().NotBeNull();
            // Should contain some reasoning text
            thinking.Content.Should().ContainAny(
                "Intent classified",
                "Phase selected",
                "Calling tool",
                "Tool execution",
                "Phase",
                "Run"
            );
        }
    }

    /// <summary>
    /// Validates that the legacy endpoint returns events with consistent correlation ID in metadata.
    /// </summary>
    [Fact]
    public async Task LegacyEndpoint_Events_Have_Consistent_CorrelationId()
    {
        // Arrange
        var client = _factory.CreateClient();
        var threadId = Guid.NewGuid().ToString("N");
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "test message"),
            new KeyValuePair<string, string>("threadId", threadId)
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        var events = ParseLegacyEventStream(responseBody);
        events.Should().NotBeEmpty();

        // All events with metadata should have the same correlation ID
        var eventsWithCorrelation = events
            .Where(e => e.Metadata != null && e.Metadata.ContainsKey("correlationId"))
            .ToList();

        eventsWithCorrelation.Should().NotBeEmpty();

        var correlationIds = eventsWithCorrelation
            .Select(e => e.Metadata!["correlationId"]?.ToString())
            .Where(id => id != null)
            .Distinct()
            .ToList();

        correlationIds.Should().ContainSingle("all events should share the same correlation ID");
    }

    #endregion

    #region Helper Methods

    private List<StreamingChatEvent> ParseLegacyEventStream(string responseBody)
    {
        var events = new List<StreamingChatEvent>();
        var lines = responseBody.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        foreach (var line in lines)
        {
            try
            {
                var evt = JsonSerializer.Deserialize<StreamingChatEvent>(line, _jsonOptions);
                if (evt != null)
                {
                    events.Add(evt);
                }
            }
            catch
            {
                // Skip lines that can't be parsed
            }
        }

        return events;
    }

    #endregion
}
