using System.Text.Json;
using nirmata.Aos.Public;

namespace nirmata.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of IWorkspace for unit testing.
/// </summary>
public sealed class FakeWorkspace : IWorkspace, IDisposable
{
    private readonly string _tempDirectory;

    public FakeWorkspace()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"nirmata-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".aos"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".aos", "state"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".aos", "evidence"));

        RepositoryRootPath = _tempDirectory;
        AosRootPath = Path.Combine(_tempDirectory, ".aos");

        SeedSchemas();
    }

    private void SeedSchemas()
    {
        var schemasDir = Path.Combine(AosRootPath, "schemas");
        Directory.CreateDirectory(schemasDir);

        // Create registry.json
        var registry = new
        {
            schemaVersion = 1,
            schemas = new[] { "task-v1.schema.json" }
        };
        File.WriteAllText(Path.Combine(schemasDir, "registry.json"), JsonSerializer.Serialize(registry));

        // Create task-v1.schema.json schema
        var taskSchema = new Dictionary<string, object>
        {
            ["$id"] = "nirmata:aos:schema:task:v1",
            ["type"] = "object",
            ["properties"] = new
            {
                schemaVersion = new { type = "integer" },
                id = new { type = "string" },
                type = new { type = "string" },
                status = new { type = "string" },
                parentTaskId = new { type = "string" },
                issueIds = new { type = "array", items = new { type = "string" } },
                title = new { type = "string" },
                description = new { type = "string" },
                createdAt = new { type = "string" }
            },
            ["required"] = new[] { "schemaVersion", "id", "type", "status" }
        };
        File.WriteAllText(Path.Combine(schemasDir, "task-v1.schema.json"), JsonSerializer.Serialize(taskSchema));
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
