namespace Gmsd.Agents.Execution.ControlPlane.Tools.Firewall;

/// <summary>
/// Implementation of the scope firewall that validates file paths against allowed scopes.
/// Uses path normalization to handle relative paths, symlinks, and case sensitivity.
/// </summary>
public sealed class ScopeFirewall : IScopeFirewall
{
    private readonly IReadOnlyList<string> _allowedScopes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeFirewall"/> class.
    /// </summary>
    /// <param name="allowedScopes">List of allowed directory scopes (normalized full paths).</param>
    public ScopeFirewall(IReadOnlyList<string> allowedScopes)
    {
        ArgumentNullException.ThrowIfNull(allowedScopes);
        _allowedScopes = allowedScopes;
    }

    /// <inheritdoc />
    public void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Normalize the path to handle relative paths, symlinks, and case sensitivity
        var normalizedPath = NormalizePath(path);

        // Check if the normalized path is within any allowed scope
        var isInAllowedScope = _allowedScopes.Any(scope =>
        {
            var normalizedScope = NormalizePath(scope);
            return IsPathInScope(normalizedPath, normalizedScope);
        });

        if (!isInAllowedScope)
        {
            throw new ScopeViolationException(
                $"Path '{path}' is outside the allowed scope. Allowed scopes: {string.Join(", ", _allowedScopes)}");
        }
    }

    /// <summary>
    /// Normalizes a path to a canonical form for comparison.
    /// Handles relative paths, symlinks, and case sensitivity.
    /// </summary>
    private static string NormalizePath(string path)
    {
        try
        {
            // Get the full path, resolving relative paths and symlinks
            var fullPath = Path.GetFullPath(path);

            // Normalize path separators to forward slashes for consistent comparison
            return fullPath.Replace('\\', '/');
        }
        catch (Exception ex)
        {
            throw new ScopeViolationException($"Failed to normalize path '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if a path is within a given scope.
    /// </summary>
    private static bool IsPathInScope(string normalizedPath, string normalizedScope)
    {
        // Ensure scope ends with a separator to prevent partial directory name matches
        if (!normalizedScope.EndsWith('/'))
        {
            normalizedScope += '/';
        }

        // Check if the path starts with the scope
        return normalizedPath.StartsWith(normalizedScope, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(normalizedScope.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }
}
