using Microsoft.Extensions.Logging;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Aos.Public;

/// <summary>
/// Public cache manager implementation for workspace cache directory maintenance.
/// </summary>
public sealed class CacheManager : ICacheManager
{
    private readonly string _cacheDirectoryPath;
    private readonly ILogger<CacheManager>? _logger;

    private CacheManager(string aosRootPath, ILogger<CacheManager>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(aosRootPath))
        {
            throw new ArgumentException("Missing AOS root path.", nameof(aosRootPath));
        }

        _cacheDirectoryPath = AosPathRouter.ToAosRootPath(aosRootPath, ".aos/cache/");
        _logger = logger;
    }

    /// <summary>
    /// Creates a cache manager for an explicit <c>.aos</c> root path.
    /// </summary>
    public static CacheManager FromAosRoot(string aosRootPath, ILogger<CacheManager>? logger = null) 
        => new(aosRootPath, logger);

    /// <summary>
    /// Creates a cache manager for a workspace's <c>.aos</c> root.
    /// </summary>
    public static CacheManager FromWorkspace(IWorkspace workspace, ILogger<CacheManager>? logger = null)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));
        return new CacheManager(workspace.AosRootPath, logger);
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

    private int ClearDirectory(string directoryPath)
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
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to delete cache file: {FilePath}", file);
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
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to delete cache directory: {DirectoryPath}", subdirectory);
            }
        }

        return removedCount;
    }

    private int PruneDirectory(string directoryPath, DateTime cutoffTime)
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
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to prune cache file: {FilePath}", file);
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
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to prune cache directory: {DirectoryPath}", subdirectory);
            }
        }

        return removedCount;
    }
}
