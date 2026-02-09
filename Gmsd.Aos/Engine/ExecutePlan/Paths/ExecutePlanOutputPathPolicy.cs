namespace Gmsd.Aos.Engine.ExecutePlan;

internal static class ExecutePlanOutputPathPolicy
{
    public static bool TryValidateRelativePath(string relativePath, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            error = "path MUST be a non-empty string.";
            return false;
        }

        // Reject absolute / rooted paths:
        // - Windows: "C:\x", "\x", "\\server\share\x"
        // - Unix: "/x"
        if (Path.IsPathFullyQualified(relativePath) || Path.IsPathRooted(relativePath))
        {
            error = "absolute (rooted) paths are not allowed.";
            return false;
        }

        // Reject traversal attempts even if they might normalize back under root.
        // This keeps the rule simple and obvious for plan authors.
        var segments = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries
        );

        foreach (var segment in segments)
        {
            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                error = "path traversal ('..') is not allowed.";
                return false;
            }
        }

        return true;
    }

    public static string ResolveFilePathUnderRoot(string rootDirectoryPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootDirectoryPath))
        {
            throw new ArgumentException("Missing root directory path.", nameof(rootDirectoryPath));
        }

        if (!TryValidateRelativePath(relativePath, out var error))
        {
            throw new InvalidOperationException($"Invalid relative path '{relativePath}': {error}");
        }

        var rootFullPath = Path.GetFullPath(rootDirectoryPath);
        var candidateFullPath = Path.GetFullPath(Path.Combine(rootFullPath, relativePath));

        // Enforce that the final resolved path is still under the intended root.
        // (Defense-in-depth; also protects against odd rooted/path parsing edge cases.)
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootPrefix = EnsureTrailingDirectorySeparator(rootFullPath);

        if (!candidateFullPath.StartsWith(rootPrefix, comparison))
        {
            throw new InvalidOperationException(
                $"Invalid relative path '{relativePath}': resolved path escapes the root directory."
            );
        }

        return candidateFullPath;
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        if (path.Length == 0)
        {
            return path;
        }

        var last = path[^1];
        if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}

