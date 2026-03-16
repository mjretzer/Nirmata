using System.Diagnostics;
using System.Text;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosValidateWorkspaceSchemaViolationTests
{
    [Fact]
    public async Task AosValidateWorkspace_FailsDeterministicallyOnSchemaViolation()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-schema-violation");
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
                $"Expected exit code 0 from init, got {initExit}.{Environment.NewLine}STDOUT:{Environment.NewLine}{initOut}{Environment.NewLine}STDERR:{Environment.NewLine}{initErr}"
            );

            // Produce a schema violation (valid JSON, wrong schemaVersion).
            var projectJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "spec", "project.json");
            Assert.True(File.Exists(projectJsonPath), $"Expected file not found at '{projectJsonPath}'.");
            File.WriteAllText(
                projectJsonPath,
                """
                {"project":{"description":"","name":""},"schemaVersion":2}
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
                "FAIL [spec] .aos/spec/project.json - (nirmata:aos:schema:project:v1 @ /schemaVersion)",
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

