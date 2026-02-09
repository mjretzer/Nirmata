using System.Text.Json;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of IWorkspace for unit testing.
/// </summary>
public sealed class FakeWorkspace : IWorkspace
{
    private readonly string _tempDirectory;

    public FakeWorkspace()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"gmsd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".aos"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".aos", "state"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".aos", "evidence"));

        RepositoryRootPath = _tempDirectory;
        AosRootPath = Path.Combine(_tempDirectory, ".aos");
    }

    public string RepositoryRootPath { get; }

    public string AosRootPath { get; }

    public string GetContractPathForArtifactId(string artifactId)
    {
        return artifactId switch
        {
            "project" => ".aos/spec/project.json",
            "roadmap" => ".aos/spec/roadmap.json",
            "plan" => ".aos/spec/plan.json",
            _ => $".aos/{artifactId}.json"
        };
    }

    public string GetAbsolutePathForContractPath(string contractPath)
    {
        return Path.Combine(RepositoryRootPath, contractPath);
    }

    public string GetAbsolutePathForArtifactId(string artifactId)
    {
        return artifactId switch
        {
            "project" => Path.Combine(AosRootPath, "spec", "project.json"),
            "roadmap" => Path.Combine(AosRootPath, "spec", "roadmap.json"),
            "plan" => Path.Combine(AosRootPath, "spec", "plan.json"),
            _ => Path.Combine(AosRootPath, $"{artifactId}.json")
        };
    }

    public JsonElement ReadArtifact(string subpath, string filename)
    {
        var fullPath = Path.Combine(AosRootPath, subpath, filename);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Artifact not found: {fullPath}");
        }

        var content = File.ReadAllText(fullPath);
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
