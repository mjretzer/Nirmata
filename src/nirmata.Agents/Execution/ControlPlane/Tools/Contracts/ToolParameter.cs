namespace nirmata.Agents.Execution.ControlPlane.Tools.Contracts;

/// <summary>
/// Parameter metadata for tool introspection and UI generation.
/// Describes a single input parameter for a tool.
/// </summary>
public sealed class ToolParameter
{
    /// <summary>
    /// Name of the parameter.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of the parameter.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// JSON Schema type for the parameter (e.g., "string", "integer", "object").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Indicates whether the parameter is required.
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// Default value for the parameter, if any.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Allowed values for enum-type parameters.
    /// </summary>
    public IReadOnlyList<string>? EnumValues { get; init; }

    /// <summary>
    /// Example value for documentation/UI purposes.
    /// </summary>
    public string? Example { get; init; }
}
