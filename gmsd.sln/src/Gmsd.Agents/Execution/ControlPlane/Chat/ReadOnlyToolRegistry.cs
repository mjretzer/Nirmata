namespace Gmsd.Agents.Execution.ControlPlane.Chat;

/// <summary>
/// Defines a tool that can be used by the chat responder.
/// </summary>
public sealed class ChatTool
{
    /// <summary>
    /// The name of the tool (e.g., "read_file", "list_dir").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what the tool does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Parameters the tool accepts.
    /// </summary>
    public required Dictionary<string, ToolParameter> Parameters { get; init; }

    /// <summary>
    /// Whether this tool can modify state (write operations).
    /// </summary>
    public bool IsReadOnly { get; init; } = true;
}

/// <summary>
/// Describes a parameter for a chat tool.
/// </summary>
public sealed class ToolParameter
{
    /// <summary>
    /// The parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The parameter type (e.g., "string", "integer", "path").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Description of the parameter.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Whether this parameter is required.
    /// </summary>
    public bool Required { get; init; } = true;
}

/// <summary>
/// Registry of read-only tools available to the chat responder.
/// </summary>
public sealed class ReadOnlyToolRegistry
{
    private readonly Dictionary<string, ChatTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public ReadOnlyToolRegistry()
    {
        InitializeDefaultTools();
    }

    /// <summary>
    /// Registers a tool in the registry.
    /// </summary>
    public void RegisterTool(ChatTool tool)
    {
        if (!tool.IsReadOnly)
        {
            throw new ArgumentException("Only read-only tools can be registered in the chat responder tool registry.", nameof(tool));
        }

        _tools[tool.Name.ToLowerInvariant()] = tool;
    }

    /// <summary>
    /// Gets a tool by name.
    /// </summary>
    public ChatTool? GetTool(string name)
    {
        _tools.TryGetValue(name.ToLowerInvariant(), out var tool);
        return tool;
    }

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    public IReadOnlyList<ChatTool> GetAllTools()
    {
        return _tools.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Checks if a tool is available.
    /// </summary>
    public bool HasTool(string name)
    {
        return _tools.ContainsKey(name.ToLowerInvariant());
    }

    private void InitializeDefaultTools()
    {
        // read_file tool
        RegisterTool(new ChatTool
        {
            Name = "read_file",
            Description = "Read the contents of a file from the workspace",
            IsReadOnly = true,
            Parameters = new Dictionary<string, ToolParameter>
            {
                {
                    "path",
                    new ToolParameter
                    {
                        Name = "path",
                        Type = "path",
                        Description = "Absolute path to the file to read",
                        Required = true
                    }
                }
            }
        });

        // list_dir tool
        RegisterTool(new ChatTool
        {
            Name = "list_dir",
            Description = "List the contents of a directory in the workspace",
            IsReadOnly = true,
            Parameters = new Dictionary<string, ToolParameter>
            {
                {
                    "path",
                    new ToolParameter
                    {
                        Name = "path",
                        Type = "path",
                        Description = "Absolute path to the directory to list",
                        Required = true
                    }
                }
            }
        });

        // inspect_spec tool
        RegisterTool(new ChatTool
        {
            Name = "inspect_spec",
            Description = "Inspect a specification from the OpenSpec system",
            IsReadOnly = true,
            Parameters = new Dictionary<string, ToolParameter>
            {
                {
                    "spec_name",
                    new ToolParameter
                    {
                        Name = "spec_name",
                        Type = "string",
                        Description = "Name of the specification to inspect",
                        Required = true
                    }
                },
                {
                    "section",
                    new ToolParameter
                    {
                        Name = "section",
                        Type = "string",
                        Description = "Optional section of the spec to focus on",
                        Required = false
                    }
                }
            }
        });
    }
}
