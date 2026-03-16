using nirmata.Agents.Execution.ControlPlane.Tools.Firewall;
using nirmata.Agents.Execution.ControlPlane.Tools.Registry;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;
using nirmata.Aos.Contracts.Tools;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Firewall;

public sealed class ScopedToolRegistryTests
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
    public void ResolveByName_WrapsToolWithScopedTool()
    {
        // Arrange
        var innerRegistry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test.tool",
            Name = "test_tool",
            Description = "Test tool"
        };
        var innerTool = new MockTool();
        innerRegistry.Register(descriptor, innerTool);

        var firewall = new ScopeFirewall(new[] { "C:\\workspace" });
        var scopedRegistry = new ScopedToolRegistry(innerRegistry, firewall);

        // Act
        var resolvedTool = scopedRegistry.ResolveByName("test_tool");

        // Assert
        Assert.NotNull(resolvedTool);
        Assert.IsType<ScopedTool>(resolvedTool);
    }

    [Fact]
    public void Resolve_WrapsToolWithScopedTool()
    {
        // Arrange
        var innerRegistry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test.tool",
            Name = "test_tool",
            Description = "Test tool"
        };
        var innerTool = new MockTool();
        innerRegistry.Register(descriptor, innerTool);

        var firewall = new ScopeFirewall(new[] { "C:\\workspace" });
        var scopedRegistry = new ScopedToolRegistry(innerRegistry, firewall);

        // Act
        var resolvedTool = scopedRegistry.Resolve("test.tool");

        // Assert
        Assert.NotNull(resolvedTool);
        Assert.IsType<ScopedTool>(resolvedTool);
    }

    [Fact]
    public void ResolveByName_UnknownTool_ReturnsNull()
    {
        // Arrange
        var innerRegistry = new ToolRegistry();
        var firewall = new ScopeFirewall(new[] { "C:\\workspace" });
        var scopedRegistry = new ScopedToolRegistry(innerRegistry, firewall);

        // Act
        var resolvedTool = scopedRegistry.ResolveByName("unknown_tool");

        // Assert
        Assert.Null(resolvedTool);
    }

    [Fact]
    public void List_ReturnsSameDescriptorsAsInnerRegistry()
    {
        // Arrange
        var innerRegistry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test.tool",
            Name = "test_tool",
            Description = "Test tool"
        };
        var innerTool = new MockTool();
        innerRegistry.Register(descriptor, innerTool);

        var firewall = new ScopeFirewall(new[] { "C:\\workspace" });
        var scopedRegistry = new ScopedToolRegistry(innerRegistry, firewall);

        // Act
        var descriptors = scopedRegistry.List().ToList();

        // Assert
        Assert.Single(descriptors);
        Assert.Equal("test.tool", descriptors[0].Id);
    }

    [Fact]
    public void IsRegistered_DelegatesToInnerRegistry()
    {
        // Arrange
        var innerRegistry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test.tool",
            Name = "test_tool",
            Description = "Test tool"
        };
        var innerTool = new MockTool();
        innerRegistry.Register(descriptor, innerTool);

        var firewall = new ScopeFirewall(new[] { "C:\\workspace" });
        var scopedRegistry = new ScopedToolRegistry(innerRegistry, firewall);

        // Act & Assert
        Assert.True(scopedRegistry.IsRegistered("test.tool"));
        Assert.False(scopedRegistry.IsRegistered("unknown.tool"));
    }

    [Fact]
    public void Register_DelegatesToInnerRegistry()
    {
        // Arrange
        var innerRegistry = new ToolRegistry();
        var firewall = new ScopeFirewall(new[] { "C:\\workspace" });
        var scopedRegistry = new ScopedToolRegistry(innerRegistry, firewall);

        var descriptor = new ToolDescriptor
        {
            Id = "test.tool",
            Name = "test_tool",
            Description = "Test tool"
        };
        var tool = new MockTool();

        // Act
        scopedRegistry.Register(descriptor, tool);

        // Assert
        Assert.True(scopedRegistry.IsRegistered("test.tool"));
    }
}
