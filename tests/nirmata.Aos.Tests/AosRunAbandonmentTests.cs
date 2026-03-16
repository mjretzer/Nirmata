using System.Diagnostics;
using System.Text;
using System.Text.Json;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosRunAbandonmentTests
{
    [Fact]
    public async Task MarkRunAbandoned_UpdatesRunStatusAndIndex()
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

            var (startExit, startOut, startErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "start"
            );
            Assert.True(startExit == 0, $"Expected run start exit code 0, got {startExit}. STDERR:{Environment.NewLine}{startErr}");

            var runId = startOut.Trim();
            Assert.False(string.IsNullOrWhiteSpace(runId), "Expected run ID from 'aos run start'.");

            var runJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "evidence", "runs", runId, "artifacts", "run.json");
            Assert.True(File.Exists(runJsonPath), $"Expected run.json at '{runJsonPath}'.");

            var runJson = File.ReadAllText(runJsonPath);
            using var runDoc = JsonDocument.Parse(runJson);
            var statusBefore = runDoc.RootElement.GetProperty("status").GetString();
            Assert.Equal("started", statusBefore);

            var indexPath = Path.Combine(tempWorkspaceRoot, ".aos", "evidence", "runs", "index.json");
            var indexJson = File.ReadAllText(indexPath);
            using var indexDoc = JsonDocument.Parse(indexJson);
            var items = indexDoc.RootElement.GetProperty("items").EnumerateArray();
            var runItem = items.FirstOrDefault(i => i.GetProperty("runId").GetString() == runId);
            Assert.False(runItem.ValueKind == JsonValueKind.Undefined, "Expected run in index before abandonment.");
            var statusInIndexBefore = runItem.GetProperty("status").GetString();
            Assert.Equal("started", statusInIndexBefore);

            var bootstrapResult = AosWorkspaceBootstrapper.EnsureInitialized(tempWorkspaceRoot);
            var aosRootPath = bootstrapResult.AosRootPath;
            var runManager = nirmata.Aos.Public.RunManager.FromAosRoot(aosRootPath);
            runManager.MarkRunAbandoned(runId, DateTimeOffset.UtcNow);

            runJson = File.ReadAllText(runJsonPath);
            using var runDocAfter = JsonDocument.Parse(runJson);
            var statusAfter = runDocAfter.RootElement.GetProperty("status").GetString();
            Assert.Equal("abandoned", statusAfter);

            indexJson = File.ReadAllText(indexPath);
            using var indexDocAfter = JsonDocument.Parse(indexJson);
            var itemsAfter = indexDocAfter.RootElement.GetProperty("items").EnumerateArray();
            var runItemAfter = itemsAfter.FirstOrDefault(i => i.GetProperty("runId").GetString() == runId);
            Assert.False(runItemAfter.ValueKind == JsonValueKind.Undefined, "Expected run in index after abandonment.");
            var statusInIndexAfter = runItemAfter.GetProperty("status").GetString();
            Assert.Equal("abandoned", statusInIndexAfter);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task MarkRunAbandoned_DoesNotMarkFinishedRuns()
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

            var (startExit, startOut, startErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "start"
            );
            Assert.True(startExit == 0, $"Expected run start exit code 0, got {startExit}. STDERR:{Environment.NewLine}{startErr}");

            var runId = startOut.Trim();

            var (finishExit, _, finishErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "finish",
                "--run-id",
                runId
            );
            Assert.True(finishExit == 0, $"Expected run finish exit code 0, got {finishExit}. STDERR:{Environment.NewLine}{finishErr}");

            var bootstrapResult = AosWorkspaceBootstrapper.EnsureInitialized(tempWorkspaceRoot);
            var aosRootPath = bootstrapResult.AosRootPath;
            var runManager = nirmata.Aos.Public.RunManager.FromAosRoot(aosRootPath);
            runManager.MarkRunAbandoned(runId, DateTimeOffset.UtcNow);

            var runJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "evidence", "runs", runId, "artifacts", "run.json");
            var runJson = File.ReadAllText(runJsonPath);
            using var runDoc = JsonDocument.Parse(runJson);
            var status = runDoc.RootElement.GetProperty("status").GetString();
            Assert.Equal("finished", status);
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
        var root = Path.Combine(Path.GetTempPath(), "nirmata-aos-abandonment", Guid.NewGuid().ToString("N"));
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
