using Gmsd.Aos.Contracts.Tools;
using Gmsd.Agents.Execution.ControlPlane.Tools.Mcp;
using Gmsd.Agents.Execution.ControlPlane.Tools.Registry;
using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;
using Xunit;

namespace Gmsd.Aos.Tests;

public class ToolRegistryIntegrationTests
{
    #region 6.4 Integration test verifying end-to-end tool invocation through registry

    [Fact]
    public async Task EndToEnd_RegisterInternalTool_ResolveAndInvoke_ReturnsSuccessResult()
    {
        // Arrange - Create and register an internal tool
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "gmsd:aos:tool:test:echo",
            Name = "EchoTool",
            Description = "Echoes back the input parameters",
            Category = "test"
        };
        var tool = new EchoTool();
        registry.Register(descriptor, tool);

        // Act - Resolve and invoke through registry
        var resolvedTool = registry.Resolve("gmsd:aos:tool:test:echo");
        Assert.NotNull(resolvedTool);

        var request = new ToolRequest
        {
            Operation = "echo",
            Parameters = new Dictionary<string, object?> { { "message", "Hello, World!" } }
        };
        var context = new ToolContext { RunId = "integration-test-1" };

        var result = await resolvedTool.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello, World!", result.Data);
    }

    [Fact]
    public async Task EndToEnd_RegisterMcpTool_ResolveAndInvoke_ReturnsNormalizedResult()
    {
        // Arrange - Create MCP adapter and register it
        var registry = new ToolRegistry();
        var mockClient = new IntegrationMockMcpClient
        {
            ResponseToReturn = new McpResponse
            {
                IsSuccess = true,
                Data = new { files = new[] { "file1.txt", "file2.txt" } }
            }
        };

        var descriptor = new ToolDescriptor
        {
            Id = "mcp:filesystem:list",
            Name = "ListFiles",
            Description = "Lists files in a directory",
            Category = "mcp:filesystem"
        };

        var mcpTool = new McpToolAdapter("filesystem", "list", descriptor, mockClient);
        registry.Register(descriptor, mcpTool);

        // Act - Resolve and invoke through registry
        var resolvedTool = registry.Resolve("mcp:filesystem:list");
        Assert.NotNull(resolvedTool);

        var request = new ToolRequest
        {
            Operation = "list",
            Parameters = new Dictionary<string, object?> { { "path", "/tmp" } }
        };
        var context = new ToolContext { RunId = "integration-test-2" };

        var result = await resolvedTool.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task EndToEnd_RegisterMultipleTools_ResolveByName_Invoke_ReturnsCorrectResult()
    {
        // Arrange - Register multiple tools
        var registry = new ToolRegistry();

        // Tool A
        registry.Register(
            new ToolDescriptor
            {
                Id = "gmsd:aos:tool:test:toolA",
                Name = "ToolA",
                Description = "Returns A"
            },
            new StaticResultTool("ResultA"));

        // Tool B
        registry.Register(
            new ToolDescriptor
            {
                Id = "gmsd:aos:tool:test:toolB",
                Name = "ToolB",
                Description = "Returns B"
            },
            new StaticResultTool("ResultB"));

        // Act - Resolve by name and invoke ToolB
        var resolvedTool = registry.ResolveByName("ToolB");
        Assert.NotNull(resolvedTool);

        var request = new ToolRequest { Operation = "execute" };
        var context = new ToolContext { RunId = "integration-test-3" };

        var result = await resolvedTool.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal("ResultB", result.Data);
    }

    [Fact]
    public async Task EndToEnd_FullToolLifecycle_RegisterListResolveInvoke()
    {
        // Arrange - Complete lifecycle: create descriptor, register, verify list, resolve, invoke
        var registry = new ToolRegistry();

        var descriptor = new ToolDescriptor
        {
            Id = "gmsd:aos:tool:calculator:add",
            Name = "CalculatorAdd",
            Description = "Adds two numbers",
            Category = "math",
            Parameters = new List<ToolParameter>
            {
                new() { Name = "a", Description = "First number", Type = "number", Required = true },
                new() { Name = "b", Description = "Second number", Type = "number", Required = true }
            }
        };

        var calculatorTool = new CalculatorTool();
        registry.Register(descriptor, calculatorTool);

        // Verify tool appears in list
        var tools = registry.List().ToList();
        Assert.Single(tools);
        Assert.Equal("gmsd:aos:tool:calculator:add", tools[0].Id);
        Assert.Equal("CalculatorAdd", tools[0].Name);

        // Verify descriptor can be resolved
        var resolvedDescriptor = registry.ResolveDescriptor("gmsd:aos:tool:calculator:add");
        Assert.NotNull(resolvedDescriptor);
        Assert.Equal(2, resolvedDescriptor.Parameters.Count);

        // Invoke the tool through registry
        var resolvedTool = registry.Resolve("gmsd:aos:tool:calculator:add");
        Assert.NotNull(resolvedTool);

        var request = new ToolRequest
        {
            Operation = "add",
            Parameters = new Dictionary<string, object?>
            {
                { "a", 5 },
                { "b", 3 }
            }
        };
        var context = new ToolContext { RunId = "integration-test-4" };

        var result = await resolvedTool.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Data);
    }

    [Fact]
    public async Task EndToEnd_McpToolFailure_ReturnsFailureResultThroughRegistry()
    {
        // Arrange - Register MCP tool that will fail
        var registry = new ToolRegistry();
        var mockClient = new IntegrationMockMcpClient
        {
            ResponseToReturn = new McpResponse
            {
                IsSuccess = false,
                ErrorCode = "PERMISSION_DENIED",
                ErrorMessage = "Access to path denied"
            }
        };

        var descriptor = new ToolDescriptor
        {
            Id = "mcp:filesystem:read",
            Name = "ReadFile",
            Description = "Reads a file"
        };

        var mcpTool = new McpToolAdapter("filesystem", "read", descriptor, mockClient);
        registry.Register(descriptor, mcpTool);

        // Act - Resolve and invoke
        var resolvedTool = registry.Resolve("mcp:filesystem:read");
        Assert.NotNull(resolvedTool);

        var request = new ToolRequest
        {
            Operation = "read",
            Parameters = new Dictionary<string, object?> { { "path", "/secret.txt" } }
        };
        var context = new ToolContext { RunId = "integration-test-5" };

        var result = await resolvedTool.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal("PERMISSION_DENIED", result.Error!.Code);
        Assert.Equal("Access to path denied", result.Error.Message);
    }

    [Fact]
    public async Task EndToEnd_MixedInternalAndMcpTools_BothWorkThroughRegistry()
    {
        // Arrange - Register mix of internal and MCP tools
        var registry = new ToolRegistry();

        // Internal tool
        registry.Register(
            new ToolDescriptor
            {
                Id = "gmsd:aos:tool:internal:logger",
                Name = "Logger",
                Description = "Internal logging tool"
            },
            new StaticResultTool("logged"));

        // MCP tool
        var mockClient = new IntegrationMockMcpClient
        {
            ResponseToReturn = new McpResponse
            {
                IsSuccess = true,
                Data = "mcp result"
            }
        };
        var mcpDescriptor = new ToolDescriptor
        {
            Id = "mcp:search:query",
            Name = "SearchQuery",
            Description = "External search tool"
        };
        var mcpTool = new McpToolAdapter("search", "query", mcpDescriptor, mockClient);
        registry.Register(mcpDescriptor, mcpTool);

        // Act - List all tools (should be sorted)
        var tools = registry.List().ToList();
        Assert.Equal(2, tools.Count);
        Assert.Equal("gmsd:aos:tool:internal:logger", tools[0].Id);
        Assert.Equal("mcp:search:query", tools[1].Id);

        // Invoke internal tool
        var internalTool = registry.Resolve("gmsd:aos:tool:internal:logger");
        var internalResult = await internalTool!.InvokeAsync(
            new ToolRequest { Operation = "log" },
            new ToolContext { RunId = "test" });

        // Invoke MCP tool
        var mcpResolvedTool = registry.Resolve("mcp:search:query");
        var mcpResult = await mcpResolvedTool!.InvokeAsync(
            new ToolRequest { Operation = "search" },
            new ToolContext { RunId = "test" });

        // Assert
        Assert.True(internalResult.IsSuccess);
        Assert.Equal("logged", internalResult.Data);
        Assert.True(mcpResult.IsSuccess);
        Assert.Equal("mcp result", mcpResult.Data);
    }

    #endregion

    #region Test Helper Classes

    private class EchoTool : ITool
    {
        public Task<ToolResult> InvokeAsync(
            ToolRequest request,
            ToolContext context,
            CancellationToken cancellationToken = default)
        {
            var message = request.Parameters.TryGetValue("message", out var value) ? value : null;
            return Task.FromResult(ToolResult.Success(message));
        }
    }

    private class StaticResultTool : ITool
    {
        private readonly object? _result;

        public StaticResultTool(object? result)
        {
            _result = result;
        }

        public Task<ToolResult> InvokeAsync(
            ToolRequest request,
            ToolContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolResult.Success(_result));
        }
    }

    private class CalculatorTool : ITool
    {
        public Task<ToolResult> InvokeAsync(
            ToolRequest request,
            ToolContext context,
            CancellationToken cancellationToken = default)
        {
            var a = request.Parameters.TryGetValue("a", out var aVal) ? Convert.ToInt32(aVal) : 0;
            var b = request.Parameters.TryGetValue("b", out var bVal) ? Convert.ToInt32(bVal) : 0;
            return Task.FromResult(ToolResult.Success(a + b));
        }
    }

    private class IntegrationMockMcpClient : IMcpClient
    {
        public McpResponse? ResponseToReturn { get; set; }

        public Task<McpResponse> CallToolAsync(
            string serverName,
            string toolName,
            Dictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResponseToReturn ?? new McpResponse { IsSuccess = true });
        }
    }

    #endregion
}
