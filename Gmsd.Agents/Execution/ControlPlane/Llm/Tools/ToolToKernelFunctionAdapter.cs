using System.Text.Json;
using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;
using Gmsd.Aos.Contracts.Tools;
using Microsoft.SemanticKernel;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Tools;

/// <summary>
/// Adapter that converts GMSD ITool implementations to Semantic Kernel KernelFunction instances.
/// Handles metadata mapping, parameter schema conversion, and execution delegation.
/// </summary>
public static class ToolToKernelFunctionAdapter
{
    /// <summary>
    /// Creates a Semantic Kernel KernelFunction from a GMSD ITool implementation.
    /// </summary>
    /// <param name="tool">The ITool implementation to adapt.</param>
    /// <param name="descriptor">The tool descriptor containing metadata.</param>
    /// <returns>A KernelFunction that wraps the ITool invocation.</returns>
    public static KernelFunction FromITool(ITool tool, ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(descriptor);

        var parameters = descriptor.Parameters.Select(p => CreateKernelParameterMetadata(p)).ToList();
        var returnParameter = descriptor.OutputSchemaRef is not null
            ? new KernelReturnParameterMetadata { Description = "Tool execution result" }
            : null;

        // Create the method that will be invoked by SK
        async Task<object?> ExecuteToolAsync(Kernel kernel, KernelArguments arguments, CancellationToken cancellationToken)
        {
            // Build ToolRequest from kernel arguments
            var requestParams = new Dictionary<string, object?>();
            foreach (var argument in arguments)
            {
                if (argument.Value is not null)
                {
                    requestParams[argument.Key] = argument.Value;
                }
            }

            var request = new ToolRequest
            {
                Operation = descriptor.Name,
                Parameters = requestParams,
                RequestId = Guid.NewGuid().ToString("N"),
            };

            // Build ToolContext from kernel arguments
            var context = CreateToolContext(arguments);

            // Invoke the tool
            var result = await tool.InvokeAsync(request, context, cancellationToken);

            if (!result.IsSuccess)
            {
                throw new KernelException(
                    $"Tool '{descriptor.Name}' failed with error {result.Error?.Code}: {result.Error?.Message}");
            }

            return result.Data;
        }

        return KernelFunctionFactory.CreateFromMethod(
            method: ExecuteToolAsync,
            functionName: descriptor.Name,
            description: descriptor.Description,
            parameters: parameters,
            returnParameter: returnParameter);
    }

    /// <summary>
    /// Creates a Semantic Kernel KernelFunction from a GMSD ITool implementation.
    /// The tool must expose a Descriptor property that can be accessed via reflection or interface.
    /// </summary>
    /// <param name="tool">The ITool implementation to adapt. Must have a Descriptor property.</param>
    /// <returns>A KernelFunction that wraps the ITool invocation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the tool does not have a Descriptor property.</exception>
    public static KernelFunction FromITool(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // Try to get descriptor via IToolWithDescriptor pattern or reflection
        var descriptor = GetDescriptorFromTool(tool);
        if (descriptor is null)
        {
            throw new InvalidOperationException(
                $"Tool of type {tool.GetType().FullName} does not have a Descriptor property. " +
                "Either implement a Descriptor property or use the FromITool(ITool, ToolDescriptor) overload.");
        }

        return FromITool(tool, descriptor);
    }


    /// <summary>
    /// Creates a ToolContext from kernel function arguments.
    /// </summary>
    private static ToolContext CreateToolContext(KernelArguments arguments)
    {
        // Extract run ID from kernel context if available
        var runId = arguments.TryGetValue("RunId", out var runIdValue) && runIdValue is string rid
            ? rid
            : Guid.NewGuid().ToString("N");

        var correlationId = arguments.TryGetValue("CorrelationId", out var corrValue) && corrValue is string cid
            ? cid
            : null;

        var workspaceId = arguments.TryGetValue("WorkspaceId", out var wsValue) && wsValue is string wid
            ? wid
            : null;

        var properties = new Dictionary<string, string>();
        foreach (var kvp in arguments.Where(a => a.Value is string))
        {
            if (kvp.Value is string stringValue)
            {
                properties[kvp.Key] = stringValue;
            }
        }

        return new ToolContext
        {
            RunId = runId,
            CorrelationId = correlationId,
            WorkspaceId = workspaceId,
            Properties = properties,
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Creates a KernelParameterMetadata from a ToolParameter.
    /// </summary>
    private static KernelParameterMetadata CreateKernelParameterMetadata(ToolParameter parameter)
    {
        var schema = CreateJsonSchemaForParameter(parameter);

        return new KernelParameterMetadata(parameter.Name)
        {
            Description = parameter.Description,
            IsRequired = parameter.Required,
            DefaultValue = parameter.DefaultValue,
            Schema = schema,
        };
    }

    /// <summary>
    /// Creates a JSON schema for a tool parameter.
    /// </summary>
    private static KernelJsonSchema? CreateJsonSchemaForParameter(ToolParameter parameter)
    {
        var schemaObject = new Dictionary<string, object?>
        {
            ["type"] = MapTypeToJsonSchemaType(parameter.Type),
        };

        if (parameter.EnumValues is not null && parameter.EnumValues.Count > 0)
        {
            schemaObject["enum"] = parameter.EnumValues.ToList();
        }

        if (parameter.Example is not null)
        {
            schemaObject["example"] = parameter.Example;
        }

        var schemaJson = JsonSerializer.Serialize(schemaObject);
        return KernelJsonSchema.Parse(schemaJson);
    }

    /// <summary>
    /// Maps a tool parameter type to JSON schema type.
    /// </summary>
    private static string MapTypeToJsonSchemaType(string toolType)
    {
        return toolType.ToLowerInvariant() switch
        {
            "string" => "string",
            "str" => "string",
            "text" => "string",
            "integer" => "integer",
            "int" => "integer",
            "number" => "number",
            "float" => "number",
            "double" => "number",
            "decimal" => "number",
            "boolean" => "boolean",
            "bool" => "boolean",
            "array" => "array",
            "list" => "array",
            "object" => "object",
            "dict" => "object",
            "dictionary" => "object",
            _ => "string", // Default to string for unknown types
        };
    }

    /// <summary>
    /// Attempts to extract the ToolDescriptor from an ITool instance.
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
