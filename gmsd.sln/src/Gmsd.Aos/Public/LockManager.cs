using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Gmsd.Aos.Public;

/// <summary>
/// Public lock manager implementation backed by filesystem lock files.
/// </summary>
public sealed class LockManager : ILockManager
{
    private readonly string _aosRootPath;
    private readonly string _lockFilePath;
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private LockManager(string aosRootPath)
    {
        if (string.IsNullOrWhiteSpace(aosRootPath))
        {
            throw new ArgumentException("Missing AOS root path.", nameof(aosRootPath));
        }

        _aosRootPath = aosRootPath;
        _lockFilePath = Path.Combine(aosRootPath, "locks", "workspace.lock.json");
    }

    /// <summary>
    /// Creates a lock manager for an explicit <c>.aos</c> root path.
    /// </summary>
    public static LockManager FromAosRoot(string aosRootPath) => new(aosRootPath);

    /// <summary>
    /// Creates a lock manager for a workspace's <c>.aos</c> root.
    /// </summary>
    public static LockManager FromWorkspace(IWorkspace workspace)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));
        return new LockManager(workspace.AosRootPath);
    }

    /// <inheritdoc />
    public bool Acquire()
    {
        // Ensure locks directory exists
        var locksDir = Path.GetDirectoryName(_lockFilePath);
        if (!string.IsNullOrEmpty(locksDir) && !Directory.Exists(locksDir))
        {
            Directory.CreateDirectory(locksDir);
        }

        // Fail fast if lock already exists
        if (File.Exists(_lockFilePath))
        {
            return false;
        }

        var process = Process.GetCurrentProcess();
        var holderInfo = new LockHolderInfoDocument(
            ProcessId: process.Id,
            ProcessName: process.ProcessName,
            StartedAtUtc: DateTimeOffset.UtcNow.ToString("O"),
            MachineName: Environment.MachineName);

        var json = JsonSerializer.Serialize(holderInfo, JsonOptions);
        File.WriteAllText(_lockFilePath, json, Utf8NoBom);

        return true;
    }

    /// <inheritdoc />
    public bool Release(bool force = false)
    {
        if (!File.Exists(_lockFilePath))
        {
            return false;
        }

        if (!force)
        {
            // Try to validate the lock file can be parsed
            try
            {
                var json = File.ReadAllText(_lockFilePath, Utf8NoBom);
                _ = JsonSerializer.Deserialize<LockHolderInfoDocument>(json, JsonOptions);
            }
            catch
            {
                return false;
            }
        }

        File.Delete(_lockFilePath);
        return true;
    }

    /// <inheritdoc />
    public LockStatus GetStatus()
    {
        if (!File.Exists(_lockFilePath))
        {
            return new LockStatus(IsLocked: false, Holder: null);
        }

        try
        {
            var json = File.ReadAllText(_lockFilePath, Utf8NoBom);
            var holderDoc = JsonSerializer.Deserialize<LockHolderInfoDocument>(json, JsonOptions);

            if (holderDoc is null)
            {
                return new LockStatus(IsLocked: true, Holder: null);
            }

            var holderInfo = new LockHolderInfo(
                holderDoc.ProcessId,
                holderDoc.ProcessName,
                DateTimeOffset.Parse(holderDoc.StartedAtUtc),
                holderDoc.MachineName);

            return new LockStatus(IsLocked: true, holderInfo);
        }
        catch
        {
            // Corrupted lock file still means locked
            return new LockStatus(IsLocked: true, Holder: null);
        }
    }

    private sealed record LockHolderInfoDocument(
        int ProcessId,
        string ProcessName,
        string StartedAtUtc,
        string MachineName);
}
