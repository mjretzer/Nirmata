using System;
using System.IO;

namespace Gmsd.Aos.Engine.Cache;

internal static class AosCacheHygiene
{
    /// <summary>
    /// Clears all entries under <c>.aos/cache/**</c> while preserving the <c>.aos/cache/</c> directory itself.
    /// </summary>
    public static void Clear(string aosRootPath)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));

        var cacheRoot = GetCacheRootPath(aosRootPath);
        Directory.CreateDirectory(cacheRoot);

        foreach (var entry in Directory.EnumerateFileSystemEntries(cacheRoot, "*", SearchOption.TopDirectoryOnly))
        {
            DeleteEntry(entry);
        }

        // Guardrail: ensure the directory still exists.
        Directory.CreateDirectory(cacheRoot);
    }

    /// <summary>
    /// Prunes entries under <c>.aos/cache/**</c> whose filesystem timestamps are older than the provided threshold.
    /// Timestamp behavior: uses <see cref="File.GetLastWriteTimeUtc(string)"/> / <see cref="Directory.GetLastWriteTimeUtc(string)"/>.
    /// </summary>
    public static int Prune(string aosRootPath, int days, DateTimeOffset? nowUtc = null)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days), days, "Days must be non-negative.");

        var cacheRoot = GetCacheRootPath(aosRootPath);
        Directory.CreateDirectory(cacheRoot);

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var threshold = now.UtcDateTime - TimeSpan.FromDays(days);

        var deleted = 0;
        foreach (var entry in Directory.EnumerateFileSystemEntries(cacheRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var lastWriteUtc = Directory.Exists(entry)
                ? Directory.GetLastWriteTimeUtc(entry)
                : File.GetLastWriteTimeUtc(entry);

            if (lastWriteUtc < threshold)
            {
                DeleteEntry(entry);
                deleted++;
            }
        }

        // Guardrail: ensure the directory still exists.
        Directory.CreateDirectory(cacheRoot);
        return deleted;
    }

    private static string GetCacheRootPath(string aosRootPath) => Path.Combine(aosRootPath, "cache");

    private static void DeleteEntry(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

