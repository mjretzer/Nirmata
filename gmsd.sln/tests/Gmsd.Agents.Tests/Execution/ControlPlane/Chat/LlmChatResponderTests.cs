using Xunit;
using Moq;
using Gmsd.Agents.Execution.ControlPlane.Chat;
using Gmsd.Agents.Execution.ControlPlane.Chat.Models;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Chat;

public class LlmChatResponderTests
{
    private readonly Mock<ILlmProvider> _llmProviderMock;
    private readonly Mock<IChatContextAssembly> _contextAssemblyMock;
    private readonly ChatPromptBuilder _promptBuilder;
    private readonly LlmChatResponder _sut;

    public LlmChatResponderTests()
    {
        _llmProviderMock = new Mock<ILlmProvider>();
        _contextAssemblyMock = new Mock<IChatContextAssembly>();
        _promptBuilder = new ChatPromptBuilder();
        _sut = new LlmChatResponder(
            _llmProviderMock.Object,
            _contextAssemblyMock.Object,
            _promptBuilder,
            NullLogger<LlmChatResponder>.Instance);
    }

    [Fact]
    public void Constructor_WithNullLlmProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LlmChatResponder(null!, _contextAssemblyMock.Object, _promptBuilder));
    }

    [Fact]
    public void Constructor_WithNullContextAssembly_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LlmChatResponder(_llmProviderMock.Object, null!, _promptBuilder));
    }

    [Fact]
    public void Constructor_WithNullPromptBuilder_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LlmChatResponder(_llmProviderMock.Object, _contextAssemblyMock.Object, null!));
    }

    [Fact]
    public async Task RespondAsync_WithValidRequest_ReturnsChatResponse()
    {
        SetupBasicContext();
        SetupLlmResponse("Hello, how can I help you?");

        var request = new ChatRequest { Input = "Hi there" };
        var result = await _sut.RespondAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello, how can I help you?", result.Content);
        Assert.True(result.DurationMs >= 0);
    }

    [Fact]
    public async Task RespondAsync_CallsContextAssembly()
    {
        SetupBasicContext();
        SetupLlmResponse("Response");

        var request = new ChatRequest { Input = "Test" };
        await _sut.RespondAsync(request);

        _contextAssemblyMock.Verify(c => c.AssembleAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RespondAsync_CallsLlmProviderWithCorrectMessages()
    {
        SetupBasicContext();
        LlmCompletionRequest? capturedRequest = null;
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateLlmResponse("Response"));

        var request = new ChatRequest { Input = "Hello" };
        await _sut.RespondAsync(request);

        Assert.NotNull(capturedRequest);
        Assert.Equal(2, capturedRequest.Messages.Count);
        Assert.Equal(LlmMessageRole.System, capturedRequest.Messages[0].Role);
        Assert.Equal(LlmMessageRole.User, capturedRequest.Messages[1].Role);
    }

    [Fact]
    public async Task RespondAsync_UsesDefaultTemperature()
    {
        SetupBasicContext();
        LlmCompletionRequest? capturedRequest = null;
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateLlmResponse("Response"));

        var request = new ChatRequest { Input = "Test" };
        await _sut.RespondAsync(request);

        Assert.NotNull(capturedRequest);
        Assert.Equal(0.7f, capturedRequest.Options?.Temperature);
    }

    [Fact]
    public async Task RespondAsync_UsesCustomTemperatureWhenProvided()
    {
        SetupBasicContext();
        LlmCompletionRequest? capturedRequest = null;
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateLlmResponse("Response"));

        var request = new ChatRequest { Input = "Test", Temperature = 0.5 };
        await _sut.RespondAsync(request);

        Assert.NotNull(capturedRequest);
        Assert.Equal(0.5f, capturedRequest.Options?.Temperature);
    }

    [Fact]
    public async Task RespondAsync_UsesDefaultMaxTokens()
    {
        SetupBasicContext();
        LlmCompletionRequest? capturedRequest = null;
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateLlmResponse("Response"));

        var request = new ChatRequest { Input = "Test" };
        await _sut.RespondAsync(request);

        Assert.NotNull(capturedRequest);
        Assert.Equal(1000, capturedRequest.Options?.MaxTokens);
    }

    [Fact]
    public async Task RespondAsync_UsesCustomMaxTokensWhenProvided()
    {
        SetupBasicContext();
        LlmCompletionRequest? capturedRequest = null;
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(CreateLlmResponse("Response"));

        var request = new ChatRequest { Input = "Test", MaxTokens = 500 };
        await _sut.RespondAsync(request);

        Assert.NotNull(capturedRequest);
        Assert.Equal(500, capturedRequest.Options?.MaxTokens);
    }

    [Fact]
    public async Task RespondAsync_ReturnsTokenUsage()
    {
        SetupBasicContext();
        var llmResponse = CreateLlmResponse("Response", promptTokens: 50, completionTokens: 25);
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        var request = new ChatRequest { Input = "Test" };
        var result = await _sut.RespondAsync(request);

        Assert.Equal(50, result.PromptTokens);
        Assert.Equal(25, result.CompletionTokens);
        Assert.Equal(75, result.TotalTokens);
    }

    [Fact]
    public async Task RespondAsync_WhenContextAssemblyFails_ReturnsDegradedResponse()
    {
        SetupFailingContext("Assembly failed");
        SetupLlmResponse("Response");

        var request = new ChatRequest { Input = "Test" };
        var result = await _sut.RespondAsync(request);

        Assert.True(result.IsSuccess); // Still succeeds with degraded context
    }

    [Fact]
    public async Task RespondAsync_WhenLlmProviderThrows_ReturnsErrorResponse()
    {
        SetupBasicContext();
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM error"));

        var request = new ChatRequest { Input = "Test" };
        var result = await _sut.RespondAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("LLM error", result.ErrorMessage);
        Assert.Contains("help", result.Content.ToLower()); // Contains fallback help text
    }

    [Fact]
    public async Task RespondAsync_WhenLlmProviderThrows_ReturnsFallbackMessage()
    {
        SetupBasicContext();
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        var request = new ChatRequest { Input = "Test" };
        var result = await _sut.RespondAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Content);
        Assert.Contains("having trouble", result.Content.ToLower());
    }

    [Fact]
    public async Task RespondAsync_WithTimeout_ReturnsTimeoutResponse()
    {
        SetupBasicContext();
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var request = new ChatRequest { Input = "Test" };
        var result = await _sut.RespondAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Request timed out", result.ErrorMessage);
        Assert.Contains("timed out", result.Content.ToLower());
    }

    [Fact]
    public void DefaultTimeout_Is10Seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), _sut.Timeout);
    }

    [Fact]
    public void DefaultTemperature_Is0_7()
    {
        Assert.Equal(0.7f, _sut.DefaultTemperature);
    }

    [Fact]
    public void DefaultMaxTokens_Is1000()
    {
        Assert.Equal(1000, _sut.DefaultMaxTokens);
    }

    [Fact]
    public async Task StreamResponseAsync_WithValidRequest_YieldsDeltas()
    {
        SetupBasicContext();
        var deltas = new List<LlmDelta>
        {
            new() { Content = "Hello" },
            new() { Content = " world" },
            new() { Content = "!" }
        };
        _llmProviderMock.Setup(p => p.StreamCompletionAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(deltas.ToAsyncEnumerable());

        var request = new ChatRequest { Input = "Hi" };
        var results = new List<ChatDelta>();
        await foreach (var delta in _sut.StreamResponseAsync(request))
        {
            results.Add(delta);
        }

        Assert.Equal(4, results.Count); // 3 deltas + 1 completion marker
        Assert.Equal("Hello", results[0].Content);
        Assert.Equal(" world", results[1].Content);
        Assert.Equal("!", results[2].Content);
        Assert.True(results[3].IsComplete);
    }

    [Fact]
    public async Task StreamResponseAsync_WhenInitializationFails_YieldsErrorDelta()
    {
        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Context error"));

        var request = new ChatRequest { Input = "Hi" };
        var results = new List<ChatDelta>();
        await foreach (var delta in _sut.StreamResponseAsync(request))
        {
            results.Add(delta);
        }

        Assert.Equal(2, results.Count);
        Assert.Contains("Error", results[0].Content);
        Assert.True(results[0].IsComplete);
        Assert.True(results[1].IsComplete); // Final completion marker
    }

    [Fact]
    public async Task StreamResponseAsync_RespectsCancellationToken()
    {
        SetupBasicContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new ChatRequest { Input = "Hi" };
        _llmProviderMock.Setup(p => p.StreamCompletionAsync(It.IsAny<LlmCompletionRequest>(), cts.Token))
            .Throws(new OperationCanceledException());

        var results = new List<ChatDelta>();
        await foreach (var delta in _sut.StreamResponseAsync(request, cts.Token))
        {
            results.Add(delta);
        }

        // Should complete gracefully on cancellation
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task StreamResponseAsync_CallsContextAssembly()
    {
        SetupBasicContext();
        _llmProviderMock.Setup(p => p.StreamCompletionAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<LlmDelta>());

        var request = new ChatRequest { Input = "Test" };
        await foreach (var _ in _sut.StreamResponseAsync(request)) { }

        _contextAssemblyMock.Verify(c => c.AssembleAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StreamResponseAsync_CallsLlmProviderWithCorrectRequest()
    {
        SetupBasicContext();
        LlmCompletionRequest? capturedRequest = null;
        _llmProviderMock.Setup(p => p.StreamCompletionAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionRequest, CancellationToken>((r, _) => capturedRequest = r)
            .Returns(AsyncEnumerable.Empty<LlmDelta>());

        var request = new ChatRequest { Input = "Hello", Temperature = 0.3, MaxTokens = 200 };
        await foreach (var _ in _sut.StreamResponseAsync(request)) { }

        Assert.NotNull(capturedRequest);
        Assert.Equal(2, capturedRequest.Messages.Count);
        Assert.Equal(0.3f, capturedRequest.Options?.Temperature);
        Assert.Equal(200, capturedRequest.Options?.MaxTokens);
    }

    private void SetupBasicContext()
    {
        var context = new ChatContext
        {
            State = new StateContext(),
            AvailableCommands = Array.Empty<CommandContext>(),
            RecentRuns = Array.Empty<RunHistoryContext>(),
            IsSuccess = true
        };
        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
    }

    private void SetupFailingContext(string errorMessage)
    {
        var context = new ChatContext
        {
            State = new StateContext(),
            AvailableCommands = Array.Empty<CommandContext>(),
            RecentRuns = Array.Empty<RunHistoryContext>(),
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
        _contextAssemblyMock.Setup(c => c.AssembleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
    }

    private void SetupLlmResponse(string content)
    {
        _llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLlmResponse(content));
    }

    private static LlmCompletionResponse CreateLlmResponse(string content, int promptTokens = 0, int completionTokens = 0)
    {
        return new LlmCompletionResponse
        {
            Message = new LlmMessage { Role = LlmMessageRole.Assistant, Content = content },
            Model = "test-model",
            Provider = "test-provider",
            Usage = new LlmTokenUsage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens
            }
        };
    }
}
