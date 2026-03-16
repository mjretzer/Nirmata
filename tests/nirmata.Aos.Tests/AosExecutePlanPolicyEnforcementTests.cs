using System.Diagnostics;
using System.Text;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosExecutePlanPolicyEnforcementTests
{
    [Fact]
    public async Task AosExecutePlan_FailsWithExitCode3_WhenPolicyIsMissing()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-execute-plan-policy-missing");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var policyPath = Path.Combine(tempWorkspaceRoot, ".aos", "config", "policy.json");
            Assert.True(File.Exists(policyPath), $"Expected policy file at '{policyPath}'.");
            File.Delete(policyPath);

            var planPath = Path.Combine(tempWorkspaceRoot, "plan.json");
            File.WriteAllText(
                planPath,
                "{\n  \"schemaVersion\": 1,\n  \"outputs\": [\n    {\n      \"relativePath\": \"hello.txt\",\n      \"contentsUtf8\": \"hello\"\n    }\n  ]\n}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "execute-plan",
                "--plan",
                planPath
            );

            Assert.Equal(3, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(stdout), $"Expected no stdout on policy failure. STDOUT:{Environment.NewLine}{stdout}");
            Assert.Contains("Missing required policy file:", stderr, StringComparison.Ordinal);
            Assert.Contains("\"code\":\"aos.policy.violation\"", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosExecutePlan_FailsWithExitCode3_WhenWriteScopeDoesNotAllowAos()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-execute-plan-policy-scope");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var policyPath = Path.Combine(tempWorkspaceRoot, ".aos", "config", "policy.json");
            Assert.True(File.Exists(policyPath), $"Expected policy file at '{policyPath}'.");

            // Deny all .aos/ writes by only allowing an unrelated folder.
            File.WriteAllText(
                policyPath,
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"scopeAllowlist\": { \"write\": [\"tmp/\"] },\n" +
                "  \"toolAllowlist\": { \"tools\": [], \"providers\": [] },\n" +
                "  \"noImplicitState\": true\n" +
                "}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var planPath = Path.Combine(tempWorkspaceRoot, "plan.json");
            File.WriteAllText(
                planPath,
                "{\n  \"schemaVersion\": 1,\n  \"outputs\": [\n    {\n      \"relativePath\": \"hello.txt\",\n      \"contentsUtf8\": \"hello\"\n    }\n  ]\n}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "execute-plan",
                "--plan",
                planPath
            );

            Assert.Equal(3, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(stdout), $"Expected no stdout on policy failure. STDOUT:{Environment.NewLine}{stdout}");
            Assert.Contains("Policy forbids writing", stderr, StringComparison.Ordinal);
            Assert.Contains("Allowed write scopes:", stderr, StringComparison.Ordinal);
            Assert.Contains("\"code\":\"aos.policy.violation\"", stderr, StringComparison.Ordinal);
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

        WriteRepoMarker(workspaceRoot);
    }

    private static void WriteRepoMarker(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "nirmata.slnx");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

