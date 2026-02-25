namespace Gmsd.Agents.Execution.Continuity.ProgressReporter;

using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public.Models;
using Gmsd.Aos.Public.Services;
using Gmsd.Aos.Public.Catalogs;

/// <summary>
/// Handler for the report-progress command.
/// Generates a deterministic progress report from current execution state.
/// </summary>
public sealed class ReportProgressCommandHandler : ICommandHandler
{
    private readonly IProgressReporter _progressReporter;

    /// <summary>
    /// Creates a new instance of the <see cref="ReportProgressCommandHandler"/> class.
    /// </summary>
    public ReportProgressCommandHandler(IProgressReporter progressReporter)
    {
        _progressReporter = progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
    }

    /// <inheritdoc />
    public CommandMetadata Metadata { get; } = new()
    {
        Group = "core",
        Command = "report-progress",
        Id = CommandIds.ReportProgress,
        Description = "Generate a deterministic progress report showing cursor position, blockers, and next recommended command.",
        Example = "aos report-progress --format json"
    };

    /// <inheritdoc />
    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        try
        {
            // Determine output format (json or markdown)
            var format = "json";
            if (context.Options.TryGetValue("format", out var formatValue) &&
                !string.IsNullOrEmpty(formatValue))
            {
                format = formatValue.ToLowerInvariant();
            }

            if (format != "json" && format != "markdown")
            {
                return CommandResult.Failure(
                    1,
                    $"Invalid format '{format}'. Supported formats: json, markdown.",
                    new[] { new CommandError("InvalidFormat", $"Format '{format}' is not supported.") }
                );
            }

            // Generate progress report
            var report = await _progressReporter.ReportAsync(format, ct);

            // Format output based on requested format
            var output = format == "json"
                ? System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                })
                : await _progressReporter.ReportAsStringAsync("markdown", ct);

            return CommandResult.Success(output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CommandResult.Failure(
                1,
                $"Progress report generation failed: {ex.Message}",
                new[] { new CommandError("ProgressReportFailed", ex.Message) }
            );
        }
    }
}
