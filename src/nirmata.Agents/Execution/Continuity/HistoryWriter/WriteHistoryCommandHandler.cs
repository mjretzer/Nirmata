namespace nirmata.Agents.Execution.Continuity.HistoryWriter;

using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Public.Models;
using nirmata.Aos.Public.Services;
using nirmata.Aos.Engine.Evidence;
using nirmata.Aos.Public.Catalogs;

/// <summary>
/// Handler for the write-history command.
/// Appends a history entry to .aos/spec/summary.md with evidence pointers.
/// </summary>
public sealed class WriteHistoryCommandHandler : ICommandHandler
{
    private readonly IHistoryWriter _historyWriter;

    /// <summary>
    /// Creates a new instance of the <see cref="WriteHistoryCommandHandler"/> class.
    /// </summary>
    public WriteHistoryCommandHandler(IHistoryWriter historyWriter)
    {
        _historyWriter = historyWriter ?? throw new ArgumentNullException(nameof(historyWriter));
    }

    /// <inheritdoc />
    public CommandMetadata Metadata { get; } = new()
    {
        Group = "core",
        Command = "write-history",
        Id = CommandIds.WriteHistory,
        Description = "Append a history entry to .aos/spec/summary.md with verification proof, commit hash, and evidence pointers.",
        Example = "aos write-history RUN-0001 --task TSK-0001 --narrative \"Completed implementation of feature X\""
    };

    /// <inheritdoc />
    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        try
        {
            // Get required run ID from arguments
            if (context.Arguments.Count == 0)
            {
                return CommandResult.Failure(
                    1,
                    "Run ID is required. Usage: write-history <run-id> [--task <task-id>] [--narrative <text>]",
                    new[] { new CommandError("MissingRunId", "Run ID argument is required.") }
                );
            }

            var runId = context.Arguments[0];

            // Validate run ID format
            if (!AosRunId.IsValid(runId))
            {
                return CommandResult.Failure(
                    1,
                    $"Invalid run ID format: '{runId}'. Expected format: RUN-XXXX where X is a digit.",
                    new[] { new CommandError("InvalidRunId", "Run ID must match pattern RUN-XXXX.") }
                );
            }

            // Get optional task ID
            string? taskId = null;
            if (context.Options.TryGetValue("task", out var taskValue))
            {
                taskId = taskValue;
            }

            // Get optional narrative
            string? narrative = null;
            if (context.Options.TryGetValue("narrative", out var narrativeValue))
            {
                narrative = narrativeValue;
            }

            // Check if already exists
            var alreadyExists = _historyWriter.Exists(runId, taskId);
            if (alreadyExists && !context.Options.ContainsKey("force"))
            {
                return CommandResult.Failure(
                    1,
                    $"History entry already exists for {runId}{(taskId != null ? $"/{taskId}" : "")}. Use --force to overwrite.",
                    new[] { new CommandError("EntryExists", "History entry already exists. Use --force to append anyway.") }
                );
            }

            // Append history entry
            var entry = await _historyWriter.AppendAsync(runId, taskId, narrative, ct);

            // Build success message
            var key = taskId != null ? $"{runId}/{taskId}" : runId;
            var output = $"History entry written for {key}\n" +
                        $"Timestamp: {entry.Timestamp}\n" +
                        $"Verification: {entry.Verification.Status}\n" +
                        $"Summary: {_historyWriter.SummaryPath}";

            if (!string.IsNullOrEmpty(entry.CommitHash))
            {
                output += $"\nCommit: {entry.CommitHash}";
            }

            return CommandResult.Success(output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException ex)
        {
            return CommandResult.Failure(
                1,
                $"Evidence not found: {ex.Message}",
                new[] { new CommandError("EvidenceNotFound", ex.Message) }
            );
        }
        catch (Exception ex)
        {
            return CommandResult.Failure(
                1,
                $"History write failed: {ex.Message}",
                new[] { new CommandError("HistoryWriteFailed", ex.Message) }
            );
        }
    }
}
