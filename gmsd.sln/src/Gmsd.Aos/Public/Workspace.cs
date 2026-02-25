using System.Text.Json;
using Gmsd.Aos.Engine.Paths;
using Gmsd.Aos.Engine.Workspace;

namespace Gmsd.Aos.Public;

/// <summary>
/// Default public workspace implementation for consumers that need repository root discovery and
/// canonical contract-path resolution without referencing internal engine namespaces.
/// </summary>
public sealed class Workspace : IWorkspace
{
    public string RepositoryRootPath { get; }
    public string AosRootPath { get; }

    private Workspace(string repositoryRootPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
        {
            throw new ArgumentException("Missing repository root path.", nameof(repositoryRootPath));
        }

        RepositoryRootPath = repositoryRootPath;
        AosRootPath = Path.Combine(repositoryRootPath, ".aos");
    }

    /// <summary>
    /// Creates a workspace rooted at an explicit repository root path.
    /// </summary>
    public static Workspace FromRepositoryRoot(string repositoryRootPath) => new(repositoryRootPath);

    /// <summary>
    /// Discovers repository root from the current working directory using deterministic markers.
    /// </summary>
    public static Workspace DiscoverFromCurrentDirectory() =>
        DiscoverFrom(Directory.GetCurrentDirectory());

    /// <summary>
    /// Discovers repository root from <paramref name="startPath"/> using deterministic markers.
    /// </summary>
    public static Workspace DiscoverFrom(string startPath) =>
        new(AosRepositoryRootDiscovery.DiscoverOrThrow(startPath));

    public string GetContractPathForArtifactId(string artifactId) =>
        AosPathRouter.GetContractPathForArtifactId(artifactId);

    public string GetAbsolutePathForContractPath(string contractPath) =>
        AosPathRouter.ToAosRootPath(AosRootPath, contractPath);

    public string GetAbsolutePathForArtifactId(string artifactId) =>
        GetAbsolutePathForContractPath(GetContractPathForArtifactId(artifactId));

    public JsonElement ReadArtifact(string subpath, string filename)
    {
        var fullPath = Path.Combine(AosRootPath, subpath, filename);
        using var stream = File.OpenRead(fullPath);
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }
}

