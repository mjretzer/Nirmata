using System.Diagnostics;
using System.Text;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosLockCliTests
{
    [Fact]
    public async Task LockStatus_PrintsUnlocked_WhenNoLockFile()
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

            var (statusExit, statusOut, statusErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "lock",
                "status"
            );

            Assert.True(statusExit == 0, $"Expected lock status exit code 0, got {statusExit}. STDERR:{Environment.NewLine}{statusErr}");
            Assert.Contains("UNLOCKED", statusOut, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(".aos/locks/workspace.lock.json", statusOut, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task LockAcquire_CreatesLockFile_AndStatusShowsLocked()
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

            var (acqExit, acqOut, acqErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "lock",
                "acquire"
            );

            Assert.True(acqExit == 0, $"Expected lock acquire exit code 0, got {acqExit}. STDERR:{Environment.NewLine}{acqErr}");
            Assert.Contains("ACQUIRED", acqOut, StringComparison.OrdinalIgnoreCase);

            var lockPath = Path.Combine(tempWorkspaceRoot, ".aos", "locks", "workspace.lock.json");
            Assert.True(File.Exists(lockPath), $"Expected lock file at '{lockPath}'.");

            var (statusExit, statusOut, statusErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "lock",
                "status"
            );

            Assert.True(statusExit == 0, $"Expected lock status exit code 0, got {statusExit}. STDERR:{Environment.NewLine}{statusErr}");
            Assert.Contains("LOCKED", statusOut, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Holder:", statusOut, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task LockAcquire_WhenAlreadyLocked_ReturnsStableExitCodeAndActionableError()
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

            var (firstExit, _, firstErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "lock",
                "acquire"
            );
            Assert.True(firstExit == 0, $"Expected initial lock acquire exit code 0, got {firstExit}. STDERR:{Environment.NewLine}{firstErr}");

            var (secondExit, _, secondErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "lock",
                "acquire"
            );

            Assert.True(secondExit == 4, $"Expected lock contention exit code 4, got {secondExit}. STDERR:{Environment.NewLine}{secondErr}");
            Assert.Contains("Workspace is locked.", secondErr, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(".aos/locks/workspace.lock.json", secondErr, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Next steps:", secondErr, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("aos lock status", secondErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task LockRelease_RemovesLockFile()
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

            var (relExit, relOut, relErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "lock",
                "release"
            );

            Assert.True(relExit == 0, $"Expected lock release exit code 0, got {relExit}. STDERR:{Environment.NewLine}{relErr}");
            Assert.Contains("RELEASED", relOut, StringComparison.OrdinalIgnoreCase);

            var lockPath = Path.Combine(tempWorkspaceRoot, ".aos", "locks", "workspace.lock.json");
            Assert.False(File.Exists(lockPath), "Expected lock file to be removed.");
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task LockRelease_WithoutForce_Fails_WhenLockIsUnparseable()
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

            var lockPath = Path.Combine(tempWorkspaceRoot, ".aos", "locks", "workspace.lock.json");
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
            File.WriteAllText(lockPath, "{not-json", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var (relExit, _, relErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "lock",
                "release"
            );

            Assert.True(relExit != 0, "Expected non-zero exit code for unparseable lock file.");
            Assert.Contains("--force", relErr, StringComparison.OrdinalIgnoreCase);
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
        var path = Path.Combine(repoRoot, "nirmata.slnx");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "nirmata-aos-locks-cli", Guid.NewGuid().ToString("N"));
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

