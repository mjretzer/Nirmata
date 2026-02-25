namespace Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;

/// <summary>
/// Definition of a tool available to the LLM.
/// Mirrors a subset of the JSON schema for function calling.
/// </summary>
[Obsolete("Use Microsoft.SemanticKernel.KernelFunction or function metadata directly. " +
          "This abstraction will be removed in a future release.", false)]
public sealed record LlmToolDefinition
{
    /// <summary>
    /// Unique name of the tool.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what the tool does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// JSON schema describing the tool's parameters.
    /// </summary>
    public required object ParametersSchema { get; init; }
}
