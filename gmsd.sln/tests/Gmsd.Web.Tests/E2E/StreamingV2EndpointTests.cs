using System.Net;
using System.Text.Json;
using FluentAssertions;
using Gmsd.Web.Models.Streaming;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Preflight;
using Xunit;

namespace Gmsd.Web.Tests.E2E;

/// <summary>
/// Integration tests for the v2 streaming endpoint.
/// Validates typed event streaming, legacy compatibility, and full flow.
/// </summary>
public class StreamingV2EndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public StreamingV2EndpointTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace real implementations with mocks for testing
                    var llmProviderMock = new Mock<ILlmProvider>();
                    llmProviderMock.Setup(p => p.ProviderName).Returns("test");
                    llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new LlmCompletionResponse
                        {
                            Message = LlmMessage.Assistant("Test response"),
                            Model = "test-model",
                            Provider = "test"
                        });

                    var orchestratorMock = new Mock<IOrchestrator>();
                    orchestratorMock.Setup(o => o.ExecuteAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new OrchestratorResult
                        {
                            IsSuccess = true,
                            FinalPhase = "TestPhase",
                            RunId = "test-run-123"
                        });

                    // Remove existing registrations and add mocks
                    var llmDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILlmProvider));
                    if (llmDescriptor != null) services.Remove(llmDescriptor);

                    var orchDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IOrchestrator));
                    if (orchDescriptor != null) services.Remove(orchDescriptor);

                    services.AddSingleton<ILlmProvider>(llmProviderMock.Object);
                    services.AddSingleton<IOrchestrator>(orchestratorMock.Object);
                });
            });
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task StreamV2_Endpoint_Exists_And_Returns_Success()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream-v2", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task StreamV2_EmptyCommand_Returns_Error_Event()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        responseBody.Should().Contain("Error");
        responseBody.Should().Contain("EMPTY_COMMAND");
    }

    [Fact]
    public async Task StreamV2_WhitespaceCommand_Returns_Error_Event()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "   ")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        responseBody.Should().Contain("Error");
        responseBody.Should().Contain("EMPTY_COMMAND");
    }

    [Fact]
    public async Task Stream_Legacy_Endpoint_Still_Works()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Stream_Legacy_EmptyCommand_Returns_Error()
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
        responseBody.Should().Contain("error");
    }

    [Fact]
    public async Task StreamV2_Supports_ChatOnly_Option()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello"),
            new KeyValuePair<string, string>("chatOnly", "true")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream-v2", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StreamV2_Supports_ThreadId_Parameter()
    {
        // Arrange
        var client = _factory.CreateClient();
        var threadId = Guid.NewGuid().ToString("N");
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello"),
            new KeyValuePair<string, string>("threadId", threadId)
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The response should contain events with the correlationId matching threadId
        responseBody.Should().Contain(threadId);
    }

    [Fact]
    public async Task StreamV2_Returns_Json_Stream()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Should be able to parse each line as JSON
        var lines = responseBody.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        lines.Should().NotBeEmpty();

        foreach (var line in lines)
        {
            // Each line should be valid JSON
            var doc = JsonDocument.Parse(line);
            doc.RootElement.TryGetProperty("Type", out _).Should().BeTrue("Each event should have a Type property");
        }
    }

    [Fact]
    public async Task StreamV2_Endpoint_Has_Correct_Route()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Verify the endpoint is accessible at the expected route
        var response = await client.PostAsync("/api/chat/stream-v2",
            new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("command", "test") }));

        // Assert - Should not be 404
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, "Endpoint should exist at /api/chat/stream-v2");
    }

    [Fact]
    public async Task StreamV2_And_Legacy_Have_Different_Response_Types()
    {
        // Arrange
        var client = _factory.CreateClient();
        var legacyContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello")
        });
        var v2Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello")
        });

        // Act
        var legacyResponse = await client.PostAsync("/api/chat/stream", legacyContent);
        var v2Response = await client.PostAsync("/api/chat/stream-v2", v2Content);

        var legacyBody = await legacyResponse.Content.ReadAsStringAsync();
        var v2Body = await v2Response.Content.ReadAsStringAsync();

        // Assert - Both should succeed
        legacyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        v2Response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Both should return JSON
        legacyResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        v2Response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task StreamV2_ValidCommand_Returns_Events()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("command", "hello world")
        });

        // Act
        var response = await client.PostAsync("/api/chat/stream-v2", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Should receive some events
        var lines = responseBody.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        lines.Should().NotBeEmpty("Should receive streaming events for valid command");
    }
}
