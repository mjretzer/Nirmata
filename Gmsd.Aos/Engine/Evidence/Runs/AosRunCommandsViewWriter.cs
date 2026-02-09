using System.Text.Json;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Aos.Engine.Evidence.Runs;

internal static class AosRunCommandsViewWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const string GlobalCommandsLogContractPath = ".aos/evidence/logs/commands.json";

    public static void WriteRunCommandsView(string aosRootPath, string runId)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        var globalPath = AosPathRouter.ToAosRootPath(aosRootPath, GlobalCommandsLogContractPath);
        var runCommandsPath = AosPathRouter.GetRunCommandsPath(aosRootPath, runId);

        Directory.CreateDirectory(Path.GetDirectoryName(runCommandsPath)!);

        CommandLogDocument doc;
        if (!File.Exists(globalPath))
        {
            doc = new CommandLogDocument(SchemaVersion: 1, Items: Array.Empty<CommandLogItemDocument>());
        }
        else
        {
            try
            {
                var json = File.ReadAllText(globalPath);
                doc = JsonSerializer.Deserialize<CommandLogDocument>(json, JsonOptions)
                      ?? throw new InvalidOperationException("Command log JSON deserialized to null.");
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                // Keep the per-run view resilient; if global log is invalid, don't overwrite an existing view.
                // (Workspace validation is responsible for flagging invalid baseline artifacts.)
                return;
            }
        }

        if (doc.SchemaVersion != 1)
        {
            // Unknown schema: don't attempt to transform.
            return;
        }

        var filtered = doc.Items
            .Where(i => string.Equals(i.RunId, runId, StringComparison.Ordinal))
            .ToArray();

        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            runCommandsPath,
            new CommandLogDocument(SchemaVersion: 1, Items: filtered),
            JsonOptions,
            writeIndented: true
        );
    }

    public static void EnsureRunCommandsViewExists(string aosRootPath, string runId)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (!AosRunId.IsValid(runId)) throw new ArgumentException("Invalid run id.", nameof(runId));

        var runCommandsPath = AosPathRouter.GetRunCommandsPath(aosRootPath, runId);
        Directory.CreateDirectory(Path.GetDirectoryName(runCommandsPath)!);

        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(
            runCommandsPath,
            new CommandLogDocument(SchemaVersion: 1, Items: Array.Empty<CommandLogItemDocument>()),
            JsonOptions
        );
    }

    private sealed record CommandLogDocument(
        int SchemaVersion,
        IReadOnlyList<CommandLogItemDocument> Items);

    private sealed record CommandLogItemDocument(
        string Command,
        IReadOnlyList<string> Args,
        int ExitCode,
        string? RunId);
}

