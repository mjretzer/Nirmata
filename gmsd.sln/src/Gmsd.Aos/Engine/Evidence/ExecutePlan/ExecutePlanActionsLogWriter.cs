using System.Text.Json;
using Gmsd.Aos.Engine;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Aos.Engine.Evidence.ExecutePlan;

internal static class ExecutePlanActionsLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Writes a deterministic actions log for <c>aos execute-plan</c> under:
    /// <c>.aos/evidence/runs/&lt;run-id&gt;/logs/execute-plan.actions.json</c>.
    /// </summary>
    public static void WriteActionsLog(string aosRootPath, string runId, IEnumerable<string> outputsWrittenRelativePaths)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));
        if (outputsWrittenRelativePaths is null) throw new ArgumentNullException(nameof(outputsWrittenRelativePaths));

        var logsRootPath = AosPathRouter.GetRunLogsRootPath(aosRootPath, runId);
        var actionsLogPath = Path.Combine(logsRootPath, "execute-plan.actions.json");

        Directory.CreateDirectory(logsRootPath);

        var outputsWritten = outputsWrittenRelativePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        // Canonical deterministic JSON (stable bytes + atomic write semantics).
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            actionsLogPath,
            new ExecutePlanActionsLog(
                SchemaVersion: 1,
                OutputsWritten: outputsWritten
            ),
            JsonOptions
        );
    }

}

