using Gmsd.Agents.Execution.ControlPlane.Tools.Registry;
using Gmsd.Agents.Execution.ToolCalling;
using Gmsd.Aos.Contracts.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ToolCalling;

/// <summary>
/// Unit tests for the ToolCallingLoop class.
/// </summary>
public class ToolCallingLoopTests
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolCallingEventEmitter _eventEmitter;
    private readonly ToolCallingLoop _loop;

    public ToolCallingLoopTests()
    {
        _chatCompletionService = Substitute.For<IChatCompletionService>();
        _toolRegistry = Substitute.For<IToolRegistry>();
        _eventEmitter = Substitute.For<IToolCallingEventEmitter>();
        _loop = new ToolCallingLoop(_chatCompletionService, _toolRegistry, _eventEmitter);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoToolCalls_ReturnsCompletedNaturally()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Hello") },
            Tools = Array.Empty<ToolCallingToolDefinition>()
        };

        var response = CreateChatMessageContent("Hello! How can I help you?");
        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(response);

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert
        Assert.Equal(ToolCallingCompletionReason.CompletedNaturally, result.CompletionReason);
        Assert.Equal(1, result.IterationCount);
        Assert.Equal("Hello! How can I help you?", result.FinalMessage.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolCallRequested_ExecutesToolAndReturnsResult()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("What's 2+2?") },
            Tools = new[]
            {
                new ToolCallingToolDefinition
                {
                    Name = "calculator",
                    Description = "Performs calculations",
                    ParametersSchema = new { type = "object", properties = new { expression = new { type = "string" } } }
                }
            }
        };

        var toolCall = new FunctionCallContent(
            id: "call-1",
            functionName: "calculator",
            pluginName: string.Empty,
            arguments: new KernelArguments { ["expression"] = "2+2" });

        var firstResponse = CreateChatMessageContentWithToolCall("I'll calculate that.", toolCall);
        var finalResponse = CreateChatMessageContent("The result is 4.");

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(firstResponse, finalResponse);

        var mockTool = Substitute.For<ITool>();
        mockTool.InvokeAsync(
            Arg.Any<ToolRequest>(),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success(4));

        _toolRegistry.ResolveByName("calculator").Returns(mockTool);

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert
        Assert.Equal(ToolCallingCompletionReason.CompletedNaturally, result.CompletionReason);
        Assert.Equal(2, result.IterationCount);
        Assert.Equal(2, result.ConversationHistory.Count(m => m.Role == ToolCallingRole.Assistant));
        Assert.Single(result.ConversationHistory.Where(m => m.Role == ToolCallingRole.Tool));

        await mockTool.Received(1).InvokeAsync(
            Arg.Is<ToolRequest>(r => r.Operation == "calculator"),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMaxIterationsReached_ReturnsMaxIterationsReason()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Keep calling tools") },
            Tools = new[]
            {
                new ToolCallingToolDefinition
                {
                    Name = "infinite_tool",
                    Description = "Always requests another tool call",
                    ParametersSchema = new { }
                }
            },
            Options = new ToolCallingOptions { MaxIterations = 3 }
        };

        // Always return a tool call to create an infinite loop scenario
        var toolCall = new FunctionCallContent(
            id: "call-1",
            functionName: "infinite_tool",
            pluginName: string.Empty,
            arguments: new KernelArguments());

        var mockTool = Substitute.For<ITool>();
        mockTool.InvokeAsync(
            Arg.Any<ToolRequest>(),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("done"));

        _toolRegistry.ResolveByName("infinite_tool").Returns(mockTool);

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(CreateChatMessageContentWithToolCall("Calling tool...", toolCall));

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert
        Assert.Equal(ToolCallingCompletionReason.MaxIterationsReached, result.CompletionReason);
        Assert.Equal(3, result.IterationCount);
        Assert.NotNull(result.Error);
        Assert.Equal("MaxIterationsReached", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolNotFound_ReturnsToolNotFoundError()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Call missing tool") },
            Tools = new[]
            {
                new ToolCallingToolDefinition
                {
                    Name = "missing_tool",
                    Description = "This tool doesn't exist",
                    ParametersSchema = new { }
                }
            }
        };

        var toolCall = new FunctionCallContent(
            id: "call-1",
            functionName: "missing_tool",
            pluginName: string.Empty,
            arguments: new KernelArguments());

        var response = CreateChatMessageContentWithToolCall("I'll try to call it.", toolCall);
        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(response);

        _toolRegistry.ResolveByName("missing_tool").Returns((ITool?)null);

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert
        Assert.Single(result.ConversationHistory.Where(m => m.Role == ToolCallingRole.Tool));
        var toolMessage = result.ConversationHistory.First(m => m.Role == ToolCallingRole.Tool);
        Assert.Contains("error", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolFails_ReturnsErrorResult()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Call failing tool") },
            Tools = new[]
            {
                new ToolCallingToolDefinition
                {
                    Name = "failing_tool",
                    Description = "This tool fails",
                    ParametersSchema = new { }
                }
            }
        };

        var toolCall = new FunctionCallContent(
            id: "call-1",
            functionName: "failing_tool",
            pluginName: string.Empty,
            arguments: new KernelArguments());

        var response = CreateChatMessageContentWithToolCall("I'll try.", toolCall);
        var finalResponse = CreateChatMessageContent("The tool failed.");

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(response, finalResponse);

        var mockTool = Substitute.For<ITool>();
        mockTool.InvokeAsync(
            Arg.Any<ToolRequest>(),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>())
            .Returns(ToolResult.Failure("ToolError", "Something went wrong"));

        _toolRegistry.ResolveByName("failing_tool").Returns(mockTool);

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert
        Assert.Single(result.ConversationHistory.Where(m => m.Role == ToolCallingRole.Tool));
        var toolMessage = result.ConversationHistory.First(m => m.Role == ToolCallingRole.Tool);
        Assert.Contains("error", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ToolCallingCompletionReason.CompletedNaturally, result.CompletionReason);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleToolCalls_ExecutesAllTools()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Get weather and time") },
            Tools = new[]
            {
                new ToolCallingToolDefinition
                {
                    Name = "get_weather",
                    Description = "Gets weather",
                    ParametersSchema = new { }
                },
                new ToolCallingToolDefinition
                {
                    Name = "get_time",
                    Description = "Gets time",
                    ParametersSchema = new { }
                }
            }
        };

        var toolCall1 = new FunctionCallContent(
            id: "call-1",
            functionName: "get_weather",
            pluginName: string.Empty,
            arguments: new KernelArguments());

        var toolCall2 = new FunctionCallContent(
            id: "call-2",
            functionName: "get_time",
            pluginName: string.Empty,
            arguments: new KernelArguments());

        var response = CreateChatMessageContent(
            "I'll get that information.",
            new[] { toolCall1, toolCall2 });
        var finalResponse = CreateChatMessageContent("It's sunny and 2 PM.");

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(response, finalResponse);

        var weatherTool = Substitute.For<ITool>();
        weatherTool.InvokeAsync(
            Arg.Is<ToolRequest>(r => r.Operation == "get_weather"),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success(new { weather = "sunny" }));

        var timeTool = Substitute.For<ITool>();
        timeTool.InvokeAsync(
            Arg.Is<ToolRequest>(r => r.Operation == "get_time"),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success(new { time = "2 PM" }));

        _toolRegistry.ResolveByName("get_weather").Returns(weatherTool);
        _toolRegistry.ResolveByName("get_time").Returns(timeTool);

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert
        Assert.Equal(ToolCallingCompletionReason.CompletedNaturally, result.CompletionReason);
        Assert.Equal(2, result.IterationCount);
        Assert.Equal(2, result.ConversationHistory.Count(m => m.Role == ToolCallingRole.Tool));

        await weatherTool.Received(1).InvokeAsync(
            Arg.Is<ToolRequest>(r => r.Operation == "get_weather"),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>());

        await timeTool.Received(1).InvokeAsync(
            Arg.Is<ToolRequest>(r => r.Operation == "get_time"),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_EmitsEventsDuringExecution()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Test") },
            Tools = new[]
            {
                new ToolCallingToolDefinition
                {
                    Name = "test_tool",
                    Description = "Test tool",
                    ParametersSchema = new { }
                }
            },
            CorrelationId = "test-correlation-id"
        };

        var toolCall = new FunctionCallContent(
            id: "call-1",
            functionName: "test_tool",
            pluginName: string.Empty,
            arguments: new KernelArguments());

        var response = CreateChatMessageContentWithToolCall("Testing...", toolCall);
        var finalResponse = CreateChatMessageContent("Done.");

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(response, finalResponse);

        var mockTool = Substitute.For<ITool>();
        mockTool.InvokeAsync(
            Arg.Any<ToolRequest>(),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("result"));

        _toolRegistry.ResolveByName("test_tool").Returns(mockTool);

        // Act
        await _loop.ExecuteAsync(request);

        // Assert
        _eventEmitter.Received().Emit(Arg.Is<ToolCallDetectedEvent>(e =>
            e.CorrelationId == "test-correlation-id" &&
            e.Iteration == 1 &&
            e.ToolCalls.Count == 1));

        _eventEmitter.Received().Emit(Arg.Is<ToolCallStartedEvent>(e =>
            e.CorrelationId == "test-correlation-id" &&
            e.Iteration == 1));

        _eventEmitter.Received().Emit(Arg.Is<ToolCallCompletedEvent>(e =>
            e.CorrelationId == "test-correlation-id" &&
            e.Iteration == 1));

        _eventEmitter.Received().Emit(Arg.Is<ToolResultsSubmittedEvent>(e =>
            e.CorrelationId == "test-correlation-id" &&
            e.Iteration == 1));

        _eventEmitter.Received().Emit(Arg.Is<ToolLoopCompletedEvent>(e =>
            e.CorrelationId == "test-correlation-id" &&
            e.TotalIterations == 2 &&
            e.TotalToolCalls == 1 &&
            e.CompletionReason == ToolCallingCompletionReason.CompletedNaturally));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Test") }
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _loop.ExecuteAsync(request, cts.Token));
    }

    [Fact]
    public void Constructor_WithNullChatCompletionService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ToolCallingLoop(null!, _toolRegistry, _eventEmitter));
    }

    [Fact]
    public void Constructor_WithNullToolRegistry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ToolCallingLoop(_chatCompletionService, null!, _eventEmitter));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _loop.ExecuteAsync(null!));
    }

    #region Helper Methods

    private static ChatMessageContent CreateChatMessageContent(string content)
    {
        return new ChatMessageContent(AuthorRole.Assistant, content);
    }

    private static ChatMessageContent CreateChatMessageContentWithToolCall(
        string content,
        FunctionCallContent toolCall)
    {
        return CreateChatMessageContent(content, new[] { toolCall });
    }

    private static ChatMessageContent CreateChatMessageContent(
        string content,
        IEnumerable<FunctionCallContent> toolCalls)
    {
        var chatMessage = new ChatMessageContent(AuthorRole.Assistant, content);
        foreach (var toolCall in toolCalls)
        {
            chatMessage.Items.Add(toolCall);
        }
        return chatMessage;
    }

    #endregion
}
