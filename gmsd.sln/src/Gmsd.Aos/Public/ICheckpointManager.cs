namespace Gmsd.Aos.Public;

/// <summary>
/// Checkpoint manager for creating, restoring, and listing state checkpoints.
/// </summary>
/// <remarks>
/// The checkpoint manager provides a service abstraction over checkpoint operations
/// defined by the aos-checkpoints spec. It handles:
/// - Checkpoint creation with state snapshots
/// - Checkpoint restoration to previous states
/// - Checkpoint enumeration and metadata access
/// - Event recording for checkpoint lifecycle
/// </remarks>
public interface ICheckpointManager
{
    /// <summary>
    /// Creates a new checkpoint of the current state.
    /// </summary>
    /// <param name="description">Optional description of the checkpoint.</param>
    /// <returns>The unique checkpoint ID (format: CHK-####).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the workspace is not initialized or state cannot be read.</exception>
    /// <remarks>
    /// Creates a checkpoint folder under .aos/state/checkpoints/{checkpointId}/
    /// containing state snapshot and metadata. Appends a checkpoint.created event.
    /// </remarks>
    string CreateCheckpoint(string? description = null);

    /// <summary>
    /// Restores the state from a checkpoint.
    /// </summary>
    /// <param name="checkpointId">The checkpoint ID to restore from.</param>
    /// <exception cref="ArgumentException">Thrown when checkpointId is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when checkpoint does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when checkpoint metadata is corrupted or state cannot be restored.</exception>
    /// <remarks>
    /// Replaces state.json with the checkpoint snapshot. Appends a checkpoint.restored event.
    /// </remarks>
    void RestoreCheckpoint(string checkpointId);

    /// <summary>
    /// Lists all checkpoints with their metadata.
    /// </summary>
    /// <returns>A read-only list of checkpoint information, ordered by checkpoint ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the checkpoints directory is corrupted.</exception>
    /// <remarks>
    /// Returns checkpoints from .aos/state/checkpoints/.
    /// Returns an empty list if no checkpoints exist.
    /// </remarks>
    IReadOnlyList<CheckpointInfo> ListCheckpoints();

    /// <summary>
    /// Gets metadata for a specific checkpoint.
    /// </summary>
    /// <param name="checkpointId">The checkpoint ID to look up.</param>
    /// <returns>The checkpoint metadata, or null if the checkpoint does not exist.</returns>
    /// <exception cref="ArgumentException">Thrown when checkpointId is null or whitespace.</exception>
    CheckpointInfo? GetCheckpoint(string checkpointId);

    /// <summary>
    /// Determines whether a checkpoint with the specified ID exists.
    /// </summary>
    /// <param name="checkpointId">The checkpoint ID to check.</param>
    /// <returns>True if the checkpoint exists; otherwise, false.</returns>
    /// <exception cref="ArgumentException">Thrown when checkpointId is null or whitespace.</exception>
    bool CheckpointExists(string checkpointId);
}

/// <summary>
/// Information about a checkpoint.
/// </summary>
public sealed record CheckpointInfo(
    string CheckpointId,
    DateTimeOffset CreatedAtUtc,
    string? Description,
    string StateSnapshotPath);
