using Gmsd.Agents.Execution.ControlPlane.Tools.Firewall;
using Gmsd.Aos.Contracts.Tools;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Firewall;

public sealed class ScopedToolTests
{
    private class MockTool : ITool
    {
        public Task<ToolResult> InvokeAsync(
            ToolRequest request,
            ToolContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolResult.Success(new { message = "Success" }));
        }
    }

    [Fact]
    public async Task InvokeAsync_AllowedPath_ExecutesInnerTool()
    {
        // Arrange
        var allowedScope = Path.GetFullPath("C:\\workspace");
        var firewall = new ScopeFirewall(new[] { allowedScope });
        var innerTool = new MockTool();
        var scopedTool = new ScopedTool(innerTool, firewall);

        var filePath = Path.Combine(allowedScope, "file.cs");
        var request = new ToolRequest
        {
            Operation = "read_file",
            Parameters = new Dictionary<string, object?> { { "path", filePath } }
        };
        var context = new ToolContext { RunId = "test", CorrelationId = "test" };

        // Act
        var result = await scopedTool.InvokeAsync(request, context);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task InvokeAsync_OutOfScopePath_ReturnsScopeViolationError()
    {
        // Arrange
        var allowedScope = Path.GetFullPath("C:\\workspace");
        var firewall = new ScopeFirewall(new[] { allowedScope });
        var innerTool = new MockTool();
        var scopedTool = new ScopedTool(innerTool, firewall);

        var filePath = Path.GetFullPath("C:\\other\\file.cs");
        var request = new ToolRequest
        {
            Operation = "read_file",
            Parameters = new Dictionary<string, object?> { { "path", filePath } }
        };
        var context = new ToolContext { RunId = "test", CorrelationId = "test" };

        // Act
        var result = await scopedTool.InvokeAsync(request, context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("ScopeViolation", result.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_NestedPathInParameters_ValidatesAllPaths()
    {
        // Arrange
        var allowedScope = Path.GetFullPath("C:\\workspace");
        var firewall = new ScopeFirewall(new[] { allowedScope });
        var innerTool = new MockTool();
        var scopedTool = new ScopedTool(innerTool, firewall);

        var validPath = Path.Combine(allowedScope, "file1.cs");
        var invalidPath = Path.GetFullPath("C:\\other\\file2.cs");
        var request = new ToolRequest
        {
            Operation = "copy_files",
            Parameters = new Dictionary<string, object?>
            {
                { "source", validPath },
                { "destination", invalidPath }
            }
        };
        var context = new ToolContext { RunId = "test", CorrelationId = "test" };

        // Act
        var result = await scopedTool.InvokeAsync(request, context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("ScopeViolation", result.Error?.Code);
    }

    [Fact]
    public async Task InvokeAsync_NoPathParameters_ExecutesInnerTool()
    {
        // Arrange
        var firewall = new ScopeFirewall(new[] { "C:\\workspace" });
        var innerTool = new MockTool();
        var scopedTool = new ScopedTool(innerTool, firewall);

        var request = new ToolRequest
        {
            Operation = "list_tools",
            Parameters = new Dictionary<string, object?>()
        };
        var context = new ToolContext { RunId = "test", CorrelationId = "test" };

        // Act
        var result = await scopedTool.InvokeAsync(request, context);

        // Assert
        Assert.True(result.IsSuccess);
    }
}
