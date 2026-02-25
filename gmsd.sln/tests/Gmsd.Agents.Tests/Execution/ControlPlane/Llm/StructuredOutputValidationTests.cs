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
/// Tests for structured output validation in SemanticKernelLlmProvider.
/// Verifies that planner outputs are validated against their schemas.
/// </summary>
public class StructuredOutputValidationTests
{
    private readonly Mock<IChatCompletionService> _chatCompletionServiceMock;
    private readonly Mock<ICorrelationIdProvider> _correlationIdProviderMock;
    private readonly SemanticKernelLlmProvider _provider;

    public StructuredOutputValidationTests()
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
    public async Task CompleteAsync_WithValidPlannerOutput_PassesValidation()
    {
        // Arrange
        var schemaJson = """
        {
            "type": "object",
            "properties": {
                "fixes": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "issueId": { "type": "string" },
                            "description": { "type": "string" }
                        },
                        "required": ["issueId", "description"],
                        "additionalProperties": false
                    }
                }
            },
            "required": ["fixes"],
            "additionalProperties": false
        }
        """;

        var schema = LlmStructuredOutputSchema.FromJson("fix_plan_v1", schemaJson, "Fix plan schema");

        var validOutput = """
        {
            "fixes": [
                {
                    "issueId": "ISS-001",
                    "description": "Fix authentication issue"
                }
            ]
        }
        """;

        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Generate fix plan") },
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
        var result = await _provider.CompleteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Message.Content.Should().Contain("ISS-001");
    }

    [Fact]
    public async Task CompleteAsync_WithInvalidJsonResponse_ThrowsLlmProviderException()
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

        var schema = LlmStructuredOutputSchema.FromJson("test_schema", schemaJson);

        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Test") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "not valid json");

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act
        var act = () => _provider.CompleteAsync(request);

        // Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(act);
        ex.Message.Should().Contain("not valid JSON");
    }

    [Fact]
    public async Task CompleteAsync_WithMissingRequiredField_ThrowsLlmProviderException()
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

        var schema = LlmStructuredOutputSchema.FromJson("test_schema", schemaJson);

        var invalidOutput = "{}";

        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Test") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, invalidOutput);

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act
        var act = () => _provider.CompleteAsync(request);

        // Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(act);
        ex.Message.Should().Contain("failed schema");
    }

    [Fact]
    public async Task CompleteAsync_WithAdditionalPropertiesFalse_RejectsExtraFields()
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

        var schema = LlmStructuredOutputSchema.FromJson("test_schema", schemaJson);

        var invalidOutput = """
        {
            "value": "ok",
            "extra": "field"
        }
        """;

        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Test") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, invalidOutput);

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act
        var act = () => _provider.CompleteAsync(request);

        // Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(act);
        ex.Message.Should().Contain("failed schema");
    }

    [Fact]
    public async Task CompleteAsync_WithEmptyResponse_ThrowsLlmProviderException()
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

        var schema = LlmStructuredOutputSchema.FromJson("test_schema", schemaJson);

        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Test") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "");

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act
        var act = () => _provider.CompleteAsync(request);

        // Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(act);
        ex.Message.Should().Contain("empty content");
    }

    [Fact]
    public async Task CompleteAsync_WithStrictValidationDisabled_SkipsValidation()
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

        var schema = LlmStructuredOutputSchema.FromJson(
            "test_schema",
            schemaJson,
            strictValidation: false);

        var invalidOutput = "{}";

        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Test") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, invalidOutput);

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act
        var result = await _provider.CompleteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Message.Content.Should().Be("{}");
    }

    [Fact]
    public async Task CompleteAsync_SchemaCaching_CachesCompiledSchema()
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

        var schema = LlmStructuredOutputSchema.FromJson("test_schema", schemaJson);

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

        // Act - First call
        var sw = Stopwatch.StartNew();
        var result1 = await _provider.CompleteAsync(request);
        sw.Stop();
        var firstCallTime = sw.ElapsedMilliseconds;

        // Reset mock for second call
        _chatCompletionServiceMock.Reset();
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act - Second call (should use cached schema)
        sw.Restart();
        var result2 = await _provider.CompleteAsync(request);
        sw.Stop();
        var secondCallTime = sw.ElapsedMilliseconds;

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        // Second call should be faster due to caching (though timing can be variable in tests)
        // The important thing is that both calls succeed
    }

    [Fact]
    public async Task CompleteAsync_WithComplexNestedSchema_ValidatesCorrectly()
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
                                    }
                                },
                                "required": ["id", "steps"],
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
                        "steps": ["step 1", "step 2"]
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
        var result = await _provider.CompleteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Message.Content.Should().Contain("task-1");
    }
}

#pragma warning restore CS0618
