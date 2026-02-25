using System.Diagnostics;
using System.Text;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosInitFixtureTests
{
    [Fact]
    public async Task AosInit_ProducesDeterministicWorkspaceFixture()
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
            var aosDllPath = typeof(AosWorkspaceBootstrapper).Assembly.Location;
            Assert.True(File.Exists(aosDllPath), $"aos assembly not found at '{aosDllPath}'.");

            var (exitCode, stdout, stderr) = await RunDotNetAsync(
                aosDllPath,
                "init",
                "--root",
                tempWorkspaceRoot
            );

            Assert.True(
                exitCode == 0,
                $"Expected exit code 0, got {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}"
            );

            var actualAosRoot = Path.Combine(tempWorkspaceRoot, ".aos");
            AssertDirectoryTreeMatchesApprovedFixture(actualAosRoot, approvedAosRoot);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static void AssertDirectoryTreeMatchesApprovedFixture(string actualRoot, string approvedRoot)
    {
        Assert.True(Directory.Exists(actualRoot), $"Actual '.aos' root not found at '{actualRoot}'.");

        var expectedDirs = EnumerateDirectoriesRelative(approvedRoot);
        var actualDirs = EnumerateDirectoriesRelative(actualRoot);
        Assert.Equal(expectedDirs, actualDirs);

        var expectedFiles = EnumerateFilesRelative(approvedRoot);
        expectedFiles.RemoveWhere(static p => p.EndsWith("/.gitkeep", StringComparison.Ordinal));
        var actualFiles = EnumerateFilesRelative(actualRoot);
        Assert.Equal(expectedFiles, actualFiles);

        foreach (var relPath in expectedFiles)
        {
            var expectedPath = Path.Combine(approvedRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
            var actualPath = Path.Combine(actualRoot, relPath.Replace('/', Path.DirectorySeparatorChar));

            if (relPath.EndsWith(".json", StringComparison.Ordinal) || relPath.EndsWith(".ndjson", StringComparison.Ordinal))
            {
                var actualBytes = File.ReadAllBytes(actualPath);

                // Guardrail: canonical writers always emit UTF-8 without BOM.
                Assert.False(
                    actualBytes.Length >= 3 && actualBytes[0] == 0xEF && actualBytes[1] == 0xBB && actualBytes[2] == 0xBF,
                    $"Expected no UTF-8 BOM in '{relPath}'."
                );

                // Guardrail: canonical JSON writer always emits a trailing LF.
                if (relPath.EndsWith(".json", StringComparison.Ordinal))
                {
                    Assert.True(
                        actualBytes.Length > 0 && actualBytes[^1] == (byte)'\n',
                        $"Expected '{relPath}' to end with LF."
                    );
                }

                // Guardrail: if the NDJSON log is non-empty, it must be LF-terminated and contain no CRs.
                if (relPath.EndsWith(".ndjson", StringComparison.Ordinal) && actualBytes.Length > 0)
                {
                    Assert.True(
                        actualBytes[^1] == (byte)'\n',
                        $"Expected '{relPath}' to end with LF when non-empty."
                    );

                    var ndjsonText = File.ReadAllText(actualPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    Assert.DoesNotContain("\r", ndjsonText, StringComparison.Ordinal);
                }
            }

            var expectedText = NormalizeFixtureText(File.ReadAllText(expectedPath, Encoding.UTF8));
            var actualText = NormalizeFixtureText(File.ReadAllText(actualPath, Encoding.UTF8));

            Assert.Equal(expectedText, actualText);
        }
    }

    private static SortedSet<string> EnumerateDirectoriesRelative(string root)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, dir);
            rel = rel.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            set.Add(rel);
        }

        return set;
    }

    private static SortedSet<string> EnumerateFilesRelative(string root)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file);
            rel = rel.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            set.Add(rel);
        }

        return set;
    }

    private static string NormalizeFixtureText(string text)
        => text.Replace("\r\n", "\n").TrimEnd('\n', '\r');

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-fixture", Guid.NewGuid().ToString("N"));
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

