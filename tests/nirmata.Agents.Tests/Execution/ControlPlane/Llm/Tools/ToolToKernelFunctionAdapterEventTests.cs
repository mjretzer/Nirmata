using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane.Llm.Tools;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;
using nirmata.Aos.Contracts.Tools;
using Microsoft.SemanticKernel;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ControlPlane.Llm.Tools;

/// <summary>
/// Tests for ToolToKernelFunctionAdapter tool event emission functionality.
/// </summary>
public class ToolToKernelFunctionAdapterEventTests
{
    #region Fake Tool Implementation

    private sealed class FakeTool : ITool
    {
        private readonly ToolResult? _result;
        private readonly Exception? _exception;
        public ToolDescriptor Descriptor { get; }

        public FakeTool(ToolResult result, ToolDescriptor? descriptor = null)
        {
            _result = result;
            Descriptor = descriptor ?? CreateTestDescriptor();
        }

        public FakeTool(Exception exception, ToolDescriptor? descriptor = null)
        {
            _exception = exception;
            Descriptor = descriptor ?? CreateTestDescriptor();
        }

        public Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
        {
            if (_exception != null)
                throw _exception;
            return Task.FromResult(_result!);
        }

        private static ToolDescriptor CreateTestDescriptor()
        {
            return new ToolDescriptor
            {
                Id = "test-tool",
                Name = "TestTool",
                Description = "A test tool for unit tests",
                Parameters = new List<ToolParameter>()
            };
        }
    }

    private sealed class FakeToolEventSink : IToolEventSink
    {
        public List<ToolCallEvent> Calls { get; } = new();
        public List<ToolResultEvent> Results { get; } = new();

        public void EmitToolCall(string callId, string toolName, Dictionary<string, object>? parameters = null, string? phaseContext = null, string? correlationId = null)
        {
            Calls.Add(new ToolCallEvent
            {
                CallId = callId,
                ToolName = toolName,
                Parameters = parameters,
                PhaseContext = phaseContext,
                CorrelationId = correlationId
            });
        }

        public void EmitToolResult(string callId, bool success, object? result = null, string? error = null, long durationMs = 0, string? correlationId = null)
        {
            Results.Add(new ToolResultEvent
            {
                CallId = callId,
                Success = success,
                Result = result,
                Error = error,
                DurationMs = durationMs,
                CorrelationId = correlationId
            });
        }
    }

    private sealed class ToolCallEvent
    {
        public required string CallId { get; init; }
        public required string ToolName { get; init; }
        public Dictionary<string, object>? Parameters { get; init; }
        public string? PhaseContext { get; init; }
        public string? CorrelationId { get; init; }
    }

    private sealed class ToolResultEvent
    {
        public required string CallId { get; init; }
        public required bool Success { get; init; }
        public object? Result { get; init; }
        public string? Error { get; init; }
        public required long DurationMs { get; init; }
        public string? CorrelationId { get; init; }
    }

    #endregion

    [Fact]
    public async Task FromITool_WithEventSink_EmitsToolCallAndResult()
    {
        // Arrange
        var expectedResult = ToolResult.Success("test output");
        var fakeTool = new FakeTool(expectedResult);
        var eventSink = new FakeToolEventSink();

        var kernelFunction = ToolToKernelFunctionAdapter.FromITool(fakeTool, fakeTool.Descriptor);

        var kernel = new Microsoft.SemanticKernel.Kernel();
        var arguments = new Microsoft.SemanticKernel.KernelArguments
        {
            ["EventSink"] = eventSink,
            ["CorrelationId"] = "test-corr-123",
            ["PhaseContext"] = "Executor"
        };

        // Act
        var result = await kernelFunction.InvokeAsync(kernel, arguments);

        // Assert
        eventSink.Calls.Should().HaveCount(1);
        eventSink.Results.Should().HaveCount(1);

        var call = eventSink.Calls[0];
        call.ToolName.Should().Be("TestTool");
        call.CorrelationId.Should().Be("test-corr-123");
        call.PhaseContext.Should().Be("Executor");
        call.CallId.Should().NotBeNullOrEmpty();

        var toolResult = eventSink.Results[0];
        toolResult.Success.Should().BeTrue();
        toolResult.CallId.Should().Be(call.CallId, "callId should match between call and result");
        toolResult.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task FromITool_WhenToolFails_EmitsFailedToolResult()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Tool execution failed");
        var fakeTool = new FakeTool(expectedException);
        var eventSink = new FakeToolEventSink();

        var kernelFunction = ToolToKernelFunctionAdapter.FromITool(fakeTool, fakeTool.Descriptor);

        var kernel = new Microsoft.SemanticKernel.Kernel();
        var arguments = new Microsoft.SemanticKernel.KernelArguments
        {
            ["EventSink"] = eventSink
        };

        // Act
        try
        {
            await kernelFunction.InvokeAsync(kernel, arguments);
        }
        catch (KernelException)
        {
            // Expected
        }

        // Assert
        eventSink.Calls.Should().HaveCount(1);
        eventSink.Results.Should().HaveCount(1);

        var call = eventSink.Calls[0];
        var result = eventSink.Results[0];

        result.Success.Should().BeFalse();
        result.CallId.Should().Be(call.CallId);
        result.Error.Should().Contain("Tool execution failed");
        result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task FromITool_WithoutEventSink_DoesNotThrow()
    {
        // Arrange
        var expectedResult = ToolResult.Success("test output");
        var fakeTool = new FakeTool(expectedResult);

        var kernelFunction = ToolToKernelFunctionAdapter.FromITool(fakeTool, fakeTool.Descriptor);

        var kernel = new Microsoft.SemanticKernel.Kernel();
        var arguments = new Microsoft.SemanticKernel.KernelArguments();

        // Act & Assert (should not throw)
        var result = await kernelFunction.InvokeAsync(kernel, arguments);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FromITool_WithParameters_EmitsParametersInCallEvent()
    {
        // Arrange
        var descriptor = new ToolDescriptor
        {
            Id = "test-tool-params",
            Name = "TestToolWithParams",
            Description = "A test tool with parameters",
            Parameters = new List<ToolParameter>
            {
                new() { Name = "input", Type = "string", Required = true, Description = "Input parameter" },
                new() { Name = "count", Type = "integer", Required = false, Description = "Count parameter" }
            }
        };

        var expectedResult = ToolResult.Success(new { output = "processed" });
        var fakeTool = new FakeTool(expectedResult, descriptor);
        var eventSink = new FakeToolEventSink();

        var kernelFunction = ToolToKernelFunctionAdapter.FromITool(fakeTool, descriptor);

        var kernel = new Microsoft.SemanticKernel.Kernel();
        var arguments = new Microsoft.SemanticKernel.KernelArguments
        {
            ["EventSink"] = eventSink,
            ["input"] = "test data",
            ["count"] = 42
        };

        // Act
        await kernelFunction.InvokeAsync(kernel, arguments);

        // Assert
        eventSink.Calls.Should().HaveCount(1);
        var call = eventSink.Calls[0];
        call.Parameters.Should().NotBeNull();
        call.Parameters.Should().ContainKey("input");
        call.Parameters!["input"].Should().Be("test data");
        call.Parameters.Should().ContainKey("count");
        call.Parameters["count"].Should().Be(42);
    }

    [Fact]
    public async Task FromITool_MultipleCalls_GenerateUniqueCallIds()
    {
        // Arrange
        var expectedResult = ToolResult.Success("test output");
        var fakeTool = new FakeTool(expectedResult);
        var eventSink = new FakeToolEventSink();

        var kernelFunction = ToolToKernelFunctionAdapter.FromITool(fakeTool, fakeTool.Descriptor);

        var kernel = new Microsoft.SemanticKernel.Kernel();

        // Act - invoke twice
        var arguments1 = new Microsoft.SemanticKernel.KernelArguments
        {
            ["EventSink"] = eventSink
        };
        var arguments2 = new Microsoft.SemanticKernel.KernelArguments
        {
            ["EventSink"] = eventSink
        };

        await kernelFunction.InvokeAsync(kernel, arguments1);
        await kernelFunction.InvokeAsync(kernel, arguments2);

        // Assert
        eventSink.Calls.Should().HaveCount(2);
        eventSink.Results.Should().HaveCount(2);

        var callId1 = eventSink.Calls[0].CallId;
        var callId2 = eventSink.Calls[1].CallId;

        callId1.Should().NotBe(callId2, "each call should have a unique callId");

        // Verify each result matches its call
        eventSink.Results[0].CallId.Should().Be(callId1);
        eventSink.Results[1].CallId.Should().Be(callId2);
    }
}
