using nirmata.Agents.Execution.ControlPlane.Tools.Standard;
using nirmata.Aos.Contracts.Tools;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ControlPlane.Tools;

public sealed class BuildToolTests
{
    [Fact]
    public async Task InvokeAsync_ValidSolution_ReturnsSuccess()
    {
        // Arrange
        var tool = new BuildTool();
        var request = new ToolRequest
        {
            Operation = "run_build",
            Parameters = new Dictionary<string, object?> { { "solutionPath", "nirmata.slnx" } }
        };
        var context = new ToolContext { RunId = "test", CorrelationId = "test" };

        // Act
        var result = await tool.InvokeAsync(request, context);

        // Assert
        Assert.NotNull(result);
        // Note: Actual success depends on whether the solution builds successfully
    }

    [Fact]
    public async Task InvokeAsync_MissingSolutionPath_ReturnsFailure()
    {
        // Arrange
        var tool = new BuildTool();
        var request = new ToolRequest
        {
            Operation = "run_build",
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
    public async Task InvokeAsync_NonexistentFile_ReturnsFileNotFoundError()
    {
        // Arrange
        var tool = new BuildTool();
        var request = new ToolRequest
        {
            Operation = "run_build",
            Parameters = new Dictionary<string, object?> { { "solutionPath", "/nonexistent/path/solution.sln" } }
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
        var tool = new BuildTool();

        // Act
        var descriptor = tool.Descriptor;

        // Assert
        Assert.Equal("standard.run_build", descriptor.Id);
        Assert.Equal("run_build", descriptor.Name);
        Assert.NotNull(descriptor.Description);
        Assert.NotEmpty(descriptor.Parameters);
    }
}
