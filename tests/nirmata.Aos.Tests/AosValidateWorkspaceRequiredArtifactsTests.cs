using System.Diagnostics;
using System.Text;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests;

public sealed class AosValidateWorkspaceRequiredArtifactsTests
{
    [Theory]
    [InlineData("spec", "issues", "index.json", "FAIL [spec] .aos/spec/issues/index.json - Missing required file.")]
    [InlineData("spec", "uat", "index.json", "FAIL [spec] .aos/spec/uat/index.json - Missing required file.")]
    public async Task AosValidateWorkspace_FailsWhenRequiredSpecIndexIsMissing(
        string layerDir,
        string subDir,
        string fileName,
        string expectedFailureLine)
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-required-spec-index-missing");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var filePath = Path.Combine(tempWorkspaceRoot, ".aos", layerDir, subDir, fileName);
            Assert.True(File.Exists(filePath), $"Expected file not found at '{filePath}'.");
            File.Delete(filePath);

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains(expectedFailureLine, stdout, StringComparison.Ordinal);
            Assert.Contains("Validation failed:", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Theory]
    [InlineData("spec", "issues", "index.json", "FAIL [spec] .aos/spec/issues/index.json - Invalid JSON.")]
    [InlineData("spec", "uat", "index.json", "FAIL [spec] .aos/spec/uat/index.json - Invalid JSON.")]
    public async Task AosValidateWorkspace_FailsWhenRequiredSpecIndexIsMalformed(
        string layerDir,
        string subDir,
        string fileName,
        string expectedFailureLine)
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-required-spec-index-malformed");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var filePath = Path.Combine(tempWorkspaceRoot, ".aos", layerDir, subDir, fileName);
            Assert.True(File.Exists(filePath), $"Expected file not found at '{filePath}'.");
            File.WriteAllText(filePath, "{", Encoding.UTF8);

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains(expectedFailureLine, stdout, StringComparison.Ordinal);
            Assert.Contains("Validation failed:", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenStateJsonIsMissing()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-state-missing");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var stateJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "state.json");
            Assert.True(File.Exists(stateJsonPath), $"Expected file not found at '{stateJsonPath}'.");
            File.Delete(stateJsonPath);

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains("FAIL [state] .aos/state/state.json - Missing required file.", stdout, StringComparison.Ordinal);
            Assert.Contains("Validation failed:", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenStateJsonIsMalformed()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-state-malformed");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var stateJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "state.json");
            Assert.True(File.Exists(stateJsonPath), $"Expected file not found at '{stateJsonPath}'.");
            File.WriteAllText(stateJsonPath, "{", Encoding.UTF8);

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains("FAIL [state] .aos/state/state.json - Invalid JSON.", stdout, StringComparison.Ordinal);
            Assert.Contains("Validation failed:", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenEventsNdjsonIsMissing()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-events-missing");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var eventsPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "events.ndjson");
            Assert.True(File.Exists(eventsPath), $"Expected file not found at '{eventsPath}'.");
            File.Delete(eventsPath);

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains("FAIL [state] .aos/state/events.ndjson - Missing required file.", stdout, StringComparison.Ordinal);
            Assert.Contains("Validation failed:", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenEventsNdjsonHasInvalidNonEmptyLine()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-events-malformed");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var eventsPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "events.ndjson");
            Assert.True(File.Exists(eventsPath), $"Expected file not found at '{eventsPath}'.");
            File.WriteAllText(eventsPath, "{", Encoding.UTF8);

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
                "FAIL [state] .aos/state/events.ndjson - Invalid NDJSON: non-empty line 1 is not valid JSON.",
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

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenEventsNdjsonHasNonObjectJsonLine()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-events-non-object");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var eventsPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "events.ndjson");
            Assert.True(File.Exists(eventsPath), $"Expected file not found at '{eventsPath}'.");

            // Valid JSON but not a JSON object.
            File.WriteAllText(eventsPath, "[]\n", Encoding.UTF8);

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
                "FAIL [state] .aos/state/events.ndjson - Invalid NDJSON: non-empty line 1 is not a JSON object.",
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

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenEventsNdjsonLineViolatesEventSchema()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-events-schema-violation");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var eventsPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "events.ndjson");
            Assert.True(File.Exists(eventsPath), $"Expected file not found at '{eventsPath}'.");

            // Valid JSON object, but schemaVersion must be 1 per event schema.
            File.WriteAllText(eventsPath, """{"schemaVersion":2}""" + "\n", Encoding.UTF8);

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
                "FAIL [state] .aos/state/events.ndjson - (nirmata:aos:schema:event:v1 @ /lines/1/schemaVersion)",
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

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenCommandsLogIsMissing()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-commands-missing");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var commandsPath = Path.Combine(tempWorkspaceRoot, ".aos", "evidence", "logs", "commands.json");
            Assert.True(File.Exists(commandsPath), $"Expected file not found at '{commandsPath}'.");
            File.Delete(commandsPath);

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains("FAIL [evidence] .aos/evidence/logs/commands.json - Missing required file.", stdout, StringComparison.Ordinal);
            Assert.Contains("Validation failed:", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenCommandsLogIsMalformed()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-commands-malformed");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var commandsPath = Path.Combine(tempWorkspaceRoot, ".aos", "evidence", "logs", "commands.json");
            Assert.True(File.Exists(commandsPath), $"Expected file not found at '{commandsPath}'.");
            File.WriteAllText(commandsPath, "{", Encoding.UTF8);

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                workingDirectory: null,
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.Equal(2, exitCode);
            Assert.Contains("FAIL [evidence] .aos/evidence/logs/commands.json - Invalid JSON.", stdout, StringComparison.Ordinal);
            Assert.Contains("Validation failed:", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenFinishedRunManifestIsMissing()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-run-manifest-missing");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var runId = await StartRunAsync(aosDllPath, tempWorkspaceRoot);
            await FinishRunAsync(aosDllPath, tempWorkspaceRoot, runId);

            var manifestPath = Path.Combine(tempWorkspaceRoot, ".aos", "evidence", "runs", runId, "artifacts", "manifest.json");
            Assert.True(File.Exists(manifestPath), $"Expected file not found at '{manifestPath}'.");
            File.Delete(manifestPath);

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
                $"FAIL [evidence] .aos/evidence/runs/{runId}/artifacts/manifest.json - Missing required file.",
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

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenFinishedRunManifestIsMalformed()
    {
        var tempWorkspaceRoot = CreateTempDirectory("nirmata-aos-validate-workspace-run-manifest-malformed");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var runId = await StartRunAsync(aosDllPath, tempWorkspaceRoot);
            await FinishRunAsync(aosDllPath, tempWorkspaceRoot, runId);

            var manifestPath = Path.Combine(tempWorkspaceRoot, ".aos", "evidence", "runs", runId, "artifacts", "manifest.json");
            Assert.True(File.Exists(manifestPath), $"Expected file not found at '{manifestPath}'.");
            File.WriteAllText(manifestPath, "{", Encoding.UTF8);

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
                $"FAIL [evidence] .aos/evidence/runs/{runId}/artifacts/manifest.json - Invalid JSON.",
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

    private static async Task<string> StartRunAsync(string aosDllPath, string workspaceRoot)
    {
        var (exitCode, stdout, stderr) = await RunDotNetAsync(
            workingDirectory: workspaceRoot,
            aosDllPath,
            "run",
            "start"
        );

        Assert.True(
            exitCode == 0,
            $"Expected exit code 0 from run start, got {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}"
        );

        var runId = stdout.Trim();
        Assert.False(string.IsNullOrWhiteSpace(runId), "Expected run start to print a run id.");
        return runId;
    }

    private static async Task FinishRunAsync(string aosDllPath, string workspaceRoot, string runId)
    {
        var (exitCode, stdout, stderr) = await RunDotNetAsync(
            workingDirectory: workspaceRoot,
            aosDllPath,
            "run",
            "finish",
            "--run-id",
            runId
        );

        Assert.True(
            exitCode == 0,
            $"Expected exit code 0 from run finish, got {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}"
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

