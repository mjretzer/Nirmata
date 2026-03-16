using nirmata.Aos.Contracts.Tools;
using nirmata.Aos.Public.Catalogs;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;

namespace nirmata.Agents.Execution.ControlPlane.Tools.Mcp;

/// <summary>
/// Factory for creating MCP tool adapter instances.
/// Generates ToolDescriptor from MCP server metadata and wraps tools as ITool implementations.
/// </summary>
public sealed class McpToolFactory
{
    private readonly IMcpClient _client;

    /// <summary>
    /// Creates a new MCP tool factory.
    /// </summary>
    public McpToolFactory(IMcpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Creates an McpToolAdapter from MCP server tool metadata.
    /// </summary>
    /// <param name="serverName">Name of the MCP server.</param>
    /// <param name="toolMetadata">Metadata describing the MCP tool.</param>
    /// <returns>A configured McpToolAdapter ready for registration.</returns>
    public McpToolAdapter CreateAdapter(string serverName, McpToolMetadata toolMetadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentNullException.ThrowIfNull(toolMetadata);

        var descriptor = CreateDescriptor(serverName, toolMetadata);
        return new McpToolAdapter(serverName, toolMetadata.Name, descriptor, _client);
    }

    /// <summary>
    /// Creates a ToolDescriptor from MCP tool metadata.
    /// </summary>
    private ToolDescriptor CreateDescriptor(string serverName, McpToolMetadata metadata)
    {
        var toolId = ToolIds.McpToolId(serverName, metadata.Name);

        return new ToolDescriptor
        {
            Id = toolId,
            Name = metadata.Name,
            Description = metadata.Description,
            Category = $"mcp:{serverName}",
            InputSchemaRef = metadata.InputSchema,
            OutputSchemaRef = metadata.OutputSchema,
            Parameters = MapParameters(metadata.Parameters),
            Capabilities = MapCapabilities(metadata)
        };
    }

    private static IReadOnlyList<ToolParameter> MapParameters(IEnumerable<McpParameterMetadata>? parameters)
    {
        if (parameters == null)
            return Array.Empty<ToolParameter>();

        return parameters.Select(p => new ToolParameter
        {
            Name = p.Name,
            Description = p.Description,
            Type = p.Type,
            Required = p.Required,
            DefaultValue = p.DefaultValue,
            EnumValues = p.EnumValues?.ToList()
        }).ToList();
    }

    private static ToolCapability MapCapabilities(McpToolMetadata metadata)
    {
        var caps = ToolCapability.None;

        if (metadata.SupportsStreaming)
            caps |= ToolCapability.Streaming;

        if (metadata.IsIdempotent)
            caps |= ToolCapability.Idempotent;

        return caps;
    }
}

/// <summary>
/// MCP tool metadata structure from server discovery.
/// </summary>
public sealed class McpToolMetadata
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? InputSchema { get; init; }
    public string? OutputSchema { get; init; }
    public List<McpParameterMetadata>? Parameters { get; init; }
    public bool SupportsStreaming { get; init; }
    public bool IsIdempotent { get; init; }
}

/// <summary>
/// MCP parameter metadata structure.
/// </summary>
public sealed class McpParameterMetadata
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Type { get; init; }
    public bool Required { get; init; } = true;
    public object? DefaultValue { get; init; }
    public List<string>? EnumValues { get; init; }
}
