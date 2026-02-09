namespace Gmsd.Aos.Engine.Workspace;

/// <summary>
/// Deterministic repository root discovery for commands and consumers that do not provide <c>--root</c>.
/// </summary>
internal static class AosRepositoryRootDiscovery
{
    public static string DiscoverOrThrow(string? startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            startPath = Directory.GetCurrentDirectory();
        }

        var dir = new DirectoryInfo(startPath);
        while (dir is not null)
        {
            var gitPath = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitPath))
            {
                return dir.FullName;
            }

            var slnxPath = Path.Combine(dir.FullName, "Gmsd.slnx");
            if (File.Exists(slnxPath))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new AosRepositoryRootNotFoundException(startPath);
    }
}

internal sealed class AosRepositoryRootNotFoundException : InvalidOperationException
{
    public string StartPath { get; }

    public AosRepositoryRootNotFoundException(string startPath)
        : base(
            $"Repository root could not be determined from '{startPath}'. " +
            "Expected to find one of: '.git/' directory, 'Gmsd.slnx' file. " +
            "Re-run with '--root <path>' to specify the repository root explicitly."
        )
    {
        StartPath = startPath ?? "";
    }
}

