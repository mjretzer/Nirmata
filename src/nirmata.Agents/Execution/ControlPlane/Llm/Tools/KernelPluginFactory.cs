using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;
using nirmata.Agents.Execution.ControlPlane.Tools.Registry;
using nirmata.Aos.Contracts.Tools;
using Microsoft.SemanticKernel;

namespace nirmata.Agents.Execution.ControlPlane.Llm.Tools;

/// <summary>
/// Factory for creating Semantic Kernel plugins from nirmata tool collections.
/// Groups tools by category and registers them as KernelFunction instances.
/// </summary>
public static class KernelPluginFactory
{
    /// <summary>
    /// Creates a KernelPlugin from a collection of tools and their descriptors.
    /// Tools are grouped by category; tools without a category use a default group.
    /// </summary>
    /// <param name="tools">A collection of tuples containing the tool and its descriptor.</param>
    /// <returns>A KernelPlugin containing all tools as KernelFunction instances.</returns>
    public static KernelPlugin CreateFromTools(IEnumerable<(ITool Tool, ToolDescriptor Descriptor)> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var toolList = tools.ToList();
        if (toolList.Count == 0)
        {
            return Microsoft.SemanticKernel.KernelPluginFactory.CreateFromFunctions("nirmata_tools", []);
        }

        // Group tools by category
        var groupedTools = toolList.GroupBy(
            t => string.IsNullOrWhiteSpace(t.Descriptor.Category) ? "general" : t.Descriptor.Category,
            StringComparer.OrdinalIgnoreCase);

        // Create functions for all tools
        var functions = new List<KernelFunction>();
        foreach (var group in groupedTools)
        {
            foreach (var (tool, descriptor) in group)
            {
                var function = ToolToKernelFunctionAdapter.FromITool(tool, descriptor);
                functions.Add(function);
            }
        }

        // Create a single plugin with all functions
        // Use the category with most tools as the primary name, or default
        var primaryCategory = groupedTools
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()
            ?.Key ?? "nirmata_tools";

        return Microsoft.SemanticKernel.KernelPluginFactory.CreateFromFunctions(primaryCategory, functions);
    }

    /// <summary>
    /// Creates a KernelPlugin from a collection of ITool implementations.
    /// Each tool must have a Descriptor property that can be accessed.
    /// </summary>
    /// <param name="tools">A collection of ITool implementations with Descriptor properties.</param>
    /// <returns>A KernelPlugin containing all tools as KernelFunction instances.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a tool does not have a Descriptor property.
    /// </exception>
    public static KernelPlugin CreateFromTools(IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var toolList = tools.ToList();
        var toolWithDescriptors = new List<(ITool Tool, ToolDescriptor Descriptor)>();

        foreach (var tool in toolList)
        {
            var descriptor = GetDescriptorFromTool(tool);
            if (descriptor is null)
            {
                throw new InvalidOperationException(
                    $"Tool of type {tool.GetType().FullName} does not have a Descriptor property. " +
                    "Either implement a Descriptor property or use the CreateFromTools(IEnumerable<(ITool, ToolDescriptor)>) overload.");
            }

            toolWithDescriptors.Add((tool, descriptor));
        }

        return CreateFromTools(toolWithDescriptors);
    }

    /// <summary>
    /// Creates a KernelPlugin from a tool registry.
    /// Retrieves all registered tools and their descriptors from the registry.
    /// </summary>
    /// <param name="registry">The tool registry containing registered tools.</param>
    /// <returns>A KernelPlugin containing all registered tools as KernelFunction instances.</returns>
    public static KernelPlugin CreateFromRegistry(IToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var descriptors = registry.List().ToList();
        var toolWithDescriptors = new List<(ITool Tool, ToolDescriptor Descriptor)>();

        foreach (var descriptor in descriptors)
        {
            var tool = registry.Resolve(descriptor.Id);
            if (tool is not null)
            {
                toolWithDescriptors.Add((tool, descriptor));
            }
        }

        return CreateFromTools(toolWithDescriptors);
    }

    /// <summary>
    /// Attempts to extract the ToolDescriptor from an ITool instance using reflection.
    /// </summary>
    private static ToolDescriptor? GetDescriptorFromTool(ITool tool)
    {
        // First try: look for a Descriptor property directly
        var property = tool.GetType().GetProperty("Descriptor");
        if (property?.PropertyType == typeof(ToolDescriptor))
        {
            return property.GetValue(tool) as ToolDescriptor;
        }

        // Second try: look for an interface that exposes descriptor
        var interfaceType = tool.GetType().GetInterfaces()
            .FirstOrDefault(i => i.GetProperty("Descriptor")?.PropertyType == typeof(ToolDescriptor));

        if (interfaceType is not null)
        {
            var descriptorProperty = interfaceType.GetProperty("Descriptor");
            return descriptorProperty?.GetValue(tool) as ToolDescriptor;
        }

        return null;
    }
}
