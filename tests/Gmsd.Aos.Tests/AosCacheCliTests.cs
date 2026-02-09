using System.Diagnostics;
using System.Text;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosCacheCliTests
{
    [Fact]
    public async Task CacheClear_RemovesEntries_ButKeepsCacheDirectory()
    {
        var tempWorkspaceRoot = CreateTempDirectory("gmsd-aos-cache-clear");
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
            WriteRepoMarker(tempWorkspaceRoot);

            var cacheRoot = Path.Combine(tempWorkspaceRoot, ".aos", "cache");
            Directory.CreateDirectory(cacheRoot);
            WriteUtf8NoBom(Path.Combine(cacheRoot, "a.txt"), "hello\n");
            Directory.CreateDirectory(Path.Combine(cacheRoot, "dir"));
            WriteUtf8NoBom(Path.Combine(cacheRoot, "dir", "b.txt"), "world\n");

            var (clearExit, clearOut, clearErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "cache",
                "clear"
            );

            Assert.True(clearExit == 0, $"Expected cache clear exit code 0, got {clearExit}. STDERR:{Environment.NewLine}{clearErr}");
            Assert.Contains("CLEARED", clearOut, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(cacheRoot), "Expected '.aos/cache/' to still exist.");
            Assert.Empty(Directory.EnumerateFileSystemEntries(cacheRoot));
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task CachePrune_RemovesEntriesOlderThanThreshold_AndKeepsNewerEntries()
    {
        var tempWorkspaceRoot = CreateTempDirectory("gmsd-aos-cache-prune");
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
            WriteRepoMarker(tempWorkspaceRoot);

            var cacheRoot = Path.Combine(tempWorkspaceRoot, ".aos", "cache");
            Directory.CreateDirectory(cacheRoot);

            var oldFile = Path.Combine(cacheRoot, "old.txt");
            var newFile = Path.Combine(cacheRoot, "new.txt");
            WriteUtf8NoBom(oldFile, "old\n");
            WriteUtf8NoBom(newFile, "new\n");

            var oldDir = Path.Combine(cacheRoot, "old-dir");
            Directory.CreateDirectory(oldDir);
            WriteUtf8NoBom(Path.Combine(oldDir, "x.txt"), "x\n");

            // Make some entries "old".
            var oldTime = DateTime.UtcNow - TimeSpan.FromDays(40);
            File.SetLastWriteTimeUtc(oldFile, oldTime);
            Directory.SetLastWriteTimeUtc(oldDir, oldTime);

            var (pruneExit, pruneOut, pruneErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "cache",
                "prune"
            );

            Assert.True(pruneExit == 0, $"Expected cache prune exit code 0, got {pruneExit}. STDERR:{Environment.NewLine}{pruneErr}");
            Assert.Contains("PRUNED", pruneOut, StringComparison.OrdinalIgnoreCase);

            Assert.True(Directory.Exists(cacheRoot), "Expected '.aos/cache/' to still exist.");
            Assert.False(File.Exists(oldFile), "Expected old file to be pruned.");
            Assert.False(Directory.Exists(oldDir), "Expected old directory to be pruned.");
            Assert.True(File.Exists(newFile), "Expected newer file to remain.");
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task CachePrune_WithDays0_RemovesAllEntries()
    {
        var tempWorkspaceRoot = CreateTempDirectory("gmsd-aos-cache-prune-days0");
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
            WriteRepoMarker(tempWorkspaceRoot);

            var cacheRoot = Path.Combine(tempWorkspaceRoot, ".aos", "cache");
            Directory.CreateDirectory(cacheRoot);
            WriteUtf8NoBom(Path.Combine(cacheRoot, "a.txt"), "hello\n");
            Directory.CreateDirectory(Path.Combine(cacheRoot, "dir"));
            WriteUtf8NoBom(Path.Combine(cacheRoot, "dir", "b.txt"), "world\n");

            var (pruneExit, _, pruneErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "cache",
                "prune",
                "--days",
                "0"
            );

            Assert.True(pruneExit == 0, $"Expected cache prune exit code 0, got {pruneExit}. STDERR:{Environment.NewLine}{pruneErr}");
            Assert.True(Directory.Exists(cacheRoot), "Expected '.aos/cache/' to still exist.");
            Assert.Empty(Directory.EnumerateFileSystemEntries(cacheRoot));
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task CacheCommands_WhenLocked_FailWithStableExitCode()
    {
        var tempWorkspaceRoot = CreateTempDirectory("gmsd-aos-cache-locked");
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
            WriteRepoMarker(tempWorkspaceRoot);

            var (lockExit, _, lockErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "lock",
                "acquire"
            );
            Assert.True(lockExit == 0, $"Expected lock acquire exit code 0, got {lockExit}. STDERR:{Environment.NewLine}{lockErr}");

            var (clearExit, _, clearErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "cache",
                "clear"
            );
            Assert.True(clearExit == 4, $"Expected lock contention exit code 4, got {clearExit}. STDERR:{Environment.NewLine}{clearErr}");
            Assert.Contains("Workspace is locked.", clearErr, StringComparison.OrdinalIgnoreCase);

            var (pruneExit, _, pruneErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "cache",
                "prune"
            );
            Assert.True(pruneExit == 4, $"Expected lock contention exit code 4, got {pruneExit}. STDERR:{Environment.NewLine}{pruneErr}");
            Assert.Contains("Workspace is locked.", pruneErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    [Fact]
    public async Task CacheClear_DoesNotBreakValidateWorkspace()
    {
        var tempWorkspaceRoot = CreateTempDirectory("gmsd-aos-cache-validate");
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
            WriteRepoMarker(tempWorkspaceRoot);

            var (preExit, _, preErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "validate",
                "workspace"
            );
            Assert.True(preExit == 0, $"Expected validate workspace exit code 0, got {preExit}. STDERR:{Environment.NewLine}{preErr}");

            var cacheRoot = Path.Combine(tempWorkspaceRoot, ".aos", "cache");
            Directory.CreateDirectory(cacheRoot);
            WriteUtf8NoBom(Path.Combine(cacheRoot, "x.txt"), "x\n");

            var (clearExit, _, clearErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "cache",
                "clear"
            );
            Assert.True(clearExit == 0, $"Expected cache clear exit code 0, got {clearExit}. STDERR:{Environment.NewLine}{clearErr}");

            var (postExit, _, postErr) = await RunDotNetAsync(
                workingDirectory: tempWorkspaceRoot,
                aosDllPath,
                "validate",
                "workspace"
            );
            Assert.True(postExit == 0, $"Expected validate workspace exit code 0, got {postExit}. STDERR:{Environment.NewLine}{postErr}");
        }
        finally
        {
            TryDeleteDirectory(tempWorkspaceRoot);
        }
    }

    private static void WriteUtf8NoBom(string path, string content)
        => File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private static void WriteRepoMarker(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "Gmsd.slnx");
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

