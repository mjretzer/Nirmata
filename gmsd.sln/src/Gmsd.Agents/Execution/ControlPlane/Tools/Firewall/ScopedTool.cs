using System.Text.Json;
using Gmsd.Aos.Contracts.Tools;

namespace Gmsd.Agents.Execution.ControlPlane.Tools.Firewall;

/// <summary>
/// Decorator for ITool that validates file paths before execution.
/// Wraps any ITool implementation and enforces scope firewall constraints.
/// </summary>
public sealed class ScopedTool : ITool
{
    private readonly ITool _innerTool;
    private readonly IScopeFirewall _firewall;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedTool"/> class.
    /// </summary>
    /// <param name="innerTool">The underlying tool implementation to wrap.</param>
    /// <param name="firewall">The scope firewall for path validation.</param>
    public ScopedTool(ITool innerTool, IScopeFirewall firewall)
    {
        _innerTool = innerTool ?? throw new ArgumentNullException(nameof(innerTool));
        _firewall = firewall ?? throw new ArgumentNullException(nameof(firewall));
    }

    /// <inheritdoc />
    public async Task<ToolResult> InvokeAsync(
        ToolRequest request,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            // Validate all file paths in the request parameters
            ValidatePathsInRequest(request);

            // If validation passes, invoke the inner tool
            return await _innerTool.InvokeAsync(request, context, cancellationToken);
        }
        catch (ScopeViolationException ex)
        {
            // Return a structured error result for scope violations
            return ToolResult.Failure("ScopeViolation", ex.Message);
        }
    }

    /// <summary>
    /// Validates all file paths in the tool request parameters.
    /// </summary>
    private void ValidatePathsInRequest(ToolRequest request)
    {
        foreach (var parameter in request.Parameters)
        {
            ValidateParameterValue(parameter.Value);
        }
    }

    /// <summary>
    /// Recursively validates file paths in parameter values.
    /// Handles strings, arrays, and nested objects.
    /// </summary>
    private void ValidateParameterValue(object? value)
    {
        if (value == null)
            return;

        switch (value)
        {
            case string strValue:
                // Check if this looks like a file path
                if (IsLikelyFilePath(strValue))
                {
                    _firewall.ValidatePath(strValue);
                }
                break;

            case JsonElement jsonElement:
                ValidateJsonElement(jsonElement);
                break;

            case System.Collections.IDictionary dict:
                // Handle dictionaries (must come before IEnumerable since IDictionary implements IEnumerable)
                foreach (var key in dict.Keys)
                {
                    ValidateParameterValue(dict[key]);
                }
                break;

            case System.Collections.IEnumerable enumerable:
                // Handle arrays and collections
                foreach (var item in enumerable)
                {
                    ValidateParameterValue(item);
                }
                break;
        }
    }

    /// <summary>
    /// Validates file paths in a JsonElement.
    /// </summary>
    private void ValidateJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var strValue = element.GetString();
                if (!string.IsNullOrEmpty(strValue) && IsLikelyFilePath(strValue))
                {
                    _firewall.ValidatePath(strValue);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    ValidateJsonElement(item);
                }
                break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    ValidateJsonElement(property.Value);
                }
                break;
        }
    }

    /// <summary>
    /// Heuristic to determine if a string is likely a file path.
    /// Checks for common path separators and file extensions.
    /// </summary>
    private static bool IsLikelyFilePath(string value)
    {
        // Check for path separators
        if (value.Contains('/') || value.Contains('\\'))
            return true;

        // Check for common file extensions
        var commonExtensions = new[] { ".cs", ".json", ".xml", ".txt", ".md", ".csproj", ".sln", ".ps1" };
        return commonExtensions.Any(ext => value.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
}
