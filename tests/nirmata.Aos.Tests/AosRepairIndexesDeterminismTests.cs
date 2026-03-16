using System.Diagnostics;
using System.Text;
using System.Text.Json;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosRepairIndexesDeterminismTests
{
    [Fact]
    public async Task AosRepairIndexes_ProducesDeterministicIndexBytes_ForSameWorkspaceState()
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

            // Seed some contract-shaped artifacts out of order to ensure repair writes sorted indexes deterministically.
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "milestones", artifactId: "MS-0002", fileName: "milestone.json");
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "milestones", artifactId: "MS-0001", fileName: "milestone.json");
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "phases", artifactId: "PH-0002", fileName: "phase.json");
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "phases", artifactId: "PH-0001", fileName: "phase.json");
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "tasks", artifactId: "TSK-000002", fileName: "task.json");
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "tasks", artifactId: "TSK-000001", fileName: "task.json");
            SeedFlatSpecArtifact(tempWorkspaceRoot, folderName: "issues", artifactId: "ISS-0002");
            SeedFlatSpecArtifact(tempWorkspaceRoot, folderName: "issues", artifactId: "ISS-0001");
            SeedFlatSpecArtifact(tempWorkspaceRoot, folderName: "uat", artifactId: "UAT-0002");
            SeedFlatSpecArtifact(tempWorkspaceRoot, folderName: "uat", artifactId: "UAT-0001");

            SeedRun(
                tempWorkspaceRoot,
                runId: "00000000000000000000000000000002",
                status: "finished",
                startedAtUtc: "2020-01-01T00:00:01.0000000Z",
                finishedAtUtc: "2020-01-01T00:00:02.0000000Z"
            );
            SeedRun(
                tempWorkspaceRoot,
                runId: "00000000000000000000000000000001",
                status: "started",
                startedAtUtc: "2020-01-01T00:00:00.0000000Z",
                finishedAtUtc: null
            );

            await RunRepairIndexesAsync(aosDllPath, tempWorkspaceRoot);
            var bytesAfterFirst = ReadIndexBytes(tempWorkspaceRoot);

            await RunRepairIndexesAsync(aosDllPath, tempWorkspaceRoot);
            var bytesAfterSecond = ReadIndexBytes(tempWorkspaceRoot);

            Assert.Equal(bytesAfterFirst.Keys, bytesAfterSecond.Keys);
            foreach (var key in bytesAfterFirst.Keys)
            {
                Assert.Equal(bytesAfterFirst[key], bytesAfterSecond[key]);
            }
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosRepairIndexes_RebuildsDeletedIndexes_Deterministically()
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

            // Seed some contract-shaped artifacts (out of order) and runs so repair has work to do.
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "milestones", artifactId: "MS-0002", fileName: "milestone.json");
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "milestones", artifactId: "MS-0001", fileName: "milestone.json");
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "phases", artifactId: "PH-0002", fileName: "phase.json");
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "phases", artifactId: "PH-0001", fileName: "phase.json");
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "tasks", artifactId: "TSK-000002", fileName: "task.json");
            SeedSpecArtifact(tempWorkspaceRoot, folderName: "tasks", artifactId: "TSK-000001", fileName: "task.json");
            SeedFlatSpecArtifact(tempWorkspaceRoot, folderName: "issues", artifactId: "ISS-0002");
            SeedFlatSpecArtifact(tempWorkspaceRoot, folderName: "issues", artifactId: "ISS-0001");
            SeedFlatSpecArtifact(tempWorkspaceRoot, folderName: "uat", artifactId: "UAT-0002");
            SeedFlatSpecArtifact(tempWorkspaceRoot, folderName: "uat", artifactId: "UAT-0001");

            SeedRun(
                tempWorkspaceRoot,
                runId: "00000000000000000000000000000002",
                status: "finished",
                startedAtUtc: "2020-01-01T00:00:01.0000000Z",
                finishedAtUtc: "2020-01-01T00:00:02.0000000Z"
            );
            SeedRun(
                tempWorkspaceRoot,
                runId: "00000000000000000000000000000001",
                status: "started",
                startedAtUtc: "2020-01-01T00:00:00.0000000Z",
                finishedAtUtc: null
            );

            await RunRepairIndexesAsync(aosDllPath, tempWorkspaceRoot);
            var bytesAfterFirst = ReadIndexBytes(tempWorkspaceRoot);

            // Delete indexes (all spec catalog indexes + runs) and ensure repair recreates them deterministically.
            foreach (var contractPath in bytesAfterFirst.Keys)
            {
                DeleteIndexFile(tempWorkspaceRoot, contractPath);
            }

            await RunRepairIndexesAsync(aosDllPath, tempWorkspaceRoot);
            var bytesAfterRebuild = ReadIndexBytes(tempWorkspaceRoot);

            Assert.Equal(bytesAfterFirst.Keys, bytesAfterRebuild.Keys);
            foreach (var key in bytesAfterFirst.Keys)
            {
                Assert.Equal(bytesAfterFirst[key], bytesAfterRebuild[key]);
            }

            // Re-run to confirm idempotent determinism.
            await RunRepairIndexesAsync(aosDllPath, tempWorkspaceRoot);
            var bytesAfterSecond = ReadIndexBytes(tempWorkspaceRoot);

            Assert.Equal(bytesAfterFirst.Keys, bytesAfterSecond.Keys);
            foreach (var key in bytesAfterFirst.Keys)
            {
                Assert.Equal(bytesAfterFirst[key], bytesAfterSecond[key]);
            }
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static async Task RunRepairIndexesAsync(string aosDllPath, string workspaceRoot)
    {
        var (exitCode, stdout, stderr) = await RunDotNetAsync(
            workingDirectory: null,
            aosDllPath,
            "repair",
            "indexes",
            "--root",
            workspaceRoot
        );

        Assert.True(
            exitCode == 0,
            $"Expected exit code 0, got {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}"
        );
    }

    private static SortedDictionary<string, byte[]> ReadIndexBytes(string workspaceRoot)
    {
        var aosRoot = Path.Combine(workspaceRoot, ".aos");
        var paths = new[]
        {
            ".aos/spec/milestones/index.json",
            ".aos/spec/phases/index.json",
            ".aos/spec/tasks/index.json",
            ".aos/spec/issues/index.json",
            ".aos/spec/uat/index.json",
            ".aos/evidence/runs/index.json"
        };

        var dict = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var contractPath in paths)
        {
            var fullPath = Path.Combine(aosRoot, contractPath[".aos/".Length..].Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), $"Expected index file at '{contractPath}'.");

            var bytes = File.ReadAllBytes(fullPath);

            // Guardrails: no UTF-8 BOM and canonical trailing LF for JSON.
            Assert.False(
                bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                $"Expected no UTF-8 BOM in '{contractPath}'."
            );
            Assert.True(bytes.Length > 0 && bytes[^1] == (byte)'\n', $"Expected '{contractPath}' to end with LF.");

            dict[contractPath] = bytes;
        }

        // Spot-check sorted ordering in a representative index.
        var milestoneIndexPath = Path.Combine(aosRoot, "spec", "milestones", "index.json");
        var milestoneIndex = JsonDocument.Parse(File.ReadAllText(milestoneIndexPath, new UTF8Encoding(false)));
        var items = milestoneIndex.RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
        var sorted = items.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(sorted, items);

        return dict;
    }

    private static void DeleteIndexFile(string workspaceRoot, string contractPath)
    {
        var fullPath = Path.Combine(
            workspaceRoot,
            ".aos",
            contractPath[".aos/".Length..].Replace('/', Path.DirectorySeparatorChar)
        );

        Assert.True(File.Exists(fullPath), $"Expected file to delete at '{contractPath}'.");
        File.Delete(fullPath);
    }

    private static void SeedSpecArtifact(string workspaceRoot, string folderName, string artifactId, string fileName)
    {
        var fullDir = Path.Combine(workspaceRoot, ".aos", "spec", folderName, artifactId);
        Directory.CreateDirectory(fullDir);

        var fullPath = Path.Combine(fullDir, fileName);
        if (!File.Exists(fullPath))
        {
            File.WriteAllText(fullPath, "{\n}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static void SeedFlatSpecArtifact(string workspaceRoot, string folderName, string artifactId)
    {
        var fullDir = Path.Combine(workspaceRoot, ".aos", "spec", folderName);
        Directory.CreateDirectory(fullDir);

        var fullPath = Path.Combine(fullDir, artifactId + ".json");
        if (!File.Exists(fullPath))
        {
            File.WriteAllText(fullPath, "{\n}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static void SeedRun(
        string workspaceRoot,
        string runId,
        string status,
        string startedAtUtc,
        string? finishedAtUtc)
    {
        var runDir = Path.Combine(workspaceRoot, ".aos", "evidence", "runs", runId);
        Directory.CreateDirectory(runDir);

        var runJsonPath = Path.Combine(runDir, "run.json");

        // Keep it minimal but valid for index repair (schemaVersion=1, runId matches folder, supported status, timestamps parseable).
        var json =
            "{\n" +
            $"  \"finishedAtUtc\": {(finishedAtUtc is null ? "null" : $"\"{finishedAtUtc}\"")},\n" +
            $"  \"runId\": \"{runId}\",\n" +
            $"  \"schemaVersion\": 1,\n" +
            $"  \"startedAtUtc\": \"{startedAtUtc}\",\n" +
            $"  \"status\": \"{status}\"\n" +
            "}\n";

        File.WriteAllText(runJsonPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "nirmata-aos-repair-indexes-determinism", Guid.NewGuid().ToString("N"));
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

