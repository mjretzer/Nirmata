using System.Collections.Concurrent;
using Gmsd.Aos.Contracts.Tools;
using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;

namespace Gmsd.Agents.Execution.ControlPlane.Tools.Registry;

/// <summary>
/// In-memory implementation of the tool registry with deterministic ordering.
/// Thread-safe for registration operations.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, (ToolDescriptor Descriptor, ITool Tool)> _tools = new();

    /// <inheritdoc />
    public void Register(ToolDescriptor descriptor, ITool tool)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(tool);

        if (string.IsNullOrWhiteSpace(descriptor.Id))
        {
            throw new ArgumentException("Tool descriptor must have a valid Id", nameof(descriptor));
        }

        _tools[descriptor.Id] = (descriptor, tool);
    }

    /// <inheritdoc />
    public ITool? Resolve(string toolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        return _tools.TryGetValue(toolId, out var entry) ? entry.Tool : null;
    }

    /// <inheritdoc />
    public ToolDescriptor? ResolveDescriptor(string toolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        return _tools.TryGetValue(toolId, out var entry) ? entry.Descriptor : null;
    }

    /// <inheritdoc />
    public ITool? ResolveByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _tools.Values
            .FirstOrDefault(t => t.Descriptor.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Tool;
    }

    /// <inheritdoc />
    public IEnumerable<ToolDescriptor> List()
    {
        return _tools.Values
            .Select(t => t.Descriptor)
            .OrderBy(d => d.Id, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public bool IsRegistered(string toolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        return _tools.ContainsKey(toolId);
    }
}
