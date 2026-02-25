using Gmsd.Agents.Execution.ControlPlane.Chat;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Chat;

public class ReadOnlyToolRegistryTests
{
    private readonly ReadOnlyToolRegistry _registry = new();

    [Fact]
    public void Constructor_InitializesDefaultTools()
    {
        var tools = _registry.GetAllTools();

        Assert.NotEmpty(tools);
        Assert.Contains(tools, t => t.Name.Equals("read_file", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, t => t.Name.Equals("list_dir", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, t => t.Name.Equals("inspect_spec", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetTool_WithValidName_ReturnsTool()
    {
        var tool = _registry.GetTool("read_file");

        Assert.NotNull(tool);
        Assert.Equal("read_file", tool.Name);
        Assert.True(tool.IsReadOnly);
    }

    [Fact]
    public void GetTool_IsCaseInsensitive()
    {
        var tool1 = _registry.GetTool("READ_FILE");
        var tool2 = _registry.GetTool("Read_File");
        var tool3 = _registry.GetTool("read_file");

        Assert.NotNull(tool1);
        Assert.NotNull(tool2);
        Assert.NotNull(tool3);
        Assert.Equal(tool1.Name, tool2.Name);
        Assert.Equal(tool2.Name, tool3.Name);
    }

    [Fact]
    public void GetTool_WithInvalidName_ReturnsNull()
    {
        var tool = _registry.GetTool("nonexistent_tool");

        Assert.Null(tool);
    }

    [Fact]
    public void HasTool_WithValidName_ReturnsTrue()
    {
        Assert.True(_registry.HasTool("read_file"));
        Assert.True(_registry.HasTool("list_dir"));
        Assert.True(_registry.HasTool("inspect_spec"));
    }

    [Fact]
    public void HasTool_WithInvalidName_ReturnsFalse()
    {
        Assert.False(_registry.HasTool("nonexistent_tool"));
    }

    [Fact]
    public void HasTool_IsCaseInsensitive()
    {
        Assert.True(_registry.HasTool("READ_FILE"));
        Assert.True(_registry.HasTool("List_Dir"));
        Assert.True(_registry.HasTool("INSPECT_SPEC"));
    }

    [Fact]
    public void RegisterTool_AddsToolToRegistry()
    {
        var customTool = new ChatTool
        {
            Name = "custom_tool",
            Description = "A custom read-only tool",
            IsReadOnly = true,
            Parameters = new Dictionary<string, ToolParameter>()
        };

        _registry.RegisterTool(customTool);
        var retrieved = _registry.GetTool("custom_tool");

        Assert.NotNull(retrieved);
        Assert.Equal("custom_tool", retrieved.Name);
    }

    [Fact]
    public void RegisterTool_WithWriteTool_ThrowsException()
    {
        var writeTool = new ChatTool
        {
            Name = "write_file",
            Description = "A write tool",
            IsReadOnly = false,
            Parameters = new Dictionary<string, ToolParameter>()
        };

        Assert.Throws<ArgumentException>(() => _registry.RegisterTool(writeTool));
    }

    [Fact]
    public void GetAllTools_ReturnsReadOnlyList()
    {
        var tools = _registry.GetAllTools();

        Assert.NotNull(tools);
        Assert.IsAssignableFrom<IReadOnlyList<ChatTool>>(tools);
    }

    [Fact]
    public void ReadFileTool_HasCorrectParameters()
    {
        var tool = _registry.GetTool("read_file");

        Assert.NotNull(tool);
        Assert.Contains("path", tool.Parameters.Keys);
        var pathParam = tool.Parameters["path"];
        Assert.Equal("path", pathParam.Name);
        Assert.Equal("path", pathParam.Type);
        Assert.True(pathParam.Required);
    }

    [Fact]
    public void ListDirTool_HasCorrectParameters()
    {
        var tool = _registry.GetTool("list_dir");

        Assert.NotNull(tool);
        Assert.Contains("path", tool.Parameters.Keys);
        var pathParam = tool.Parameters["path"];
        Assert.Equal("path", pathParam.Name);
        Assert.Equal("path", pathParam.Type);
        Assert.True(pathParam.Required);
    }

    [Fact]
    public void InspectSpecTool_HasCorrectParameters()
    {
        var tool = _registry.GetTool("inspect_spec");

        Assert.NotNull(tool);
        Assert.Contains("spec_name", tool.Parameters.Keys);
        Assert.Contains("section", tool.Parameters.Keys);

        var specNameParam = tool.Parameters["spec_name"];
        Assert.True(specNameParam.Required);

        var sectionParam = tool.Parameters["section"];
        Assert.False(sectionParam.Required);
    }

    [Fact]
    public void AllDefaultTools_AreReadOnly()
    {
        var tools = _registry.GetAllTools();

        foreach (var tool in tools)
        {
            Assert.True(tool.IsReadOnly, $"Tool {tool.Name} should be read-only");
        }
    }

    [Fact]
    public void RegisterTool_OverwritesPreviousRegistration()
    {
        var tool1 = new ChatTool
        {
            Name = "test_tool",
            Description = "First version",
            IsReadOnly = true,
            Parameters = new Dictionary<string, ToolParameter>()
        };

        var tool2 = new ChatTool
        {
            Name = "test_tool",
            Description = "Second version",
            IsReadOnly = true,
            Parameters = new Dictionary<string, ToolParameter>()
        };

        _registry.RegisterTool(tool1);
        _registry.RegisterTool(tool2);

        var retrieved = _registry.GetTool("test_tool");
        Assert.Equal("Second version", retrieved?.Description);
    }

    [Fact]
    public void ChatTool_HasDescription()
    {
        var tool = _registry.GetTool("read_file");

        Assert.NotNull(tool);
        Assert.NotNull(tool.Description);
        Assert.NotEmpty(tool.Description);
    }

    [Fact]
    public void ToolParameter_HasDescription()
    {
        var tool = _registry.GetTool("read_file");
        var pathParam = tool?.Parameters["path"];

        Assert.NotNull(pathParam);
        Assert.NotNull(pathParam.Description);
        Assert.NotEmpty(pathParam.Description);
    }
}
