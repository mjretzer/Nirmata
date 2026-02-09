using System.Diagnostics;
using System.Text;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosValidateWorkspaceMalformedJsonTests
{
    [Fact]
    public async Task AosValidateWorkspace_FailsOnMalformedJsonArtifact()
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

            // Corrupt a required spec-layer artifact that is NOT part of invariant checks.
            var milestonesIndexPath = Path.Combine(tempAosRoot, "spec", "milestones", "index.json");
            Assert.True(File.Exists(milestonesIndexPath), $"Fixture file not found at '{milestonesIndexPath}'.");
            File.WriteAllText(milestonesIndexPath, "{", Encoding.UTF8);

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

            Assert.Contains("FAIL [spec] .aos/spec/milestones/index.json - Invalid JSON.", stdout, StringComparison.Ordinal);
            Assert.Contains("Validation failed:", stderr, StringComparison.Ordinal);
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
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-validate-workspace", Guid.NewGuid().ToString("N"));
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

