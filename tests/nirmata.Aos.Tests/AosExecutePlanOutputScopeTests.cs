using System.Diagnostics;
using System.Text;
using nirmata.Aos.Engine.Evidence;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosExecutePlanOutputScopeTests
{
    [Fact]
    public async Task AosExecutePlan_WritesPlanOutputsOnlyUnderRunOutputsFolder()
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
                // Minimal plan v1 with a single output.
                "{\n  \"schemaVersion\": 1,\n  \"outputs\": [\n    {\n      \"relativePath\": \"hello.txt\",\n      \"contentsUtf8\": \"hello\"\n    }\n  ]\n}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var (execExit, execOut, execErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "execute-plan",
                "--plan",
                planPath
            );

            Assert.True(
                execExit == 0,
                $"Expected exit code 0, got {execExit}.{Environment.NewLine}STDOUT:{Environment.NewLine}{execOut}{Environment.NewLine}STDERR:{Environment.NewLine}{execErr}"
            );

            var runId = execOut.Trim();
            Assert.True(AosRunId.IsValid(runId), $"Expected a valid run id, got '{runId}'.");

            var aosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            var runRoot = Path.Combine(aosRoot, "evidence", "runs", runId);
            var outputsRoot = Path.Combine(runRoot, "outputs");
            var expectedOutputPath = Path.Combine(outputsRoot, "hello.txt");
            Assert.True(File.Exists(expectedOutputPath), $"Expected output file at '{expectedOutputPath}'.");

            // Prove the plan output was NOT written anywhere else in the workspace root.
            // (execute-plan may write evidence under .aos/, but plan outputs must only land under run outputs.)
            var unexpectedAtWorkspaceRoot = Path.Combine(tempWorkspaceRoot, "hello.txt");
            Assert.False(File.Exists(unexpectedAtWorkspaceRoot), $"Did not expect output file at '{unexpectedAtWorkspaceRoot}'.");

            var unexpectedAtAosRoot = Path.Combine(aosRoot, "hello.txt");
            Assert.False(File.Exists(unexpectedAtAosRoot), $"Did not expect output file at '{unexpectedAtAosRoot}'.");

            // No additional top-level entries should be created besides .aos and the plan file.
            var topLevel = Directory.EnumerateFileSystemEntries(tempWorkspaceRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(new[] { ".aos", "nirmata.slnx", "plan.json" }, topLevel);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Theory]
    [InlineData("../x.txt")]
    [InlineData("a/../x.txt")]
    [InlineData(@"..\x.txt")]
    [InlineData(@"a\..\x.txt")]
    public async Task AosExecutePlan_RejectsTraversalPaths_WithActionableError(string relativePath)
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
            var planJson =
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"outputs\": [\n" +
                "    {\n" +
                $"      \"relativePath\": \"{relativePath.Replace("\\", "\\\\")}\",\n" +
                "      \"contentsUtf8\": \"hello\"\n" +
                "    }\n" +
                "  ]\n" +
                "}\n";

            File.WriteAllText(planPath, planJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var (execExit, execOut, execErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "execute-plan",
                "--plan",
                planPath
            );

            Assert.True(
                execExit == 2,
                $"Expected exit code 2 for traversal path rejection, got {execExit}.{Environment.NewLine}STDOUT:{Environment.NewLine}{execOut}{Environment.NewLine}STDERR:{Environment.NewLine}{execErr}"
            );

            Assert.True(
                string.IsNullOrWhiteSpace(execOut),
                $"Expected no stdout on failure.{Environment.NewLine}STDOUT:{Environment.NewLine}{execOut}{Environment.NewLine}STDERR:{Environment.NewLine}{execErr}"
            );

            Assert.Contains("Plan outputs[0].relativePath is not allowed:", execErr, StringComparison.Ordinal);
            Assert.Contains("path traversal ('..') is not allowed.", execErr, StringComparison.Ordinal);
            Assert.Contains($"Value: '{relativePath}'.", execErr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "nirmata-aos-execute-plan", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteRepoMarker(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "nirmata.slnx");
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

