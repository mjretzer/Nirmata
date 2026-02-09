namespace Gmsd.Aos.Public;

/// <summary>
/// Run lifecycle manager for creating, finishing, and listing runs.
/// </summary>
/// <remarks>
/// The run manager provides a service abstraction over run lifecycle operations
/// defined by the aos-run-lifecycle spec. It handles:
/// - Run creation with deterministic folder scaffolding
/// - Run metadata and index management
/// - Packet and result artifact generation
/// - Run enumeration via the run index
/// </remarks>
public interface IRunManager
{
    /// <summary>
    /// Starts a new run, creating the evidence scaffold and returning the run ID.
    /// </summary>
    /// <param name="command">The normalized command name (e.g., "execute-plan").</param>
    /// <param name="args">The raw CLI arguments as received by the command handler.</param>
    /// <returns>The unique run ID for the newly created run.</returns>
    /// <exception cref="ArgumentException">Thrown when command is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the workspace is not initialized.</exception>
    /// <remarks>
    /// Creates the run evidence folder structure under .aos/evidence/runs/{runId}/
    /// including run.json, packet.json, logs/, outputs/, and updates the run index.
    /// </remarks>
    string StartRun(string command, IReadOnlyList<string> args);

    /// <summary>
    /// Finishes a run, updating metadata and producing result artifacts.
    /// </summary>
    /// <param name="runId">The run ID to finish.</param>
    /// <exception cref="ArgumentException">Thrown when runId is invalid.</exception>
    /// <exception cref="FileNotFoundException">Thrown when run metadata is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when run index is corrupted or schema version is unsupported.</exception>
    /// <remarks>
    /// Updates run.json status to "finished", updates the run index,
    /// produces result.json and summary.json artifacts.
    /// </remarks>
    void FinishRun(string runId);

    /// <summary>
    /// Finishes a run with additional produced artifacts.
    /// </summary>
    /// <param name="runId">The run ID to finish.</param>
    /// <param name="additionalProducedArtifacts">Additional artifacts produced by the run.</param>
    /// <exception cref="ArgumentException">Thrown when runId is invalid.</exception>
    /// <exception cref="FileNotFoundException">Thrown when run metadata is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when run index is corrupted or schema version is unsupported.</exception>
    void FinishRun(string runId, IReadOnlyList<RunProducedArtifact> additionalProducedArtifacts);

    /// <summary>
    /// Fails a run, updating metadata and producing result artifacts with error information.
    /// </summary>
    /// <param name="runId">The run ID to fail.</param>
    /// <param name="exitCode">The exit code indicating failure.</param>
    /// <param name="error">The error envelope describing the failure.</param>
    /// <exception cref="ArgumentException">Thrown when runId is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when error is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when run metadata is not found.</exception>
    void FailRun(string runId, int exitCode, RunErrorInfo error);

    /// <summary>
    /// Lists all runs from the run index.
    /// </summary>
    /// <returns>A read-only list of run information entries, ordered by run ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the run index is corrupted.</exception>
    /// <remarks>
    /// Returns runs from .aos/evidence/runs/index.json.
    /// Returns an empty list if the index does not exist.
    /// </remarks>
    IReadOnlyList<RunInfo> ListRuns();

    /// <summary>
    /// Gets information about a specific run.
    /// </summary>
    /// <param name="runId">The run ID to look up.</param>
    /// <returns>The run information, or null if the run does not exist.</returns>
    /// <exception cref="ArgumentException">Thrown when runId is invalid.</exception>
    RunInfo? GetRun(string runId);

    /// <summary>
    /// Determines whether a run with the specified ID exists.
    /// </summary>
    /// <param name="runId">The run ID to check.</param>
    /// <returns>True if the run exists; otherwise, false.</returns>
    /// <exception cref="ArgumentException">Thrown when runId is invalid.</exception>
    bool RunExists(string runId);
}

/// <summary>
/// Information about a run from the run index.
/// </summary>
public sealed record RunInfo(
    string RunId,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);

/// <summary>
/// Information about an artifact produced by a run.
/// </summary>
public sealed record RunProducedArtifact(
    string Kind,
    string ContractPath,
    string? Sha256);

/// <summary>
/// Error information for a failed run.
/// </summary>
public sealed record RunErrorInfo(
    string Code,
    string Message,
    string? Detail = null);
