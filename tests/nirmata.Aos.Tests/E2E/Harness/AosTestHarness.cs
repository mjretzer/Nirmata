namespace nirmata.Aos.Tests.E2E.Harness;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Provides high-level operations for driving AOS commands from E2E tests.
/// Supports both CLI subprocess and in-process execution modes.
/// </summary>
public sealed class AosTestHarness
{
    private readonly string _repoRoot;
    private readonly bool _useCliMode;

    /// <summary>
    /// Creates a new test harness for the specified repository root.
    /// </summary>
    /// <param name="repoRoot">The root path of the repository to test against.</param>
    /// <param name="useCliMode">If true, uses CLI subprocess execution. If false, uses in-process routing (when available).</param>
    public AosTestHarness(string repoRoot, bool useCliMode = true)
    {
        _repoRoot = repoRoot ?? throw new ArgumentNullException(nameof(repoRoot));
        _useCliMode = useCliMode;
    }

    /// <summary>
    /// Runs an AOS command asynchronously.
    /// </summary>
    /// <param name="command">The command to run (e.g., "init", "validate schemas").</param>
    /// <param name="args">Additional arguments for the command.</param>
    /// <returns>A RunResult containing exit code and captured output.</returns>
    public async Task<RunResult> RunAsync(string command, params string[] args)
    {
        if (_useCliMode)
        {
            return await RunCliAsync(command, args);
        }

        // In-process mode would route through ICommandRouter
        // For now, fall back to CLI mode
        return await RunCliAsync(command, args);
    }

    /// <summary>
    /// Asserts that the AOS workspace layout is valid (all 6 layers exist).
    /// </summary>
    public void AssertLayout()
    {
        AssertAosLayout.AssertAllLayersExist(_repoRoot);
    }

    /// <summary>
    /// Reads and deserializes a state file from the .aos/ directory.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="relativePath">The relative path within .aos/ (e.g., "state/cursor.json").</param>
    /// <returns>The deserialized object.</returns>
    public T ReadState<T>(string relativePath)
    {
        return StateReader.Read<T>(_repoRoot, relativePath);
    }

    /// <summary>
    /// Reads the last N events from the events log.
    /// </summary>
    /// <param name="count">The number of events to read from the tail.</param>
    /// <returns>A list of event entries.</returns>
    public IReadOnlyList<EventEntry> ReadEventsTail(int count)
    {
        return EventLogReader.ReadTail(_repoRoot, count);
    }

    private async Task<RunResult> RunCliAsync(string command, string[] args)
    {
        // Find aos.dll - look in common output locations
        var aosDllPath = FindAosDllPath();
        if (aosDllPath is null)
        {
            throw new InvalidOperationException("Could not find aos.dll. Ensure the nirmata.Aos project is built.");
        }

        var arguments = $"\"{aosDllPath}\" {command}";
        if (args.Length > 0)
        {
            arguments += " " + string.Join(" ", args);
        }
        arguments += $" --root \"{_repoRoot}\"";

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new RunResult(
            ExitCode: process.ExitCode,
            StdOut: stdout,
            StdErr: stderr);
    }

    private static string? FindAosDllPath()
    {
        // Search in likely locations relative to the test assembly
        var baseDir = AppContext.BaseDirectory;
        var searchPaths = new[]
        {
            Path.Combine(baseDir, "aos.dll"),
            // From tests/nirmata.Aos.Tests/bin/Debug/net10.0/ go up 5 levels to solution root
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "nirmata.Aos", "bin", "Debug", "net10.0", "aos.dll"),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "nirmata.Aos", "bin", "Release", "net10.0", "aos.dll"),
            Path.Combine(baseDir, "..", "..", "..", "nirmata.Aos", "aos.dll"),
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}

/// <summary>
/// Represents the result of running an AOS command.
/// </summary>
public sealed record RunResult(
    int ExitCode,
    string StdOut,
    string StdErr);

/// <summary>
/// Represents a single event entry from the events log.
/// </summary>
public sealed record EventEntry(
    string EventType,
    DateTimeOffset Timestamp,
    JsonElement Data);
