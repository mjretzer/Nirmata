using System.Diagnostics;
using System.Text;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosConfigCliTests
{
    [Fact]
    public async Task ConfigValidate_Succeeds_WhenConfigIsValid()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-config-validate-valid");
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

            var configDir = Path.Combine(tempWorkspaceRoot, ".aos", "config");
            Directory.CreateDirectory(configDir);

            var configPath = Path.Combine(configDir, "config.json");
            WriteUtf8NoBom(
                configPath,
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"secrets\": {\n" +
                "    \"OPENAI_API_KEY\": { \"kind\": \"env\", \"env\": \"OPENAI_API_KEY\" }\n" +
                "  }\n" +
                "}\n"
            );

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "config",
                "validate",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(0, exitCode);
            Assert.Contains("PASS .aos/config/config.json", stdout, StringComparison.Ordinal);
            Assert.Contains("OK", stdout, StringComparison.Ordinal);
            Assert.True(string.IsNullOrWhiteSpace(stderr), $"Expected no stderr on success. STDERR:{Environment.NewLine}{stderr}");
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task ConfigValidate_FailsWithExitCode2_WhenConfigIsMissing()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-config-validate-missing");
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

            // Ensure config.json is absent (init may or may not create it).
            var configPath = Path.Combine(tempWorkspaceRoot, ".aos", "config", "config.json");
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "config",
                "validate",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(stdout), $"Expected no stdout on missing config. STDOUT:{Environment.NewLine}{stdout}");
            Assert.Contains("Missing required config file: .aos/config/config.json", stderr, StringComparison.Ordinal);
            Assert.Contains("\"code\":\"aos.config.invalid\"", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task ConfigValidate_FailsWithExitCode2_WhenConfigContainsPlaintextSecret()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-config-validate-plaintext");
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

            var configDir = Path.Combine(tempWorkspaceRoot, ".aos", "config");
            Directory.CreateDirectory(configDir);

            var configPath = Path.Combine(configDir, "config.json");
            WriteUtf8NoBom(configPath, "{\"schemaVersion\":1,\"secrets\":{\"k\":\"PLAINTEXT\"}}\n");

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "config",
                "validate",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains(
                "FAIL .aos/config/config.json $.secrets.k - Plaintext secret values are not allowed.",
                stdout,
                StringComparison.Ordinal
            );
            Assert.Contains("Config validation failed:", stderr, StringComparison.Ordinal);
            Assert.Contains("\"code\":\"aos.config.invalid\"", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static void WriteUtf8NoBom(string path, string content)
        => File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

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

