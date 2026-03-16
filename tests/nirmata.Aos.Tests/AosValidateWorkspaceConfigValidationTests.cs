using System.Diagnostics;
using System.Text;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosValidateWorkspaceConfigValidationTests
{
    [Fact]
    public async Task AosValidateWorkspace_FailsWhenConfigExistsButIsInvalid()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-config-invalid");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var configDir = Path.Combine(tempWorkspaceRoot, ".aos", "config");
            Directory.CreateDirectory(configDir);

            var configPath = Path.Combine(configDir, "config.json");
            File.WriteAllText(
                configPath,
                """{"schemaVersion":1,"secrets":{"k":"PLAINTEXT"}}""",
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
                "FAIL [config] .aos/config/config.json - (nirmata:aos:schema:config:v1 @ /secrets/k)",
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

