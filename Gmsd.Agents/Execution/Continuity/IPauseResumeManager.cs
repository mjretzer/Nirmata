using Gmsd.Aos.Contracts.State;

namespace Gmsd.Agents.Execution.Continuity;

/// <summary>
/// Manages pause and resume operations for workflow execution.
/// Captures handoff snapshots and reconstructs execution state for deterministic continuation.
/// </summary>
public interface IPauseResumeManager
{
    /// <summary>
    /// Captures the current execution state and writes a handoff snapshot.
    /// </summary>
    /// <param name="reason">Optional reason or message for the pause.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Handoff metadata including timestamp and captured run ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no active execution exists to pause.</exception>
    Task<HandoffMetadata> PauseAsync(string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Reconstructs execution state from the handoff snapshot and resumes workflow.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Resume result including the new run ID and status.</returns>
    /// <exception cref="FileNotFoundException">Thrown when no handoff.json exists to resume from.</exception>
    /// <exception cref="InvalidDataException">Thrown when handoff validation fails.</exception>
    Task<ResumeResult> ResumeAsync(CancellationToken ct = default);

    /// <summary>
    /// Resumes execution from a historical RUN ID by reconstructing context from evidence.
    /// </summary>
    /// <param name="runId">The historical RUN ID to resume from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Resume result including the new continuation run ID.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the run evidence folder does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when run evidence is corrupted or incomplete.</exception>
    Task<ResumeResult> ResumeFromRunAsync(string runId, CancellationToken ct = default);

    /// <summary>
    /// Validates the handoff state without performing resumption.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation result indicating if handoff is valid and can be resumed.</returns>
    Task<HandoffValidationResult> ValidateHandoffAsync(CancellationToken ct = default);
}

/// <summary>
/// Metadata returned from a pause operation.
/// </summary>
public sealed record HandoffMetadata
{
    /// <summary>
    /// The timestamp when the handoff was created.
    /// </summary>
    public required string Timestamp { get; init; }

    /// <summary>
    /// The source run ID captured in the handoff.
    /// </summary>
    public required string SourceRunId { get; init; }

    /// <summary>
    /// The path to the handoff file.
    /// </summary>
    public required string HandoffPath { get; init; }

    /// <summary>
    /// Optional reason provided for the pause.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Result returned from a resume operation.
/// </summary>
public sealed record ResumeResult
{
    /// <summary>
    /// The new run ID created for the resumed execution.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// The source run ID from which execution was resumed.
    /// </summary>
    public required string SourceRunId { get; init; }

    /// <summary>
    /// The status of the resume operation.
    /// </summary>
    public required ResumeStatus Status { get; init; }

    /// <summary>
    /// The restored cursor position.
    /// </summary>
    public required StateCursor Cursor { get; init; }
}

/// <summary>
/// Status of a resume operation.
/// </summary>
public enum ResumeStatus
{
    /// <summary>
    /// Resume completed successfully and execution is continuing.
    /// </summary>
    Success,

    /// <summary>
    /// Resume completed but with warnings (e.g., scope adjustments).
    /// </summary>
    SuccessWithWarnings,

    /// <summary>
    /// Resume failed due to validation errors.
    /// </summary>
    Failed
}

/// <summary>
/// Result of handoff validation.
/// </summary>
public sealed record HandoffValidationResult
{
    /// <summary>
    /// Whether the handoff is valid and can be resumed.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors if the handoff is invalid.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The handoff state if validation succeeded.
    /// </summary>
    public HandoffState? Handoff { get; init; }
}
