#pragma warning disable CS0618 // Intentionally testing obsolete ILlmProvider interface

using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane.Llm.Adapters;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Agents.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ControlPlane.Llm;

public class SemanticKernelLlmProviderTests
{
    private readonly Mock<IChatCompletionService> _chatCompletionServiceMock;
    private readonly Mock<ICorrelationIdProvider> _correlationIdProviderMock;
    private readonly SemanticKernelLlmProvider _provider;

    public SemanticKernelLlmProviderTests()
    {
        _chatCompletionServiceMock = new Mock<IChatCompletionService>();
        _correlationIdProviderMock = new Mock<ICorrelationIdProvider>();
        _correlationIdProviderMock.Setup(c => c.Generate()).Returns("test-correlation-id");
        _provider = new SemanticKernelLlmProvider(
            _chatCompletionServiceMock.Object,
            NullLogger<SemanticKernelLlmProvider>.Instance,
            _correlationIdProviderMock.Object);
    }

    #region Constructor

    [Fact]
    public void Constructor_WithNullChatCompletionService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new SemanticKernelLlmProvider(null!, NullLogger<SemanticKernelLlmProvider>.Instance, _correlationIdProviderMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("chatCompletionService");
    }

    [Fact]
    public void ProviderName_ReturnsSemanticKernel()
    {
        // Assert
        _provider.ProviderName.Should().Be("semantic-kernel");
    }

    #endregion

    #region CompleteAsync - Translation Logic

    [Fact]
    public async Task CompleteAsync_WithSingleUserMessage_TranslatesToChatHistory()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello, world!") }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "Hi there!");
        response.ModelId = "gpt-4";

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.Is<ChatHistory>(h => h.Count == 1 && h[0].Content == "Hello, world!"),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act
        var result = await _provider.CompleteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Message.Content.Should().Be("Hi there!");
        result.Model.Should().Be("gpt-4");
        result.Provider.Should().Be("semantic-kernel");
    }

    [Fact]
    public async Task CompleteAsync_WithMultipleMessages_TranslatesAllMessages()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[]
            {
                LlmMessage.System("You are helpful"),
                LlmMessage.User("Hello"),
                LlmMessage.Assistant("Hi!")
            }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "How can I help?");

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.Is<ChatHistory>(h => h.Count == 3),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { response });

        // Act
        var result = await _provider.CompleteAsync(request);

        // Assert
        result.Message.Content.Should().Be("How can I help?");
        _chatCompletionServiceMock.Verify(x => x.GetChatMessageContentsAsync(
            It.Is<ChatHistory>(h =>
                h[0].Role == AuthorRole.System &&
                h[1].Role == AuthorRole.User &&
                h[2].Role == AuthorRole.Assistant),
            It.IsAny<PromptExecutionSettings>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteAsync_WithModelOverride_PassesModelToSettings()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") },
            Model = "gpt-4-turbo"
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "Hi!");

        PromptExecutionSettings? capturedSettings = null;
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel?, CancellationToken>((_, s, _, _) => capturedSettings = s)
            .ReturnsAsync(new[] { response });

        // Act
        await _provider.CompleteAsync(request);

        // Assert
        capturedSettings.Should().NotBeNull();
        capturedSettings!.ModelId.Should().Be("gpt-4-turbo");
    }

    [Fact]
    public async Task CompleteAsync_WithOptions_TranslatesToExtensionData()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") },
            Options = new LlmProviderOptions
            {
                Temperature = 0.7f,
                MaxTokens = 100,
                TopP = 0.9f,
                FrequencyPenalty = 0.5f,
                PresencePenalty = 0.2f,
                StopSequences = new[] { "STOP" },
                Seed = 42
            }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "Hi!");

        PromptExecutionSettings? capturedSettings = null;
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel?, CancellationToken>((_, s, _, _) => capturedSettings = s)
            .ReturnsAsync(new[] { response });

        // Act
        await _provider.CompleteAsync(request);

        // Assert
        capturedSettings.Should().NotBeNull();
        capturedSettings!.ExtensionData.Should().ContainKey("temperature").WhoseValue.Should().Be(0.7f);
        capturedSettings.ExtensionData.Should().ContainKey("max_tokens").WhoseValue.Should().Be(100);
        capturedSettings.ExtensionData.Should().ContainKey("top_p").WhoseValue.Should().Be(0.9f);
        capturedSettings.ExtensionData.Should().ContainKey("frequency_penalty").WhoseValue.Should().Be(0.5f);
        capturedSettings.ExtensionData.Should().ContainKey("presence_penalty").WhoseValue.Should().Be(0.2f);
        capturedSettings.ExtensionData.Should().ContainKey("stop").WhoseValue.Should().BeEquivalentTo(new[] { "STOP" });
        capturedSettings.ExtensionData.Should().ContainKey("seed").WhoseValue.Should().Be(42);
    }

    [Fact]
    public async Task CompleteAsync_WithResponseFormat_TranslatesToExtensionData()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") },
            Options = new LlmProviderOptions
            {
                ResponseFormat = new { type = "json_object" }
            }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "{}");

        PromptExecutionSettings? capturedSettings = null;
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel?, CancellationToken>((_, s, _, _) => capturedSettings = s)
            .ReturnsAsync(new[] { response });

        // Act
        await _provider.CompleteAsync(request);

        // Assert
        capturedSettings.Should().NotBeNull();
        capturedSettings!.ExtensionData!.Should().ContainKey("response_format");
        capturedSettings!.ExtensionData!["response_format"].Should().BeEquivalentTo(new { type = "json_object" });
    }

    [Fact]
    public async Task CompleteAsync_WithStructuredOutputSchema_SetsResponseFormatPayload()
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
            Messages = new[] { LlmMessage.User("Hello") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "{\"value\":\"ok\"}");

        PromptExecutionSettings? capturedSettings = null;
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel?, CancellationToken>((_, s, _, _) => capturedSettings = s)
            .ReturnsAsync(new[] { response });

        // Act
        await _provider.CompleteAsync(request);

        // Assert
        capturedSettings.Should().NotBeNull();
        capturedSettings!.ExtensionData.Should().ContainKey("response_format");
        capturedSettings.ExtensionData!["response_format"].Should().BeEquivalentTo(schema.ToResponseFormatPayload());
    }

    [Fact]
    public async Task CompleteAsync_WithStructuredOutputSchemaInvalidResponse_Throws()
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
            Messages = new[] { LlmMessage.User("Hello") },
            StructuredOutputSchema = schema
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "{}");

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
    public async Task CompleteAsync_WithResponseFormatMapping_TranslatesToExtensionData()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") },
            Options = new LlmProviderOptions
            {
                ResponseFormat = new { type = "json_object" }
            }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "{}");

        PromptExecutionSettings? capturedSettings = null;
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel?, CancellationToken>((_, s, _, _) => capturedSettings = s)
            .ReturnsAsync(new[] { response });

        // Act
        await _provider.CompleteAsync(request);

        // Assert
        capturedSettings.Should().NotBeNull();
        capturedSettings!.ExtensionData!.Should().ContainKey("response_format");
        capturedSettings!.ExtensionData!["response_format"].Should().BeEquivalentTo(new { type = "json_object" });
    }

    [Fact]
    public async Task CompleteAsync_WithToolCallsInResponse_ReturnsToolCalls()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("What's the weather?") }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "I'll check the weather");
        response.Items.Add(new FunctionCallContent(
            id: "call_123",
            functionName: "get_weather",
            pluginName: string.Empty,
            arguments: new KernelArguments { ["city"] = "Paris" }));

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
        result.ToolCalls.Should().NotBeNull();
        result.ToolCalls.Should().HaveCount(1);
        result.ToolCalls![0].Id.Should().Be("call_123");
        result.ToolCalls[0].Name.Should().Be("get_weather");
        result.ToolCalls[0].ArgumentsJson.Should().Contain("Paris");
    }

    [Fact]
    public async Task CompleteAsync_WithUsageMetadata_ReturnsTokenUsage()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "Hi!");
        response.Metadata = new Dictionary<string, object?>
        {
            ["Usage"] = new Dictionary<string, object?>
            {
                ["InputTokens"] = 10,
                ["OutputTokens"] = 5
            }
        };

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
        result.Usage.Should().NotBeNull();
        result.Usage!.PromptTokens.Should().Be(10);
        result.Usage!.CompletionTokens.Should().Be(5);
        result.Usage!.TotalTokens.Should().Be(15);
    }

    [Fact]
    public async Task CompleteAsync_WithFinishReason_ReturnsFinishReason()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "Hi!");
        response.Metadata = new Dictionary<string, object?> { ["FinishReason"] = "stop" };

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
        result.FinishReason.Should().Be("stop");
    }

    #endregion

    #region CompleteAsync - Error Handling

    [Fact]
    public async Task CompleteAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _provider.CompleteAsync(null!));
    }

    [Fact]
    public async Task CompleteAsync_WhenServiceThrowsHttpRequestException_WrapsInLlmProviderException()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => _provider.CompleteAsync(request));
        ex.ProviderName.Should().Be("semantic-kernel");
        ex.Message.Should().Contain("Failed to complete chat");
        ex.InnerException.Should().BeOfType<HttpRequestException>();
        ex.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_WhenServiceThrowsTimeoutException_WrapsInLlmProviderException()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Request timed out"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => _provider.CompleteAsync(request));
        ex.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_WhenServiceThrowsOperationCanceledException_WrapsInLlmProviderException()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("Cancelled"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => _provider.CompleteAsync(request));
        ex.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_WhenServiceThrowsGenericException_WrapsInLlmProviderException_NotRetryable()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid state"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => _provider.CompleteAsync(request));
        ex.IsRetryable.Should().BeFalse();
    }

    #endregion

    #region StreamCompletionAsync - Streaming Behavior

    [Fact]
    public async Task StreamCompletionAsync_YieldsContentChunks()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Tell me a story") }
        };

        var chunks = new[]
        {
            new StreamingChatMessageContent(AuthorRole.Assistant, "Once"),
            new StreamingChatMessageContent(AuthorRole.Assistant, " upon"),
            new StreamingChatMessageContent(AuthorRole.Assistant, " a time")
        };

        _chatCompletionServiceMock
            .Setup(x => x.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(chunks.ToAsyncEnumerable());

        // Act
        var results = new List<LlmDelta>();
        await foreach (var delta in _provider.StreamCompletionAsync(request))
        {
            results.Add(delta);
        }

        // Assert
        results.Should().HaveCount(3);
        results[0].Content.Should().Be("Once");
        results[1].Content.Should().Be(" upon");
        results[2].Content.Should().Be(" a time");
    }

    [Fact]
    public async Task StreamCompletionAsync_WithFinishReason_SetsIsFinal()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        var chunk = new StreamingChatMessageContent(AuthorRole.Assistant, "Done");
        chunk.Metadata = new Dictionary<string, object?> { ["FinishReason"] = "stop" };

        _chatCompletionServiceMock
            .Setup(x => x.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(new[] { chunk }.ToAsyncEnumerable());

        // Act
        var results = new List<LlmDelta>();
        await foreach (var delta in _provider.StreamCompletionAsync(request))
        {
            results.Add(delta);
        }

        // Assert
        results.Should().HaveCount(1);
        results[0].FinishReason.Should().Be("stop");
        results[0].IsFinal.Should().BeTrue();
    }

    [Fact]
    public async Task StreamCompletionAsync_WithUsage_ReturnsUsage()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        var chunk = new StreamingChatMessageContent(AuthorRole.Assistant, "Hi!");
        chunk.Metadata = new Dictionary<string, object?>
        {
            ["Usage"] = new Dictionary<string, object?>
            {
                ["InputTokens"] = 5,
                ["OutputTokens"] = 2
            }
        };

        _chatCompletionServiceMock
            .Setup(x => x.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(new[] { chunk }.ToAsyncEnumerable());

        // Act
        var results = new List<LlmDelta>();
        await foreach (var delta in _provider.StreamCompletionAsync(request))
        {
            results.Add(delta);
        }

        // Assert
        results[0].Usage.Should().NotBeNull();
        results[0].Usage!.PromptTokens.Should().Be(5);
        results[0].Usage!.CompletionTokens.Should().Be(2);
    }

    [Fact]
    public async Task StreamCompletionAsync_RespectsCancellationToken()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        var chunk = new StreamingChatMessageContent(AuthorRole.Assistant, "Hi");
        _chatCompletionServiceMock
            .Setup(x => x.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(new[] { chunk }.ToAsyncEnumerable());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _provider.StreamCompletionAsync(request, cts.Token).ToListAsync().AsTask());
    }

    #endregion

    #region StreamCompletionAsync - Error Handling

    [Fact]
    public async Task StreamCompletionAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _provider.StreamCompletionAsync(null!).ToListAsync().AsTask());
    }

    [Fact]
    public async Task StreamCompletionAsync_WhenServiceThrowsHttpRequestException_WrapsInLlmProviderException()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        _chatCompletionServiceMock
            .Setup(x => x.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Throws(new HttpRequestException("Connection failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() =>
            _provider.StreamCompletionAsync(request).ToListAsync().AsTask());
        ex.ProviderName.Should().Be("semantic-kernel");
        ex.Message.Should().Contain("Failed to start streaming chat");
        ex.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public async Task StreamCompletionAsync_WhenServiceThrowsTimeoutException_WrapsInLlmProviderException()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        _chatCompletionServiceMock
            .Setup(x => x.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Throws(new TimeoutException("Request timed out"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() =>
            _provider.StreamCompletionAsync(request).ToListAsync().AsTask());
        ex.IsRetryable.Should().BeTrue();
    }

    #endregion

    #region Tool Calling

    [Fact]
    public async Task CompleteAsync_WithToolsInRequest_TranslatesToExtensionData()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("What's the weather?") },
            Tools = new[]
            {
                new LlmToolDefinition
                {
                    Name = "get_weather",
                    Description = "Get weather information",
                    ParametersSchema = new { type = "object", properties = new { city = new { type = "string" } } }
                }
            },
            ToolChoice = "auto"
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "I'll check the weather");

        PromptExecutionSettings? capturedSettings = null;
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel?, CancellationToken>((_, s, _, _) => capturedSettings = s)
            .ReturnsAsync(new[] { response });

        // Act
        await _provider.CompleteAsync(request);

        // Assert
        capturedSettings.Should().NotBeNull();
        capturedSettings!.ExtensionData.Should().ContainKey("tools");
        capturedSettings.ExtensionData.Should().ContainKey("tool_choice").WhoseValue.Should().Be("auto");
    }

    #endregion

    #region LlmCompletionRequest Builder - Tool Calling Integration

    [Fact]
    public void LlmCompletionRequest_WithMessage_AddsMessageToConversation()
    {
        // Arrange
        var initialRequest = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.System("You are helpful") }
        };

        // Act
        var newRequest = initialRequest.WithMessage(LlmMessage.User("Hello"));

        // Assert
        newRequest.Should().NotBeSameAs(initialRequest);
        newRequest.Messages.Should().HaveCount(2);
        newRequest.Messages[0].Role.Should().Be(LlmMessageRole.System);
        newRequest.Messages[1].Role.Should().Be(LlmMessageRole.User);
        newRequest.Messages[1].Content.Should().Be("Hello");
    }

    [Fact]
    public void LlmCompletionRequest_WithMessages_AddsMultipleMessages()
    {
        // Arrange
        var initialRequest = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.System("You are helpful") }
        };

        var toolResults = new[]
        {
            LlmMessage.Tool("call_1", "get_weather", "{\"temp\": 72}"),
            LlmMessage.Tool("call_2", "get_time", "{\"time\": \"12:00\"}")
        };

        // Act
        var newRequest = initialRequest.WithMessages(toolResults);

        // Assert
        newRequest.Messages.Should().HaveCount(3);
        newRequest.Messages[1].ToolCallId.Should().Be("call_1");
        newRequest.Messages[2].ToolCallId.Should().Be("call_2");
    }

    [Fact]
    public void LlmCompletionRequest_WithMessageReplaced_ReplacesMessageAtIndex()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[]
            {
                LlmMessage.System("Old system"),
                LlmMessage.User("Hello")
            }
        };

        // Act
        var newRequest = request.WithMessageReplaced(0, LlmMessage.System("New system"));

        // Assert
        newRequest.Messages[0].Content.Should().Be("New system");
        newRequest.Messages[1].Content.Should().Be("Hello");
    }

    [Fact]
    public void LlmCompletionRequest_WithMessageReplaced_InvalidIndex_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            request.WithMessageReplaced(5, LlmMessage.System("Test")));
    }

    [Fact]
    public void LlmCompletionRequest_CreateBuilder_BuildsRequest()
    {
        // Act
        var request = LlmCompletionRequest.CreateBuilder()
            .WithMessage(LlmMessage.System("You are helpful"))
            .WithMessage(LlmMessage.User("Hello"))
            .WithModel("gpt-4")
            .WithToolChoice("auto")
            .WithTool(new LlmToolDefinition
            {
                Name = "get_weather",
                Description = "Get weather",
                ParametersSchema = new { }
            })
            .Build();

        // Assert
        request.Messages.Should().HaveCount(2);
        request.Model.Should().Be("gpt-4");
        request.ToolChoice.Should().Be("auto");
        request.Tools.Should().HaveCount(1);
    }

    [Fact]
    public void LlmCompletionRequest_CreateBuilder_WithInitialMessage_StartsWithMessage()
    {
        // Act
        var request = LlmCompletionRequest.CreateBuilder(LlmMessage.System("You are helpful"))
            .WithMessage(LlmMessage.User("Hello"))
            .Build();

        // Assert
        request.Messages.Should().HaveCount(2);
        request.Messages[0].Role.Should().Be(LlmMessageRole.System);
    }

    [Fact]
    public void LlmCompletionRequest_CreateBuilder_NoMessages_ThrowsInvalidOperation()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            LlmCompletionRequest.CreateBuilder().Build());
    }

    #endregion

    #region Configuration Validation Error Handling

    [Fact]
    public async Task CompleteAsync_WhenServiceThrowsAuthenticationException_WrapsInNonRetryableLlmProviderException()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        var authException = new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(authException);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => _provider.CompleteAsync(request));
        ex.IsRetryable.Should().BeFalse();
        ex.Message.Should().Contain("Failed to complete chat");
    }

    [Fact]
    public async Task CompleteAsync_WhenServiceThrowsInvalidModelException_WrapsInNonRetryableLlmProviderException()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") },
            Model = "invalid-model-xyz"
        };

        var invalidModelException = new InvalidOperationException("Model not found: invalid-model-xyz");
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(invalidModelException);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => _provider.CompleteAsync(request));
        ex.IsRetryable.Should().BeFalse();
        ex.Message.Should().Contain("Failed to complete chat");
    }

    [Fact]
    public async Task StreamCompletionAsync_WhenServiceThrowsAuthenticationException_WrapsInNonRetryableLlmProviderException()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        var authException = new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);
        _chatCompletionServiceMock
            .Setup(x => x.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Throws(authException);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() =>
            _provider.StreamCompletionAsync(request).ToListAsync().AsTask());
        ex.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteAsync_WhenRetryableErrorOccurs_RetriesWithExponentialBackoff()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, "Hi!");
        var callCount = 0;

        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount < 2)
                {
                    // First call fails with timeout (retryable)
                    throw new TimeoutException("Request timed out");
                }
                // Second call succeeds
                return Task.FromResult(response);
            });

        // Act
        var result = await _provider.CompleteAsync(request);

        // Assert
        result.Message.Content.Should().Be("Hi!");
        callCount.Should().Be(2); // Should have retried once
    }

    [Fact]
    public async Task CompleteAsync_WhenMaxRetriesExceeded_ThrowsLlmProviderException()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        var callCount = 0;
        _chatCompletionServiceMock
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                // Always fail with retryable error
                throw new TimeoutException("Request timed out");
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => _provider.CompleteAsync(request));
        ex.IsRetryable.Should().BeTrue();
        callCount.Should().Be(4); // Initial attempt + 3 retries
    }

    #endregion

    #region Tool Call Detection - LlmCompletionResponse

    [Fact]
    public void LlmCompletionResponse_HasToolCalls_WhenFinishReasonIsToolCalls_ReturnsTrue()
    {
        // Arrange
        var response = new LlmCompletionResponse
        {
            Message = LlmMessage.Assistant("", new[] { new LlmToolCall { Id = "1", Name = "test", ArgumentsJson = "{}" } }),
            Model = "gpt-4",
            Provider = "test",
            FinishReason = "tool_calls"
        };

        // Assert
        response.HasToolCalls.Should().BeTrue();
    }

    [Fact]
    public void LlmCompletionResponse_HasToolCalls_WhenToolCallsPresent_ReturnsTrue()
    {
        // Arrange
        var response = new LlmCompletionResponse
        {
            Message = LlmMessage.Assistant(""),
            Model = "gpt-4",
            Provider = "test",
            ToolCalls = new[] { new LlmToolCall { Id = "1", Name = "test", ArgumentsJson = "{}" } }
        };

        // Assert
        response.HasToolCalls.Should().BeTrue();
    }

    [Fact]
    public void LlmCompletionResponse_HasToolCalls_WhenNoToolCalls_ReturnsFalse()
    {
        // Arrange
        var response = new LlmCompletionResponse
        {
            Message = LlmMessage.Assistant("Hello"),
            Model = "gpt-4",
            Provider = "test",
            FinishReason = "stop"
        };

        // Assert
        response.HasToolCalls.Should().BeFalse();
    }

    [Fact]
    public void LlmCompletionResponse_IsComplete_WhenStopAndNoToolCalls_ReturnsTrue()
    {
        // Arrange
        var response = new LlmCompletionResponse
        {
            Message = LlmMessage.Assistant("Hello"),
            Model = "gpt-4",
            Provider = "test",
            FinishReason = "stop"
        };

        // Assert
        response.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void LlmCompletionResponse_IsComplete_WhenToolCalls_ReturnsFalse()
    {
        // Arrange
        var response = new LlmCompletionResponse
        {
            Message = LlmMessage.Assistant("", new[] { new LlmToolCall { Id = "1", Name = "test", ArgumentsJson = "{}" } }),
            Model = "gpt-4",
            Provider = "test",
            FinishReason = "tool_calls"
        };

        // Assert
        response.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void LlmCompletionResponse_CreateToolResultMessage_ReturnsFormattedMessage()
    {
        // Act
        var message = LlmCompletionResponse.CreateToolResultMessage("call_1", "get_weather", "{\"temp\": 72}");

        // Assert
        message.Role.Should().Be(LlmMessageRole.Tool);
        message.ToolCallId.Should().Be("call_1");
        message.ToolName.Should().Be("get_weather");
        message.Content.Should().Be("{\"temp\": 72}");
    }

    [Fact]
    public void LlmCompletionResponse_CreateToolErrorMessage_ReturnsFormattedErrorMessage()
    {
        // Act
        var message = LlmCompletionResponse.CreateToolErrorMessage("call_1", "get_weather", "APIError");

        // Assert
        message.Role.Should().Be(LlmMessageRole.Tool);
        message.ToolCallId.Should().Be("call_1");
        message.Content.Should().Contain("error");
        message.Content.Should().Contain("APIError");
    }

    #endregion

    #region Tool Result Message Formatting - LlmMessage

    [Fact]
    public void LlmMessage_HasToolCalls_WhenToolCallsPresent_ReturnsTrue()
    {
        // Arrange
        var message = LlmMessage.Assistant("", new[] { new LlmToolCall { Id = "1", Name = "test", ArgumentsJson = "{}" } });

        // Assert
        message.HasToolCalls.Should().BeTrue();
    }

    [Fact]
    public void LlmMessage_HasToolCalls_WhenNoToolCalls_ReturnsFalse()
    {
        // Arrange
        var message = LlmMessage.Assistant("Hello");

        // Assert
        message.HasToolCalls.Should().BeFalse();
    }

    [Fact]
    public void LlmMessage_IsToolResult_WhenRoleIsTool_ReturnsTrue()
    {
        // Arrange
        var message = LlmMessage.Tool("call_1", "test", "result");

        // Assert
        message.IsToolResult.Should().BeTrue();
    }

    [Fact]
    public void LlmMessage_ToolResult_WithObject_SerializesToJson()
    {
        // Arrange
        var result = new { temperature = 72, unit = "F" };

        // Act
        var message = LlmMessage.ToolResult("call_1", "get_weather", result);

        // Assert
        message.Content.Should().Contain("temperature");
        message.Content.Should().Contain("72");
        message.ToolCallId.Should().Be("call_1");
    }

    [Fact]
    public void LlmMessage_ToolResult_WithString_UsesStringDirectly()
    {
        // Act
        var message = LlmMessage.ToolResult("call_1", "get_weather", "Already a string");

        // Assert
        message.Content.Should().Be("Already a string");
    }

    [Fact]
    public void LlmMessage_ToolError_CreatesProperlyFormattedError()
    {
        // Act
        var message = LlmMessage.ToolError("call_1", "get_weather", "NotFound", "City not found");

        // Assert
        message.Role.Should().Be(LlmMessageRole.Tool);
        message.Content.Should().Contain("error");
        message.Content.Should().Contain("City not found");
        message.Content.Should().Contain("NotFound");
    }

    [Fact]
    public void LlmMessage_ToolException_CreatesProperlyFormattedExceptionMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Something went wrong");

        // Act
        var message = LlmMessage.ToolException("call_1", "get_weather", exception);

        // Assert
        message.Role.Should().Be(LlmMessageRole.Tool);
        message.Content.Should().Contain("Something went wrong");
        message.Content.Should().Contain("ExecutionError");
        message.Content.Should().Contain("InvalidOperationException");
    }

    #endregion

    #region Streaming Tool Call Support - LlmDelta

    [Fact]
    public void LlmDelta_HasStreamingToolCall_WhenToolCallPresent_ReturnsTrue()
    {
        // Arrange
        var delta = new LlmDelta
        {
            Content = "",
            ToolCall = new LlmStreamingToolCall { Id = "1", Name = "test" }
        };

        // Assert
        delta.HasStreamingToolCall.Should().BeTrue();
    }

    [Fact]
    public void LlmDelta_HasStreamingToolCall_WhenNoToolCall_ReturnsFalse()
    {
        // Arrange
        var delta = new LlmDelta { Content = "Hello" };

        // Assert
        delta.HasStreamingToolCall.Should().BeFalse();
    }

    [Fact]
    public void LlmStreamingToolCall_WithCompleteArguments_MarksAsComplete()
    {
        // Arrange
        var toolCall = new LlmStreamingToolCall
        {
            Id = "call_1",
            Name = "get_weather",
            ArgumentsJson = "{\"city\": \"Paris\"}",
            Index = 0,
            IsComplete = true
        };

        // Assert
        toolCall.IsComplete.Should().BeTrue();
        toolCall.ArgumentsJson.Should().Be("{\"city\": \"Paris\"}");
    }

    #endregion

    #region Null Content Handling

    [Fact]
    public async Task CompleteAsync_WithNullContent_ReturnsEmptyString()
    {
        // Arrange
        var request = new LlmCompletionRequest
        {
            Messages = new[] { LlmMessage.User("Hello") }
        };

        var response = new ChatMessageContent(AuthorRole.Assistant, (string?)null);

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
        result.Message.Content.Should().BeNull();
    }

    #endregion
}

#pragma warning restore CS0618
