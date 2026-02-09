using System.Diagnostics;
using System.Text;
using Gmsd.Aos.Engine.Evidence;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosExecutePlanDeterminismTests
{
    [Fact]
    public async Task AosExecutePlan_ProducesDeterministicOutputs_ForIdenticalPlanInputs()
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

            var planPath = Path.Combine(tempWorkspaceRoot, "plan.json");
            File.WriteAllText(
                planPath,
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"outputs\": [\n" +
                "    { \"relativePath\": \"b.txt\", \"contentsUtf8\": \"B\" },\n" +
                "    { \"relativePath\": \"a/alpha.txt\", \"contentsUtf8\": \"Alpha\\nLine2\\n\" },\n" +
                "    { \"relativePath\": \"a/beta.txt\", \"contentsUtf8\": \"Beta\" },\n" +
                "    { \"relativePath\": \"z.txt\", \"contentsUtf8\": \"Z\" }\n" +
                "  ]\n" +
                "}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var runIdA = await ExecutePlanAsync(aosDllPath, tempWorkspaceRoot, planPath);
            var runIdB = await ExecutePlanAsync(aosDllPath, tempWorkspaceRoot, planPath);

            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            var outputsRootA = Path.Combine(aosRoot, "evidence", "runs", runIdA, "outputs");
            var outputsRootB = Path.Combine(aosRoot, "evidence", "runs", runIdB, "outputs");

            AssertDirectoryTreeBytesEqual(outputsRootA, outputsRootB);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosExecutePlan_CreatesDeterministicActionsLog_ForIdenticalPlanInputs()
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

            var planPath = Path.Combine(tempWorkspaceRoot, "plan.json");
            File.WriteAllText(
                planPath,
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"outputs\": [\n" +
                "    { \"relativePath\": \"b.txt\", \"contentsUtf8\": \"B\" },\n" +
                "    { \"relativePath\": \"a/alpha.txt\", \"contentsUtf8\": \"Alpha\\nLine2\\n\" },\n" +
                "    { \"relativePath\": \"a/beta.txt\", \"contentsUtf8\": \"Beta\" },\n" +
                "    { \"relativePath\": \"z.txt\", \"contentsUtf8\": \"Z\" }\n" +
                "  ]\n" +
                "}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var runIdA = await ExecutePlanAsync(aosDllPath, tempWorkspaceRoot, planPath);
            var runIdB = await ExecutePlanAsync(aosDllPath, tempWorkspaceRoot, planPath);

            var actionsLogA = Path.Combine(tempWorkspaceRoot, ".aos", "evidence", "runs", runIdA, "logs", "execute-plan.actions.json");
            var actionsLogB = Path.Combine(tempWorkspaceRoot, ".aos", "evidence", "runs", runIdB, "logs", "execute-plan.actions.json");

            Assert.True(File.Exists(actionsLogA), $"Expected actions log at '{actionsLogA}'.");
            Assert.True(File.Exists(actionsLogB), $"Expected actions log at '{actionsLogB}'.");

            var bytesA = File.ReadAllBytes(actionsLogA);
            var bytesB = File.ReadAllBytes(actionsLogB);

            // Guardrail: actions log should not emit UTF-8 BOM.
            Assert.False(
                bytesA.Length >= 3 && bytesA[0] == 0xEF && bytesA[1] == 0xBB && bytesA[2] == 0xBF,
                "Expected no UTF-8 BOM in actions log."
            );

            // Guardrail: canonical JSON writer always emits a trailing LF.
            Assert.True(bytesA.Length > 0 && bytesA[^1] == (byte)'\n', "Expected actions log to end with LF.");

            // Determinism: identical plans should produce identical log contents (excluding run-id folder name).
            Assert.Equal(bytesA, bytesB);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static async Task<string> ExecutePlanAsync(string aosDllPath, string workspaceRoot, string planPath)
    {
        var (exitCode, stdout, stderr) = await RunDotNetAsync(
            workingDirectory: workspaceRoot,
            aosDllPath,
            "execute-plan",
            "--plan",
            planPath
        );

        Assert.True(
            exitCode == 0,
            $"Expected exit code 0, got {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}"
        );

        var runId = stdout.Trim();
        Assert.True(AosRunId.IsValid(runId), $"Expected a valid run id, got '{runId}'.");
        return runId;
    }

    private static void AssertDirectoryTreeBytesEqual(string rootA, string rootB)
    {
        Assert.True(Directory.Exists(rootA), $"Expected directory at '{rootA}'.");
        Assert.True(Directory.Exists(rootB), $"Expected directory at '{rootB}'.");

        var filesA = EnumerateFilesRelative(rootA);
        var filesB = EnumerateFilesRelative(rootB);
        Assert.Equal(filesA, filesB);

        foreach (var relPath in filesA)
        {
            var pathA = Path.Combine(rootA, relPath.Replace('/', Path.DirectorySeparatorChar));
            var pathB = Path.Combine(rootB, relPath.Replace('/', Path.DirectorySeparatorChar));

            var bytesA = File.ReadAllBytes(pathA);
            var bytesB = File.ReadAllBytes(pathB);
            Assert.Equal(bytesA, bytesB);

            // Guardrail: executor should not emit UTF-8 BOM.
            Assert.False(
                bytesA.Length >= 3 && bytesA[0] == 0xEF && bytesA[1] == 0xBB && bytesA[2] == 0xBF,
                $"Expected no UTF-8 BOM in '{relPath}'."
            );
        }
    }

    private static SortedSet<string> EnumerateFilesRelative(string root)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file);
            rel = rel.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            set.Add(rel);
        }

        return set;
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-execute-plan-determinism", Guid.NewGuid().ToString("N"));
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

