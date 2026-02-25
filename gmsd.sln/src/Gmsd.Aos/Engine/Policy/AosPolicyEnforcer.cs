using Gmsd.Aos.Engine.Errors;

namespace Gmsd.Aos.Engine.Policy;

internal static class AosPolicyEnforcer
{
    public static void EnsureWritePathAllowed(
        string repositoryRootPath,
        IReadOnlyList<string> writeAllowlist,
        string fullTargetPath,
        string targetLabel)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));
        if (writeAllowlist is null) throw new ArgumentNullException(nameof(writeAllowlist));
        if (fullTargetPath is null) throw new ArgumentNullException(nameof(fullTargetPath));
        if (targetLabel is null) throw new ArgumentNullException(nameof(targetLabel));

        var targetFull = Path.GetFullPath(fullTargetPath);
        if (IsUnderAnyAllowedRoot(repositoryRootPath, writeAllowlist, targetFull))
        {
            return;
        }

        var allowed = writeAllowlist
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().Replace('\\', '/'))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var targetNormalized = targetFull.Replace('\\', '/');

        throw new AosPolicyViolationException(
            $"Policy forbids writing '{targetLabel}' at '{targetNormalized}'. " +
            $"Allowed write scopes: [{string.Join(", ", allowed.Select(a => $"'{a}'"))}]."
        );
    }

    private static bool IsUnderAnyAllowedRoot(
        string repositoryRootPath,
        IReadOnlyList<string> allowlist,
        string targetFullPath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var raw in allowlist)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var entry = raw.Trim();
            if (Path.IsPathFullyQualified(entry) || Path.IsPathRooted(entry))
            {
                // Reject rooted allowlist entries; validator should already catch these.
                continue;
            }

            // Treat allowlist entries as repository-root relative prefixes (contract-ish paths).
            var normalizedEntry = entry.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var allowedFull = Path.GetFullPath(Path.Combine(repositoryRootPath, normalizedEntry));

            if (string.Equals(targetFullPath, allowedFull, comparison))
            {
                return true;
            }

            var allowedPrefix = EnsureTrailingDirectorySeparator(allowedFull);
            if (targetFullPath.StartsWith(allowedPrefix, comparison))
            {
                return true;
            }
        }

        return false;
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        if (string.IsNullOrEmpty(path))
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

