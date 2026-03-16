using System.Diagnostics;
using System.Text.Json;
using nirmata.Aos.Engine.Evidence;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosRunPauseResumeIntegrationTests
{
    [Fact]
    public async Task PauseResume_WithTaskExecutor_MaintainsStateConsistency()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, initOut, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(
                initExit == 0,
                $"Expected exit code 0, got {initExit}.{Environment.NewLine}STDOUT:{Environment.NewLine}{initOut}{Environment.NewLine}STDERR:{Environment.NewLine}{initErr}"
            );
            WriteRepoMarker(tempWorkspaceRoot);

            var (startExit, startOut, startErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "start"
            );

            Assert.True(
                startExit == 0,
                $"Expected exit code 0, got {startExit}.{Environment.NewLine}STDOUT:{Environment.NewLine}{startOut}{Environment.NewLine}STDERR:{Environment.NewLine}{startErr}"
            );

            var runId = startOut.Trim();
            Assert.True(AosRunId.IsValid(runId), $"Expected a valid run id, got '{runId}'.");

            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            var runJsonPath = Path.Combine(aosRoot, "evidence", "runs", runId, "artifacts", "run.json");

            var initialJson = ReadJson(runJsonPath);
            Assert.Equal("started", initialJson.RootElement.GetProperty("status").GetString());

            var (pauseExit, pauseOut, pauseErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "pause",
                "--run-id",
                runId
            );

            Assert.True(
                pauseExit == 0,
                $"Expected exit code 0, got {pauseExit}.{Environment.NewLine}STDOUT:{Environment.NewLine}{pauseOut}{Environment.NewLine}STDERR:{Environment.NewLine}{pauseErr}"
            );

            var pausedJson = ReadJson(runJsonPath);
            Assert.Equal("paused", pausedJson.RootElement.GetProperty("status").GetString());

            var (resumeExit, resumeOut, resumeErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "resume",
                "--run-id",
                runId
            );

            Assert.True(
                resumeExit == 0,
                $"Expected exit code 0, got {resumeExit}.{Environment.NewLine}STDOUT:{Environment.NewLine}{resumeOut}{Environment.NewLine}STDERR:{Environment.NewLine}{resumeErr}"
            );

            var resumedJson = ReadJson(runJsonPath);
            Assert.Equal("started", resumedJson.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task PauseRun_WithLockAcquisition_SucceedsWithExclusiveLock()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, initOut, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(initExit == 0);
            WriteRepoMarker(tempWorkspaceRoot);

            var (startExit, startOut, startErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "start"
            );

            Assert.True(startExit == 0);
            var runId = startOut.Trim();

            var (pauseExit, pauseOut, pauseErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "pause",
                "--run-id",
                runId
            );

            Assert.True(pauseExit == 0, $"Pause failed: {pauseErr}");
            Assert.Contains("paused successfully", pauseOut);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task ResumeRun_WithLockAcquisition_SucceedsWithExclusiveLock()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, initOut, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(initExit == 0);
            WriteRepoMarker(tempWorkspaceRoot);

            var (startExit, startOut, startErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "start"
            );

            Assert.True(startExit == 0);
            var runId = startOut.Trim();

            var (pauseExit, pauseOut, pauseErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "pause",
                "--run-id",
                runId
            );

            Assert.True(pauseExit == 0);

            var (resumeExit, resumeOut, resumeErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "resume",
                "--run-id",
                runId
            );

            Assert.True(resumeExit == 0, $"Resume failed: {resumeErr}");
            Assert.Contains("resumed successfully", resumeOut);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task PauseRun_WithInvalidRunId_FailsWithProperErrorCode()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, initOut, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(initExit == 0);
            WriteRepoMarker(tempWorkspaceRoot);

            var (pauseExit, pauseOut, pauseErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "pause",
                "--run-id",
                "invalid-run-id"
            );

            Assert.Equal(1, pauseExit);
            Assert.Contains("Invalid run id", pauseErr);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task ResumeRun_WithNonexistentRun_FailsWithProperErrorCode()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, initOut, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(initExit == 0);
            WriteRepoMarker(tempWorkspaceRoot);

            var validRunId = AosRunId.New();

            var (resumeExit, resumeOut, resumeErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "resume",
                "--run-id",
                validRunId
            );

            Assert.Equal(2, resumeExit);
            Assert.Contains("Run metadata not found", resumeErr);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task PauseRun_OnAlreadyPausedRun_FailsWithConflictError()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, initOut, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(initExit == 0);
            WriteRepoMarker(tempWorkspaceRoot);

            var (startExit, startOut, startErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "start"
            );

            Assert.True(startExit == 0);
            var runId = startOut.Trim();

            var (pauseExit, pauseOut, pauseErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "pause",
                "--run-id",
                runId
            );

            Assert.True(pauseExit == 0);

            var (pauseAgainExit, pauseAgainOut, pauseAgainErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "pause",
                "--run-id",
                runId
            );

            Assert.Equal(2, pauseAgainExit);
            Assert.Contains("Cannot pause run in 'paused' status", pauseAgainErr);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task ResumeRun_OnRunningRun_FailsWithConflictError()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, initOut, initErr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(initExit == 0);
            WriteRepoMarker(tempWorkspaceRoot);

            var (startExit, startOut, startErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "start"
            );

            Assert.True(startExit == 0);
            var runId = startOut.Trim();

            var (resumeExit, resumeOut, resumeErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "resume",
                "--run-id",
                runId
            );

            Assert.Equal(2, resumeExit);
            Assert.Contains("Cannot resume run in 'started' status", resumeErr);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"nirmata-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        return tempPath;
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

    private static void WriteRepoMarker(string workspaceRoot)
    {
        var gitDir = Path.Combine(workspaceRoot, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/main\n");
    }

    private static JsonDocument ReadJson(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonDocument.Parse(json);
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
