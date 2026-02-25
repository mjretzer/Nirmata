using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane.Llm.Tools;
using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;
using Gmsd.Aos.Contracts.Tools;
using Microsoft.SemanticKernel;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Llm;

/// <summary>
/// Integration tests for tool calling through Semantic Kernel's function invocation.
/// Tests single tool calls, parallel tool calls, tool failure handling, and evidence capture.
/// </summary>
public class ToolCallingTests
{
    #region Fake Tool Implementations

    private sealed class SimpleTool : ITool
    {
        public ToolDescriptor Descriptor { get; }
        private readonly string _result;

        public SimpleTool(string result = "success")
        {
            _result = result;
            Descriptor = new ToolDescriptor
            {
                Id = "simple-tool",
                Name = "SimpleTool",
                Description = "A simple tool that returns a fixed result",
                Parameters = new List<ToolParameter>
                {
                    new() { Name = "input", Type = "string", Required = true, Description = "Input parameter" }
                }
            };
        }

        public Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolResult.Success(_result));
        }
    }

    private sealed class FailingTool : ITool
    {
        public ToolDescriptor Descriptor { get; }
        private readonly string _errorCode;
        private readonly string _errorMessage;

        public FailingTool(string errorCode = "TOOL_ERROR", string errorMessage = "Tool execution failed")
        {
            _errorCode = errorCode;
            _errorMessage = errorMessage;
            Descriptor = new ToolDescriptor
            {
                Id = "failing-tool",
                Name = "FailingTool",
                Description = "A tool that always fails",
                Parameters = new List<ToolParameter>()
            };
        }

        public Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolResult.Failure(_errorCode, _errorMessage));
        }
    }

    private sealed class ExceptionThrowingTool : ITool
    {
        public ToolDescriptor Descriptor { get; }
        private readonly Exception _exception;

        public ExceptionThrowingTool(Exception? exception = null)
        {
            _exception = exception ?? new InvalidOperationException("Tool threw an exception");
            Descriptor = new ToolDescriptor
            {
                Id = "exception-tool",
                Name = "ExceptionTool",
                Description = "A tool that throws an exception",
                Parameters = new List<ToolParameter>()
            };
        }

        public Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class LargeResultTool : ITool
    {
        public ToolDescriptor Descriptor { get; }
        private readonly int _resultSizeBytes;

        public LargeResultTool(int resultSizeBytes = 15000)
        {
            _resultSizeBytes = resultSizeBytes;
            Descriptor = new ToolDescriptor
            {
                Id = "large-result-tool",
                Name = "LargeResultTool",
                Description = "A tool that returns a large result",
                Parameters = new List<ToolParameter>()
            };
        }

        public Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
        {
            var largeData = new string('x', _resultSizeBytes);
            return Task.FromResult(ToolResult.Success(new { data = largeData }));
        }
    }

    private sealed class NestedToolCallTool : ITool
    {
        public ToolDescriptor Descriptor { get; }

        public NestedToolCallTool()
        {
            Descriptor = new ToolDescriptor
            {
                Id = "nested-tool",
                Name = "NestedTool",
                Description = "A tool that simulates calling another tool",
                Parameters = new List<ToolParameter>()
            };
        }

        public Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolResult.Success(new { nested_result = "called another tool" }));
        }
    }

    private sealed class FakeEventSink : IToolEventSink
    {
        public List<(string CallId, string ToolName, Dictionary<string, object>? Parameters)> ToolCalls { get; } = new();
        public List<(string CallId, bool Success, object? Result, string? Error, long DurationMs)> ToolResults { get; } = new();

        public void EmitToolCall(string callId, string toolName, Dictionary<string, object>? parameters = null, string? phaseContext = null, string? correlationId = null)
        {
            ToolCalls.Add((callId, toolName, parameters));
        }

        public void EmitToolResult(string callId, bool success, object? result = null, string? error = null, long durationMs = 0, string? correlationId = null)
        {
            ToolResults.Add((callId, success, result, error, durationMs));
        }
    }

    #endregion

    #region 4.1 Single Tool Call Execution

    [Fact]
    public async Task SingleToolInvocation_ExecutesSuccessfully()
    {
        // Arrange
        var tool = new SimpleTool("test result");
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments { ["input"] = "test input" };

        // Act
        var result = await function.InvokeAsync(kernel, arguments);

        // Assert
        result.Should().NotBeNull();
        result.ToString().Should().Contain("test result");
    }

    [Fact]
    public async Task SingleToolInvocation_WithParameters_PassesParametersCorrectly()
    {
        // Arrange
        var descriptor = new ToolDescriptor
        {
            Id = "param-tool",
            Name = "ParamTool",
            Description = "Tool with parameters",
            Parameters = new List<ToolParameter>
            {
                new() { Name = "param1", Type = "string", Required = true, Description = "First parameter" },
                new() { Name = "param2", Type = "integer", Required = false, Description = "Second parameter" }
            }
        };

        var tool = new SimpleTool("param result");
        var function = ToolToKernelFunctionAdapter.FromITool(tool, descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments
        {
            ["param1"] = "value1",
            ["param2"] = 42
        };

        // Act
        var result = await function.InvokeAsync(kernel, arguments);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SingleToolInvocation_WithEventSink_EmitsCallAndResult()
    {
        // Arrange
        var tool = new SimpleTool("success");
        var eventSink = new FakeEventSink();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments
        {
            ["EventSink"] = eventSink,
            ["input"] = "test"
        };

        // Act
        await function.InvokeAsync(kernel, arguments);

        // Assert
        eventSink.ToolCalls.Should().HaveCount(1);
        eventSink.ToolResults.Should().HaveCount(1);
        eventSink.ToolCalls[0].ToolName.Should().Be("SimpleTool");
        eventSink.ToolResults[0].Success.Should().BeTrue();
    }

    #endregion

    #region 4.2 Parallel Tool Calls

    [Fact]
    public async Task ParallelToolCalls_ExecuteMultipleToolsSimultaneously()
    {
        // Arrange
        var tool1 = new SimpleTool("result1");
        var tool2 = new SimpleTool("result2");
        var tool3 = new SimpleTool("result3");

        var function1 = ToolToKernelFunctionAdapter.FromITool(tool1, tool1.Descriptor);
        var function2 = ToolToKernelFunctionAdapter.FromITool(tool2, tool2.Descriptor);
        var function3 = ToolToKernelFunctionAdapter.FromITool(tool3, tool3.Descriptor);

        var kernel = new Kernel();
        var args1 = new KernelArguments { ["input"] = "input1" };
        var args2 = new KernelArguments { ["input"] = "input2" };
        var args3 = new KernelArguments { ["input"] = "input3" };

        // Act
        var results = await Task.WhenAll(
            function1.InvokeAsync(kernel, args1),
            function2.InvokeAsync(kernel, args2),
            function3.InvokeAsync(kernel, args3)
        );

        // Assert
        results.Should().HaveCount(3);
        results[0].Should().NotBeNull();
        results[1].Should().NotBeNull();
        results[2].Should().NotBeNull();
    }

    [Fact]
    public async Task ParallelToolCalls_WithEventSink_CapturesToolCalls()
    {
        // Arrange
        var tool1 = new SimpleTool("result1");
        var tool2 = new SimpleTool("result2");
        var eventSink = new FakeEventSink();

        var function1 = ToolToKernelFunctionAdapter.FromITool(tool1, tool1.Descriptor);
        var function2 = ToolToKernelFunctionAdapter.FromITool(tool2, tool2.Descriptor);

        var kernel = new Kernel();
        var args1 = new KernelArguments { ["EventSink"] = eventSink, ["input"] = "input1" };
        var args2 = new KernelArguments { ["EventSink"] = eventSink, ["input"] = "input2" };

        // Act
        await Task.WhenAll(
            function1.InvokeAsync(kernel, args1),
            function2.InvokeAsync(kernel, args2)
        );

        // Assert
        eventSink.ToolCalls.Should().HaveCount(2);
        eventSink.ToolResults.Should().HaveCount(2);
        eventSink.ToolCalls.Should().Contain(c => c.ToolName == "SimpleTool");
    }

    #endregion

    #region 4.3 Tool Failure Handling

    [Fact]
    public async Task ToolFailure_WithToolResultFailure_ThrowsKernelException()
    {
        // Arrange
        var tool = new FailingTool("EXECUTION_ERROR", "Tool failed to execute");
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KernelException>(() => function.InvokeAsync(kernel, arguments));
        ex.Message.Should().Contain("EXECUTION_ERROR");
    }

    [Fact]
    public async Task ToolFailure_WithException_ThrowsKernelException()
    {
        // Arrange
        var exception = new InvalidOperationException("Tool threw an exception");
        var tool = new ExceptionThrowingTool(exception);
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KernelException>(() => function.InvokeAsync(kernel, arguments));
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ToolFailure_WithEventSink_EmitsFailureEvent()
    {
        // Arrange
        var tool = new FailingTool("FAILURE", "Tool execution failed");
        var eventSink = new FakeEventSink();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments { ["EventSink"] = eventSink };

        // Act
        try
        {
            await function.InvokeAsync(kernel, arguments);
        }
        catch (KernelException)
        {
            // Expected
        }

        // Assert
        eventSink.ToolCalls.Should().HaveCount(1);
        eventSink.ToolResults.Should().HaveCount(1);
        eventSink.ToolResults[0].Success.Should().BeFalse();
        eventSink.ToolResults[0].Error.Should().Contain("FAILURE");
    }

    [Fact]
    public async Task ToolFailure_WithException_EmitsFailureEvent()
    {
        // Arrange
        var tool = new ExceptionThrowingTool(new TimeoutException("Tool timed out"));
        var eventSink = new FakeEventSink();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments { ["EventSink"] = eventSink };

        // Act
        try
        {
            await function.InvokeAsync(kernel, arguments);
        }
        catch (KernelException)
        {
            // Expected
        }

        // Assert
        eventSink.ToolResults.Should().HaveCount(1);
        eventSink.ToolResults[0].Success.Should().BeFalse();
        eventSink.ToolResults[0].Error.Should().Contain("timed out");
    }

    #endregion

    #region 4.4 Evidence Capture for Tool Interactions

    [Fact]
    public async Task EvidenceCapture_IncludesToolName()
    {
        // Arrange
        var tool = new SimpleTool("result");
        var eventSink = new FakeEventSink();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments { ["EventSink"] = eventSink, ["input"] = "test" };

        // Act
        await function.InvokeAsync(kernel, arguments);

        // Assert
        eventSink.ToolCalls[0].ToolName.Should().Be("SimpleTool");
    }

    [Fact]
    public async Task EvidenceCapture_IncludesArguments()
    {
        // Arrange
        var descriptor = new ToolDescriptor
        {
            Id = "test-tool",
            Name = "TestTool",
            Description = "Test tool",
            Parameters = new List<ToolParameter>
            {
                new() { Name = "arg1", Type = "string", Required = true, Description = "Argument 1" },
                new() { Name = "arg2", Type = "integer", Required = false, Description = "Argument 2" }
            }
        };

        var tool = new SimpleTool("result");
        var eventSink = new FakeEventSink();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments
        {
            ["EventSink"] = eventSink,
            ["arg1"] = "value1",
            ["arg2"] = 42
        };

        // Act
        await function.InvokeAsync(kernel, arguments);

        // Assert
        eventSink.ToolCalls[0].Parameters.Should().NotBeNull();
        eventSink.ToolCalls[0].Parameters.Should().ContainKey("arg1").WhoseValue.Should().Be("value1");
        eventSink.ToolCalls[0].Parameters.Should().ContainKey("arg2").WhoseValue.Should().Be(42);
    }

    [Fact]
    public async Task EvidenceCapture_IncludesCallId()
    {
        // Arrange
        var tool = new SimpleTool("result");
        var eventSink = new FakeEventSink();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments { ["EventSink"] = eventSink, ["input"] = "test" };

        // Act
        await function.InvokeAsync(kernel, arguments);

        // Assert
        eventSink.ToolCalls[0].CallId.Should().NotBeNullOrEmpty();
        eventSink.ToolResults[0].CallId.Should().Be(eventSink.ToolCalls[0].CallId);
    }

    [Fact]
    public async Task EvidenceCapture_IncludesDuration()
    {
        // Arrange
        var tool = new SimpleTool("result");
        var eventSink = new FakeEventSink();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments { ["EventSink"] = eventSink, ["input"] = "test" };

        // Act
        await function.InvokeAsync(kernel, arguments);

        // Assert
        eventSink.ToolResults[0].DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task EvidenceCapture_IncludesResultData()
    {
        // Arrange
        var tool = new SimpleTool("test result data");
        var eventSink = new FakeEventSink();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments { ["EventSink"] = eventSink, ["input"] = "test" };

        // Act
        await function.InvokeAsync(kernel, arguments);

        // Assert
        eventSink.ToolResults[0].Success.Should().BeTrue();
        eventSink.ToolResults[0].Result.Should().NotBeNull();
    }

    [Fact]
    public async Task EvidenceCapture_MultipleToolCalls_GenerateUniqueCallIds()
    {
        // Arrange
        var tool = new SimpleTool("result");
        var eventSink = new FakeEventSink();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();

        // Act
        var args1 = new KernelArguments { ["EventSink"] = eventSink, ["input"] = "test1" };
        var args2 = new KernelArguments { ["EventSink"] = eventSink, ["input"] = "test2" };

        await function.InvokeAsync(kernel, args1);
        await function.InvokeAsync(kernel, args2);

        // Assert
        eventSink.ToolCalls.Should().HaveCount(2);
        eventSink.ToolCalls[0].CallId.Should().NotBe(eventSink.ToolCalls[1].CallId);
    }

    #endregion

    #region Complex Tool Scenarios

    [Fact]
    public async Task ComplexScenario_NestedToolCall_ExecutesSuccessfully()
    {
        // Arrange
        var tool = new NestedToolCallTool();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments();

        // Act
        var result = await function.InvokeAsync(kernel, arguments);

        // Assert
        result.Should().NotBeNull();
        result.ToString().Should().Contain("nested_result");
    }

    [Fact]
    public async Task ComplexScenario_LargeResult_ExecutesSuccessfully()
    {
        // Arrange
        var tool = new LargeResultTool(15000);
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments();

        // Act
        var result = await function.InvokeAsync(kernel, arguments);

        // Assert
        result.Should().NotBeNull();
        result.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ComplexScenario_LargeResult_WithEventSink_CapturesResult()
    {
        // Arrange
        var tool = new LargeResultTool(15000);
        var eventSink = new FakeEventSink();
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments { ["EventSink"] = eventSink };

        // Act
        await function.InvokeAsync(kernel, arguments);

        // Assert
        eventSink.ToolResults.Should().HaveCount(1);
        eventSink.ToolResults[0].Success.Should().BeTrue();
        eventSink.ToolResults[0].Result.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexScenario_ToolFailureFollowedBySuccess_BothCaptured()
    {
        // Arrange
        var failingTool = new FailingTool("ERROR", "First tool failed");
        var successTool = new SimpleTool("success");
        var eventSink = new FakeEventSink();

        var failingFunction = ToolToKernelFunctionAdapter.FromITool(failingTool, failingTool.Descriptor);
        var successFunction = ToolToKernelFunctionAdapter.FromITool(successTool, successTool.Descriptor);

        var kernel = new Kernel();
        var failArgs = new KernelArguments { ["EventSink"] = eventSink };
        var successArgs = new KernelArguments { ["EventSink"] = eventSink, ["input"] = "test" };

        // Act
        try
        {
            await failingFunction.InvokeAsync(kernel, failArgs);
        }
        catch (KernelException)
        {
            // Expected
        }

        await successFunction.InvokeAsync(kernel, successArgs);

        // Assert
        eventSink.ToolCalls.Should().HaveCount(2);
        eventSink.ToolResults.Should().HaveCount(2);
        eventSink.ToolResults[0].Success.Should().BeFalse();
        eventSink.ToolResults[1].Success.Should().BeTrue();
    }

    #endregion

    #region Tool Result Propagation

    [Fact]
    public async Task ToolResultPropagation_SuccessfulResult_ReturnedCorrectly()
    {
        // Arrange
        var expectedResult = new { status = "ok", data = "test data" };
        var tool = new SimpleTool(expectedResult.ToString()!);
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments { ["input"] = "test" };

        // Act
        var result = await function.InvokeAsync(kernel, arguments);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ToolResultPropagation_FailedResult_ThrowsException()
    {
        // Arrange
        var tool = new FailingTool("FAILURE", "Tool failed");
        var function = ToolToKernelFunctionAdapter.FromITool(tool, tool.Descriptor);
        var kernel = new Kernel();
        var arguments = new KernelArguments();

        // Act & Assert
        await Assert.ThrowsAsync<KernelException>(() => function.InvokeAsync(kernel, arguments));
    }

    #endregion
}
