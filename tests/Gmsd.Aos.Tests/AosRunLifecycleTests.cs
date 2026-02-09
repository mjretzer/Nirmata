using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosRunLifecycleTests
{
    [Fact]
    public async Task AosRunStart_CreatesEvidenceScaffoldAndIndexEntry()
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
            var runRoot = Path.Combine(aosRoot, "evidence", "runs", runId);
            var logsPath = Path.Combine(runRoot, "logs");
            var outputsPath = Path.Combine(runRoot, "outputs");
            var artifactsPath = Path.Combine(runRoot, "artifacts");
            var commandsJsonPath = Path.Combine(runRoot, "commands.json");
            var summaryJsonPath = Path.Combine(runRoot, "summary.json");
            var runJsonPath = Path.Combine(artifactsPath, "run.json");
            var packetJsonPath = Path.Combine(artifactsPath, "packet.json");
            var indexJsonPath = Path.Combine(aosRoot, "evidence", "runs", "index.json");

            Assert.True(Directory.Exists(logsPath), $"Expected logs directory at '{logsPath}'.");
            Assert.True(Directory.Exists(outputsPath), $"Expected outputs directory at '{outputsPath}'.");
            Assert.True(Directory.Exists(artifactsPath), $"Expected artifacts directory at '{artifactsPath}'.");
            Assert.True(File.Exists(commandsJsonPath), $"Expected run commands view at '{commandsJsonPath}'.");
            Assert.True(File.Exists(summaryJsonPath), $"Expected run summary at '{summaryJsonPath}'.");
            Assert.True(File.Exists(runJsonPath), $"Expected run metadata at '{runJsonPath}'.");
            Assert.True(File.Exists(packetJsonPath), $"Expected run packet at '{packetJsonPath}'.");
            Assert.True(File.Exists(indexJsonPath), $"Expected run index at '{indexJsonPath}'.");

            AssertDeterministicJsonFile(commandsJsonPath);
            AssertDeterministicJsonFile(summaryJsonPath);
            AssertDeterministicJsonFile(runJsonPath);
            AssertDeterministicJsonFile(packetJsonPath);
            AssertDeterministicJsonFile(indexJsonPath);

            var runJson = ReadJson(runJsonPath);
            Assert.Equal("started", runJson.RootElement.GetProperty("status").GetString());
            Assert.Equal(runId, runJson.RootElement.GetProperty("runId").GetString());
            Assert.True(runJson.RootElement.TryGetProperty("finishedAtUtc", out var finishedAt));
            Assert.True(finishedAt.ValueKind is JsonValueKind.Null);

            var packetJson = ReadJson(packetJsonPath);
            Assert.Equal(1, packetJson.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(runId, packetJson.RootElement.GetProperty("runId").GetString());
            Assert.Equal("run start", packetJson.RootElement.GetProperty("command").GetString());
            var rawArgs = packetJson.RootElement.GetProperty("args").EnumerateArray().Select(e => e.GetString()).ToArray();
            Assert.Empty(rawArgs);

            var indexJson = ReadJson(indexJsonPath);
            var items = indexJson.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Contains(items, i => i.GetProperty("runId").GetString() == runId);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosRunFinish_UpdatesRunMetadataAndIndexDeterministically()
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

            var runIdA = await StartRunAsync(aosDllPath, tempWorkspaceRoot);
            var runIdB = await StartRunAsync(aosDllPath, tempWorkspaceRoot);

            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            var runRootB = Path.Combine(aosRoot, "evidence", "runs", runIdB);
            var runJsonPathB = Path.Combine(runRootB, "artifacts", "run.json");
            var resultJsonPathB = Path.Combine(runRootB, "artifacts", "result.json");
            var summaryJsonPathB = Path.Combine(runRootB, "summary.json");
            var indexJsonPath = Path.Combine(aosRoot, "evidence", "runs", "index.json");

            var beforeRunB = ReadJson(runJsonPathB);
            var startedAtBefore = beforeRunB.RootElement.GetProperty("startedAtUtc").GetString();
            Assert.False(string.IsNullOrWhiteSpace(startedAtBefore));

            // Index should be deterministically ordered by runId.
            AssertRunIndexSortedByRunId(indexJsonPath);

            var (finishExit, finishOut, finishErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "run",
                "finish",
                "--run-id",
                runIdB
            );

            Assert.True(
                finishExit == 0,
                $"Expected exit code 0, got {finishExit}.{Environment.NewLine}STDOUT:{Environment.NewLine}{finishOut}{Environment.NewLine}STDERR:{Environment.NewLine}{finishErr}"
            );

            Assert.True(File.Exists(resultJsonPathB), $"Expected run result at '{resultJsonPathB}'.");
            Assert.True(File.Exists(summaryJsonPathB), $"Expected run summary at '{summaryJsonPathB}'.");

            AssertDeterministicJsonFile(runJsonPathB);
            AssertDeterministicJsonFile(resultJsonPathB);
            AssertDeterministicJsonFile(summaryJsonPathB);
            AssertDeterministicJsonFile(indexJsonPath);

            var afterRunB = ReadJson(runJsonPathB);
            Assert.Equal(runIdB, afterRunB.RootElement.GetProperty("runId").GetString());
            Assert.Equal("finished", afterRunB.RootElement.GetProperty("status").GetString());
            Assert.Equal(startedAtBefore, afterRunB.RootElement.GetProperty("startedAtUtc").GetString());
            var finishedAtAfter = afterRunB.RootElement.GetProperty("finishedAtUtc").GetString();
            Assert.False(string.IsNullOrWhiteSpace(finishedAtAfter));

            // Index reflects the finished run deterministically and remains sorted.
            var indexJson = ReadJson(indexJsonPath);
            var items = indexJson.RootElement.GetProperty("items").EnumerateArray().ToArray();
            var itemB = items.Single(i => i.GetProperty("runId").GetString() == runIdB);
            Assert.Equal("finished", itemB.GetProperty("status").GetString());
            Assert.Equal(finishedAtAfter, itemB.GetProperty("finishedAtUtc").GetString());

            var itemA = items.Single(i => i.GetProperty("runId").GetString() == runIdA);
            Assert.Equal("started", itemA.GetProperty("status").GetString());
            Assert.True(itemA.GetProperty("finishedAtUtc").ValueKind is JsonValueKind.Null);

            AssertRunIndexSortedByRunId(indexJsonPath);

            var resultJson = ReadJson(resultJsonPathB);
            Assert.Equal(1, resultJson.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(runIdB, resultJson.RootElement.GetProperty("runId").GetString());
            Assert.Equal("succeeded", resultJson.RootElement.GetProperty("status").GetString());
            Assert.Equal(0, resultJson.RootElement.GetProperty("exitCode").GetInt32());

            var produced = resultJson.RootElement.GetProperty("producedArtifacts").EnumerateArray().ToArray();
            Assert.Contains(
                produced,
                a => a.GetProperty("contractPath").GetString() == $".aos/evidence/runs/{runIdB}/artifacts/manifest.json"
            );
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static async Task<string> StartRunAsync(string aosDllPath, string tempWorkspaceRoot)
    {
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
        return runId;
    }

    private static void AssertRunIndexSortedByRunId(string indexJsonPath)
    {
        var json = ReadJson(indexJsonPath);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var runIds = items.Select(i => i.GetProperty("runId").GetString() ?? string.Empty).ToArray();
        var sorted = runIds.OrderBy(r => r, StringComparer.Ordinal).ToArray();
        Assert.Equal(sorted, runIds);
    }

    private static void AssertDeterministicJsonFile(string path)
    {
        // No UTF-8 BOM.
        var bytes = File.ReadAllBytes(path);
        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            $"Expected no UTF-8 BOM in '{path}'."
        );

        // LF line endings.
        var text = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);

        // Stable key ordering for top-level run.json payloads (when present).
        if (Path.GetFileName(path).Equals("run.json", StringComparison.Ordinal))
        {
            // Canonical deterministic JSON requires ordinal key sorting (recursive).
            var keys = new[]
            {
                "schemaVersion",
                "runId",
                "status",
                "startedAtUtc",
                "finishedAtUtc"
            };

            var positions = keys
                .Select(k => (Key: k, Index: text.IndexOf($"\"{k}\"", StringComparison.Ordinal)))
                .ToArray();

            Assert.All(positions, p => Assert.True(p.Index >= 0, $"Expected to find key '{p.Key}' in '{path}'."));

            var expectedOrder = keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            var expectedPositions = expectedOrder
                .Select(k => positions.Single(p => p.Key == k).Index)
                .ToArray();

            var actualPositions = positions
                .OrderBy(p => p.Index)
                .Select(p => p.Index)
                .ToArray();

            Assert.Equal(expectedPositions, actualPositions);
        }
    }

    private static JsonDocument ReadJson(string path)
    {
        var text = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return JsonDocument.Parse(text);
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-run-lifecycle", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteRepoMarker(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "Gmsd.slnx");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
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

