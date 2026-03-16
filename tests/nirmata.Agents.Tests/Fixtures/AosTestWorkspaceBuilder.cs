using System.Text.Json;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;

namespace nirmata.Agents.Tests.Fixtures;

/// <summary>
/// Builder for creating test workspaces with fluent API.
/// Creates a disposable temp directory with .aos/ structure.
/// </summary>
public sealed class AosTestWorkspaceBuilder : IDisposable
{
    private readonly string _tempDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private object? _projectData;
    private object? _roadmapData;
    private StateSnapshot? _stateData;

    /// <summary>
    /// Initializes a new instance of the <see cref="AosTestWorkspaceBuilder"/> class.
    /// </summary>
    public AosTestWorkspaceBuilder()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"nirmata-test-{Guid.NewGuid():N}");
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // Create base directory structure
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".aos"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".aos", "state"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".aos", "evidence"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".aos", "spec"));
    }

    /// <summary>
    /// Gets the repository root path.
    /// </summary>
    public string RepositoryRootPath => _tempDirectory;

    /// <summary>
    /// Gets the AOS root path (.aos directory).
    /// </summary>
    public string AosRootPath => Path.Combine(_tempDirectory, ".aos");

    /// <summary>
    /// Configures the project.json file.
    /// </summary>
    /// <param name="name">Project name.</param>
    /// <param name="description">Project description.</param>
    /// <returns>The builder for method chaining.</returns>
    public AosTestWorkspaceBuilder WithProject(string name = "Test Project", string description = "")
    {
        _projectData = new
        {
            schemaVersion = 1,
            project = new
            {
                name,
                description
            }
        };
        return this;
    }

    /// <summary>
    /// Configures the project.json file with custom data.
    /// </summary>
    /// <param name="projectData">Custom project data object.</param>
    /// <returns>The builder for method chaining.</returns>
    public AosTestWorkspaceBuilder WithProject(object projectData)
    {
        _projectData = projectData;
        return this;
    }

    /// <summary>
    /// Configures the roadmap.json file.
    /// </summary>
    /// <param name="title">Roadmap title.</param>
    /// <param name="items">Roadmap items.</param>
    /// <returns>The builder for method chaining.</returns>
    public AosTestWorkspaceBuilder WithRoadmap(string title = "Test Roadmap", object[]? items = null)
    {
        _roadmapData = new
        {
            schemaVersion = 1,
            roadmap = new
            {
                title,
                items = items ?? Array.Empty<object>()
            }
        };
        return this;
    }

    /// <summary>
    /// Configures the roadmap.json file with custom data.
    /// </summary>
    /// <param name="roadmapData">Custom roadmap data object.</param>
    /// <returns>The builder for method chaining.</returns>
    public AosTestWorkspaceBuilder WithRoadmap(object roadmapData)
    {
        _roadmapData = roadmapData;
        return this;
    }

    /// <summary>
    /// Configures the state.json file.
    /// </summary>
    /// <param name="snapshot">State snapshot to use.</param>
    /// <returns>The builder for method chaining.</returns>
    public AosTestWorkspaceBuilder WithState(StateSnapshot snapshot)
    {
        _stateData = snapshot;
        return this;
    }

    /// <summary>
    /// Configures the state.json file with default values.
    /// </summary>
    /// <param name="milestoneId">Milestone ID.</param>
    /// <param name="phaseId">Phase ID.</param>
    /// <param name="taskId">Task ID.</param>
    /// <param name="stepId">Step ID.</param>
    /// <returns>The builder for method chaining.</returns>
    public AosTestWorkspaceBuilder WithState(
        string? milestoneId = null,
        string? phaseId = null,
        string? taskId = null,
        string? stepId = null)
    {
        _stateData = new StateSnapshot
        {
            SchemaVersion = 1,
            Cursor = new StateCursor
            {
                MilestoneId = milestoneId,
                PhaseId = phaseId,
                TaskId = taskId,
                StepId = stepId,
                MilestoneStatus = "active",
                PhaseStatus = "active",
                TaskStatus = "in-progress",
                StepStatus = "pending"
            }
        };
        return this;
    }

    /// <summary>
    /// Builds the workspace by writing all configured files.
    /// </summary>
    /// <returns>An IWorkspace instance wrapping the created temp directory.</returns>
    public IWorkspace Build()
    {
        // Write project.json if configured
        if (_projectData != null)
        {
            var projectPath = Path.Combine(_tempDirectory, ".aos", "spec", "project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
            var projectJson = JsonSerializer.Serialize(_projectData, _jsonOptions);
            File.WriteAllText(projectPath, projectJson);
        }

        // Write roadmap.json if configured
        if (_roadmapData != null)
        {
            var roadmapPath = Path.Combine(_tempDirectory, ".aos", "spec", "roadmap.json");
            Directory.CreateDirectory(Path.GetDirectoryName(roadmapPath)!);
            var roadmapJson = JsonSerializer.Serialize(_roadmapData, _jsonOptions);
            File.WriteAllText(roadmapPath, roadmapJson);
        }

        // Write state.json if configured
        if (_stateData != null)
        {
            var statePath = Path.Combine(_tempDirectory, ".aos", "state", "state.json");
            var stateJson = JsonSerializer.Serialize(_stateData, _jsonOptions);
            File.WriteAllText(statePath, stateJson);
        }

        return new TestWorkspace(_tempDirectory);
    }

    /// <summary>
    /// Disposes the builder by cleaning up the temp directory.
    /// </summary>
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

    /// <summary>
    /// Simple IWorkspace implementation for test workspaces.
    /// </summary>
    private sealed class TestWorkspace : IWorkspace
    {
        private readonly string _tempDirectory;

        public TestWorkspace(string tempDirectory)
        {
            _tempDirectory = tempDirectory;
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
}
