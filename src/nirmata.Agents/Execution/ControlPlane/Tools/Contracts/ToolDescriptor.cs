namespace nirmata.Agents.Execution.ControlPlane.Tools.Contracts;

/// <summary>
/// Metadata model describing a tool for registration and discovery.
/// Contains identity, descriptive, and schema information.
/// </summary>
public sealed class ToolDescriptor
{
    /// <summary>
    /// Stable unique identifier for the tool (e.g., "nirmata:aos:tool:filesystem:read").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for the tool.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description of what the tool does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Reference to the JSON schema for input validation (URI or embedded schema ID).
    /// </summary>
    public string? InputSchemaRef { get; init; }

    /// <summary>
    /// Reference to the JSON schema for output validation (URI or embedded schema ID).
    /// </summary>
    public string? OutputSchemaRef { get; init; }

    /// <summary>
    /// Version of the tool descriptor schema.
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Category or grouping for the tool (e.g., "filesystem", "process", "git").
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Parameters metadata for introspection and UI generation.
    /// </summary>
    public IReadOnlyList<ToolParameter> Parameters { get; init; } = Array.Empty<ToolParameter>();

    /// <summary>
    /// Capability flags indicating tool behaviors.
    /// </summary>
    public ToolCapability Capabilities { get; init; } = ToolCapability.None;
}
