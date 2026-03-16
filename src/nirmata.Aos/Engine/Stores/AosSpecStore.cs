using System.Text.Json;
using nirmata.Aos.Engine.Paths;
using nirmata.Aos.Engine.Spec;

namespace nirmata.Aos.Engine.Stores;

internal sealed class AosSpecStore : AosJsonStoreBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public const string ProjectContractPath = ".aos/spec/project.json";
    public const string RoadmapContractPath = ".aos/spec/roadmap.json";

    public AosSpecStore(string aosRootPath)
        : base(aosRootPath, ".aos/spec/")
    {
    }

    public ProjectSpecDocument ReadProject() =>
        ReadJson<ProjectSpecDocument>(ProjectContractPath, JsonOptions);

    public void WriteProjectIfMissing(ProjectSpecDocument doc) =>
        WriteJsonIfMissing(ProjectContractPath, doc, JsonOptions, writeIndented: true);

    public void WriteProjectOverwrite(ProjectSpecDocument doc) =>
        WriteJsonOverwrite(ProjectContractPath, doc, JsonOptions, writeIndented: true);

    public RoadmapSpecDocument ReadRoadmap() =>
        ReadJson<RoadmapSpecDocument>(RoadmapContractPath, JsonOptions);

    public void WriteRoadmapIfMissing(RoadmapSpecDocument doc) =>
        WriteJsonIfMissing(RoadmapContractPath, doc, JsonOptions, writeIndented: true);

    public void WriteRoadmapOverwrite(RoadmapSpecDocument doc) =>
        WriteJsonOverwrite(RoadmapContractPath, doc, JsonOptions, writeIndented: true);

    // ------------------------------
    // Spec artifact CRUD (PH-ENG-0004)
    // ------------------------------

    public JsonElement ReadMilestone(string milestoneId) =>
        ReadJsonElement(GetSpecArtifactContractPath(AosArtifactKind.Milestone, milestoneId));

    public void WriteMilestoneIfMissing(string milestoneId, JsonElement doc)
    {
        WriteJsonIfMissing(GetSpecArtifactContractPath(AosArtifactKind.Milestone, milestoneId), doc, JsonOptions, writeIndented: true);
        EnsureCatalogIndexIncludes(AosArtifactKind.Milestone, milestoneId);
    }

    public void WriteMilestoneOverwrite(string milestoneId, JsonElement doc)
    {
        WriteJsonOverwrite(GetSpecArtifactContractPath(AosArtifactKind.Milestone, milestoneId), doc, JsonOptions, writeIndented: true);
        EnsureCatalogIndexIncludes(AosArtifactKind.Milestone, milestoneId);
    }

    public void DeleteMilestone(string milestoneId)
    {
        DeleteFile(GetSpecArtifactContractPath(AosArtifactKind.Milestone, milestoneId));
        EnsureCatalogIndexExcludes(AosArtifactKind.Milestone, milestoneId);
    }

    public JsonElement ReadPhase(string phaseId) =>
        ReadJsonElement(GetSpecArtifactContractPath(AosArtifactKind.Phase, phaseId));

    public void WritePhaseIfMissing(string phaseId, JsonElement doc)
    {
        WriteJsonIfMissing(GetSpecArtifactContractPath(AosArtifactKind.Phase, phaseId), doc, JsonOptions, writeIndented: true);
        EnsureCatalogIndexIncludes(AosArtifactKind.Phase, phaseId);
    }

    public void WritePhaseOverwrite(string phaseId, JsonElement doc)
    {
        WriteJsonOverwrite(GetSpecArtifactContractPath(AosArtifactKind.Phase, phaseId), doc, JsonOptions, writeIndented: true);
        EnsureCatalogIndexIncludes(AosArtifactKind.Phase, phaseId);
    }

    public void DeletePhase(string phaseId)
    {
        DeleteFile(GetSpecArtifactContractPath(AosArtifactKind.Phase, phaseId));
        EnsureCatalogIndexExcludes(AosArtifactKind.Phase, phaseId);
    }

    public JsonElement ReadTask(string taskId) =>
        ReadJsonElement(GetSpecArtifactContractPath(AosArtifactKind.Task, taskId));

    public void WriteTaskIfMissing(string taskId, JsonElement doc)
    {
        WriteJsonIfMissing(GetSpecArtifactContractPath(AosArtifactKind.Task, taskId), doc, JsonOptions, writeIndented: true);
        EnsureCatalogIndexIncludes(AosArtifactKind.Task, taskId);
    }

    public void WriteTaskOverwrite(string taskId, JsonElement doc)
    {
        WriteJsonOverwrite(GetSpecArtifactContractPath(AosArtifactKind.Task, taskId), doc, JsonOptions, writeIndented: true);
        EnsureCatalogIndexIncludes(AosArtifactKind.Task, taskId);
    }

    public void DeleteTask(string taskId)
    {
        DeleteFile(GetSpecArtifactContractPath(AosArtifactKind.Task, taskId));
        EnsureCatalogIndexExcludes(AosArtifactKind.Task, taskId);
    }

    public JsonElement ReadIssue(string issueId) =>
        ReadJsonElement(GetSpecArtifactContractPath(AosArtifactKind.Issue, issueId));

    public void WriteIssueIfMissing(string issueId, JsonElement doc)
    {
        WriteJsonIfMissing(GetSpecArtifactContractPath(AosArtifactKind.Issue, issueId), doc, JsonOptions, writeIndented: true);
        EnsureCatalogIndexIncludes(AosArtifactKind.Issue, issueId);
    }

    public void WriteIssueOverwrite(string issueId, JsonElement doc)
    {
        WriteJsonOverwrite(GetSpecArtifactContractPath(AosArtifactKind.Issue, issueId), doc, JsonOptions, writeIndented: true);
        EnsureCatalogIndexIncludes(AosArtifactKind.Issue, issueId);
    }

    public void DeleteIssue(string issueId)
    {
        DeleteFile(GetSpecArtifactContractPath(AosArtifactKind.Issue, issueId));
        EnsureCatalogIndexExcludes(AosArtifactKind.Issue, issueId);
    }

    public JsonElement ReadUat(string uatId) =>
        ReadJsonElement(GetSpecArtifactContractPath(AosArtifactKind.Uat, uatId));

    public void WriteUatIfMissing(string uatId, JsonElement doc)
    {
        WriteJsonIfMissing(GetSpecArtifactContractPath(AosArtifactKind.Uat, uatId), doc, JsonOptions, writeIndented: true);
        EnsureCatalogIndexIncludes(AosArtifactKind.Uat, uatId);
    }

    public void WriteUatOverwrite(string uatId, JsonElement doc)
    {
        WriteJsonOverwrite(GetSpecArtifactContractPath(AosArtifactKind.Uat, uatId), doc, JsonOptions, writeIndented: true);
        EnsureCatalogIndexIncludes(AosArtifactKind.Uat, uatId);
    }

    public void DeleteUat(string uatId)
    {
        DeleteFile(GetSpecArtifactContractPath(AosArtifactKind.Uat, uatId));
        EnsureCatalogIndexExcludes(AosArtifactKind.Uat, uatId);
    }

    // Task sub-artifacts (schema-light)
    public JsonElement ReadTaskPlan(string taskId) =>
        ReadJsonElement(GetTaskSubArtifactContractPath(taskId, "plan.json"));

    public void WriteTaskPlanIfMissing(string taskId, JsonElement doc) =>
        WriteJsonIfMissing(GetTaskSubArtifactContractPath(taskId, "plan.json"), doc, JsonOptions, writeIndented: true);

    public void WriteTaskPlanOverwrite(string taskId, JsonElement doc) =>
        WriteJsonOverwrite(GetTaskSubArtifactContractPath(taskId, "plan.json"), doc, JsonOptions, writeIndented: true);

    public void DeleteTaskPlan(string taskId) =>
        DeleteFile(GetTaskSubArtifactContractPath(taskId, "plan.json"));

    public JsonElement ReadTaskLinks(string taskId) =>
        ReadJsonElement(GetTaskSubArtifactContractPath(taskId, "links.json"));

    public void WriteTaskLinksIfMissing(string taskId, JsonElement doc) =>
        WriteJsonIfMissing(GetTaskSubArtifactContractPath(taskId, "links.json"), doc, JsonOptions, writeIndented: true);

    public void WriteTaskLinksOverwrite(string taskId, JsonElement doc) =>
        WriteJsonOverwrite(GetTaskSubArtifactContractPath(taskId, "links.json"), doc, JsonOptions, writeIndented: true);

    public void DeleteTaskLinks(string taskId) =>
        DeleteFile(GetTaskSubArtifactContractPath(taskId, "links.json"));

    public string GetSpecArtifactContractPath(string artifactId)
    {
        if (!AosPathRouter.TryParseArtifactId(artifactId, out var kind, out var normalized, out var error))
        {
            throw new ArgumentException(error, nameof(artifactId));
        }

        if (kind == AosArtifactKind.Run)
        {
            throw new ArgumentException("RUN artifacts are not part of the spec store.", nameof(artifactId));
        }

        return AosPathRouter.GetContractPath(kind, normalized);
    }

    private static string GetSpecArtifactContractPath(AosArtifactKind expectedKind, string artifactId)
    {
        var normalizedId = NormalizeAndValidateKind(expectedKind, artifactId);
        return AosPathRouter.GetContractPath(expectedKind, normalizedId);
    }

    private static string GetTaskSubArtifactContractPath(string taskId, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("Missing file name.", nameof(fileName));
        if (fileName.Contains('/')) throw new ArgumentException("Sub-artifact file name MUST NOT contain '/'.", nameof(fileName));
        if (fileName.Contains('\\')) throw new ArgumentException("Sub-artifact file name MUST NOT contain '\\'.", nameof(fileName));
        if (fileName is "." or "..") throw new ArgumentException("Sub-artifact file name MUST NOT be '.' or '..'.", nameof(fileName));

        var normalizedId = NormalizeAndValidateKind(AosArtifactKind.Task, taskId);
        return $".aos/spec/tasks/{normalizedId}/{fileName}";
    }

    public CatalogIndexDocument ReadCatalogIndex(AosArtifactKind kind) =>
        ReadJson<CatalogIndexDocument>(GetCatalogIndexContractPath(kind), JsonOptions);

    public void WriteCatalogIndexIfMissing(AosArtifactKind kind, CatalogIndexDocument doc) =>
        WriteJsonIfMissing(GetCatalogIndexContractPath(kind), CanonicalizeIndex(doc), JsonOptions, writeIndented: true);

    public void WriteCatalogIndexOverwrite(AosArtifactKind kind, CatalogIndexDocument doc) =>
        WriteJsonOverwrite(GetCatalogIndexContractPath(kind), CanonicalizeIndex(doc), JsonOptions, writeIndented: true);

    public void EnsureCatalogIndexIncludes(AosArtifactKind kind, string artifactId)
    {
        var normalizedId = NormalizeAndValidateKind(kind, artifactId);
        var contractPath = GetCatalogIndexContractPath(kind);

        CatalogIndexDocument existing;
        if (!Exists(contractPath))
        {
            existing = new CatalogIndexDocument(SchemaVersion: 1, Items: Array.Empty<string>());
        }
        else
        {
            existing = ReadJson<CatalogIndexDocument>(contractPath, JsonOptions);
        }

        if (existing.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported catalog index schemaVersion '{existing.SchemaVersion}' at '{contractPath}'.");
        }

        var items = existing.Items
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .ToHashSet(StringComparer.Ordinal);

        items.Add(normalizedId);

        var updated = new CatalogIndexDocument(
            SchemaVersion: 1,
            Items: items.OrderBy(i => i, StringComparer.Ordinal).ToArray()
        );

        WriteJsonOverwrite(contractPath, updated, JsonOptions, writeIndented: true);
    }

    public void EnsureCatalogIndexExcludes(AosArtifactKind kind, string artifactId)
    {
        var normalizedId = NormalizeAndValidateKind(kind, artifactId);
        var contractPath = GetCatalogIndexContractPath(kind);

        if (!Exists(contractPath))
        {
            // Nothing to do; we keep delete idempotent.
            return;
        }

        var existing = ReadJson<CatalogIndexDocument>(contractPath, JsonOptions);
        if (existing.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported catalog index schemaVersion '{existing.SchemaVersion}' at '{contractPath}'.");
        }

        var items = existing.Items
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Where(i => !string.Equals(i, normalizedId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(i => i, StringComparer.Ordinal)
            .ToArray();

        var updated = new CatalogIndexDocument(SchemaVersion: 1, Items: items);
        WriteJsonOverwrite(contractPath, updated, JsonOptions, writeIndented: true);
    }

    public static string GetCatalogIndexContractPath(AosArtifactKind kind) =>
        kind switch
        {
            AosArtifactKind.Milestone => ".aos/spec/milestones/index.json",
            AosArtifactKind.Phase => ".aos/spec/phases/index.json",
            AosArtifactKind.Task => ".aos/spec/tasks/index.json",
            AosArtifactKind.Issue => ".aos/spec/issues/index.json",
            AosArtifactKind.Uat => ".aos/spec/uat/index.json",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only spec artifact kinds have catalog indexes.")
        };

    private static string NormalizeAndValidateKind(AosArtifactKind expectedKind, string artifactId)
    {
        if (!AosPathRouter.TryParseArtifactId(artifactId, out var kind, out var normalized, out var error))
        {
            throw new ArgumentException(error, nameof(artifactId));
        }

        if (kind != expectedKind)
        {
            throw new ArgumentException(
                $"Artifact id '{artifactId}' is kind '{kind}', expected '{expectedKind}'.",
                nameof(artifactId)
            );
        }

        return normalized;
    }

    private static CatalogIndexDocument CanonicalizeIndex(CatalogIndexDocument doc)
    {
        if (doc is null) throw new ArgumentNullException(nameof(doc));
        if (doc.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported catalog index schemaVersion '{doc.SchemaVersion}'.");
        }

        var items = (doc.Items ?? Array.Empty<string>())
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(i => i, StringComparer.Ordinal)
            .ToArray();

        return doc with { Items = items };
    }
}

