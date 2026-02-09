using System.Text.Json;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Aos.Engine.Evidence.Runs;

internal static class AosRunSummaryWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void WriteRunSummary(
        string aosRootPath,
        string runId,
        string status,
        string startedAtUtc,
        string? finishedAtUtc,
        int exitCode)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("Missing status.", nameof(status));
        if (string.IsNullOrWhiteSpace(startedAtUtc)) throw new ArgumentException("Missing startedAtUtc.", nameof(startedAtUtc));

        var summaryPath = AosPathRouter.GetRunSummaryPath(aosRootPath, runId);
        Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);

        var artifacts = new RunSummaryArtifactsDocument(
            RunMetadata: $".aos/evidence/runs/{runId}/artifacts/run.json",
            Packet: $".aos/evidence/runs/{runId}/artifacts/packet.json",
            Result: $".aos/evidence/runs/{runId}/artifacts/result.json",
            Manifest: $".aos/evidence/runs/{runId}/artifacts/manifest.json",
            Commands: $".aos/evidence/runs/{runId}/commands.json",
            LogsRoot: $".aos/evidence/runs/{runId}/logs/",
            OutputsRoot: $".aos/evidence/runs/{runId}/outputs/"
        );

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            summaryPath,
            new RunSummaryDocument(
                SchemaVersion: 1,
                RunId: runId,
                Status: status.Trim(),
                StartedAtUtc: startedAtUtc,
                FinishedAtUtc: finishedAtUtc,
                ExitCode: exitCode,
                Artifacts: artifacts
            ),
            JsonOptions,
            writeIndented: true
        );
    }

    public static void EnsureRunSummaryExistsAtStart(
        string aosRootPath,
        string runId,
        string startedAtUtc)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (string.IsNullOrWhiteSpace(startedAtUtc)) throw new ArgumentException("Missing startedAtUtc.", nameof(startedAtUtc));

        var summaryPath = AosPathRouter.GetRunSummaryPath(aosRootPath, runId);
        if (File.Exists(summaryPath))
        {
            return;
        }

        WriteRunSummary(
            aosRootPath,
            runId,
            status: "started",
            startedAtUtc: startedAtUtc,
            finishedAtUtc: null,
            exitCode: 0
        );
    }

    private sealed record RunSummaryDocument(
        int SchemaVersion,
        string RunId,
        string Status,
        string StartedAtUtc,
        string? FinishedAtUtc,
        int ExitCode,
        RunSummaryArtifactsDocument Artifacts);

    private sealed record RunSummaryArtifactsDocument(
        string RunMetadata,
        string Packet,
        string Result,
        string Manifest,
        string Commands,
        string LogsRoot,
        string OutputsRoot);
}

