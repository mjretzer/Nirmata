using Gmsd.Aos.Contracts.Tools;
using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;

namespace Gmsd.Agents.Execution.ControlPlane.Tools.Registry;

/// <summary>
/// Interface for the tool registry, providing registration and resolution capabilities.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Registers a tool with its descriptor.
    /// </summary>
    /// <param name="descriptor">Metadata describing the tool.</param>
    /// <param name="tool">The tool implementation.</param>
    void Register(ToolDescriptor descriptor, ITool tool);

    /// <summary>
    /// Resolves a tool by its stable ID.
    /// </summary>
    /// <param name="toolId">The unique tool identifier.</param>
    /// <returns>The tool implementation if found; null otherwise.</returns>
    ITool? Resolve(string toolId);

    /// <summary>
    /// Resolves a tool descriptor by its stable ID.
    /// </summary>
    /// <param name="toolId">The unique tool identifier.</param>
    /// <returns>The tool descriptor if found; null otherwise.</returns>
    ToolDescriptor? ResolveDescriptor(string toolId);

    /// <summary>
    /// Resolves a tool by its display name.
    /// </summary>
    /// <param name="name">The tool display name.</param>
    /// <returns>The tool implementation if found; null otherwise.</returns>
    ITool? ResolveByName(string name);

    /// <summary>
    /// Lists all registered tools in deterministic order (sorted by ToolId).
    /// </summary>
    /// <returns>An enumerable of tool descriptors.</returns>
    IEnumerable<ToolDescriptor> List();

    /// <summary>
    /// Checks if a tool with the given ID is registered.
    /// </summary>
    /// <param name="toolId">The unique tool identifier.</param>
    /// <returns>True if the tool is registered; false otherwise.</returns>
    bool IsRegistered(string toolId);
}
