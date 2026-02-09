using Gmsd.Aos.Contracts.Tools;
using Gmsd.Agents.Execution.ControlPlane.Tools.Mcp;
using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;
using Xunit;

namespace Gmsd.Aos.Tests;

public class McpToolAdapterTests
{
    #region 6.3 Unit tests for MCP adapter stub invocation returning normalized results

    [Fact]
    public async Task InvokeAsync_WithSuccessfulMcpResponse_ReturnsSuccessToolResult()
    {
        // Arrange
        var mockClient = new MockMcpClient
        {
            ResponseToReturn = new McpResponse
            {
                IsSuccess = true,
                Data = new { result = "test data", count = 42 }
            }
        };

        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        var adapter = new McpToolAdapter("test-server", "test-tool", descriptor, mockClient);
        var request = new ToolRequest { Operation = "execute" };
        var context = new ToolContext { RunId = "test-run-123" };

        // Act
        var result = await adapter.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task InvokeAsync_WithFailedMcpResponse_ReturnsFailureToolResult()
    {
        // Arrange
        var mockClient = new MockMcpClient
        {
            ResponseToReturn = new McpResponse
            {
                IsSuccess = false,
                ErrorCode = "TOOL_ERROR",
                ErrorMessage = "The tool encountered an error"
            }
        };

        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        var adapter = new McpToolAdapter("test-server", "test-tool", descriptor, mockClient);
        var request = new ToolRequest { Operation = "execute" };
        var context = new ToolContext { RunId = "test-run-123" };

        // Act
        var result = await adapter.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.NotNull(result.Error);
        Assert.Equal("TOOL_ERROR", result.Error.Code);
        Assert.Equal("The tool encountered an error", result.Error.Message);
    }

    [Fact]
    public async Task InvokeAsync_WithMcpException_ReturnsFailureWithMcpErrorCode()
    {
        // Arrange
        var mockClient = new MockMcpClient
        {
            ExceptionToThrow = new McpException("Connection refused")
        };

        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        var adapter = new McpToolAdapter("test-server", "test-tool", descriptor, mockClient);
        var request = new ToolRequest { Operation = "execute" };
        var context = new ToolContext { RunId = "test-run-123" };

        // Act
        var result = await adapter.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.NotNull(result.Error);
        Assert.Equal("MCP_ERROR", result.Error.Code);
        Assert.Contains("Connection refused", result.Error.Message);
    }

    [Fact]
    public async Task InvokeAsync_WithGeneralException_ReturnsFailureWithInvocationErrorCode()
    {
        // Arrange
        var mockClient = new MockMcpClient
        {
            ExceptionToThrow = new InvalidOperationException("Unexpected error occurred")
        };

        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        var adapter = new McpToolAdapter("test-server", "test-tool", descriptor, mockClient);
        var request = new ToolRequest { Operation = "execute" };
        var context = new ToolContext { RunId = "test-run-123" };

        // Act
        var result = await adapter.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.NotNull(result.Error);
        Assert.Equal("INVOCATION_ERROR", result.Error.Code);
        Assert.Contains("Unexpected error occurred", result.Error.Message);
    }

    [Fact]
    public async Task InvokeAsync_TranslatesRequestParameters_ToMcpParameters()
    {
        // Arrange
        var mockClient = new MockMcpClient
        {
            ResponseToReturn = new McpResponse
            {
                IsSuccess = true,
                Data = "success"
            }
        };

        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        var adapter = new McpToolAdapter("test-server", "test-tool", descriptor, mockClient);
        var request = new ToolRequest
        {
            Operation = "execute",
            Parameters = new Dictionary<string, object?>
            {
                { "path", "/test/path" },
                { "recursive", true },
                { "maxDepth", 5 }
            }
        };
        var context = new ToolContext { RunId = "test-run-123" };

        // Act
        await adapter.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(mockClient.LastParameters);
        Assert.Equal("/test/path", mockClient.LastParameters["path"]);
        Assert.Equal(true, mockClient.LastParameters["recursive"]);
        Assert.Equal(5, mockClient.LastParameters["maxDepth"]);
    }

    [Fact]
    public async Task InvokeAsync_PassesCorrectServerAndToolNames_ToMcpClient()
    {
        // Arrange
        var mockClient = new MockMcpClient
        {
            ResponseToReturn = new McpResponse
            {
                IsSuccess = true,
                Data = "success"
            }
        };

        var descriptor = new ToolDescriptor
        {
            Id = "mcp:my-server:my-tool",
            Name = "MyTool",
            Description = "My MCP tool"
        };

        var adapter = new McpToolAdapter("my-server", "my-tool", descriptor, mockClient);
        var request = new ToolRequest { Operation = "execute" };
        var context = new ToolContext { RunId = "test-run-123" };

        // Act
        await adapter.InvokeAsync(request, context);

        // Assert
        Assert.Equal("my-server", mockClient.LastServerName);
        Assert.Equal("my-tool", mockClient.LastToolName);
    }

    [Fact]
    public async Task InvokeAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var mockClient = new MockMcpClient();
        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        var adapter = new McpToolAdapter("test-server", "test-tool", descriptor, mockClient);
        var context = new ToolContext { RunId = "test-run-123" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.InvokeAsync(null!, context));
    }

    [Fact]
    public async Task InvokeAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var mockClient = new MockMcpClient();
        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        var adapter = new McpToolAdapter("test-server", "test-tool", descriptor, mockClient);
        var request = new ToolRequest { Operation = "execute" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.InvokeAsync(request, null!));
    }

    [Fact]
    public async Task InvokeAsync_WithMcpResponseWithoutErrorCode_UsesDefaultMcpErrorCode()
    {
        // Arrange
        var mockClient = new MockMcpClient
        {
            ResponseToReturn = new McpResponse
            {
                IsSuccess = false,
                ErrorCode = null,
                ErrorMessage = "Something went wrong"
            }
        };

        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        var adapter = new McpToolAdapter("test-server", "test-tool", descriptor, mockClient);
        var request = new ToolRequest { Operation = "execute" };
        var context = new ToolContext { RunId = "test-run-123" };

        // Act
        var result = await adapter.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal("MCP_ERROR", result.Error!.Code);
    }

    [Fact]
    public async Task InvokeAsync_WithMcpResponseWithoutErrorMessage_UsesDefaultUnknownErrorMessage()
    {
        // Arrange
        var mockClient = new MockMcpClient
        {
            ResponseToReturn = new McpResponse
            {
                IsSuccess = false,
                ErrorCode = "CUSTOM_ERROR",
                ErrorMessage = null
            }
        };

        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        var adapter = new McpToolAdapter("test-server", "test-tool", descriptor, mockClient);
        var request = new ToolRequest { Operation = "execute" };
        var context = new ToolContext { RunId = "test-run-123" };

        // Act
        var result = await adapter.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal("Unknown MCP error", result.Error!.Message);
    }

    [Fact]
    public void Constructor_WithNullServerName_ThrowsArgumentNullException()
    {
        // Arrange
        var mockClient = new MockMcpClient();
        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new McpToolAdapter(null!, "tool-name", descriptor, mockClient));
    }

    [Fact]
    public void Constructor_WithNullToolName_ThrowsArgumentNullException()
    {
        // Arrange
        var mockClient = new MockMcpClient();
        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new McpToolAdapter("server-name", null!, descriptor, mockClient));
    }

    [Fact]
    public void Constructor_WithNullDescriptor_ThrowsArgumentNullException()
    {
        // Arrange
        var mockClient = new MockMcpClient();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new McpToolAdapter("server-name", "tool-name", null!, mockClient));
    }

    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        // Arrange
        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new McpToolAdapter("server-name", "tool-name", descriptor, null!));
    }

    [Fact]
    public void DescriptorProperty_ReturnsDescriptorPassedToConstructor()
    {
        // Arrange
        var mockClient = new MockMcpClient();
        var descriptor = new ToolDescriptor
        {
            Id = "mcp:test:tool",
            Name = "TestTool",
            Description = "A test MCP tool"
        };

        var adapter = new McpToolAdapter("test-server", "test-tool", descriptor, mockClient);

        // Act & Assert
        Assert.Same(descriptor, adapter.Descriptor);
    }

    #endregion

    #region Test Helper Classes

    private class MockMcpClient : IMcpClient
    {
        public McpResponse? ResponseToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public string? LastServerName { get; set; }
        public string? LastToolName { get; set; }
        public Dictionary<string, object?>? LastParameters { get; set; }

        public Task<McpResponse> CallToolAsync(
            string serverName,
            string toolName,
            Dictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            LastServerName = serverName;
            LastToolName = toolName;
            LastParameters = parameters;

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResponseToReturn ?? new McpResponse { IsSuccess = true });
        }
    }

    #endregion
}
