using System.Text.Json;
using nirmata.Aos.Engine.Paths;
using nirmata.Aos.Engine;

namespace nirmata.Aos.Engine.Evidence.Commands;

internal static class AosCommandLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const string CommandsLogContractPath = ".aos/evidence/logs/commands.json";

    public static void AppendCommand(
        string aosRootPath,
        string command,
        IEnumerable<string> args,
        int exitCode,
        string? runId)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("Missing command.", nameof(command));
        if (args is null) throw new ArgumentNullException(nameof(args));

        if (runId is not null && !AosRunId.IsValid(runId))
        {
            throw new ArgumentException("Invalid run id.", nameof(runId));
        }

        var fullPath = AosPathRouter.ToAosRootPath(aosRootPath, CommandsLogContractPath);

        CommandLogDocument doc;
        if (!File.Exists(fullPath))
        {
            doc = new CommandLogDocument(SchemaVersion: 1, Items: Array.Empty<CommandLogItemDocument>());
        }
        else
        {
            var json = File.ReadAllText(fullPath);
            try
            {
                doc = JsonSerializer.Deserialize<CommandLogDocument>(json, JsonOptions)
                      ?? throw new InvalidOperationException("Command log JSON deserialized to null.");
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                throw new InvalidOperationException($"Invalid command log JSON at '{CommandsLogContractPath}'.", ex);
            }
        }

        if (doc.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported command log schemaVersion '{doc.SchemaVersion}' at '{CommandsLogContractPath}'.");
        }

        var normalizedArgs = args
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .ToArray();

        var items = doc.Items.ToList();
        items.Add(
            new CommandLogItemDocument(
                Command: command.Trim(),
                Args: normalizedArgs,
                ExitCode: exitCode,
                RunId: runId
            )
        );

        // Canonical deterministic JSON (stable bytes + atomic write semantics).
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            fullPath,
            new CommandLogDocument(SchemaVersion: 1, Items: items),
            JsonOptions,
            writeIndented: true
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

