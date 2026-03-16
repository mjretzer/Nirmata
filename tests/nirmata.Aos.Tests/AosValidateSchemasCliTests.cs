using System.Diagnostics;
using System.Text;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosValidateSchemasCliTests
{
    [Fact]
    public async Task AosValidateSchemas_AfterInit_SucceedsAgainstLocalPack()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (initExit, _, initErr) = await RunDotNetAsync(
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(initExit == 0, $"Expected exit code 0 from init, got {initExit}.{Environment.NewLine}STDERR:{Environment.NewLine}{initErr}");

            var (validateExit, validateOut, validateErr) = await RunDotNetAsync(
                aosDllPath,
                "validate",
                "schemas",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(
                validateExit == 0,
                $"Expected exit code 0 from validate schemas, got {validateExit}.{Environment.NewLine}STDOUT:{Environment.NewLine}{validateOut}{Environment.NewLine}STDERR:{Environment.NewLine}{validateErr}"
            );

            Assert.Contains("Local schemas discovered:", validateOut, StringComparison.Ordinal);
            Assert.Contains("OK", validateOut, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateSchemas_MissingRegistry_FailsWithActionableError()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                aosDllPath,
                "validate",
                "schemas",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains("Schema validation failed.", stderr, StringComparison.Ordinal);
            Assert.Contains("Run 'aos init'", stderr, StringComparison.Ordinal);

            // Command should be quiet on STDOUT when failing early.
            Assert.True(string.IsNullOrWhiteSpace(stdout));
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateSchemas_EmptyRegistrySchemas_Fails()
    {
        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            // Create a minimal, but invalid (empty inventory) local pack.
            var schemasRoot = Path.Combine(tempWorkspaceRoot, ".aos", "schemas");
            Directory.CreateDirectory(schemasRoot);
            File.WriteAllText(
                Path.Combine(schemasRoot, "registry.json"),
                "{\n  \"schemaVersion\": 1,\n  \"schemas\": []\n}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var (exitCode, _, stderr) = await RunDotNetAsync(
                aosDllPath,
                "validate",
                "schemas",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains("Schema validation failed.", stderr, StringComparison.Ordinal);
            Assert.Contains("must contain at least one schema", stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "nirmata-aos-validate-schemas", Guid.NewGuid().ToString("N"));
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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotNetAsync(string dllPath, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

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

