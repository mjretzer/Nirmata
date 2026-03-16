using nirmata.Aos.Contracts.Tools;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;
using nirmata.Agents.Execution.ControlPlane.Tools.Registry;

namespace nirmata.Agents.Execution.ControlPlane.Tools.Firewall;

/// <summary>
/// Decorator for IToolRegistry that wraps tools with scope firewall validation.
/// Ensures all tools resolved from this registry enforce file scope constraints.
/// </summary>
public sealed class ScopedToolRegistry : IToolRegistry
{
    private readonly IToolRegistry _innerRegistry;
    private readonly IScopeFirewall _firewall;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedToolRegistry"/> class.
    /// </summary>
    /// <param name="innerRegistry">The underlying tool registry.</param>
    /// <param name="firewall">The scope firewall for path validation.</param>
    public ScopedToolRegistry(IToolRegistry innerRegistry, IScopeFirewall firewall)
    {
        _innerRegistry = innerRegistry ?? throw new ArgumentNullException(nameof(innerRegistry));
        _firewall = firewall ?? throw new ArgumentNullException(nameof(firewall));
    }

    /// <inheritdoc />
    public void Register(ToolDescriptor descriptor, ITool tool)
    {
        // Delegate to inner registry - registration doesn't need firewall validation
        _innerRegistry.Register(descriptor, tool);
    }

    /// <inheritdoc />
    public ITool? Resolve(string toolId)
    {
        var tool = _innerRegistry.Resolve(toolId);
        return tool != null ? new ScopedTool(tool, _firewall) : null;
    }

    /// <inheritdoc />
    public ToolDescriptor? ResolveDescriptor(string toolId)
    {
        return _innerRegistry.ResolveDescriptor(toolId);
    }

    /// <inheritdoc />
    public ITool? ResolveByName(string name)
    {
        var tool = _innerRegistry.ResolveByName(name);
        return tool != null ? new ScopedTool(tool, _firewall) : null;
    }

    /// <inheritdoc />
    public IEnumerable<ToolDescriptor> List()
    {
        return _innerRegistry.List();
    }

    /// <inheritdoc />
    public bool IsRegistered(string toolId)
    {
        return _innerRegistry.IsRegistered(toolId);
    }
}
