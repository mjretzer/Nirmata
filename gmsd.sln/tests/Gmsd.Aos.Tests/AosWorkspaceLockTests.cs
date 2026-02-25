using System.Diagnostics;
using System.Text;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosWorkspaceLockTests
{
    [Fact]
    public async Task MutatingCommands_FailFast_WhenWorkspaceLockIsHeld()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, _, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );
            Assert.True(initExit == 0, $"Expected init exit code 0, got {initExit}. STDERR:{Environment.NewLine}{initErr}");
            WriteRepoMarker(tempWorkspaceRoot);

            WriteHeldWorkspaceLock(tempWorkspaceRoot);

            var (startExit, _, startErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "start"
            );

            Assert.True(startExit == 4, $"Expected lock contention exit code 4 when locked, got {startExit}.");
            Assert.Contains("Workspace is locked.", startErr, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(".aos/locks/workspace.lock.json", startErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task Validation_IsNotBlocked_WhenWorkspaceLockIsHeld()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, _, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );
            Assert.True(initExit == 0, $"Expected init exit code 0, got {initExit}. STDERR:{Environment.NewLine}{initErr}");
            WriteRepoMarker(tempWorkspaceRoot);

            WriteHeldWorkspaceLock(tempWorkspaceRoot);

            var (validateExit, _, validateErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "validate",
                "workspace"
            );

            Assert.True(
                validateExit == 0,
                $"Expected validate workspace exit code 0 even when locked, got {validateExit}. STDERR:{Environment.NewLine}{validateErr}"
            );
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static void WriteHeldWorkspaceLock(string repoRoot)
    {
        var lockPath = Path.Combine(repoRoot, ".aos", "locks", "workspace.lock.json");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);

        // Minimal v1 lock artifact that satisfies schema requirements.
        var json =
            "{\n" +
            "  \"schemaVersion\": 1,\n" +
            "  \"lockKind\": \"workspace\",\n" +
            "  \"acquiredAtUtc\": \"2026-01-01T00:00:00.0000000Z\",\n" +
            "  \"holder\": {\n" +
            "    \"pid\": 12345,\n" +
            "    \"machine\": \"test-machine\",\n" +
            "    \"user\": \"test-user\",\n" +
            "    \"command\": \"test\",\n" +
            "    \"processName\": \"test-process\",\n" +
            "    \"workingDirectory\": \"C:/test\"\n" +
            "  },\n" +
            "  \"releaseHint\": \"Delete the lock file if it is stale.\"\n" +
            "}\n";

        File.WriteAllText(lockPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteRepoMarker(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "Gmsd.slnx");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-locks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotNetAsync(
        string? workingDirectory,
        string dllPath,
        params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        psi.Environment["DOTNET_NOLOGO"] = "1";
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        psi.ArgumentList.Add(dllPath);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        var stdoutTask = process!.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }
}

