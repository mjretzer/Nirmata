using System.Text.Json;
using System.Text.Json.Serialization;
using nirmata.Data.Dto.Models.Evidence;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

/// <summary>
/// Reads workspace evidence artifacts from <c>.aos/evidence/runs/</c> and maps them to API DTOs.
/// All methods are resilient to missing directories or malformed files — they return <c>null</c>
/// or empty collections rather than throwing so callers always get a usable result.
/// </summary>
public sealed class EvidenceService : IEvidenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<RunSummaryDto>> GetRunsAsync(
        string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var runsDir = Path.Combine(workspaceRoot, ".aos", "evidence", "runs");
        if (!Directory.Exists(runsDir))
            return [];

        var results = new List<RunSummaryDto>();

        foreach (var dir in Directory.EnumerateDirectories(runsDir).OrderDescending())
        {
            var runId = Path.GetFileName(dir);
            var summaryFile = Path.Combine(dir, "summary.json");

            SummaryFileModel? model = null;
            if (File.Exists(summaryFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(summaryFile, cancellationToken);
                    model = JsonSerializer.Deserialize<SummaryFileModel>(json, JsonOptions);
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    // skip malformed summaries but still surface the run folder
                }
            }

            results.Add(new RunSummaryDto
            {
                Id = model?.RunId ?? runId,
                TaskId = model?.TaskId,
                Status = model?.Status,
                Timestamp = model?.Timestamp,
            });
        }

        return results;
    }

    public async Task<RunDetailDto?> GetRunAsync(
        string workspaceRoot, string runId, CancellationToken cancellationToken = default)
    {
        var runDir = Path.Combine(workspaceRoot, ".aos", "evidence", "runs", runId);
        if (!Directory.Exists(runDir))
            return null;

        // Read summary.json for metadata.
        SummaryFileModel? summary = null;
        var summaryFile = Path.Combine(runDir, "summary.json");
        if (File.Exists(summaryFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(summaryFile, cancellationToken);
                summary = JsonSerializer.Deserialize<SummaryFileModel>(json, JsonOptions);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // use defaults below
            }
        }

        // Read commands.json for the command list.
        var commands = await ReadCommandsAsync(Path.Combine(runDir, "commands.json"), cancellationToken);

        // Enumerate log files under logs/.
        var logsDir = Path.Combine(runDir, "logs");
        var logFiles = Directory.Exists(logsDir)
            ? Directory.EnumerateFiles(logsDir).Select(Path.GetFileName).OfType<string>().Order().ToList()
            : [];

        // Enumerate artifact file names under artifacts/.
        var artifactsDir = Path.Combine(runDir, "artifacts");
        var artifacts = Directory.Exists(artifactsDir)
            ? Directory.EnumerateFiles(artifactsDir).Select(Path.GetFileName).OfType<string>().Order().ToList()
            : [];

        return new RunDetailDto
        {
            Id = summary?.RunId ?? runId,
            TaskId = summary?.TaskId,
            Status = summary?.Status,
            Timestamp = summary?.Timestamp,
            Commands = commands,
            LogFiles = logFiles,
            Artifacts = artifacts,
        };
    }

    private static async Task<IReadOnlyList<string>> ReadCommandsAsync(
        string commandsFile, CancellationToken cancellationToken)
    {
        if (!File.Exists(commandsFile))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(commandsFile, cancellationToken);
            var model = JsonSerializer.Deserialize<CommandsFileModel>(json, JsonOptions);
            return model?.Commands ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    // ── Private JSON deserialization models ───────────────────────────────────
    // Mirror the AOS evidence.schema.json structure.

    private sealed class SummaryFileModel
    {
        [JsonPropertyName("runId")]
        public string? RunId { get; init; }
        [JsonPropertyName("taskId")]
        public string? TaskId { get; init; }
        public string? Status { get; init; }
        public DateTimeOffset? Timestamp { get; init; }
    }

    private sealed class CommandsFileModel
    {
        public IReadOnlyList<string>? Commands { get; init; }
    }
}
