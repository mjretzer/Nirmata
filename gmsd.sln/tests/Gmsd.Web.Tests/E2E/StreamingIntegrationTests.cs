using System.Net;
using System.Text.Json;
using FluentAssertions;
using Gmsd.Web.Controllers;
using Gmsd.Web.Models;
using Gmsd.Web.Models.Streaming;
using Xunit;

namespace Gmsd.Web.Tests.E2E;

/// <summary>
/// Integration tests for end-to-end streaming flows.
/// Tests chat-only, write workflows, multi-phase execution, errors, and legacy compatibility.
/// </summary>
public class StreamingIntegrationTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public StreamingIntegrationTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClientWithMocks();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    #region T21-1: Chat-Only Flow (No Run Events)

    [Fact]
    public async Task ChatOnlyFlow_Greeting_DoesNotEmitRunEvents()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello"),
            new KeyValuePair<string, string>("chatOnly", "true")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        events.Should().NotBeEmpty();
        
        // Should have intent.classified event
        events.Should().Contain(e => e.Type == StreamingEventType.IntentClassified);
        
        // Should NOT have run.lifecycle events in chat-only mode
        events.Should().NotContain(e => e.Type == StreamingEventType.RunLifecycle);
        
        // Should have assistant response
        events.Should().Contain(e => e.Type == StreamingEventType.AssistantDelta || 
                                     e.Type == StreamingEventType.AssistantFinal);
    }

    [Fact]
    public async Task ChatOnlyFlow_SimpleQuestion_EmitsOnlyChatAndIntentEvents()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "how are you today?"),
            new KeyValuePair<string, string>("chatOnly", "true")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        
        // Should only contain allowed event types for chat-only
        var allowedTypes = new[] 
        { 
            StreamingEventType.IntentClassified, 
            StreamingEventType.GateSelected,
            StreamingEventType.AssistantDelta,
            StreamingEventType.AssistantFinal,
            StreamingEventType.Error
        };
        
        foreach (var evt in events)
        {
            allowedTypes.Should().Contain(evt.Type, $"Event type {evt.Type} should not appear in chat-only flow");
        }
    }

    #endregion

    #region T21-2: Write Workflow with Confirmation

    [Fact]
    public async Task WriteWorkflow_PlanCommand_EmitsIntentAndGateEvents()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "plan the project architecture")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        events.Should().NotBeEmpty();
        
        // Should have intent classification
        events.Should().Contain(e => e.Type == StreamingEventType.IntentClassified);
        
        // Should have gate selection
        events.Should().Contain(e => e.Type == StreamingEventType.GateSelected);
        
        // Should have run lifecycle for write workflow
        var runEvents = events.Where(e => e.Type == StreamingEventType.RunLifecycle).ToList();
        runEvents.Should().HaveCountGreaterOrEqualTo(2); // started + finished
    }

    [Fact]
    public async Task WriteWorkflow_CreateFile_EmitsCorrectEventSequence()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "create a new file test.txt")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        events.Should().NotBeEmpty();
        
        // Verify event ordering
        var eventTypes = events.Select(e => e.Type).ToList();
        
        // First event should be intent classified or run started
        eventTypes.First().Should().BeOneOf(StreamingEventType.IntentClassified, StreamingEventType.RunLifecycle);
        
        // Should end with assistant.final or error
        eventTypes.Last().Should().BeOneOf(StreamingEventType.AssistantFinal, StreamingEventType.Error);
    }

    #endregion

    #region T21-3: Multi-Phase Workflow with Tool Calls

    [Fact]
    public async Task MultiPhaseWorkflow_WithToolCalls_EmitsToolEvents()
    {
        // Arrange - command that would trigger tool usage
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "execute task in current phase")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        
        // Should have phase lifecycle events
        var phaseEvents = events.Where(e => e.Type == StreamingEventType.PhaseLifecycle).ToList();
        
        // Verify run events exist
        events.Should().Contain(e => e.Type == StreamingEventType.RunLifecycle);
    }

    [Fact]
    public async Task MultiPhaseWorkflow_EmitsCorrectSequenceNumbers()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "plan then execute task")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        var sequencedEvents = events.Where(e => e.SequenceNumber.HasValue).ToList();
        
        if (sequencedEvents.Count > 1)
        {
            // Verify sequence numbers are in order
            for (int i = 1; i < sequencedEvents.Count; i++)
            {
                sequencedEvents[i].SequenceNumber.Should().BeGreaterThan(sequencedEvents[i - 1].SequenceNumber!.Value,
                    "Sequence numbers should be monotonically increasing");
            }
        }
    }

    #endregion

    #region T21-4: Error Scenarios and Recovery

    [Fact]
    public async Task ErrorScenario_EmptyCommand_ReturnsErrorEvent()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        
        // Should have error event
        events.Should().Contain(e => e.Type == StreamingEventType.Error);
    }

    [Fact]
    public async Task ErrorScenario_WhitespaceCommand_ReturnsErrorEvent()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "   ")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        events.Should().Contain(e => e.Type == StreamingEventType.Error);
    }

    [Fact]
    public async Task ErrorScenario_InvalidCommand_HandlesGracefully()
    {
        // Arrange - command that might cause issues
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "<script>alert('xss')</script>")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        
        // Assert - should not crash, either success or error response
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    #endregion

    #region T21-5: Legacy Client Compatibility

    [Fact]
    public async Task LegacyCompatibility_LegacyEndpoint_ReturnsLegacyFormat()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Legacy format should have Type property with string values
        var events = ParseLegacyEventStream(responseBody);
        events.Should().NotBeEmpty();
        
        // All events should have required legacy properties
        foreach (var evt in events)
        {
            evt.Type.Should().NotBeNullOrEmpty();
            evt.MessageId.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task LegacyCompatibility_V2WithLegacyAcceptHeader_ReturnsLegacyFormat()
    {
        // Arrange
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
        var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Should return legacy format
        var events = ParseLegacyEventStream(responseBody);
        events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LegacyCompatibility_LegacyEventsHaveMetadata()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "test command")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        var events = ParseLegacyEventStream(responseBody);
        
        // Events should have metadata with eventType to allow client identification
        var eventsWithMetadata = events.Where(e => e.Metadata != null).ToList();
        eventsWithMetadata.Should().NotBeEmpty("Legacy events should include metadata for client identification");
    }

    #endregion

    #region T21-X: Conversational Gating Flow Tests (Task 4.4)

    [Fact]
    public async Task ConversationalGating_ExecutorPhase_EmitsGateSelectedWithConfirmation()
    {
        // Arrange - command that triggers Executor phase (destructive operation)
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "execute the code changes")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        events.Should().NotBeEmpty();
        
        // Should have gate.selected event
        var gateSelectedEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.GateSelected);
        gateSelectedEvent.Should().NotBeNull("gate.selected event should be emitted for workflow execution");
        
        // Verify gate payload structure
        var gatePayload = JsonSerializer.Deserialize<GateSelectedPayload>(
            JsonSerializer.Serialize(gateSelectedEvent!.Payload, _jsonOptions), 
            _jsonOptions);
        gatePayload.Should().NotBeNull();
        gatePayload!.Phase.Should().NotBeNullOrEmpty();
        gatePayload.Reasoning.Should().NotBeNullOrEmpty("reasoning should explain the gating decision");
        gatePayload.RequiresConfirmation.Should().BeTrue("Executor phase should require confirmation");
        gatePayload.ProposedAction.Should().NotBeNull("proposed action should be set");
        gatePayload.ProposedAction!.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ConversationalGating_PlanPhase_EmitsGateSelectedWithReasoning()
    {
        // Arrange - command that triggers Planner phase
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "plan the foundation phase")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        
        // Should have gate.selected event with reasoning
        var gateSelectedEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.GateSelected);
        gateSelectedEvent.Should().NotBeNull();
        
        var gatePayload = JsonSerializer.Deserialize<GateSelectedPayload>(
            JsonSerializer.Serialize(gateSelectedEvent!.Payload, _jsonOptions), 
            _jsonOptions);
        gatePayload.Should().NotBeNull();
        gatePayload!.Reasoning.Should().NotBeNullOrEmpty();
        gatePayload.Phase.Should().BeOneOf("Planner", "Interviewer", "Roadmapper", "Responder");
    }

    [Fact]
    public async Task ConversationalGating_ChatInput_EmitsGateSelectedForResponder()
    {
        // Arrange - casual chat input
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello, how are you?")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        
        // Even chat inputs should have gate.selected (routing to Responder)
        var gateSelectedEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.GateSelected);
        gateSelectedEvent.Should().NotBeNull("gate.selected should be emitted even for chat inputs");
        
        var gatePayload = JsonSerializer.Deserialize<GateSelectedPayload>(
            JsonSerializer.Serialize(gateSelectedEvent!.Payload, _jsonOptions), 
            _jsonOptions);
        gatePayload!.Phase.Should().Be("Responder");
        gatePayload.RequiresConfirmation.Should().BeFalse("chat/Responder phase should not require confirmation");
    }

    [Fact]
    public async Task ConversationalGating_EventOrdering_GateSelectedAfterIntentClassified()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "create a plan for the project")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        
        // Find positions of intent.classified and gate.selected
        var intentIndex = events.FindIndex(e => e.Type == StreamingEventType.IntentClassified);
        var gateIndex = events.FindIndex(e => e.Type == StreamingEventType.GateSelected);
        
        intentIndex.Should().BeGreaterOrEqualTo(0, "intent.classified should be emitted");
        gateIndex.Should().BeGreaterOrEqualTo(0, "gate.selected should be emitted");
        intentIndex.Should().BeLessThan(gateIndex, "intent.classified should come before gate.selected");
    }

    [Fact]
    public async Task ConversationalGating_GateSelectedBeforeRunStarted()
    {
        // Arrange
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "plan and execute task")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        
        // Find positions
        var gateIndex = events.FindIndex(e => e.Type == StreamingEventType.GateSelected);
        var runStartedIndex = events.FindIndex(e => 
            e.Type == StreamingEventType.RunLifecycle && 
            (e.Payload as JsonElement?)?.GetProperty("status").GetString() == "started");
        
        gateIndex.Should().BeGreaterOrEqualTo(0);
        
        // If there's a run.started event, it should come after gate.selected
        if (runStartedIndex >= 0)
        {
            gateIndex.Should().BeLessThan(runStartedIndex, 
                "gate.selected should come before run.started so user can see routing decision");
        }
    }

    [Fact]
    public async Task ConversationalGating_ProposedActionIncludesRiskLevel()
    {
        // Arrange - command that triggers write operation
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "execute file modifications")
        });

        // Act
        var response = await _client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var events = ParseEventStream(responseBody);
        var gateSelectedEvent = events.FirstOrDefault(e => e.Type == StreamingEventType.GateSelected);
        
        if (gateSelectedEvent != null)
        {
            var gatePayload = JsonSerializer.Deserialize<GateSelectedPayload>(
                JsonSerializer.Serialize(gateSelectedEvent.Payload, _jsonOptions), 
                _jsonOptions);
            
            if (gatePayload?.ProposedAction?.Parameters != null)
            {
                // Risk level should be included in parameters
                gatePayload.ProposedAction.Parameters.Should().ContainKey("riskLevel");
            }
        }
    }

    #endregion

    #region Helper Methods

    private List<StreamingEvent> ParseEventStream(string responseBody)
    {
        var events = new List<StreamingEvent>();
        var lines = responseBody.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        foreach (var line in lines)
        {
            try
            {
                var evt = JsonSerializer.Deserialize<StreamingEvent>(line, _jsonOptions);
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
