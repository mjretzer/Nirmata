using System.Diagnostics;
using System.Text;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosLockContentionTests
{
    [Fact]
    [Trait("Category", "Reliability")]
    public async Task LockHandle_PreventsConcurrentAccess_ViaFileSharingViolation()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            // 1. Initialize workspace
            var (initExit, _, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );
            Assert.True(initExit == 0, $"Expected init exit code 0, got {initExit}. STDERR:{Environment.NewLine}{initErr}");
            WriteRepoMarker(tempWorkspaceRoot);

            // 2. Start a long-running process that holds the lock
            // We'll use 'aos lock acquire' which now holds the handle for the duration of its own process if it were to stay alive,
            // but for a true E2E test, we need a process that *keeps* the handle open.
            // Since 'aos lock acquire' in the CLI currently exits after acquisition (relying on the file existence for 'aos lock status'),
            // we should test the *engine* level behavior or use a simulation.
            
            var lockPath = Path.Combine(tempWorkspaceRoot, ".aos", "locks", "workspace.lock.json");
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);

            // Simulate a process holding the lock with FileShare.None
            using var fs = new FileStream(lockPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var bytes = Encoding.UTF8.GetBytes("{\"lockId\":\"test-lock-id\"}");
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(flushToDisk: true);

            // 3. Attempt to run another command that requires the lock
            var (startExit, _, startErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "start"
            );

            // Should fail with lock contention exit code (4)
            Assert.Equal(4, startExit);
            Assert.Contains("Workspace is locked.", startErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    [Trait("Category", "Reliability")]
    public async Task MultiProcess_LockContention_StressTest()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            
            // Initialize
            await RunDotNetAsync(null, aosDllPath, "init", "--root", tempWorkspaceRoot);
            WriteRepoMarker(tempWorkspaceRoot);

            int concurrentAttempts = 5;
            var tasks = new List<Task<(int ExitCode, string StdOut, string StdErr)>>();

            for (int i = 0; i < concurrentAttempts; i++)
            {
                tasks.Add(RunDotNetAsync(tempWorkspaceRoot, aosDllPath, "lock", "acquire"));
            }

            var results = await Task.WhenAll(tasks);

            int successes = results.Count(r => r.ExitCode == 0);
            int contentions = results.Count(r => r.ExitCode == 4);

            // Exactly one should succeed, others should fail with contention
            Assert.Equal(1, successes);
            Assert.Equal(concurrentAttempts - 1, contentions);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
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
        var root = Path.Combine(Path.GetTempPath(), "nirmata-aos-contention", Guid.NewGuid().ToString("N"));
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
            // Best-effort.
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
