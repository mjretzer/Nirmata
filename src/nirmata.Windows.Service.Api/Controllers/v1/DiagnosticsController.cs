using Microsoft.AspNetCore.Mvc;
using nirmata.Windows.Service.Api;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class DiagLogEntry
{
    public string Label { get; init; } = string.Empty;
    public int Lines { get; init; }
    public int Warnings { get; init; }
    public int Errors { get; init; }
    public string Path { get; init; } = string.Empty;
}

public sealed class DiagArtifactEntry
{
    public string Name { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
}

public sealed class DiagLockEntry
{
    public string Id { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string Acquired { get; init; } = string.Empty;
    public bool Stale { get; init; }
}

public sealed class DiagCacheEntry
{
    public string Label { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public bool Stale { get; init; }
}

public sealed class DiagnosticsSnapshot
{
    public IReadOnlyList<DiagLogEntry> Logs { get; init; } = [];
    public IReadOnlyList<DiagArtifactEntry> Artifacts { get; init; } = [];
    public IReadOnlyList<DiagLockEntry> Locks { get; init; } = [];
    public IReadOnlyList<DiagCacheEntry> CacheEntries { get; init; } = [];
}

[ApiController]
[Route("api/v1/diagnostics")]
public class DiagnosticsController(DaemonRuntimeState state) : ControllerBase
{
    private static readonly TimeSpan StaleLockThreshold = TimeSpan.FromHours(1);
    private static readonly TimeSpan StaleCacheThreshold = TimeSpan.FromHours(24);

    /// <summary>
    /// Returns a diagnostics snapshot: logs from .aos/evidence runs, artifacts,
    /// lock files from .aos/cache/locks, and cache entries from .aos/cache.
    /// Returns empty collections when no workspace is registered.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DiagnosticsSnapshot), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var workspacePath = state.HostProfile?.WorkspacePath;

        var snapshot = new DiagnosticsSnapshot
        {
            Logs = workspacePath is not null ? ReadLogs(workspacePath) : [],
            Artifacts = workspacePath is not null ? ReadArtifacts(workspacePath) : [],
            Locks = workspacePath is not null ? ReadLocks(workspacePath) : [],
            CacheEntries = workspacePath is not null ? ReadCacheEntries(workspacePath) : []
        };

        return Ok(snapshot);
    }

    /// <summary>
    /// Deletes stale lock files from .aos/cache/locks.
    /// Returns the number of files removed.
    /// </summary>
    [HttpDelete("locks")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult ClearLocks()
    {
        var workspacePath = state.HostProfile?.WorkspacePath;
        if (workspacePath is null)
            return Ok(new { removed = 0, message = "No workspace registered." });

        var locksDir = System.IO.Path.Combine(workspacePath, ".aos", "cache", "locks");
        if (!Directory.Exists(locksDir))
            return Ok(new { removed = 0, message = "Locks directory does not exist." });

        var cutoff = DateTime.UtcNow - StaleLockThreshold;
        var removed = 0;

        foreach (var file in Directory.EnumerateFiles(locksDir))
        {
            if (System.IO.File.GetLastWriteTimeUtc(file) < cutoff)
            {
                System.IO.File.Delete(file);
                removed++;
            }
        }

        return Ok(new { removed, message = $"Removed {removed} stale lock(s)." });
    }

    /// <summary>
    /// Deletes stale entries from .aos/cache (tmp and stale top-level files).
    /// Returns the number of files removed.
    /// </summary>
    [HttpDelete("cache")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult ClearCache()
    {
        var workspacePath = state.HostProfile?.WorkspacePath;
        if (workspacePath is null)
            return Ok(new { removed = 0, message = "No workspace registered." });

        var cacheDir = System.IO.Path.Combine(workspacePath, ".aos", "cache");
        if (!Directory.Exists(cacheDir))
            return Ok(new { removed = 0, message = "Cache directory does not exist." });

        var cutoff = DateTime.UtcNow - StaleCacheThreshold;
        var removed = 0;

        // Clear tmp subdirectory entirely
        var tmpDir = System.IO.Path.Combine(cacheDir, "tmp");
        if (Directory.Exists(tmpDir))
        {
            foreach (var file in Directory.EnumerateFiles(tmpDir, "*", SearchOption.AllDirectories))
            {
                System.IO.File.Delete(file);
                removed++;
            }
        }

        // Clear stale top-level cache files (excluding locks/ subdirectory)
        foreach (var file in Directory.EnumerateFiles(cacheDir))
        {
            if (System.IO.File.GetLastWriteTimeUtc(file) < cutoff)
            {
                System.IO.File.Delete(file);
                removed++;
            }
        }

        return Ok(new { removed, message = $"Removed {removed} stale cache entry(s)." });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static List<DiagLogEntry> ReadLogs(string workspacePath)
    {
        var runsDir = System.IO.Path.Combine(workspacePath, ".aos", "evidence", "runs");
        if (!Directory.Exists(runsDir))
            return [];

        var entries = new List<DiagLogEntry>();

        foreach (var logFile in Directory.EnumerateFiles(runsDir, "*.log", SearchOption.AllDirectories))
        {
            try
            {
                var lines = System.IO.File.ReadAllLines(logFile);
                entries.Add(new DiagLogEntry
                {
                    Label = System.IO.Path.GetFileName(logFile),
                    Lines = lines.Length,
                    Warnings = lines.Count(l => l.Contains("warning", StringComparison.OrdinalIgnoreCase)),
                    Errors = lines.Count(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)),
                    Path = System.IO.Path.GetRelativePath(workspacePath, logFile)
                });
            }
            catch (IOException) { /* skip unreadable files */ }
        }

        return entries;
    }

    private static List<DiagArtifactEntry> ReadArtifacts(string workspacePath)
    {
        var runsDir = System.IO.Path.Combine(workspacePath, ".aos", "evidence", "runs");
        if (!Directory.Exists(runsDir))
            return [];

        var entries = new List<DiagArtifactEntry>();

        foreach (var artifactsDir in Directory.EnumerateDirectories(runsDir, "artifacts", SearchOption.AllDirectories))
        {
            foreach (var file in Directory.EnumerateFiles(artifactsDir))
            {
                var info = new FileInfo(file);
                entries.Add(new DiagArtifactEntry
                {
                    Name = info.Name,
                    Size = FormatBytes(info.Length),
                    Type = info.Extension.TrimStart('.').ToUpperInvariant(),
                    Path = System.IO.Path.GetRelativePath(workspacePath, file)
                });
            }
        }

        return entries;
    }

    private static List<DiagLockEntry> ReadLocks(string workspacePath)
    {
        var locksDir = System.IO.Path.Combine(workspacePath, ".aos", "cache", "locks");
        if (!Directory.Exists(locksDir))
            return [];

        var entries = new List<DiagLockEntry>();
        var cutoff = DateTime.UtcNow - StaleLockThreshold;

        foreach (var file in Directory.EnumerateFiles(locksDir))
        {
            var info = new FileInfo(file);
            var acquired = info.CreationTimeUtc;
            string scope = "unknown", owner = "unknown";

            try
            {
                var content = System.IO.File.ReadAllText(file);
                // Expect simple JSON-ish or plain-text: try to parse "scope" and "owner" fields
                var scopeMatch = System.Text.RegularExpressions.Regex.Match(content, @"""scope""\s*:\s*""([^""]+)""");
                var ownerMatch = System.Text.RegularExpressions.Regex.Match(content, @"""owner""\s*:\s*""([^""]+)""");
                if (scopeMatch.Success) scope = scopeMatch.Groups[1].Value;
                if (ownerMatch.Success) owner = ownerMatch.Groups[1].Value;
            }
            catch (IOException) { /* leave defaults */ }

            entries.Add(new DiagLockEntry
            {
                Id = System.IO.Path.GetFileNameWithoutExtension(file),
                Scope = scope,
                Owner = owner,
                Acquired = acquired.ToString("o"),
                Stale = acquired < cutoff
            });
        }

        return entries;
    }

    private static List<DiagCacheEntry> ReadCacheEntries(string workspacePath)
    {
        var cacheDir = System.IO.Path.Combine(workspacePath, ".aos", "cache");
        if (!Directory.Exists(cacheDir))
            return [];

        var entries = new List<DiagCacheEntry>();
        var cutoff = DateTime.UtcNow - StaleCacheThreshold;

        // Top-level files in cache/
        foreach (var file in Directory.EnumerateFiles(cacheDir))
        {
            var info = new FileInfo(file);
            entries.Add(new DiagCacheEntry
            {
                Label = info.Name,
                Size = FormatBytes(info.Length),
                Path = System.IO.Path.GetRelativePath(workspacePath, file),
                Stale = info.LastWriteTimeUtc < cutoff
            });
        }

        // Subdirectories (tmp, etc.) — report directory totals
        foreach (var dir in Directory.EnumerateDirectories(cacheDir))
        {
            // Skip locks/ — already reported separately
            if (System.IO.Path.GetFileName(dir).Equals("locks", StringComparison.OrdinalIgnoreCase))
                continue;

            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
            var totalSize = files.Sum(f => f.Length);
            var oldestWrite = files.Length > 0 ? files.Min(f => f.LastWriteTimeUtc) : DateTime.UtcNow;

            entries.Add(new DiagCacheEntry
            {
                Label = dirInfo.Name + "/",
                Size = FormatBytes(totalSize),
                Path = System.IO.Path.GetRelativePath(workspacePath, dir),
                Stale = oldestWrite < cutoff
            });
        }

        return entries;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
