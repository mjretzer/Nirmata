using nirmata.Aos.Contracts.Commands;

namespace nirmata.Agents.Execution.Continuity;

/// <summary>
/// Command handler for the pause-work command.
/// Creates an interruption-safe handoff snapshot for later resumption.
/// </summary>
public sealed class PauseWorkCommandHandler
{
    private readonly IPauseResumeManager _pauseResumeManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PauseWorkCommandHandler"/> class.
    /// </summary>
    public PauseWorkCommandHandler(IPauseResumeManager pauseResumeManager)
    {
        _pauseResumeManager = pauseResumeManager ?? throw new ArgumentNullException(nameof(pauseResumeManager));
    }

    /// <summary>
    /// Handles the pause-work command.
    /// </summary>
    /// <param name="request">The command request containing optional reason.</param>
    /// <param name="runId">The current run identifier (optional, will be discovered if not provided).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result indicating success or failure.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string? runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Extract optional reason from request
            var reason = ExtractReason(request);

            // Execute pause
            var handoffMetadata = await _pauseResumeManager.PauseAsync(reason, ct);

            return new CommandRouteResult
            {
                IsSuccess = true,
                Output = $"Work paused successfully.\n" +
                        $"  Handoff file: {handoffMetadata.HandoffPath}\n" +
                        $"  Source run: {handoffMetadata.SourceRunId}\n" +
                        $"  Timestamp: {handoffMetadata.Timestamp}" +
                        (reason != null ? $"\n  Reason: {reason}" : "")
            };
        }
        catch (InvalidOperationException ex)
        {
            return CommandRouteResult.Failure(1, ex.Message);
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Pause operation failed: {ex.Message}");
        }
    }

    private static string? ExtractReason(CommandRequest request)
    {
        // Try to extract from options
        if (request.Options.TryGetValue("reason", out var reason) && !string.IsNullOrEmpty(reason))
        {
            return reason;
        }

        // Try to extract from arguments (e.g., --reason="user interruption")
        foreach (var arg in request.Arguments)
        {
            if (arg.StartsWith("--reason=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[9..].Trim('"', '\'');
            }
        }

        // Check first argument if not a flag
        foreach (var arg in request.Arguments)
        {
            if (!arg.StartsWith("--"))
            {
                return arg;
            }
        }

        return null;
    }
}
