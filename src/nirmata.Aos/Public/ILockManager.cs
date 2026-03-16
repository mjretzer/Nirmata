namespace nirmata.Aos.Public;

/// <summary>
/// Lock manager for workspace-wide exclusive lock acquisition and release.
/// </summary>
/// <remarks>
/// The lock manager provides a service abstraction over lock management operations
/// defined by the aos-lock-manager spec. It handles:
/// - Exclusive workspace lock acquisition (fail-fast on contention)
/// - Lock release with optional force override
/// - Lock status inspection including holder information
/// </remarks>
public interface ILockManager
{
    /// <summary>
    /// Acquires the exclusive workspace lock.
    /// </summary>
    /// <returns>True if the lock was acquired; false if already held (fail-fast).</returns>
    /// <remarks>
    /// Creates the lock file at .aos/locks/workspace.lock.json with holder details.
    /// Returns immediately without waiting if lock is already held.
    /// </remarks>
    bool Acquire();

    /// <summary>
    /// Releases the exclusive workspace lock.
    /// </summary>
    /// <param name="force">If true, bypasses validation and removes lock file unconditionally.</param>
    /// <returns>True if the lock was released; false if not held or validation failed.</returns>
    /// <remarks>
    /// Removes the lock file at .aos/locks/workspace.lock.json.
    /// With force=true, removes even corrupted/unparseable lock files.
    /// </remarks>
    bool Release(bool force = false);

    /// <summary>
    /// Gets the current lock status.
    /// </summary>
    /// <returns>Lock status including locked/unlocked state and holder information.</returns>
    LockStatus GetStatus();
}

/// <summary>
/// Status information for the workspace lock.
/// </summary>
public sealed record LockStatus(
    bool IsLocked,
    LockHolderInfo? Holder = null);

/// <summary>
/// Information about the current lock holder.
/// </summary>
public sealed record LockHolderInfo(
    int ProcessId,
    string ProcessName,
    DateTimeOffset StartedAtUtc,
    string MachineName);
