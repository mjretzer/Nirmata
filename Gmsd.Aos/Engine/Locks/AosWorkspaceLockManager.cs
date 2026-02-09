using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Aos.Engine.Locks;

internal static class AosWorkspaceLockManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static (WorkspaceLockDocument? Doc, string? Raw, string LockPath) TryReadExisting(string aosRootPath)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));

        var lockPath = AosPathRouter.GetWorkspaceLockPath(aosRootPath);
        if (!File.Exists(lockPath))
        {
            return (null, null, lockPath);
        }

        var (doc, raw) = TryReadLock(lockPath);
        return (doc, raw, lockPath);
    }

    public static AosWorkspaceLockAcquireResult TryAcquireExclusive(
        string aosRootPath,
        string command,
        bool createDirectories)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (command is null) throw new ArgumentNullException(nameof(command));

        var lockPath = AosPathRouter.GetWorkspaceLockPath(aosRootPath);

        if (!createDirectories)
        {
            var lockDir = Path.GetDirectoryName(lockPath);
            if (string.IsNullOrWhiteSpace(lockDir) || !Directory.Exists(lockDir))
            {
                return AosWorkspaceLockAcquireResult.NotAcquired(
                    lockPath,
                    existingLock: null,
                    existingLockRaw: null,
                    message: "Lock directory does not exist."
                );
            }
        }
        else
        {
            var lockDir = Path.GetDirectoryName(lockPath);
            if (!string.IsNullOrWhiteSpace(lockDir))
            {
                Directory.CreateDirectory(lockDir);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var lockId = Guid.NewGuid().ToString("N");

        var holder = CreateHolder(command);
        var doc = new WorkspaceLockDocument(
            SchemaVersion: 1,
            LockKind: "workspace",
            AcquiredAtUtc: now.ToString("O"),
            Holder: holder,
            LockId: lockId,
            ExpiresAtUtc: null,
            ReleaseHint: $"If this lock is stale, delete '{AosPathRouter.WorkspaceLockContractPath}' (or use 'aos lock release' when available)."
        );

        try
        {
            // Exclusive lock acquisition is enforced via atomic "create new file".
            // If the file already exists, another process holds (or left) the lock.
            using var fs = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var bytes = DeterministicJsonFileWriter.SerializeToCanonicalUtf8Bytes(doc, JsonOptions, writeIndented: true);
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(flushToDisk: true);

            return AosWorkspaceLockAcquireResult.CreateAcquired(
                new AosWorkspaceLockHandle(lockPath, lockId)
            );
        }
        catch (IOException) when (File.Exists(lockPath))
        {
            var (existing, raw) = TryReadLock(lockPath);
            var msg = existing is null ? "Workspace lock file exists but could not be parsed." : "Workspace lock file exists.";
            return AosWorkspaceLockAcquireResult.NotAcquired(lockPath, existing, raw, msg);
        }
    }

    private static (WorkspaceLockDocument? Doc, string? Raw) TryReadLock(string lockPath)
    {
        try
        {
            var raw = File.ReadAllText(lockPath);
            var doc = JsonSerializer.Deserialize<WorkspaceLockDocument>(raw, JsonOptions);
            return (doc, raw);
        }
        catch
        {
            return (null, null);
        }
    }

    private static WorkspaceLockHolder CreateHolder(string command)
    {
        var machine = Environment.MachineName;
        var user = Environment.UserName;
        var pid = Environment.ProcessId;
        var workingDirectory = Directory.GetCurrentDirectory();

        string? processName = null;
        try
        {
            processName = Process.GetCurrentProcess().ProcessName;
        }
        catch
        {
            // Best-effort.
        }

        return new WorkspaceLockHolder(
            Pid: pid,
            Machine: machine,
            User: user,
            Command: command,
            ProcessName: processName,
            WorkingDirectory: workingDirectory
        );
    }
}

internal sealed record AosWorkspaceLockAcquireResult(
    bool Acquired,
    AosWorkspaceLockHandle? Handle,
    string LockPath,
    WorkspaceLockDocument? ExistingLock,
    string? ExistingLockRaw,
    string Message)
{
    public static AosWorkspaceLockAcquireResult CreateAcquired(AosWorkspaceLockHandle handle) =>
        new(
            Acquired: true,
            Handle: handle,
            LockPath: handle.LockPath,
            ExistingLock: null,
            ExistingLockRaw: null,
            Message: "Acquired."
        );

    public static AosWorkspaceLockAcquireResult NotAcquired(
        string lockPath,
        WorkspaceLockDocument? existingLock,
        string? existingLockRaw,
        string message) =>
        new(
            Acquired: false,
            Handle: null,
            LockPath: lockPath,
            ExistingLock: existingLock,
            ExistingLockRaw: existingLockRaw,
            Message: message
        );
}

internal sealed class AosWorkspaceLockHandle : IDisposable
{
    public string LockPath { get; }
    public string LockId { get; }

    public AosWorkspaceLockHandle(string lockPath, string lockId)
    {
        LockPath = lockPath ?? throw new ArgumentNullException(nameof(lockPath));
        LockId = lockId ?? throw new ArgumentNullException(nameof(lockId));
    }

    public void Dispose()
    {
        try
        {
            if (!File.Exists(LockPath))
            {
                return;
            }

            // Guardrail: only delete if the lockId matches, to avoid deleting a lock
            // created by a different process after ours was forcibly removed.
            var raw = File.ReadAllText(LockPath);
            using var json = JsonDocument.Parse(raw);

            if (!json.RootElement.TryGetProperty("lockId", out var lockIdProp) ||
                lockIdProp.ValueKind != JsonValueKind.String)
            {
                return;
            }

            var current = lockIdProp.GetString();
            if (!string.Equals(current, LockId, StringComparison.Ordinal))
            {
                return;
            }

            File.Delete(LockPath);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}

internal sealed record WorkspaceLockDocument(
    int SchemaVersion,
    string LockKind,
    string AcquiredAtUtc,
    WorkspaceLockHolder Holder,
    string? LockId,
    string? ExpiresAtUtc,
    string? ReleaseHint);

internal sealed record WorkspaceLockHolder(
    int Pid,
    string Machine,
    string User,
    string? Command,
    string? ProcessName,
    string? WorkingDirectory);
