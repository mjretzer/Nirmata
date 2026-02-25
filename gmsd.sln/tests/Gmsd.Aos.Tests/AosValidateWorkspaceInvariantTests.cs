using System.Diagnostics;
using System.Text;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosValidateWorkspaceInvariantTests
{
    [Fact]
    public async Task AosValidateWorkspace_FailsWhenProjectJsonIsMissing()
    {
        var approvedAosRoot = Path.Combine(
            FindRepositoryRootFrom(AppContext.BaseDirectory),
            "tests",
            "Gmsd.Aos.Tests",
            "Fixtures",
            "Approved",
            ".aos"
        );

        Assert.True(Directory.Exists(approvedAosRoot), $"Approved fixture not found at '{approvedAosRoot}'.");

        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var tempAosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            CopyDirectory(approvedAosRoot, tempAosRoot);

            var projectJsonPath = Path.Combine(tempAosRoot, "spec", "project.json");
            Assert.True(File.Exists(projectJsonPath), $"Fixture file not found at '{projectJsonPath}'.");
            File.Delete(projectJsonPath);

            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(
                exitCode == 2,
                $"Expected exit code 2, got {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}"
            );

            Assert.Contains(
                "FAIL [spec] .aos/spec/project.json - Missing required artifact for single-project workspace.",
                stdout,
                StringComparison.Ordinal
            );
            Assert.Contains("Validation failed: 1 issue(s).", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenProjectsJsonExists()
    {
        var approvedAosRoot = Path.Combine(
            FindRepositoryRootFrom(AppContext.BaseDirectory),
            "tests",
            "Gmsd.Aos.Tests",
            "Fixtures",
            "Approved",
            ".aos"
        );

        Assert.True(Directory.Exists(approvedAosRoot), $"Approved fixture not found at '{approvedAosRoot}'.");

        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var tempAosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            CopyDirectory(approvedAosRoot, tempAosRoot);

            var projectsJsonPath = Path.Combine(tempAosRoot, "spec", "projects.json");
            Directory.CreateDirectory(Path.GetDirectoryName(projectsJsonPath)!);
            File.WriteAllText(projectsJsonPath, "{}", Encoding.UTF8);

            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(
                exitCode == 2,
                $"Expected exit code 2, got {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}"
            );

            Assert.Contains(
                "FAIL [spec] .aos/spec/projects.json - Forbidden multi-project artifact exists; only .aos/spec/project.json is permitted.",
                stdout,
                StringComparison.Ordinal
            );
            Assert.Contains("Validation failed: 1 issue(s).", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenActiveProjectJsonExists()
    {
        var approvedAosRoot = Path.Combine(
            FindRepositoryRootFrom(AppContext.BaseDirectory),
            "tests",
            "Gmsd.Aos.Tests",
            "Fixtures",
            "Approved",
            ".aos"
        );

        Assert.True(Directory.Exists(approvedAosRoot), $"Approved fixture not found at '{approvedAosRoot}'.");

        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var tempAosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            CopyDirectory(approvedAosRoot, tempAosRoot);

            var activeProjectJsonPath = Path.Combine(tempAosRoot, "state", "active-project.json");
            Directory.CreateDirectory(Path.GetDirectoryName(activeProjectJsonPath)!);
            File.WriteAllText(activeProjectJsonPath, "{}", Encoding.UTF8);

            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(
                exitCode == 2,
                $"Expected exit code 2, got {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}"
            );

            Assert.Contains(
                "FAIL [state] .aos/state/active-project.json - Forbidden multi-project artifact exists; only a single-project workspace is permitted.",
                stdout,
                StringComparison.Ordinal
            );
            Assert.Contains("Validation failed: 1 issue(s).", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task AosValidateWorkspace_FailsWhenRoadmapReferencesMultipleProjects()
    {
        var approvedAosRoot = Path.Combine(
            FindRepositoryRootFrom(AppContext.BaseDirectory),
            "tests",
            "Gmsd.Aos.Tests",
            "Fixtures",
            "Approved",
            ".aos"
        );

        Assert.True(Directory.Exists(approvedAosRoot), $"Approved fixture not found at '{approvedAosRoot}'.");

        var tempWorkspaceRoot = CreateTempDirectory();
        try
        {
            var tempAosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            CopyDirectory(approvedAosRoot, tempAosRoot);

            var roadmapJsonPath = Path.Combine(tempAosRoot, "spec", "roadmap.json");
            Directory.CreateDirectory(Path.GetDirectoryName(roadmapJsonPath)!);
            File.WriteAllText(roadmapJsonPath, """{"projects":["p1","p2"]}""", Encoding.UTF8);

            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                aosDllPath,
                "validate",
                "workspace",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(
                exitCode == 2,
                $"Expected exit code 2, got {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}"
            );

            Assert.Contains(
                "FAIL [spec] .aos/spec/roadmap.json - Roadmap references multiple projects; only a single project is permitted.",
                stdout,
                StringComparison.Ordinal
            );
            Assert.Contains("Validation failed: 1 issue(s).", stderr, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(destDir, rel));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            var destPath = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, overwrite: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-validate-workspace-invariants", Guid.NewGuid().ToString("N"));
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

    private static string FindRepositoryRootFrom(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Gmsd.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repository root from '{startPath}'.");
    }
}

