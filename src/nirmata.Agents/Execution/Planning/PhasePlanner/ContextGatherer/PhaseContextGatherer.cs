using System.Text.Json;
using nirmata.Aos.Engine.Stores;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Planning.PhasePlanner.ContextGatherer;

/// <summary>
/// Default implementation of the phase context gatherer.
/// Collects roadmap, project, and codebase context to produce a phase brief.
/// </summary>
public sealed class PhaseContextGatherer : IPhaseContextGatherer
{
    private readonly IWorkspace _workspace;
    private readonly SpecStore _specStore;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseContextGatherer"/> class.
    /// </summary>
    public PhaseContextGatherer(IWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _specStore = SpecStore.FromWorkspace(workspace);
    }

    /// <inheritdoc />
    public async Task<PhaseBrief> GatherContextAsync(string phaseId, string runId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(phaseId);
        ArgumentException.ThrowIfNullOrEmpty(runId);

        // Read phase spec from store
        var phaseElement = _specStore.Inner.ReadPhase(phaseId);
        var phaseDoc = JsonSerializer.Deserialize<PhaseDocument>(phaseElement.GetRawText(), _jsonOptions);

        if (phaseDoc is null)
        {
            throw new InvalidOperationException($"Failed to deserialize phase document for {phaseId}");
        }

        // Validate required fields to avoid NREs later
        if (string.IsNullOrEmpty(phaseDoc.Name))
        {
            throw new InvalidOperationException($"Phase document for {phaseId} is missing required field 'Name'");
        }

        // Read project context
        var projectContext = await GatherProjectContextAsync(ct);

        // Read roadmap context for this phase
        var roadmapContext = await GatherRoadmapContextAsync(phaseDoc.MilestoneId, ct);

        // Gather relevant files from codebase
        var relevantFiles = GatherRelevantFiles(phaseId, phaseDoc);

        // Build the phase brief
        var brief = new PhaseBrief
        {
            PhaseId = phaseId,
            PhaseName = phaseDoc.Name,
            Description = phaseDoc.Description,
            MilestoneId = phaseDoc.MilestoneId,
            Goals = roadmapContext.PhaseGoals.GetValueOrDefault(phaseId, new List<string>()).AsReadOnly(),
            Constraints = roadmapContext.Constraints.AsReadOnly(),
            Scope = new PhaseScope
            {
                InScope = phaseDoc.InScope.AsReadOnly(),
                OutOfScope = phaseDoc.OutOfScope.AsReadOnly(),
                Boundaries = phaseDoc.ScopeBoundaries.AsReadOnly()
            },
            InputArtifacts = phaseDoc.InputArtifacts.AsReadOnly(),
            ExpectedOutputs = phaseDoc.OutputArtifacts.AsReadOnly(),
            RelevantFiles = relevantFiles.AsReadOnly(),
            ProjectContext = projectContext,
            RunId = runId,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        return brief;
    }

    private async Task<ProjectContext> GatherProjectContextAsync(CancellationToken ct)
    {
        try
        {
            var projectDoc = _specStore.Inner.ReadProject();

            return new ProjectContext
            {
                TechnologyStack = projectDoc.Project?.Name ?? string.Empty,
                Conventions = new List<string>(),
                ArchitecturePatterns = new List<string>()
            };
        }
        catch
        {
            return new ProjectContext();
        }
    }

    private async Task<RoadmapContextInfo> GatherRoadmapContextAsync(string milestoneId, CancellationToken ct)
    {
        try
        {
            var roadmapDoc = _specStore.Inner.ReadRoadmap();

            // Extract phase goals from milestone deliverables
            var phaseGoals = new Dictionary<string, List<string>>();
            var constraints = new List<string>();

            foreach (var item in roadmapDoc.Roadmap.Items)
            {
                phaseGoals[item.Id] = new List<string> { item.Title };
            }

            return new RoadmapContextInfo
            {
                PhaseGoals = phaseGoals,
                Constraints = constraints
            };
        }
        catch
        {
            return new RoadmapContextInfo();
        }
    }

    private List<CodeFileReference> GatherRelevantFiles(string phaseId, PhaseDocument phaseDoc)
    {
        var files = new List<CodeFileReference>();
        var repositoryRoot = _workspace.RepositoryRootPath;

        // Look for files mentioned in input artifacts
        foreach (var artifact in phaseDoc.InputArtifacts)
        {
            if (artifact.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                artifact.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                artifact.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                var fullPath = Path.Combine(repositoryRoot, artifact.TrimStart('/', '\\'));
                if (File.Exists(fullPath))
                {
                    files.Add(new CodeFileReference
                    {
                        FilePath = fullPath,
                        RelativePath = artifact,
                        Relevance = "Input artifact for phase",
                        FileType = GetFileType(artifact)
                    });
                }
            }
        }

        // Find relevant source files based on phase name and description
        var searchTerms = ExtractSearchTerms(phaseDoc.Name, phaseDoc.Description);
        var sourceFiles = FindSourceFilesMatchingTerms(repositoryRoot, searchTerms);

        foreach (var sourceFile in sourceFiles)
        {
            if (!files.Any(f => f.FilePath == sourceFile))
            {
                var relativePath = Path.GetRelativePath(repositoryRoot, sourceFile);
                files.Add(new CodeFileReference
                {
                    FilePath = sourceFile,
                    RelativePath = relativePath,
                    Relevance = "Potentially relevant based on phase description",
                    FileType = GetFileType(relativePath)
                });
            }
        }

        return files.Take(20).ToList(); // Limit to 20 relevant files
    }

    private static List<string> ExtractSearchTerms(string name, string description)
    {
        var terms = new List<string>();

        // Extract keywords from name
        var nameKeywords = name.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Select(w => w.ToLowerInvariant());
        terms.AddRange(nameKeywords);

        // Extract keywords from description (first 100 chars only)
        if (!string.IsNullOrEmpty(description))
        {
            var descKeywords = description[..Math.Min(100, description.Length)]
                .Split(new[] { ' ', '-', '_', '.', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 4)
                .Select(w => w.ToLowerInvariant())
                .Take(5);
            terms.AddRange(descKeywords);
        }

        return terms.Distinct().ToList();
    }

    private static List<string> FindSourceFilesMatchingTerms(string repositoryRoot, List<string> searchTerms)
    {
        var matches = new List<string>();

        try
        {
            var sourceFiles = Directory.GetFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("/obj/") &&
                           !f.Contains("\\bin\\") && !f.Contains("/bin/"))
                .ToList();

            foreach (var file in sourceFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (searchTerms.Any(term => fileName.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    matches.Add(file);
                }
            }
        }
        catch
        {
            // Ignore filesystem errors
        }

        return matches.Take(10).ToList();
    }

    private static string GetFileType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".cs" => Path.GetFileNameWithoutExtension(path).EndsWith("Tests") ? "test" : "implementation",
            ".json" => "config",
            ".md" => "documentation",
            ".yml" => "config",
            ".yaml" => "config",
            ".csproj" => "project",
            ".sln" => "solution",
            _ => "unknown"
        };
    }
}

// Internal DTOs for JSON deserialization
internal record PhaseDocument(
    string PhaseId,
    string Name,
    string Description,
    string MilestoneId,
    List<string> InScope,
    List<string> OutOfScope,
    List<string> ScopeBoundaries,
    List<string> InputArtifacts,
    List<string> OutputArtifacts
)
{
    public List<string> InScope { get; init; } = InScope ?? new List<string>();
    public List<string> OutOfScope { get; init; } = OutOfScope ?? new List<string>();
    public List<string> ScopeBoundaries { get; init; } = ScopeBoundaries ?? new List<string>();
    public List<string> InputArtifacts { get; init; } = InputArtifacts ?? new List<string>();
    public List<string> OutputArtifacts { get; init; } = OutputArtifacts ?? new List<string>();
}

internal record ProjectDocument(
    string? TechnologyStack,
    List<string>? Conventions,
    List<string>? ArchitecturePatterns
);

internal record RoadmapDocument(
    List<MilestoneDocument> Milestones
);

internal record MilestoneDocument(
    string MilestoneId,
    string Name,
    string Description,
    List<PhaseInMilestone> Phases
);

internal record PhaseInMilestone(
    string PhaseId,
    string Name,
    string Description,
    List<string> Deliverables
);

internal class RoadmapContextInfo
{
    public Dictionary<string, List<string>> PhaseGoals { get; init; } = new();
    public List<string> Constraints { get; init; } = new();
}
