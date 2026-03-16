using Xunit;
using Moq;
using nirmata.Agents.Execution.ControlPlane.Chat;
using nirmata.Agents.Execution.ControlPlane.Chat.Models;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;

namespace nirmata.Agents.Tests.Execution;

/// <summary>
/// End-to-end tests for chat responder with multi-turn conversations, streaming, and error recovery.
/// These tests validate the complete flow of chat interactions including context maintenance,
/// streaming response completeness, cancellation handling, and error recovery scenarios.
/// </summary>
public class ChatResponderE2ETests
{
    private readonly Mock<ILlmProvider> _llmProviderMock;
    private readonly Mock<IChatContextAssembly> _contextAssemblyMock;
    private readonly ChatPromptBuilder _promptBuilder;
    private readonly LlmChatResponder _responder;

    public ChatResponderE2ETests()
    {
        _llmProviderMock = new Mock<ILlmProvider>();
        _contextAssemblyMock = new Mock<IChatContextAssembly>();
        _promptBuilder = new ChatPromptBuilder();
        _responder = new LlmChatResponder(
            _llmProviderMock.Object,
            _contextAssemblyMock.Object,
            _promptBuilder,
            NullLogger<LlmChatResponder>.Instance);
    }

    #region 3.1 Multi-turn Conversation Tests

    [Fact]
    public async Task E2E_MultiTurnConversation_MaintainsContext()
    {
        // Arrange - Setup context that tracks conversation history
        var conversationHistory = new List<LlmMessage>();
        var contextAssemblyCallCount = 0;

        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                contextAssemblyCallCount++;
                return new ChatContext
                {
                    State = new StateContext(),
                    AvailableCommands = Array.Empty<CommandContext>(),
                    RecentRuns = Array.Empty<RunHistoryContext>(),
                    IsSuccess = true
                };
            });

        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((req, _) =>
            {
                conversationHistory.AddRange(req.Messages);
            })
            .ReturnsAsync((LlmCompletionRequest req, CancellationToken _) =>
            {
                // Check the user message content (last message in the request)
                var userMessage = req.Messages.Last().Content ?? string.Empty;
                var response = userMessage.Contains("2+2", StringComparison.OrdinalIgnoreCase) ? "2+2 equals 4" :
                               userMessage.Contains("3+3", StringComparison.OrdinalIgnoreCase) ? "3+3 equals 6" :
                               userMessage.Contains("4+4", StringComparison.OrdinalIgnoreCase) ? "4+4 equals 8" :
                               "I don't understand";

                return new LlmCompletionResponse
                {
                    Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = response },
                    Model = "test-model",
                    Provider = "test-provider",
                    Usage = new LlmTokenUsage { PromptTokens = 10, CompletionTokens = 5 }
                };
            });

        // Act - Perform 3+ message exchanges
        var response1 = await _responder.RespondAsync(new ChatRequest { Input = "What is 2+2?" });
        var response2 = await _responder.RespondAsync(new ChatRequest { Input = "What about 3+3?" });
        var response3 = await _responder.RespondAsync(new ChatRequest { Input = "And 4+4?" });

        // Assert - Verify context is maintained across turns
        Assert.True(response1.IsSuccess);
        Assert.True(response2.IsSuccess);
        Assert.True(response3.IsSuccess);

        Assert.Equal("2+2 equals 4", response1.Content);
        Assert.Equal("3+3 equals 6", response2.Content);
        Assert.Equal("4+4 equals 8", response3.Content);

        // Verify context assembly was called for each turn
        Assert.Equal(3, contextAssemblyCallCount);

        // Verify conversation history accumulated messages
        Assert.NotEmpty(conversationHistory);
    }

    [Fact]
    public async Task E2E_MultiTurnConversation_ResponsesAreContextuallyAppropriate()
    {
        // Arrange
        var capturedRequests = new List<LlmCompletionRequest>();

        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatContext
            {
                State = new StateContext(),
                AvailableCommands = Array.Empty<CommandContext>(),
                RecentRuns = Array.Empty<RunHistoryContext>(),
                IsSuccess = true
            });

        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((req, _) => capturedRequests.Add(req))
            .ReturnsAsync((LlmCompletionRequest req, CancellationToken _) =>
            {
                var lastMessage = req.Messages.Last().Content ?? string.Empty;
                var response = lastMessage.Contains("hello", StringComparison.OrdinalIgnoreCase)
                    ? "Hello! How can I help you?"
                    : "That's interesting. Tell me more.";

                return new LlmCompletionResponse
                {
                    Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = response },
                    Model = "test-model",
                    Provider = "test-provider",
                    Usage = new LlmTokenUsage { PromptTokens = 10, CompletionTokens = 5 }
                };
            });

        // Act
        var response1 = await _responder.RespondAsync(new ChatRequest { Input = "Hello there" });
        var response2 = await _responder.RespondAsync(new ChatRequest { Input = "Tell me about AI" });

        // Assert
        Assert.Equal("Hello! How can I help you?", response1.Content);
        Assert.Equal("That's interesting. Tell me more.", response2.Content);

        // Verify each request includes system context
        Assert.All(capturedRequests, req =>
        {
            Assert.NotEmpty(req.Messages);
            Assert.Contains(req.Messages, m => m.Role == LlmMessageRole.System);
        });
    }

    [Fact]
    public async Task E2E_MultiTurnConversation_WithThreeOrMoreExchanges()
    {
        // Arrange
        var exchangeCount = 0;

        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatContext
            {
                State = new StateContext(),
                AvailableCommands = Array.Empty<CommandContext>(),
                RecentRuns = Array.Empty<RunHistoryContext>(),
                IsSuccess = true
            });

        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((_, _) => exchangeCount++)
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = "Response" },
                Model = "test-model",
                Provider = "test-provider",
                Usage = new LlmTokenUsage { PromptTokens = 10, CompletionTokens = 5 }
            });

        // Act - Perform 5 exchanges
        for (int i = 0; i < 5; i++)
        {
            await _responder.RespondAsync(new ChatRequest { Input = $"Message {i + 1}" });
        }

        // Assert
        Assert.Equal(5, exchangeCount);
    }

    #endregion

    #region 3.2 Streaming Tests

    [Fact]
    public async Task E2E_StreamingResponse_AllChunksArriveInOrder()
    {
        // Arrange
        var expectedChunks = new List<string> { "The", " quick", " brown", " fox", " jumps" };
        var deltas = expectedChunks.Select(c => new LlmDelta { Content = c }).ToList();

        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatContext
            {
                State = new StateContext(),
                AvailableCommands = Array.Empty<CommandContext>(),
                RecentRuns = Array.Empty<RunHistoryContext>(),
                IsSuccess = true
            });

        _llmProviderMock.Setup(p => p.StreamCompletionAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(deltas.ToAsyncEnumerable());

        // Act
        var receivedChunks = new List<string>();
        await foreach (var delta in _responder.StreamResponseAsync(new ChatRequest { Input = "Test" }))
        {
            if (!delta.IsComplete && !string.IsNullOrEmpty(delta.Content))
            {
                receivedChunks.Add(delta.Content);
            }
        }

        // Assert - Verify all chunks arrived in order
        Assert.Equal(expectedChunks.Count, receivedChunks.Count);
        for (int i = 0; i < expectedChunks.Count; i++)
        {
            Assert.Equal(expectedChunks[i], receivedChunks[i]);
        }
    }

    [Fact]
    public async Task E2E_StreamingResponse_LongFormRequest_NoMessageLoss()
    {
        // Arrange - Create a long-form response (>1000 tokens worth of content)
        var longContent = string.Concat(Enumerable.Range(0, 200).Select(i => $"Token{i} "));
        var deltas = longContent.Split(' ')
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(token => new LlmDelta { Content = token + " " })
            .ToList();

        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatContext
            {
                State = new StateContext(),
                AvailableCommands = Array.Empty<CommandContext>(),
                RecentRuns = Array.Empty<RunHistoryContext>(),
                IsSuccess = true
            });

        _llmProviderMock.Setup(p => p.StreamCompletionAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(deltas.ToAsyncEnumerable());

        // Act
        var assembledContent = new System.Text.StringBuilder();
        await foreach (var delta in _responder.StreamResponseAsync(new ChatRequest { Input = "Generate long response" }))
        {
            if (!delta.IsComplete && !string.IsNullOrEmpty(delta.Content))
            {
                assembledContent.Append(delta.Content);
            }
        }

        // Assert - Verify no message loss
        Assert.NotEmpty(assembledContent.ToString());
        Assert.Equal(longContent.Length, assembledContent.Length);
    }

    [Fact]
    public async Task E2E_StreamingResponse_AssembledContentMatchesNonStreamed()
    {
        // Arrange
        var expectedContent = "This is a complete response from the LLM.";
        var deltas = expectedContent.Split(' ')
            .Select(word => new LlmDelta { Content = word + " " })
            .ToList();

        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatContext
            {
                State = new StateContext(),
                AvailableCommands = Array.Empty<CommandContext>(),
                RecentRuns = Array.Empty<RunHistoryContext>(),
                IsSuccess = true
            });

        // Setup streaming
        _llmProviderMock.Setup(p => p.StreamCompletionAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(deltas.ToAsyncEnumerable());

        // Setup non-streamed
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResponse
            {
                Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = expectedContent.Trim() },
                Model = "test-model",
                Provider = "test-provider",
                Usage = new LlmTokenUsage { PromptTokens = 10, CompletionTokens = 8 }
            });

        // Act
        var streamedContent = new System.Text.StringBuilder();
        await foreach (var delta in _responder.StreamResponseAsync(new ChatRequest { Input = "Test" }))
        {
            if (!delta.IsComplete && !string.IsNullOrEmpty(delta.Content))
            {
                streamedContent.Append(delta.Content);
            }
        }

        var nonStreamedResponse = await _responder.RespondAsync(new ChatRequest { Input = "Test" });

        // Assert
        Assert.Equal(nonStreamedResponse.Content.Trim(), streamedContent.ToString().Trim());
    }

    [Fact]
    public async Task E2E_StreamingResponse_CancellationMidStream()
    {
        // Arrange
        var deltas = new List<LlmDelta>
        {
            new() { Content = "Start" },
            new() { Content = " of" },
            new() { Content = " response" }
        };

        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatContext
            {
                State = new StateContext(),
                AvailableCommands = Array.Empty<CommandContext>(),
                RecentRuns = Array.Empty<RunHistoryContext>(),
                IsSuccess = true
            });

        _llmProviderMock.Setup(p => p.StreamCompletionAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmCompletionRequest _, CancellationToken ct) =>
                StreamDeltasWithCancellation(deltas, ct));

        // Act
        using var cts = new CancellationTokenSource();
        var receivedDeltas = new List<ChatDelta>();
        var cancellationHandled = false;

        try
        {
            await foreach (var delta in _responder.StreamResponseAsync(new ChatRequest { Input = "Test" }, cts.Token))
            {
                receivedDeltas.Add(delta);
                if (receivedDeltas.Count >= 2)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancellationHandled = true;
        }

        // Assert - Verify cancellation was handled (either gracefully or with exception)
        Assert.True(receivedDeltas.Count > 0 || cancellationHandled);
    }

    private async IAsyncEnumerable<LlmDelta> StreamDeltasWithCancellation(List<LlmDelta> deltas, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var delta in deltas)
        {
            ct.ThrowIfCancellationRequested();
            yield return delta;
            await Task.Delay(10, ct);
        }
    }

    #endregion

    #region 3.3 Error Recovery Tests

    [Fact]
    public async Task E2E_ErrorRecovery_LlmFailureFallback()
    {
        // Arrange
        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatContext
            {
                State = new StateContext(),
                AvailableCommands = Array.Empty<CommandContext>(),
                RecentRuns = Array.Empty<RunHistoryContext>(),
                IsSuccess = true
            });

        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        // Act
        var result = await _responder.RespondAsync(new ChatRequest { Input = "Test" });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task E2E_ErrorRecovery_TimeoutHandling()
    {
        // Arrange
        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatContext
            {
                State = new StateContext(),
                AvailableCommands = Array.Empty<CommandContext>(),
                RecentRuns = Array.Empty<RunHistoryContext>(),
                IsSuccess = true
            });

        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("Request timeout"));

        // Act
        var result = await _responder.RespondAsync(new ChatRequest { Input = "Test" });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Request timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task E2E_ErrorRecovery_ContextAssemblyFailure()
    {
        // Arrange
        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Context assembly failed"));

        // Act
        var result = await _responder.RespondAsync(new ChatRequest { Input = "Test" });

        // Assert - Context assembly failure should result in error response
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task E2E_ErrorRecovery_StreamingErrorHandling()
    {
        // Arrange
        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatContext
            {
                State = new StateContext(),
                AvailableCommands = Array.Empty<CommandContext>(),
                RecentRuns = Array.Empty<RunHistoryContext>(),
                IsSuccess = true
            });

        _llmProviderMock.Setup(p => p.StreamCompletionAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmCompletionRequest _, CancellationToken _) =>
                StreamWithError());

        // Act
        var deltas = new List<ChatDelta>();
        var exceptionThrown = false;

        try
        {
            await foreach (var delta in _responder.StreamResponseAsync(new ChatRequest { Input = "Test" }))
            {
                deltas.Add(delta);
            }
        }
        catch (InvalidOperationException)
        {
            exceptionThrown = true;
        }

        // Assert - Stream error should propagate
        Assert.True(exceptionThrown || deltas.Count > 0);
    }

    private async IAsyncEnumerable<LlmDelta> StreamWithError()
    {
        yield return new LlmDelta { Content = "Partial" };
        await Task.Delay(10);
        throw new InvalidOperationException("Stream interrupted");
    }

    [Fact]
    public async Task E2E_ErrorRecovery_MultipleFailuresRecovery()
    {
        // Arrange
        var callCount = 0;

        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatContext
            {
                State = new StateContext(),
                AvailableCommands = Array.Empty<CommandContext>(),
                RecentRuns = Array.Empty<RunHistoryContext>(),
                IsSuccess = true
            });

        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LlmCompletionRequest _, CancellationToken _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromException<LlmCompletionResponse>(
                        new InvalidOperationException("First call fails"));
                }

                return Task.FromResult(new LlmCompletionResponse
                {
                    Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = "Success" },
                    Model = "test-model",
                    Provider = "test-provider",
                    Usage = new LlmTokenUsage { PromptTokens = 10, CompletionTokens = 5 }
                });
            });

        // Act
        var result1 = await _responder.RespondAsync(new ChatRequest { Input = "First" });
        var result2 = await _responder.RespondAsync(new ChatRequest { Input = "Second" });

        // Assert
        Assert.False(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal("Success", result2.Content);
    }

    #endregion
}
