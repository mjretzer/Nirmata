using System.Text;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosRepositoryRootDiscoveryTests
{
    [Fact]
    public void DiscoverOrThrow_UsesGitDirectoryMarker()
    {
        var root = CreateTempDirectory("nirmata-aos-repo-root-git");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            var child = Path.Combine(root, "a", "b");
            Directory.CreateDirectory(child);

            var discovered = AosRepositoryRootDiscovery.DiscoverOrThrow(child);
            Assert.Equal(root, discovered);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void DiscoverOrThrow_UsesSlnxMarker()
    {
        var root = CreateTempDirectory("nirmata-aos-repo-root-slnx");
        try
        {
            File.WriteAllText(Path.Combine(root, "nirmata.slnx"), "", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var child = Path.Combine(root, "x");
            Directory.CreateDirectory(child);

            var discovered = AosRepositoryRootDiscovery.DiscoverOrThrow(child);
            Assert.Equal(root, discovered);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void DiscoverOrThrow_FailsWithActionableError_WhenNoMarkerFound()
    {
        var root = CreateTempDirectory("nirmata-aos-repo-root-missing");
        try
        {
            var ex = Assert.Throws<AosRepositoryRootNotFoundException>(() => AosRepositoryRootDiscovery.DiscoverOrThrow(root));
            Assert.Contains("Repository root could not be determined", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("--root", ex.Message, StringComparison.Ordinal);
            Assert.Contains(".git", ex.Message, StringComparison.Ordinal);
            Assert.Contains("nirmata.slnx", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory(string prefix)
    {
        var root = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}

