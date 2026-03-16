using System.Text.Json.Serialization;
using nirmata.Aos.Contracts.Tools;
using nirmata.Agents.Execution.ControlPlane.Tools.Contracts;

namespace nirmata.Agents.Execution.ControlPlane.Tools.Standard;

/// <summary>
/// Tool for writing content to files within an allowed scope.
/// </summary>
public sealed class FileWriteTool : ITool
{
    public ToolDescriptor Descriptor => new()
    {
        Id = "standard.file_write",
        Name = "file_write",
        Description = "Writes content to a file within the allowed workspace scope.",
        Parameters = new[]
        {
            new ToolParameter { Name = "path", Description = "The relative path to the file to write.", Type = "string", Required = true },
            new ToolParameter { Name = "content", Description = "The content to write to the file.", Type = "string", Required = true }
        }
    };

    public async Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
    {
        if (!request.Parameters.TryGetValue("path", out var pathObj) || pathObj is not string relativePath)
        {
            return ToolResult.Failure("MissingPath", "The 'path' parameter is required.");
        }

        if (!request.Parameters.TryGetValue("content", out var contentObj) || contentObj is not string content)
        {
            return ToolResult.Failure("MissingContent", "The 'content' parameter is required.");
        }

        try
        {
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

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content, cancellationToken);
            
            return ToolResult.Success(new 
            { 
                path = relativePath, 
                bytesWritten = System.Text.Encoding.UTF8.GetByteCount(content),
                modifiedFile = relativePath
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Failure("Error", $"Failed to write file: {ex.Message}");
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
                // Fallback
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
        var normalizedPath = Path.GetFullPath(path).Replace('\\', '/');
        foreach (var scope in allowedScopes)
        {
            var normalizedScope = Path.GetFullPath(scope).Replace('\\', '/');
            if (normalizedPath.StartsWith(normalizedScope, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return allowedScopes.Count == 0;
    }
}
