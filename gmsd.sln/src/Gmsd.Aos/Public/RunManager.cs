using System.Text;
using System.Text.Json;
using Gmsd.Aos.Engine.Errors;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Aos.Public;

/// <summary>
/// Public run manager implementation backed by the internal engine scaffolder.
/// </summary>
public sealed class RunManager : IRunManager
{
    private readonly string _aosRootPath;
    private static readonly Utf8EncodingNoBom Utf8NoBom = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private RunManager(string aosRootPath)
    {
        if (string.IsNullOrWhiteSpace(aosRootPath))
        {
            throw new ArgumentException("Missing AOS root path.", nameof(aosRootPath));
        }

        _aosRootPath = aosRootPath;
    }

    /// <summary>
    /// Creates a run manager for an explicit <c>.aos</c> root path.
    /// </summary>
    public static RunManager FromAosRoot(string aosRootPath) => new(aosRootPath);

    /// <summary>
    /// Creates a run manager for a workspace's <c>.aos</c> root.
    /// </summary>
    public static RunManager FromWorkspace(IWorkspace workspace)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));
        return new RunManager(workspace.AosRootPath);
    }

    /// <inheritdoc />
    public string StartRun(string command, IReadOnlyList<string> args)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command cannot be null or whitespace.", nameof(command));
        if (args is null)
            throw new ArgumentNullException(nameof(args));

        var runId = AosRunId.New();
        var startedAtUtc = DateTimeOffset.UtcNow;

        AosRunEvidenceScaffolder.EnsureRunEvidenceScaffold(
            _aosRootPath,
            runId,
            startedAtUtc,
            command,
            args);

        return runId;
    }

    /// <inheritdoc />
    public void FinishRun(string runId)
    {
        FinishRun(runId, null);
    }

    /// <inheritdoc />
    public void FinishRun(string runId, IReadOnlyList<RunProducedArtifact>? additionalProducedArtifacts)
    {
        if (!AosRunId.IsValid(runId))
            throw new ArgumentException("Invalid run id.", nameof(runId));

        var finishedAtUtc = DateTimeOffset.UtcNow;

        var artifacts = additionalProducedArtifacts?.Select(a =>
            (a.Kind, a.ContractPath, a.Sha256)).ToList();

        AosRunEvidenceScaffolder.FinishRun(_aosRootPath, runId, finishedAtUtc, artifacts);
    }

    /// <inheritdoc />
    public void FailRun(string runId, int exitCode, RunErrorInfo error)
    {
        if (!AosRunId.IsValid(runId))
            throw new ArgumentException("Invalid run id.", nameof(runId));
        if (error is null)
            throw new ArgumentNullException(nameof(error));

        var finishedAtUtc = DateTimeOffset.UtcNow;

        var errorEnvelope = new AosErrorEnvelope(
            error.Code,
            error.Message,
            error.Detail);

        var artifacts = new List<(string Kind, string ContractPath, string? Sha256)>();

        AosRunEvidenceScaffolder.FailRun(_aosRootPath, runId, finishedAtUtc, exitCode, errorEnvelope, artifacts);
    }

    /// <inheritdoc />
    public IReadOnlyList<RunInfo> ListRuns()
    {
        var runsIndexJsonPath = AosPathRouter.GetRunsIndexPath(_aosRootPath);

        if (!File.Exists(runsIndexJsonPath))
        {
            return Array.Empty<RunInfo>();
        }

        try
        {
            var json = File.ReadAllText(runsIndexJsonPath, Utf8NoBom.Instance);
            var doc = JsonSerializer.Deserialize<RunIndexDocument>(json, JsonOptions)
                      ?? throw new InvalidOperationException("Run index JSON deserialized to null.");

            if (doc.SchemaVersion != 1)
            {
                throw new InvalidOperationException($"Unsupported run index schemaVersion '{doc.SchemaVersion}'.");
            }

            return doc.Items
                .Select(i => new RunInfo(
                    i.RunId,
                    i.Status,
                    DateTimeOffset.Parse(i.StartedAtUtc),
                    string.IsNullOrEmpty(i.FinishedAtUtc) ? null : DateTimeOffset.Parse(i.FinishedAtUtc)))
                .OrderBy(r => r.RunId, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Invalid run index JSON at '{runsIndexJsonPath}'.", ex);
        }
    }

    /// <inheritdoc />
    public RunInfo? GetRun(string runId)
    {
        if (!AosRunId.IsValid(runId))
            throw new ArgumentException("Invalid run id.", nameof(runId));

        var runs = ListRuns();
        return runs.FirstOrDefault(r => string.Equals(r.RunId, runId, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public bool RunExists(string runId)
    {
        if (!AosRunId.IsValid(runId))
            throw new ArgumentException("Invalid run id.", nameof(runId));

        var runJsonPath = AosPathRouter.GetRunMetadataPath(_aosRootPath, runId);
        var legacyRunJsonPath = AosPathRouter.GetLegacyRunMetadataPath(_aosRootPath, runId);

        return File.Exists(runJsonPath) || File.Exists(legacyRunJsonPath);
    }

    /// <inheritdoc />
    public void MarkRunAbandoned(string runId, DateTimeOffset abandonedAtUtc)
    {
        if (!AosRunId.IsValid(runId))
            throw new ArgumentException("Invalid run id.", nameof(runId));

        AosRunEvidenceScaffolder.MarkRunAbandoned(_aosRootPath, runId, abandonedAtUtc);
    }

    /// <inheritdoc />
    public void PauseRun(string runId, DateTimeOffset pausedAtUtc)
    {
        if (!AosRunId.IsValid(runId))
            throw new ArgumentException("Invalid run id.", nameof(runId));

        AosRunEvidenceScaffolder.PauseRun(_aosRootPath, runId, pausedAtUtc);
    }

    /// <inheritdoc />
    public void ResumeRun(string runId, DateTimeOffset resumedAtUtc)
    {
        if (!AosRunId.IsValid(runId))
            throw new ArgumentException("Invalid run id.", nameof(runId));

        AosRunEvidenceScaffolder.ResumeRun(_aosRootPath, runId, resumedAtUtc);
    }

    private sealed class Utf8EncodingNoBom
    {
        public Encoding Instance { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    private sealed record RunIndexDocument(
        int SchemaVersion,
        IReadOnlyList<RunIndexItemDocument> Items);

    private sealed record RunIndexItemDocument(
        string RunId,
        string Status,
        string StartedAtUtc,
        string? FinishedAtUtc);
}
