using System.Text.RegularExpressions;
using System.Linq;
using Gmsd.Aos.Engine.Evidence;

namespace Gmsd.Aos.Engine.Paths;

internal enum AosArtifactKind
{
    Run,
    Milestone,
    Phase,
    Task,
    Issue,
    Uat,
    ContextPack
}

/// <summary>
/// Single source of truth for deterministic AOS artifact ID parsing and routing to canonical contract paths under <c>.aos/*</c>.
/// </summary>
internal static class AosPathRouter
{
    private const string AosPrefix = ".aos/";
    public const string WorkspaceLockContractPath = ".aos/locks/workspace.lock.json";

    // Folder-safe, opaque, dependency-free: GUID (N) lower-case hex.
    // NOTE: We reference AosRunId for the canonical run id rule (current engine format).
    private static readonly Regex LowerHex32 = new("^[0-9a-f]{32}$", RegexOptions.Compiled);
    private static readonly Regex Hex32AnyCase = new("^[0-9a-fA-F]{32}$", RegexOptions.Compiled);

    public static bool TryParseArtifactId(
        string? rawId,
        out AosArtifactKind kind,
        out string normalizedId,
        out string error)
    {
        kind = default;
        normalizedId = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawId))
        {
            error = "Missing artifact id.";
            return false;
        }

        normalizedId = rawId.Trim();

        // RUN: current engine format (32 lower-hex chars).
        if (LowerHex32.IsMatch(normalizedId))
        {
            kind = AosArtifactKind.Run;
            return true;
        }

        if (Hex32AnyCase.IsMatch(normalizedId))
        {
            error = $"Invalid RUN id '{normalizedId}'. Expected 32 lower-case hex characters (GUID 'N' format).";
            return false;
        }

        var ms = TryParseFixedDigits(normalizedId, "MS-", 4, out _);
        if (normalizedId.StartsWith("MS-", StringComparison.Ordinal) && !ms)
        {
            error = $"Invalid milestone id '{normalizedId}'. Expected format MS-#### (4 digits), e.g. MS-0001.";
            return false;
        }
        if (ms)
        {
            kind = AosArtifactKind.Milestone;
            return true;
        }

        var ph = TryParseFixedDigits(normalizedId, "PH-", 4, out _);
        if (normalizedId.StartsWith("PH-", StringComparison.Ordinal) && !ph)
        {
            error = $"Invalid phase id '{normalizedId}'. Expected format PH-#### (4 digits), e.g. PH-0001.";
            return false;
        }
        if (ph)
        {
            kind = AosArtifactKind.Phase;
            return true;
        }

        var tsk = TryParseFixedDigits(normalizedId, "TSK-", 6, out _);
        if (normalizedId.StartsWith("TSK-", StringComparison.Ordinal) && !tsk)
        {
            error = $"Invalid task id '{normalizedId}'. Expected format TSK-###### (6 digits), e.g. TSK-000001.";
            return false;
        }
        if (tsk)
        {
            kind = AosArtifactKind.Task;
            return true;
        }

        var iss = TryParseFixedDigits(normalizedId, "ISS-", 4, out _);
        if (normalizedId.StartsWith("ISS-", StringComparison.Ordinal) && !iss)
        {
            error = $"Invalid issue id '{normalizedId}'. Expected format ISS-#### (4 digits), e.g. ISS-0001.";
            return false;
        }
        if (iss)
        {
            kind = AosArtifactKind.Issue;
            return true;
        }

        var uat = TryParseFixedDigits(normalizedId, "UAT-", 4, out _);
        if (normalizedId.StartsWith("UAT-", StringComparison.Ordinal) && !uat)
        {
            error = $"Invalid uat id '{normalizedId}'. Expected format UAT-#### (4 digits), e.g. UAT-0001.";
            return false;
        }
        if (uat)
        {
            kind = AosArtifactKind.Uat;
            return true;
        }

        var pck = TryParseFixedDigits(normalizedId, "PCK-", 4, out _);
        if (normalizedId.StartsWith("PCK-", StringComparison.Ordinal) && !pck)
        {
            error = $"Invalid context pack id '{normalizedId}'. Expected format PCK-#### (4 digits), e.g. PCK-0001.";
            return false;
        }
        if (pck)
        {
            kind = AosArtifactKind.ContextPack;
            return true;
        }

        // Provide an actionable error for common "close but wrong" shapes.
        error =
            $"Invalid artifact id '{normalizedId}'. Expected one of: " +
            "RUN (32 lower-hex), MS-####, PH-####, TSK-######, ISS-####, UAT-####, PCK-####.";
        return false;
    }

    public static string GetContractPathForArtifactId(string artifactId)
    {
        if (!TryParseArtifactId(artifactId, out var kind, out var normalized, out var error))
        {
            throw new ArgumentException(error, nameof(artifactId));
        }

        return GetContractPath(kind, normalized);
    }

    public static string GetContractPath(AosArtifactKind kind, string normalizedArtifactId)
    {
        if (string.IsNullOrWhiteSpace(normalizedArtifactId))
        {
            throw new ArgumentException("Missing artifact id.", nameof(normalizedArtifactId));
        }

        // Contract paths MUST use forward slashes to remain deterministic across platforms.
        return kind switch
        {
            AosArtifactKind.Milestone => $".aos/spec/milestones/{normalizedArtifactId}/milestone.json",
            AosArtifactKind.Phase => $".aos/spec/phases/{normalizedArtifactId}/phase.json",
            AosArtifactKind.Task => $".aos/spec/tasks/{normalizedArtifactId}/task.json",
            AosArtifactKind.Issue => $".aos/spec/issues/{normalizedArtifactId}.json",
            AosArtifactKind.Uat => $".aos/spec/uat/{normalizedArtifactId}.json",
            AosArtifactKind.ContextPack => $".aos/context/packs/{normalizedArtifactId}.json",
            AosArtifactKind.Run => $".aos/evidence/runs/{normalizedArtifactId}/",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported artifact kind.")
        };
    }

    public static string GetRunEvidenceRootPath(string aosRootPath, string runId)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        return Path.Combine(aosRootPath, "evidence", "runs", runId);
    }

    /// <summary>
    /// Canonical root folder for run artifacts under a run:
    /// <c>.aos/evidence/runs/&lt;run-id&gt;/artifacts/</c>.
    /// </summary>
    public static string GetRunArtifactsRootPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunEvidenceRootPath(aosRootPath, runId), "artifacts");

    public static string GetRunLogsRootPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunEvidenceRootPath(aosRootPath, runId), "logs");

    /// <summary>
    /// Canonical root folder for provider/tool call envelope logs under a run:
    /// <c>.aos/evidence/runs/&lt;run-id&gt;/logs/calls/</c>.
    /// </summary>
    public static string GetRunCallEnvelopesLogsRootPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunLogsRootPath(aosRootPath, runId), "calls");

    /// <summary>
    /// Canonical log file location for a single provider/tool call envelope record:
    /// <c>.aos/evidence/runs/&lt;run-id&gt;/logs/calls/&lt;call-id&gt;.json</c>.
    /// </summary>
    public static string GetRunCallEnvelopeLogPath(string aosRootPath, string runId, string callId)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (string.IsNullOrWhiteSpace(callId)) throw new ArgumentException("Missing call id.", nameof(callId));

        // NOTE: callId is kept flexible at the contract layer; callers MUST ensure it is file-name safe.
        return Path.Combine(GetRunCallEnvelopesLogsRootPath(aosRootPath, runId), callId.Trim() + ".json");
    }

    public static string GetRunOutputsRootPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunEvidenceRootPath(aosRootPath, runId), "outputs");

    public static string GetRunCommandsPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunEvidenceRootPath(aosRootPath, runId), "commands.json");

    public static string GetRunSummaryPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunEvidenceRootPath(aosRootPath, runId), "summary.json");

    public static string GetRunManifestPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunArtifactsRootPath(aosRootPath, runId), "manifest.json");

    public static string GetRunMetadataPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunArtifactsRootPath(aosRootPath, runId), "run.json");

    public static string GetRunPacketPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunArtifactsRootPath(aosRootPath, runId), "packet.json");

    public static string GetRunResultPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunArtifactsRootPath(aosRootPath, runId), "result.json");

    // Legacy paths tolerated during transition.
    public static string GetLegacyRunManifestPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunEvidenceRootPath(aosRootPath, runId), "manifest.json");

    public static string GetLegacyRunMetadataPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunEvidenceRootPath(aosRootPath, runId), "run.json");

    public static string GetLegacyRunPacketPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunEvidenceRootPath(aosRootPath, runId), "packet.json");

    public static string GetLegacyRunResultPath(string aosRootPath, string runId) =>
        Path.Combine(GetRunEvidenceRootPath(aosRootPath, runId), "result.json");

    /// <summary>
    /// Canonical task-evidence latest pointer location:
    /// <c>.aos/evidence/task-evidence/&lt;task-id&gt;/latest.json</c>.
    /// </summary>
    public static string GetTaskEvidenceLatestPath(string aosRootPath, string taskId)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentNullException(nameof(taskId));

        if (!TryParseArtifactId(taskId, out var kind, out var normalizedId, out var error) || kind != AosArtifactKind.Task)
        {
            throw new ArgumentException(error, nameof(taskId));
        }

        return Path.Combine(aosRootPath, "evidence", "task-evidence", normalizedId, "latest.json");
    }

    public static string GetRunsIndexPath(string aosRootPath)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        return Path.Combine(aosRootPath, "evidence", "runs", "index.json");
    }

    public static string GetWorkspaceLockPath(string aosRootPath)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        return ToAosRootPath(aosRootPath, WorkspaceLockContractPath);
    }

    /// <summary>
    /// Validates that a subpath is safe and does not attempt directory traversal.
    /// </summary>
    /// <param name="subpath">The subpath to validate (relative to a root).</param>
    /// <exception cref="ArgumentException">Thrown if the path contains traversal segments or backslashes.</exception>
    public static void ValidateSubpath(string subpath)
    {
        if (string.IsNullOrWhiteSpace(subpath))
        {
            throw new ArgumentException("Subpath cannot be empty.", nameof(subpath));
        }

        if (subpath.Contains('\\'))
        {
            throw new ArgumentException("Paths MUST use '/' separators.", nameof(subpath));
        }

        var segments = subpath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            if (seg is "." or "..")
            {
                throw new ArgumentException("Path MUST NOT contain '.' or '..' segments.", nameof(subpath));
            }
        }
    }

    public static string ToAosRootPath(string aosRootPath, string contractPath)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (contractPath is null) throw new ArgumentNullException(nameof(contractPath));

        ValidateSubpath(contractPath);

        if (!contractPath.StartsWith(AosPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Contract path must start with '{AosPrefix}'.", nameof(contractPath));
        }

        var relative = contractPath[AosPrefix.Length..];
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length == 0
            ? aosRootPath
            : Path.Combine(new[] { aosRootPath }.Concat(segments).ToArray());
    }

    private static bool TryParseFixedDigits(string value, string prefix, int digitCount, out string digits)
    {
        digits = string.Empty;

        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var tail = value[prefix.Length..];
        if (tail.Length != digitCount)
        {
            return false;
        }

        for (var i = 0; i < tail.Length; i++)
        {
            if (!char.IsDigit(tail[i]))
            {
                return false;
            }
        }

        digits = tail;
        return true;
    }
}

