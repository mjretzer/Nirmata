using System.Diagnostics;
using System.Text;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosValidateWorkspaceRoadmapMissingArtifactTests
{
    [Fact]
    public async Task AosValidateWorkspace_FailsDeterministicallyWhenRoadmapReferencesMissingArtifact()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-roadmap-missing-artifact");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            // Valid roadmap JSON, but references a non-existent artifact.
            var roadmapJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "spec", "roadmap.json");
            Assert.True(File.Exists(roadmapJsonPath), $"Expected file not found at '{roadmapJsonPath}'.");
            File.WriteAllText(
                roadmapJsonPath,
                """
                {
                  "roadmap": {
                    "items": [
                      {
                        "id": "MS-0001",
                        "kind": "milestone",
                        "title": "Missing milestone"
                      }
                    ],
                    "title": ""
                  },
                  "schemaVersion": 1
                }
                """,
                Encoding.UTF8
            );

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains(
                "FAIL [spec] .aos/spec/roadmap.json - Roadmap item references missing artifact at '.aos/spec/milestones/MS-0001/milestone.json'.",
                stdout,
                StringComparison.Ordinal
            );
            Assert.Contains("Validation failed:", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static async Task InitWorkspaceAsync(string aosDllPath, string workspaceRoot)
    {
        var (exitCode, stdout, stderr) = await RunDotNetAsync(
            workingDirectory: null,
            aosDllPath,
            "init",
            "--root",
            workspaceRoot
        );

        Assert.True(
            exitCode == 0,
            $"Expected exit code 0 from init, got {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}"
        );
    }

    private static string CreateTempDirectory(string prefix)
    {
        var root = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
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

