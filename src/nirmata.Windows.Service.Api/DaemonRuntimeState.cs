using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace nirmata.Windows.Service.Api;

/// <summary>
/// A single host log entry captured by <see cref="DaemonLogSink"/>.
/// Returned by <c>GET /api/v1/logs</c>.
/// </summary>
public sealed class HostLogEntry
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}

/// <summary>
/// Summary of a single engine run, returned by <c>GET /api/v1/runs</c>.
/// </summary>
public sealed class RunSummary
{
    public string RunId { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}

/// <summary>
/// Request body for <c>PUT /api/v1/service/host-profile</c>.
/// </summary>
public sealed class HostProfileRequest
{
    /// <summary>Human-readable name identifying this host instance.</summary>
    public required string HostName { get; init; }

    /// <summary>Absolute path to the workspace root this host manages.</summary>
    public required string WorkspacePath { get; init; }

    /// <summary>Optional metadata key/value pairs (e.g. OS, machine id, environment).</summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Singleton holder for mutable daemon process runtime state.
/// Registered as a singleton in DI so all controllers share one instance.
/// </summary>
public sealed class DaemonRuntimeState
{
    private static readonly DateTime _startedAt = DateTime.UtcNow;

    /// <summary>UTC instant the daemon process started.</summary>
    public DateTime StartedAt => _startedAt;

    /// <summary>
    /// Most-recently received host profile from <c>PUT /api/v1/service/host-profile</c>.
    /// Null until the frontend registers a profile.
    /// </summary>
    public HostProfileRequest? HostProfile { get; set; }

    /// <summary>
    /// In-memory run summaries recorded by the daemon during this process lifetime.
    /// Populated by the engine host when runs are started/finished.
    /// </summary>
    public List<RunSummary> Runs { get; } = [];

    // ── Log buffer ────────────────────────────────────────────────────────────
    private const int MaxLogEntries = 500;
    private readonly ConcurrentQueue<HostLogEntry> _logEntries = new();

    /// <summary>
    /// Adds a log entry to the circular buffer, evicting the oldest entry when
    /// the buffer exceeds <see cref="MaxLogEntries"/>.
    /// </summary>
    public void AddLogEntry(HostLogEntry entry)
    {
        _logEntries.Enqueue(entry);
        while (_logEntries.Count > MaxLogEntries)
            _logEntries.TryDequeue(out _);
    }

    /// <summary>
    /// Returns all buffered log entries, newest-last, optionally limited to
    /// the most recent <paramref name="tail"/> entries.
    /// </summary>
    public IReadOnlyList<HostLogEntry> GetLogEntries(int? tail = null)
    {
        var entries = _logEntries.ToArray();
        return tail is > 0 ? entries[^Math.Min(tail.Value, entries.Length)..] : entries;
    }
}

/// <summary>
/// Executes CLI commands as subprocesses and captures their output.
/// Used by <c>POST /api/v1/commands</c> to run AOS commands through the daemon backend.
/// </summary>
public sealed class DaemonCommandExecutor
{
    public sealed record ExecutionResult(bool Ok, string Output);

    private const string AosExecutableName = "aos";
    private const string DotNetExecutableName = "dotnet";

    /// <summary>
    /// Runs <paramref name="argv"/> as a subprocess.
    /// <paramref name="argv"/>[0] is the executable; remaining elements are arguments.
    /// </summary>
    public async Task<ExecutionResult> RunAsync(string[] argv, string? workingDirectory = null)
    {
        var (program, args) = ResolveLaunchCommand(argv, workingDirectory);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = program,
                Arguments = args,
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        var outputBuilder = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) outputBuilder.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new ExecutionResult(false, $"Failed to start process '{program}': {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return new ExecutionResult(process.ExitCode == 0, outputBuilder.ToString().TrimEnd());
    }

    private static (string Program, string Arguments) ResolveLaunchCommand(string[] argv, string? workingDirectory)
    {
        var requestedProgram = argv[0];
        var extraArgs = argv.Length > 1 ? argv[1..] : Array.Empty<string>();

        if (IsAosCommand(requestedProgram))
        {
            if (TryResolveLocalAosDllPath(workingDirectory, out var aosDllPath))
            {
                var resolvedArgs = string.Join(' ', new[] { QuoteIfNeeded(aosDllPath) }
                    .Concat(extraArgs.Select(QuoteIfNeeded)));
                return (DotNetExecutableName, resolvedArgs);
            }

            if (TryResolveLocalAosProjectPath(workingDirectory, out var aosProjectPath))
            {
                var dotnetArgs = new[] { "run", "--project", QuoteIfNeeded(aosProjectPath), "--" }
                    .Concat(extraArgs.Select(QuoteIfNeeded));
                return (DotNetExecutableName, string.Join(' ', dotnetArgs));
            }
        }

        var args = extraArgs.Length > 0
            ? string.Join(' ', extraArgs.Select(QuoteIfNeeded))
            : string.Empty;

        return (requestedProgram, args);
    }

    private static bool IsAosCommand(string program) =>
        string.Equals(program, AosExecutableName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(program, $"{AosExecutableName}.exe", StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveLocalAosDllPath(string? workingDirectory, out string aosDllPath)
    {
        foreach (var searchRoot in GetSearchRoots(workingDirectory))
        {
            foreach (var candidate in GetCandidateAosDllPaths(searchRoot))
            {
                if (File.Exists(candidate))
                {
                    aosDllPath = candidate;
                    return true;
                }
            }
        }

        aosDllPath = string.Empty;
        return false;
    }

    private static bool TryResolveLocalAosProjectPath(string? workingDirectory, out string aosProjectPath)
    {
        foreach (var searchRoot in GetSearchRoots(workingDirectory))
        {
            foreach (var candidate in GetCandidateAosProjectPaths(searchRoot))
            {
                if (File.Exists(candidate))
                {
                    aosProjectPath = candidate;
                    return true;
                }
            }
        }

        aosProjectPath = string.Empty;
        return false;
    }

    private static IEnumerable<string> GetSearchRoots(string? workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            yield return Path.GetFullPath(workingDirectory);
        }

        var roots = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.GetDirectoryName(typeof(DaemonCommandExecutor).Assembly.Location) ?? string.Empty
        };

        foreach (var root in roots)
        {
            var current = Path.GetFullPath(root);
            while (!string.IsNullOrWhiteSpace(current))
            {
                yield return current;

                var parent = Directory.GetParent(current)?.FullName;
                if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
            }
        }
    }

    private static IEnumerable<string> GetCandidateAosDllPaths(string root)
    {
        yield return Path.Combine(root, "aos.dll");
        yield return Path.Combine(root, "nirmata.Aos", "bin", "Debug", "net10.0", "aos.dll");
        yield return Path.Combine(root, "nirmata.Aos", "bin", "Release", "net10.0", "aos.dll");
        yield return Path.Combine(root, "src", "nirmata.Aos", "bin", "Debug", "net10.0", "aos.dll");
        yield return Path.Combine(root, "src", "nirmata.Aos", "bin", "Release", "net10.0", "aos.dll");
    }

    private static IEnumerable<string> GetCandidateAosProjectPaths(string root)
    {
        yield return Path.Combine(root, "nirmata.Aos", "nirmata.Aos.csproj");
        yield return Path.Combine(root, "src", "nirmata.Aos", "nirmata.Aos.csproj");
    }

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ') ? $"\"{value}\"" : value;
}
