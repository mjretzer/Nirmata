namespace Gmsd.Aos.Public;

/// <summary>
/// Public cache manager implementation for workspace cache directory maintenance.
/// </summary>
public sealed class CacheManager : ICacheManager
{
    private readonly string _cacheDirectoryPath;

    private CacheManager(string aosRootPath)
    {
        if (string.IsNullOrWhiteSpace(aosRootPath))
        {
            throw new ArgumentException("Missing AOS root path.", nameof(aosRootPath));
        }

        _cacheDirectoryPath = Path.Combine(aosRootPath, "cache");
    }

    /// <summary>
    /// Creates a cache manager for an explicit <c>.aos</c> root path.
    /// </summary>
    public static CacheManager FromAosRoot(string aosRootPath) => new(aosRootPath);

    /// <summary>
    /// Creates a cache manager for a workspace's <c>.aos</c> root.
    /// </summary>
    public static CacheManager FromWorkspace(IWorkspace workspace)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));
        return new CacheManager(workspace.AosRootPath);
    }

    /// <inheritdoc />
    public int Clear()
    {
        if (!Directory.Exists(_cacheDirectoryPath))
        {
            return 0;
        }

        return ClearDirectory(_cacheDirectoryPath);
    }

    /// <inheritdoc />
    public int Prune(TimeSpan ageThreshold)
    {
        if (!Directory.Exists(_cacheDirectoryPath))
        {
            return 0;
        }

        // TimeSpan.Zero is equivalent to Clear
        if (ageThreshold == TimeSpan.Zero)
        {
            return Clear();
        }

        var cutoffTime = DateTime.UtcNow - ageThreshold;
        return PruneDirectory(_cacheDirectoryPath, cutoffTime);
    }

    private static int ClearDirectory(string directoryPath)
    {
        var removedCount = 0;

        // Delete all files in the directory
        foreach (var file in Directory.GetFiles(directoryPath))
        {
            try
            {
                File.Delete(file);
                removedCount++;
            }
            catch (IOException)
            {
                // Skip files that can't be deleted
            }
        }

        // Recursively delete all subdirectories
        foreach (var subdirectory in Directory.GetDirectories(directoryPath))
        {
            removedCount += ClearDirectory(subdirectory);
            try
            {
                Directory.Delete(subdirectory);
                removedCount++;
            }
            catch (IOException)
            {
                // Skip directories that can't be deleted
            }
        }

        return removedCount;
    }

    private static int PruneDirectory(string directoryPath, DateTime cutoffTime)
    {
        var removedCount = 0;

        // Check files in the directory
        foreach (var file in Directory.GetFiles(directoryPath))
        {
            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc < cutoffTime)
                {
                    File.Delete(file);
                    removedCount++;
                }
            }
            catch (IOException)
            {
                // Skip files that can't be accessed or deleted
            }
        }

        // Recursively check subdirectories
        foreach (var subdirectory in Directory.GetDirectories(directoryPath))
        {
            removedCount += PruneDirectory(subdirectory, cutoffTime);

            // Check if directory is now empty and remove it if so
            try
            {
                if (!Directory.EnumerateFileSystemEntries(subdirectory).Any())
                {
                    Directory.Delete(subdirectory);
                    removedCount++;
                }
            }
            catch (IOException)
            {
                // Skip directories that can't be deleted
            }
        }

        return removedCount;
    }
}
