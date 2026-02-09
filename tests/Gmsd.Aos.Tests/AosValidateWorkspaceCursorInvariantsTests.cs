using System.Diagnostics;
using System.Text;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosValidateWorkspaceCursorInvariantsTests
{
    [Fact]
    public async Task AosValidateWorkspace_FailsWhenCursorKindIsPresentButCursorIdIsMissing()
    {
        var tempWorkspaceRoot = CreateTempDirectory("gmsd-aos-validate-workspace-cursor-kind-without-id");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var stateJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "state.json");
            Assert.True(File.Exists(stateJsonPath), $"Expected file not found at '{stateJsonPath}'.");
            File.WriteAllText(
                stateJsonPath,
                """
                {
                  "cursor": {
                    "kind": "milestone"
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
                "FAIL [state] .aos/state/state.json - Cursor reference is malformed: cursor.kind and cursor.id must either both be present or both be absent.",
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
    public async Task AosValidateWorkspace_FailsWhenCursorKindIsUnrecognized()
    {
        var tempWorkspaceRoot = CreateTempDirectory("gmsd-aos-validate-workspace-cursor-unrecognized-kind");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var stateJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "state.json");
            Assert.True(File.Exists(stateJsonPath), $"Expected file not found at '{stateJsonPath}'.");
            File.WriteAllText(
                stateJsonPath,
                """
                {
                  "cursor": {
                    "id": "MS-0001",
                    "kind": "nope"
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
                "FAIL [state] .aos/state/state.json - Unrecognized cursor kind 'nope'. Expected one of: milestone, phase, task, issue, uat, run.",
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
    public async Task AosValidateWorkspace_FailsWhenCursorKindDoesNotMatchCursorIdKind()
    {
        var tempWorkspaceRoot = CreateTempDirectory("gmsd-aos-validate-workspace-cursor-kind-id-mismatch");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            var stateJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "state.json");
            Assert.True(File.Exists(stateJsonPath), $"Expected file not found at '{stateJsonPath}'.");
            File.WriteAllText(
                stateJsonPath,
                """
                {
                  "cursor": {
                    "id": "MS-0001",
                    "kind": "phase"
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
                "FAIL [state] .aos/state/state.json - Cursor id 'MS-0001' is kind 'Milestone', but cursor.kind is 'Phase'.",
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
    public async Task AosValidateWorkspace_FailsDeterministicallyWhenCursorReferencesMissingArtifact()
    {
        var tempWorkspaceRoot = CreateTempDirectory("gmsd-aos-validate-workspace-cursor-missing-artifact");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            // Valid cursor JSON, but references a non-existent artifact.
            var stateJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "state.json");
            Assert.True(File.Exists(stateJsonPath), $"Expected file not found at '{stateJsonPath}'.");
            File.WriteAllText(
                stateJsonPath,
                """
                {
                  "cursor": {
                    "id": "MS-0001",
                    "kind": "milestone"
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
                "FAIL [state] .aos/state/state.json - Cursor references missing artifact at '.aos/spec/milestones/MS-0001/milestone.json'.",
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
    public async Task AosValidateWorkspace_FailsDeterministicallyWhenCursorReferencesArtifactMissingFromCatalogIndex()
    {
        var tempWorkspaceRoot = CreateTempDirectory("gmsd-aos-validate-workspace-cursor-missing-from-catalog-index");
        try
        {
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            await InitWorkspaceAsync(aosDllPath, tempWorkspaceRoot);

            // Create the referenced artifact on disk...
            var milestoneJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "spec", "milestones", "MS-0001", "milestone.json");
            Directory.CreateDirectory(Path.GetDirectoryName(milestoneJsonPath)!);
            File.WriteAllText(milestoneJsonPath, """{"schemaVersion":1}""", Encoding.UTF8);
            Assert.True(File.Exists(milestoneJsonPath), $"Expected file not found at '{milestoneJsonPath}'.");

            // ...but keep it absent from the catalog index (index exists and is valid JSON/schema).
            var milestonesIndexJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "spec", "milestones", "index.json");
            Assert.True(File.Exists(milestonesIndexJsonPath), $"Expected file not found at '{milestonesIndexJsonPath}'.");
            File.WriteAllText(
                milestonesIndexJsonPath,
                """
                {
                  "items": [],
                  "schemaVersion": 1
                }
                """,
                Encoding.UTF8
            );

            // Valid cursor JSON, references an existing artifact, but the id is missing from the catalog index.
            var stateJsonPath = Path.Combine(tempWorkspaceRoot, ".aos", "state", "state.json");
            Assert.True(File.Exists(stateJsonPath), $"Expected file not found at '{stateJsonPath}'.");
            File.WriteAllText(
                stateJsonPath,
                """
                {
                  "cursor": {
                    "id": "MS-0001",
                    "kind": "milestone"
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
                "FAIL [state] .aos/state/state.json - Cursor id 'MS-0001' is not present in catalog index '.aos/spec/milestones/index.json'.",
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

