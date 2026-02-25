using Gmsd.Agents.Execution.ControlPlane.Tools.Standard;
using Gmsd.Aos.Contracts.Tools;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Tools;

public sealed class TestToolTests
{
    [Fact]
    public async Task InvokeAsync_MissingProjectPath_ReturnsFailure()
    {
        // Arrange
        var tool = new TestTool();
        var request = new ToolRequest
        {
            Operation = "run_test",
            Parameters = new Dictionary<string, object?>()
        };
        var context = new ToolContext { RunId = "test", CorrelationId = "test" };

        // Act
        var result = await tool.InvokeAsync(request, context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("MissingPath", result.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_NonexistentPath_ReturnsFileNotFoundError()
    {
        // Arrange
        var tool = new TestTool();
        var request = new ToolRequest
        {
            Operation = "run_test",
            Parameters = new Dictionary<string, object?> { { "projectPath", "/nonexistent/path/project.csproj" } }
        };
        var context = new ToolContext { RunId = "test", CorrelationId = "test" };

        // Act
        var result = await tool.InvokeAsync(request, context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("FileNotFound", result.Error.Code);
    }

    [Fact]
    public void Descriptor_HasCorrectProperties()
    {
        // Arrange
        var tool = new TestTool();

        // Act
        var descriptor = tool.Descriptor;

        // Assert
        Assert.Equal("standard.run_test", descriptor.Id);
        Assert.Equal("run_test", descriptor.Name);
        Assert.NotNull(descriptor.Description);
        Assert.NotEmpty(descriptor.Parameters);
    }
}
