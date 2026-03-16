using System.Text.Json.Serialization;
using nirmata.Aos.Contracts.Tools;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;

namespace nirmata.Agents.Execution.ControlPlane.Tools.Standard;

/// <summary>
/// Tool for reading files within an allowed scope.
/// </summary>
public sealed class FileReadTool : ITool
{
    public ToolDescriptor Descriptor => new()
    {
        Id = "standard.file_read",
        Name = "file_read",
        Description = "Reads the content of a file within the allowed workspace scope.",
        Parameters = new[]
        {
            new ToolParameter { Name = "path", Description = "The relative path to the file to read.", Type = "string", Required = true }
        }
    };

    public async Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
    {
        if (!request.Parameters.TryGetValue("path", out var pathObj) || pathObj is not string relativePath)
        {
            return ToolResult.Failure("MissingPath", "The 'path' parameter is required and must be a string.");
        }

        try
        {
            // Extract allowed scopes and working directory from context metadata if available
            // In SubagentOrchestrator, these are passed in the request.Context
            var allowedScopes = GetAllowedScopes(request);
            var workingDirectory = GetWorkingDirectory(request);

            if (string.IsNullOrEmpty(workingDirectory))
            {
                return ToolResult.Failure("MissingWorkingDirectory", "Working directory not found in context.");
            }

            var fullPath = Path.GetFullPath(Path.Combine(workingDirectory, relativePath));

            if (!IsPathInAllowedScope(fullPath, allowedScopes))
            {
                return ToolResult.Failure("ScopeViolation", $"Path '{relativePath}' is outside the allowed scope.");
            }

            if (!File.Exists(fullPath))
            {
                return ToolResult.Failure("FileNotFound", $"File '{relativePath}' not found.");
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            return ToolResult.Success(new { content, path = relativePath });
        }
        catch (Exception ex)
        {
            return ToolResult.Failure("Error", $"Failed to read file: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> GetAllowedScopes(ToolRequest request)
    {
        if (request.Metadata.TryGetValue("allowedFileScope", out var scopesJson) && !string.IsNullOrEmpty(scopesJson))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(scopesJson) ?? new List<string>();
            }
            catch
            {
                // Fallback to empty if parsing fails
            }
        }
        return new List<string>();
    }

    private static string? GetWorkingDirectory(ToolRequest request)
    {
        return request.Metadata.TryGetValue("workingDirectory", out var wd) ? wd : null;
    }

    private static bool IsPathInAllowedScope(string path, IReadOnlyList<string> allowedScopes)
    {
        // Simple implementation of scope check
        // In a real system, this would be more robust (handling symlinks, normalization, etc.)
        var normalizedPath = Path.GetFullPath(path).Replace('\\', '/');
        foreach (var scope in allowedScopes)
        {
            var normalizedScope = Path.GetFullPath(scope).Replace('\\', '/');
            if (normalizedPath.StartsWith(normalizedScope, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return allowedScopes.Count == 0; // If no scopes defined, allow all (though subagents should always have scopes)
    }
}
