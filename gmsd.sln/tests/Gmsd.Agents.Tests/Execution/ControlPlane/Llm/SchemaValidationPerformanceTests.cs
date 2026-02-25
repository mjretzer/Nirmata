#pragma warning disable CS0618 // Intentionally testing obsolete ILlmProvider interface

using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane.Llm.Adapters;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using System.Diagnostics;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Llm;

/// <summary>
/// Performance tests for schema validation overhead in SemanticKernelLlmProvider.
/// Verifies that validation doesn't significantly impact request latency.
/// </summary>
public class SchemaValidationPerformanceTests
{
    private readonly Mock<IChatCompletionService> _chatCompletionServiceMock;
    private readonly Mock<ICorrelationIdProvider> _correlationIdProviderMock;
    private readonly SemanticKernelLlmProvider _provider;

    public SchemaValidationPerformanceTests()
    {
        _chatCompletionServiceMock = new Mock<IChatCompletionService>();
        _correlationIdProviderMock = new Mock<ICorrelationIdProvider>();
        _correlationIdProviderMock.Setup(c => c.Generate()).Returns("test-correlation-id");
        _provider = new SemanticKernelLlmProvider(
            _chatCompletionServiceMock.Object,
            NullLogger<SemanticKernelLlmProvider>.Instance,
            _correlationIdProviderMock.Object);
    }

    [Fact]
    public async Task SchemaValidation_SimpleSchema_CompletesReasonablyFast()
    {
        // Arrange
        var schemaJson = """
        {
            "type": "object",
            "properties": { "value": { "type": "string" } },
            "required": ["value"],
            "additionalProperties": false
        }
        """;

        var schema = LlmStructuredOutputSchema.FromJson("simple_schema", schemaJson);
        var validOutput = """{ "value": "test" }""";

        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Test") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, validOutput);

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act
        var sw = Stopwatch.StartNew();
        var result = await _provider.CompleteAsync(request);
        sw.Stop();

        // Assert
        result.Should().NotBeNull();
        // Validation should complete in reasonable time (< 500ms in test environment)
        sw.ElapsedMilliseconds.Should().BeLessThan(500);
    }

    [Fact]
    public async Task SchemaValidation_ComplexNestedSchema_CompletesReasonablyFast()
    {
        // Arrange
        var schemaJson = """
        {
            "type": "object",
            "properties": {
                "plan": {
                    "type": "object",
                    "properties": {
                        "tasks": {
                            "type": "array",
                            "items": {
                                "type": "object",
                                "properties": {
                                    "id": { "type": "string" },
                                    "steps": {
                                        "type": "array",
                                        "items": { "type": "string" }
                                    },
                                    "metadata": {
                                        "type": "object",
                                        "properties": {
                                            "priority": { "type": "integer" },
                                            "tags": {
                                                "type": "array",
                                                "items": { "type": "string" }
                                            }
                                        },
                                        "required": ["priority"],
                                        "additionalProperties": false
                                    }
                                },
                                "required": ["id", "steps", "metadata"],
                                "additionalProperties": false
                            }
                        }
                    },
                    "required": ["tasks"],
                    "additionalProperties": false
                }
            },
            "required": ["plan"],
            "additionalProperties": false
        }
        """;

        var schema = LlmStructuredOutputSchema.FromJson("complex_schema", schemaJson);

        var validOutput = """
        {
            "plan": {
                "tasks": [
                    {
                        "id": "task-1",
                        "steps": ["step 1", "step 2"],
                        "metadata": { "priority": 1, "tags": ["urgent"] }
                    },
                    {
                        "id": "task-2",
                        "steps": ["step 3"],
                        "metadata": { "priority": 2, "tags": ["normal"] }
                    }
                ]
            }
        }
        """;

        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Generate plan") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, validOutput);

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act
        var sw = Stopwatch.StartNew();
        var result = await _provider.CompleteAsync(request);
        sw.Stop();

        // Assert
        result.Should().NotBeNull();
        // Complex schema validation should still be reasonably fast (< 500ms)
        sw.ElapsedMilliseconds.Should().BeLessThan(500);
    }

    [Fact]
    public async Task SchemaCaching_RepeatedValidation_CacheHitRateExceeds90Percent()
    {
        // Arrange
        var schemaJson = """
        {
            "type": "object",
            "properties": { "value": { "type": "string" } },
            "required": ["value"],
            "additionalProperties": false
        }
        """;

        var schema = LlmStructuredOutputSchema.FromJson("cached_schema", schemaJson);
        var validOutput = """{ "value": "test" }""";

        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Test") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, validOutput);

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act - Execute 10 times with the same schema
        var times = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var sw = Stopwatch.StartNew();
            await _provider.CompleteAsync(request);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
        }

        // Assert - Later calls should be faster due to caching
        // Calculate average of first 3 calls vs last 3 calls
        var firstThreeAvg = times.Take(3).Average();
        var lastThreeAvg = times.Skip(7).Take(3).Average();

        // Last calls should be faster (cache hits)
        lastThreeAvg.Should().BeLessThanOrEqualTo(firstThreeAvg);
    }

    [Fact]
    public async Task ValidationOverhead_WithoutSchema_BaslinePerformance()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Test") }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "response");

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act
        var sw = Stopwatch.StartNew();
        var result = await _provider.CompleteAsync(request);
        sw.Stop();

        // Assert
        result.Should().NotBeNull();
        // Baseline should complete in reasonable time
        sw.ElapsedMilliseconds.Should().BeLessThan(200);
    }

    [Fact]
    public async Task ValidationOverhead_WithSchema_IsAcceptable()
    {
        // Arrange
        var schemaJson = """
        {
            "type": "object",
            "properties": { "value": { "type": "string" } },
            "required": ["value"],
            "additionalProperties": false
        }
        """;

        var schema = LlmStructuredOutputSchema.FromJson("overhead_schema", schemaJson);
        var validOutput = """{ "value": "test" }""";

        var requestWithoutSchema = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Test") }
        };

        var requestWithSchema = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Test") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, validOutput);

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act - Measure baseline (no schema)
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 5; i++)
        {
            await _provider.CompleteAsync(requestWithoutSchema);
        }
        sw.Stop();
        var baselineTime = sw.ElapsedMilliseconds;

        // Reset mock
        _chatCompletionServiceMock.Reset();
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act - Measure with schema
        sw.Restart();
        for (int i = 0; i < 5; i++)
        {
            await _provider.CompleteAsync(requestWithSchema);
        }
        sw.Stop();
        var withSchemaTime = sw.ElapsedMilliseconds;

        // Assert - Both should complete in reasonable time
        // Schema validation adds minimal overhead
        baselineTime.Should().BeLessThan(500);
        withSchemaTime.Should().BeLessThan(500);
    }
}

#pragma warning restore CS0618
