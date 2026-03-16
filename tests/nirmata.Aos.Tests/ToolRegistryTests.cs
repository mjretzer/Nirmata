using nirmata.Aos.Contracts.Tools;
using nirmata.Agents.Execution.ControlPlane.Tools.Registry;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;
using Xunit;

namespace nirmata.Aos.Tests;

public class ToolRegistryTests
{
    #region 6.1 Unit tests for registry register/resolve operations

    [Fact]
    public void Register_WithValidDescriptorAndTool_RegistersSuccessfully()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test:tool:1",
            Name = "TestTool",
            Description = "A test tool"
        };
        var tool = new TestTool();

        // Act
        registry.Register(descriptor, tool);

        // Assert
        Assert.True(registry.IsRegistered("test:tool:1"));
    }

    [Fact]
    public void Register_WithNullDescriptor_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!, tool));
    }

    [Fact]
    public void Register_WithNullTool_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test:tool:1",
            Name = "TestTool",
            Description = "A test tool"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.Register(descriptor, null!));
    }

    [Fact]
    public void Register_WithEmptyToolId_ThrowsArgumentException()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "",
            Name = "TestTool",
            Description = "A test tool"
        };
        var tool = new TestTool();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => registry.Register(descriptor, tool));
        Assert.Contains("Id", ex.Message);
    }

    [Fact]
    public void Register_WithWhitespaceToolId_ThrowsArgumentException()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "   ",
            Name = "TestTool",
            Description = "A test tool"
        };
        var tool = new TestTool();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => registry.Register(descriptor, tool));
        Assert.Contains("Id", ex.Message);
    }

    [Fact]
    public void Register_WithDuplicateId_OverwritesExistingTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor1 = new ToolDescriptor
        {
            Id = "test:tool:1",
            Name = "OriginalTool",
            Description = "The original tool"
        };
        var originalTool = new TestTool();
        registry.Register(descriptor1, originalTool);

        var descriptor2 = new ToolDescriptor
        {
            Id = "test:tool:1",
            Name = "ReplacementTool",
            Description = "The replacement tool"
        };
        var replacementTool = new TestTool();

        // Act
        registry.Register(descriptor2, replacementTool);

        // Assert
        var resolvedDescriptor = registry.ResolveDescriptor("test:tool:1");
        Assert.NotNull(resolvedDescriptor);
        Assert.Equal("ReplacementTool", resolvedDescriptor.Name);
    }

    [Fact]
    public void Resolve_WithExistingToolId_ReturnsTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test:tool:1",
            Name = "TestTool",
            Description = "A test tool"
        };
        var tool = new TestTool();
        registry.Register(descriptor, tool);

        // Act
        var result = registry.Resolve("test:tool:1");

        // Assert
        Assert.NotNull(result);
        Assert.Same(tool, result);
    }

    [Fact]
    public void Resolve_WithNonExistentToolId_ReturnsNull()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var result = registry.Resolve("non:existent:tool");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_WithNullToolId_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.Resolve(null!));
    }

    [Fact]
    public void Resolve_WithEmptyToolId_ThrowsArgumentException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.Resolve(""));
    }

    [Fact]
    public void ResolveDescriptor_WithExistingToolId_ReturnsDescriptor()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test:tool:1",
            Name = "TestTool",
            Description = "A test tool"
        };
        var tool = new TestTool();
        registry.Register(descriptor, tool);

        // Act
        var result = registry.ResolveDescriptor("test:tool:1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestTool", result.Name);
        Assert.Equal("A test tool", result.Description);
    }

    [Fact]
    public void ResolveDescriptor_WithNonExistentToolId_ReturnsNull()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var result = registry.ResolveDescriptor("non:existent:tool");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolveByName_WithExistingName_ReturnsTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test:tool:1",
            Name = "MyTestTool",
            Description = "A test tool"
        };
        var tool = new TestTool();
        registry.Register(descriptor, tool);

        // Act
        var result = registry.ResolveByName("MyTestTool");

        // Assert
        Assert.NotNull(result);
        Assert.Same(tool, result);
    }

    [Fact]
    public void ResolveByName_WithExistingName_CaseInsensitive_ReturnsTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test:tool:1",
            Name = "MyTestTool",
            Description = "A test tool"
        };
        var tool = new TestTool();
        registry.Register(descriptor, tool);

        // Act
        var result = registry.ResolveByName("mytesttool");

        // Assert
        Assert.NotNull(result);
        Assert.Same(tool, result);
    }

    [Fact]
    public void ResolveByName_WithNonExistentName_ReturnsNull()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var result = registry.ResolveByName("NonExistentTool");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolveByName_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.ResolveByName(null!));
    }

    [Fact]
    public void IsRegistered_WithExistingToolId_ReturnsTrue()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "test:tool:1",
            Name = "TestTool",
            Description = "A test tool"
        };
        var tool = new TestTool();
        registry.Register(descriptor, tool);

        // Act
        var result = registry.IsRegistered("test:tool:1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRegistered_WithNonExistentToolId_ReturnsFalse()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var result = registry.IsRegistered("non:existent:tool");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsRegistered_WithNullToolId_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.IsRegistered(null!));
    }

    #endregion

    #region 6.2 Unit tests for catalog enumeration order stability

    [Fact]
    public void List_WithNoTools_ReturnsEmptyEnumerable()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var result = registry.List();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void List_WithSingleTool_ReturnsToolInList()
    {
        // Arrange
        var registry = new ToolRegistry();
        var descriptor = new ToolDescriptor
        {
            Id = "z:tool",
            Name = "ZTool",
            Description = "Z tool"
        };
        var tool = new TestTool();
        registry.Register(descriptor, tool);

        // Act
        var result = registry.List().ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("z:tool", result[0].Id);
    }

    [Fact]
    public void List_WithMultipleTools_ReturnsSortedByToolId()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Register tools in non-alphabetical order
        registry.Register(
            new ToolDescriptor { Id = "m:tool", Name = "MTool", Description = "M tool" },
            new TestTool());
        registry.Register(
            new ToolDescriptor { Id = "a:tool", Name = "ATool", Description = "A tool" },
            new TestTool());
        registry.Register(
            new ToolDescriptor { Id = "z:tool", Name = "ZTool", Description = "Z tool" },
            new TestTool());
        registry.Register(
            new ToolDescriptor { Id = "b:tool", Name = "BTool", Description = "B tool" },
            new TestTool());

        // Act
        var result = registry.List().ToList();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("a:tool", result[0].Id);
        Assert.Equal("b:tool", result[1].Id);
        Assert.Equal("m:tool", result[2].Id);
        Assert.Equal("z:tool", result[3].Id);
    }

    [Fact]
    public void List_WithDuplicateRegistration_ReturnsLatestDescriptorOnly()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.Register(
            new ToolDescriptor { Id = "a:tool", Name = "Original", Description = "Original tool" },
            new TestTool());
        registry.Register(
            new ToolDescriptor { Id = "a:tool", Name = "Updated", Description = "Updated tool" },
            new TestTool());

        // Act
        var result = registry.List().ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Updated", result[0].Name);
    }

    [Fact]
    public void List_DeterministicOrder_AcrossMultipleCalls()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Register multiple tools
        var tools = new[]
        {
            ("gamma:tool", "GammaTool"),
            ("alpha:tool", "AlphaTool"),
            ("beta:tool", "BetaTool"),
            ("delta:tool", "DeltaTool")
        };

        foreach (var (id, name) in tools)
        {
            registry.Register(
                new ToolDescriptor { Id = id, Name = name, Description = $"{name} description" },
                new TestTool());
        }

        // Act - call List multiple times
        var firstCall = registry.List().Select(d => d.Id).ToList();
        var secondCall = registry.List().Select(d => d.Id).ToList();
        var thirdCall = registry.List().Select(d => d.Id).ToList();

        // Assert - all calls should return the same order
        Assert.Equal(firstCall, secondCall);
        Assert.Equal(secondCall, thirdCall);
        Assert.Equal(new[] { "alpha:tool", "beta:tool", "delta:tool", "gamma:tool" }, firstCall);
    }

    [Fact]
    public void List_WithMcpAndInternalTools_ReturnsMixedSortedOrder()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Mix MCP and internal tool IDs
        registry.Register(
            new ToolDescriptor { Id = "mcp:server1:toolA", Name = "McpA", Description = "MCP A" },
            new TestTool());
        registry.Register(
            new ToolDescriptor { Id = "nirmata:aos:tool:filesystem:read", Name = "ReadFile", Description = "Read file" },
            new TestTool());
        registry.Register(
            new ToolDescriptor { Id = "mcp:server1:toolB", Name = "McpB", Description = "MCP B" },
            new TestTool());
        registry.Register(
            new ToolDescriptor { Id = "nirmata:aos:tool:git:status", Name = "GitStatus", Description = "Git status" },
            new TestTool());

        // Act
        var result = registry.List().Select(d => d.Id).ToList();

        // Assert - sorted alphabetically using ordinal comparison
        Assert.Equal(new[]
        {
            "nirmata:aos:tool:filesystem:read",
            "nirmata:aos:tool:git:status",
            "mcp:server1:toolA",
            "mcp:server1:toolB"
        }, result);
    }

    #endregion

    #region Test Helper Classes

    private class TestTool : ITool
    {
        public Task<ToolResult> InvokeAsync(
            ToolRequest request,
            ToolContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolResult.Success(null));
        }
    }

    #endregion
}
