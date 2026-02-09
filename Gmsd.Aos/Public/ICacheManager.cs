namespace Gmsd.Aos.Public;

/// <summary>
/// Cache manager for workspace cache directory maintenance operations.
/// </summary>
/// <remarks>
/// The cache manager provides a service abstraction over cache management operations
/// defined by the aos-cache-hygiene spec. It handles:
/// - Complete cache clearing (removes all entries)
/// - Pruning of stale entries based on age threshold
/// - Safe operations scoped only to .aos/cache/ directory
/// </remarks>
public interface ICacheManager
{
    /// <summary>
    /// Clears all entries from the cache directory.
    /// </summary>
    /// <returns>The count of removed entries (files and directories).</returns>
    /// <remarks>
    /// Removes all files and subdirectories under .aos/cache/ while preserving
    /// the cache directory itself. This operation only affects the cache directory
    /// and will not touch other .aos/ directories like spec/, state/, or locks/.
    /// </remarks>
    int Clear();

    /// <summary>
    /// Prunes cache entries older than the specified threshold.
    /// </summary>
    /// <param name="ageThreshold">The minimum age for entries to be removed.</param>
    /// <returns>The count of removed entries (files and directories).</returns>
    /// <remarks>
    /// Removes entries where LastWriteTimeUtc is older than (now - ageThreshold).
    /// Use <see cref="TimeSpan.Zero"/> to remove all entries (equivalent to Clear).
    /// This operation only affects the cache directory.
    /// </remarks>
    int Prune(TimeSpan ageThreshold);
}
