using System.Collections.Concurrent;
using System.Diagnostics;
using nirmata.Agents.Execution.ControlPlane.Tools.Registry;
using nirmata.Agents.Execution.ToolCalling;
using nirmata.Aos.Contracts.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ToolCalling;

/// <summary>
/// Stress and load tests for the ToolCallingLoop class.
/// Covers parallel execution, max iterations enforcement, and timeout handling.
/// </summary>
public class ToolCallingStressTests
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolCallingEventEmitter _eventEmitter;
    private readonly ToolCallingLoop _loop;

    public ToolCallingStressTests()
    {
        _chatCompletionService = Substitute.For<IChatCompletionService>();
        _toolRegistry = Substitute.For<IToolRegistry>();
        _eventEmitter = Substitute.For<IToolCallingEventEmitter>();
        _loop = new ToolCallingLoop(_chatCompletionService, _toolRegistry, _eventEmitter);
    }

    #region Load Tests - Parallel Tool Execution (Task 9.2)

    [Fact]
    public async Task ExecuteAsync_With32ConcurrentToolCalls_ExecutesAllInParallel()
    {
        // Arrange - Create 32 tool calls that the LLM will request
        const int toolCount = 32;
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Execute 32 operations in parallel") },
            Tools = Enumerable.Range(0, toolCount)
                .Select(i => new ToolCallingToolDefinition
                {
                    Name = $"operation_{i}",
                    Description = $"Operation {i}",
                    ParametersSchema = new { }
                })
                .ToArray(),
            Options = new ToolCallingOptions
            {
                EnableParallelToolExecution = true,
                MaxParallelToolExecutions = 32
            }
        };

        // Create 32 tool calls
        var toolCalls = Enumerable.Range(0, toolCount)
            .Select(i => new FunctionCallContent(
                id: $"call-{i}",
                functionName: $"operation_{i}",
                pluginName: string.Empty,
                arguments: new KernelArguments()))
            .ToList();

        var response = CreateChatMessageContent(
            "Executing 32 operations in parallel...",
            toolCalls);

        var finalResponse = CreateChatMessageContent("All 32 operations completed.");

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(response, finalResponse);

        // Setup 32 mock tools with delayed execution to verify parallelism
        var executionOrder = new List<string>();
        var semaphore = new SemaphoreSlim(32, 32);
        var toolsStarted = new ConcurrentBag<string>();
        var startTime = DateTime.UtcNow;

        for (int i = 0; i < toolCount; i++)
        {
            var toolIndex = i;
            var mockTool = Substitute.For<ITool>();
            mockTool.InvokeAsync(
                Arg.Is<ToolRequest>(r => r.Operation == $"operation_{toolIndex}"),
                Arg.Any<ToolContext>(),
                Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    toolsStarted.Add($"operation_{toolIndex}");
                    await Task.Delay(50); // 50ms delay to simulate work
                    executionOrder.Add($"operation_{toolIndex}");
                    return ToolResult.Success(new { index = toolIndex });
                });

            _toolRegistry.ResolveByName($"operation_{toolIndex}").Returns(mockTool);
        }

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert
        var elapsed = DateTime.UtcNow - startTime;

        // All tools should have completed
        Assert.Equal(toolCount, result.ConversationHistory.Count(m => m.Role == ToolCallingRole.Tool));

        // With parallel execution, all 32 tools (50ms each) should complete in ~50-200ms, not 1600ms
        Assert.True(elapsed.TotalMilliseconds < 1000,
            $"Parallel execution took {elapsed.TotalMilliseconds}ms, expected < 1000ms for 32 concurrent tools");

        // All tools should have been resolved
        for (int i = 0; i < toolCount; i++)
        {
            _toolRegistry.Received(1).ResolveByName($"operation_{i}");
        }

        // Verify all tools reported started
        Assert.Equal(toolCount, toolsStarted.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ParallelExecutionWithSemaphore_ThrottlesCorrectly()
    {
        // Arrange - Create more tool calls than the max parallel limit
        const int toolCount = 20;
        const int maxParallel = 5;
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Execute 20 operations with throttle of 5") },
            Tools = Enumerable.Range(0, toolCount)
                .Select(i => new ToolCallingToolDefinition
                {
                    Name = $"operation_{i}",
                    Description = $"Operation {i}",
                    ParametersSchema = new { }
                })
                .ToArray(),
            Options = new ToolCallingOptions
            {
                EnableParallelToolExecution = true,
                MaxParallelToolExecutions = maxParallel
            }
        };

        var toolCalls = Enumerable.Range(0, toolCount)
            .Select(i => new FunctionCallContent(
                id: $"call-{i}",
                functionName: $"operation_{i}",
                pluginName: string.Empty,
                arguments: new KernelArguments()))
            .ToList();

        var response = CreateChatMessageContent(
            "Executing 20 operations with throttling...",
            toolCalls);

        var finalResponse = CreateChatMessageContent("All operations completed.");

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(response, finalResponse);

        // Track concurrent execution
        var concurrentExecutions = 0;
        var maxConcurrentObserved = 0;
        var lockObj = new object();

        for (int i = 0; i < toolCount; i++)
        {
            var toolIndex = i;
            var mockTool = Substitute.For<ITool>();
            mockTool.InvokeAsync(
                Arg.Is<ToolRequest>(r => r.Operation == $"operation_{toolIndex}"),
                Arg.Any<ToolContext>(),
                Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    lock (lockObj)
                    {
                        concurrentExecutions++;
                        maxConcurrentObserved = Math.Max(maxConcurrentObserved, concurrentExecutions);
                    }

                    await Task.Delay(50);

                    lock (lockObj)
                    {
                        concurrentExecutions--;
                    }

                    return ToolResult.Success(new { index = toolIndex });
                });

            _toolRegistry.ResolveByName($"operation_{toolIndex}").Returns(mockTool);
        }

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert
        Assert.True(maxConcurrentObserved <= maxParallel,
            $"Max concurrent executions ({maxConcurrentObserved}) exceeded limit ({maxParallel})");
        Assert.Equal(toolCount, result.ConversationHistory.Count(m => m.Role == ToolCallingRole.Tool));
    }

    [Fact]
    public async Task ExecuteAsync_ParallelToolExecution_AllSucceedDespiteIndividualDelays()
    {
        // Arrange
        const int toolCount = 16;
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Execute operations with varying delays") },
            Tools = Enumerable.Range(0, toolCount)
                .Select(i => new ToolCallingToolDefinition
                {
                    Name = $"delayed_op_{i}",
                    Description = $"Delayed operation {i}",
                    ParametersSchema = new { }
                })
                .ToArray(),
            Options = new ToolCallingOptions
            {
                EnableParallelToolExecution = true,
                MaxParallelToolExecutions = 32
            }
        };

        var toolCalls = Enumerable.Range(0, toolCount)
            .Select(i => new FunctionCallContent(
                id: $"call-{i}",
                functionName: $"delayed_op_{i}",
                pluginName: string.Empty,
                arguments: new KernelArguments()))
            .ToList();

        var response = CreateChatMessageContent("Executing...", toolCalls);
        var finalResponse = CreateChatMessageContent("Done.");

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(response, finalResponse);

        // Each tool has a different delay (1ms to 100ms)
        for (int i = 0; i < toolCount; i++)
        {
            var toolIndex = i;
            var delayMs = (i + 1) * 5; // 5ms, 10ms, 15ms, ...
            var mockTool = Substitute.For<ITool>();
            mockTool.InvokeAsync(
                Arg.Is<ToolRequest>(r => r.Operation == $"delayed_op_{toolIndex}"),
                Arg.Any<ToolContext>(),
                Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    await Task.Delay(delayMs);
                    return ToolResult.Success(new { index = toolIndex, delayMs });
                });

            _toolRegistry.ResolveByName($"delayed_op_{toolIndex}").Returns(mockTool);
        }

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert
        Assert.Equal(ToolCallingCompletionReason.CompletedNaturally, result.CompletionReason);
        Assert.Equal(2, result.IterationCount);
        Assert.Equal(toolCount, result.ConversationHistory.Count(m => m.Role == ToolCallingRole.Tool));

        // All tool messages should contain success results
        foreach (var toolMessage in result.ConversationHistory.Where(m => m.Role == ToolCallingRole.Tool))
        {
            Assert.DoesNotContain("error", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion

    #region Stress Tests - Max Iterations Enforcement (Task 9.3)

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public async Task ExecuteAsync_MaxIterationsEnforcement_StopsAtLimit(int maxIterations)
    {
        // Arrange - Tool always requests another tool call, creating an infinite loop scenario
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Keep calling tools") },
            Tools = new[]
            {
                new ToolCallingToolDefinition
                {
                    Name = "recursive_tool",
                    Description = "Always requests another tool call",
                    ParametersSchema = new { }
                }
            },
            Options = new ToolCallingOptions { MaxIterations = maxIterations }
        };

        // Always return a tool call to create an infinite loop scenario
        var toolCall = new FunctionCallContent(
            id: "call-recursive",
            functionName: "recursive_tool",
            pluginName: string.Empty,
            arguments: new KernelArguments());

        var mockTool = Substitute.For<ITool>();
        mockTool.InvokeAsync(
            Arg.Any<ToolRequest>(),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("step completed"));

        _toolRegistry.ResolveByName("recursive_tool").Returns(mockTool);

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
        Assert.Equal(maxIterations, result.IterationCount);
        Assert.NotNull(result.Error);
        Assert.Equal("MaxIterationsReached", result.Error!.Code);

        // Should have maxIterations assistant messages (one per iteration)
        Assert.Equal(maxIterations, result.ConversationHistory.Count(m => m.Role == ToolCallingRole.Assistant));

        // Should have maxIterations tool messages (one per iteration, since every turn has a tool call)
        Assert.Equal(maxIterations, result.ConversationHistory.Count(m => m.Role == ToolCallingRole.Tool));
    }

    [Fact]
    public async Task ExecuteAsync_MaxIterationsWithZeroIterationsAllowed_FailsImmediately()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Test") },
            Tools = Array.Empty<ToolCallingToolDefinition>(),
            Options = new ToolCallingOptions { MaxIterations = 0 }
        };

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert - With 0 iterations, should fail immediately without calling LLM
        Assert.Equal(ToolCallingCompletionReason.MaxIterationsReached, result.CompletionReason);
        Assert.Equal(0, result.IterationCount);
        Assert.NotNull(result.Error);

        // Should not have called the LLM at all
        await _chatCompletionService.DidNotReceive().GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RapidIterations_HighFrequencyLoop_StopsAtMax()
    {
        // Arrange - Very fast tool calls that could cause rapid iteration
        const int maxIterations = 100;
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Rapid fire") },
            Tools = new[]
            {
                new ToolCallingToolDefinition
                {
                    Name = "fast_tool",
                    Description = "Completes instantly",
                    ParametersSchema = new { }
                }
            },
            Options = new ToolCallingOptions
            {
                MaxIterations = maxIterations,
                EnableParallelToolExecution = false // Sequential for simplicity
            }
        };

        var toolCall = new FunctionCallContent(
            id: "fast-call",
            functionName: "fast_tool",
            pluginName: string.Empty,
            arguments: new KernelArguments());

        var mockTool = Substitute.For<ITool>();
        mockTool.InvokeAsync(
            Arg.Any<ToolRequest>(),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ToolResult.Success("done")));

        _toolRegistry.ResolveByName("fast_tool").Returns(mockTool);

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(CreateChatMessageContentWithToolCall("Fast", toolCall));

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _loop.ExecuteAsync(request);

        stopwatch.Stop();

        // Assert
        Assert.Equal(ToolCallingCompletionReason.MaxIterationsReached, result.CompletionReason);
        Assert.Equal(maxIterations, result.IterationCount);

        // Should complete reasonably fast even with 100 iterations
        Assert.True(stopwatch.ElapsedMilliseconds < 5000,
            $"100 iterations took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Timeout Handling Tests (Task 9.4)

    [Fact]
    public async Task ExecuteAsync_TimeoutDuringLlmCall_ReturnsTimeoutResult()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Slow LLM request") },
            Tools = Array.Empty<ToolCallingToolDefinition>(),
            Options = new ToolCallingOptions { Timeout = TimeSpan.FromMilliseconds(50) }
        };

        // LLM takes longer than the timeout
        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                await Task.Delay(200, ct); // 200ms delay, longer than 50ms timeout
                return CreateChatMessageContent("This should not be reached");
            });

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert
        Assert.Equal(ToolCallingCompletionReason.Timeout, result.CompletionReason);
        Assert.NotNull(result.Error);
        Assert.Equal("Timeout", result.Error!.Code);
        Assert.Contains("50ms", result.Error.Message);
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutDuringToolExecution_ReturnsTimeoutResult()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Call slow tool") },
            Tools = new[]
            {
                new ToolCallingToolDefinition
                {
                    Name = "slow_tool",
                    Description = "Takes a long time",
                    ParametersSchema = new { }
                }
            },
            Options = new ToolCallingOptions
            {
                Timeout = TimeSpan.FromMilliseconds(100),
                EnableParallelToolExecution = false
            }
        };

        var toolCall = new FunctionCallContent(
            id: "slow-call",
            functionName: "slow_tool",
            pluginName: string.Empty,
            arguments: new KernelArguments());

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(CreateChatMessageContentWithToolCall("Calling slow tool...", toolCall));

        // Tool takes longer than timeout
        var mockTool = Substitute.For<ITool>();
        mockTool.InvokeAsync(
            Arg.Any<ToolRequest>(),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                await Task.Delay(500, ct); // 500ms delay, longer than 100ms timeout
                return ToolResult.Success("done");
            });

        _toolRegistry.ResolveByName("slow_tool").Returns(mockTool);

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert - Should timeout during tool execution
        Assert.Equal(ToolCallingCompletionReason.Timeout, result.CompletionReason);
        Assert.NotNull(result.Error);
        Assert.Equal("Timeout", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutAcrossMultipleIterations_CumulativeTimeout()
    {
        // Arrange - Multiple iterations, cumulative time exceeds timeout
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Multiple iterations") },
            Tools = new[]
            {
                new ToolCallingToolDefinition
                {
                    Name = "quick_tool",
                    Description = "Quick tool",
                    ParametersSchema = new { }
                }
            },
            Options = new ToolCallingOptions
            {
                Timeout = TimeSpan.FromMilliseconds(200),
                MaxIterations = 100, // High limit, but timeout should stop it
                EnableParallelToolExecution = false
            }
        };

        var toolCall = new FunctionCallContent(
            id: "call-1",
            functionName: "quick_tool",
            pluginName: string.Empty,
            arguments: new KernelArguments());

        var mockTool = Substitute.For<ITool>();
        mockTool.InvokeAsync(
            Arg.Any<ToolRequest>(),
            Arg.Any<ToolContext>(),
            Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                await Task.Delay(25); // Each tool takes 25ms
                return ToolResult.Success("done");
            });

        _toolRegistry.ResolveByName("quick_tool").Returns(mockTool);

        // Each LLM call also takes some time
        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(
                CreateChatMessageContentWithToolCall("Iteration...", toolCall),
                CreateChatMessageContentWithToolCall("Another...", toolCall),
                CreateChatMessageContentWithToolCall("Again...", toolCall),
                CreateChatMessageContentWithToolCall("More...", toolCall),
                CreateChatMessageContentWithToolCall("Keep going...", toolCall),
                CreateChatMessageContentWithToolCall("Almost...", toolCall),
                CreateChatMessageContentWithToolCall("Still...", toolCall),
                CreateChatMessageContentWithToolCall("Yet another...", toolCall),
                CreateChatMessageContentWithToolCall("More to do...", toolCall),
                CreateChatMessageContentWithToolCall("Last one?", toolCall),
                CreateChatMessageContentWithToolCall("Just kidding...", toolCall),
                CreateChatMessageContent("Done"));

        // Act
        var result = await _loop.ExecuteAsync(request);

        // Assert - Should timeout before completing all iterations
        Assert.Equal(ToolCallingCompletionReason.Timeout, result.CompletionReason);
        Assert.True(result.IterationCount < 100, "Should have stopped due to timeout, not max iterations");
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutRespectsCancellationToken_WorksTogether()
    {
        // Arrange - Both timeout and external cancellation
        using var cts = new CancellationTokenSource();
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Dual cancellation test") },
            Tools = Array.Empty<ToolCallingToolDefinition>(),
            Options = new ToolCallingOptions { Timeout = TimeSpan.FromMinutes(5) } // Long timeout
        };

        // Delay then cancel externally
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            cts.Cancel();
        });

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                await Task.Delay(1000, ct); // Long operation
                return CreateChatMessageContent("Done");
            });

        // Act & Assert - Should throw OperationCanceledException from external token
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _loop.ExecuteAsync(request, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_VeryShortTimeout_FailsFast()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Immediate timeout test") },
            Tools = Array.Empty<ToolCallingToolDefinition>(),
            Options = new ToolCallingOptions { Timeout = TimeSpan.FromMilliseconds(1) } // 1ms timeout
        };

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.Arg<CancellationToken>();
                await Task.Delay(100, ct);
                return CreateChatMessageContent("Too late");
            });

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _loop.ExecuteAsync(request);

        stopwatch.Stop();

        // Assert
        Assert.Equal(ToolCallingCompletionReason.Timeout, result.CompletionReason);

        // Should fail very quickly (well under 100ms)
        Assert.True(stopwatch.ElapsedMilliseconds < 50,
            $"1ms timeout test took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutEmitsCorrectEvent()
    {
        // Arrange
        var request = new ToolCallingRequest
        {
            Messages = new[] { ToolCallingMessage.User("Event test") },
            Tools = Array.Empty<ToolCallingToolDefinition>(),
            CorrelationId = "timeout-test-123",
            Options = new ToolCallingOptions { Timeout = TimeSpan.FromMilliseconds(50) }
        };

        _chatCompletionService.GetChatMessageContentAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel?>(),
            Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                await Task.Delay(200);
                return CreateChatMessageContent("Too late");
            });

        // Act
        await _loop.ExecuteAsync(request);

        // Assert - Should emit loop failed event with timeout error
        _eventEmitter.Received().Emit(Arg.Is<ToolLoopFailedEvent>(e =>
            e.CorrelationId == "timeout-test-123" &&
            e.ErrorCode == "Timeout"));
    }

    #endregion

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
