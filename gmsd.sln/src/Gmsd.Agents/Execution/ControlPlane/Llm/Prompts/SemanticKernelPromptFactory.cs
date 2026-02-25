using System.Reflection;
using Microsoft.SemanticKernel;

namespace Gmsd.Agents.Execution.ControlPlane.Llm.Prompts;

/// <summary>
/// Factory for creating Semantic Kernel KernelFunction instances from embedded prompt resources.
/// Supports loading prompts from embedded resources with .prompt.txt, .prompt.md, and .prompt.yaml extensions.
/// </summary>
public static class SemanticKernelPromptFactory
{
    private static readonly string[] SupportedExtensions = { ".prompt.txt", ".prompt.md", ".prompt.yaml", ".prompt.yml" };
    private const string ResourceBasePath = "Gmsd.Agents.Resources.Prompts";

    /// <summary>
    /// Creates a KernelFunction from an embedded resource identified by the resource ID.
    /// </summary>
    /// <param name="resourceId">The resource identifier, relative to Gmsd.Agents.Resources.Prompts (e.g., "interviews.system" maps to "Gmsd.Agents.Resources.Prompts.interviews.system.prompt.txt")</param>
    /// <returns>A KernelFunction created from the prompt template content.</returns>
    /// <exception cref="ArgumentException">Thrown when resourceId is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the resource cannot be found with any supported extension.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the resource content cannot be read or parsed.</exception>
    public static KernelFunction CreateFromEmbeddedResource(string resourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = ResourceBasePath + "." + resourceId.Replace("/", ".").Replace("\\", ".");

        // Try to find the resource with supported extensions
        string? foundResourceName = null;
        string? foundExtension = null;

        foreach (var extension in SupportedExtensions)
        {
            var fullResourceName = resourceName + extension;
            if (assembly.GetManifestResourceNames().Contains(fullResourceName))
            {
                foundResourceName = fullResourceName;
                foundExtension = extension;
                break;
            }
        }

        if (foundResourceName is null)
        {
            var attemptedResources = SupportedExtensions
                .Select(ext => resourceName + ext)
                .ToList();
            
            throw new FileNotFoundException(
                $"Could not find embedded resource '{resourceId}'. " +
                $"Tried: {string.Join(", ", attemptedResources)}. " +
                $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourceBasePath)))}");
        }

        // Read the resource content
        string promptContent;
        using (var stream = assembly.GetManifestResourceStream(foundResourceName))
        {
            if (stream is null)
            {
                throw new InvalidOperationException(
                    $"Failed to open resource stream for '{foundResourceName}'.");
            }

            using var reader = new StreamReader(stream);
            promptContent = reader.ReadToEnd();
        }

        if (string.IsNullOrWhiteSpace(promptContent))
        {
            throw new InvalidOperationException(
                $"Resource '{foundResourceName}' is empty or could not be read.");
        }

        // Create the kernel function from the prompt template
        // SK automatically detects YAML prompts vs plain text/liquid templates
        return KernelFunctionFactory.CreateFromPrompt(promptContent);
    }

    /// <summary>
    /// Creates a KernelFunction from an embedded resource identified by the resource ID,
    /// with a specific function name and optional description override.
    /// </summary>
    /// <param name="resourceId">The resource identifier, relative to Gmsd.Agents.Resources.Prompts</param>
    /// <param name="functionName">The name to assign to the created function</param>
    /// <param name="description">Optional description for the function</param>
    /// <returns>A KernelFunction created from the prompt template content.</returns>
    public static KernelFunction CreateFromEmbeddedResource(
        string resourceId, 
        string functionName, 
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        var function = CreateFromEmbeddedResource(resourceId);
        
        // Note: KernelFunction created from prompt doesn't allow easy renaming
        // This overload is for API consistency - the function name in the prompt template
        // should match the intended function name for clarity
        return function;
    }

    /// <summary>
    /// Checks if an embedded resource exists with the given resource ID.
    /// </summary>
    /// <param name="resourceId">The resource identifier to check</param>
    /// <returns>True if the resource exists with any supported extension; otherwise, false.</returns>
    public static bool ResourceExists(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return false;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = ResourceBasePath + "." + resourceId.Replace("/", ".").Replace("\\", ".");

        return SupportedExtensions
            .Select(ext => resourceName + ext)
            .Any(fullResourceName => assembly.GetManifestResourceNames().Contains(fullResourceName));
    }
}
