using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Services.Implementations;
using Xunit;

namespace nirmata.Services.Tests;

/// <summary>
/// Tests for structured failure cases in <see cref="WorkspaceBootstrapService"/>.
/// Verifies that each distinct failure condition produces the correct <see cref="BootstrapFailureKind"/>
/// so callers can surface context-appropriate diagnostics to the user.
/// </summary>
public sealed class WorkspaceBootstrapServiceFailureTests : IDisposable
{
    private readonly WorkspaceBootstrapService _sut;
    private readonly List<string> _tempPaths = [];

    public WorkspaceBootstrapServiceFailureTests()
    {
        _sut = new WorkspaceBootstrapService(NullLogger<WorkspaceBootstrapService>.Instance);
    }

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { /* best-effort */ }
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { /* best-effort */ }
        }
    }

    // ── Path validation failures ──────────────────────────────────────────────

    [Fact]
    public async Task BootstrapAsync_WhenPathIsNull_ReturnsInvalidPath()
    {
        var result = await _sut.BootstrapAsync(null!);

        Assert.False(result.Success);
        Assert.Equal(BootstrapFailureKind.InvalidPath, result.FailureKind);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task BootstrapAsync_WhenPathIsWhitespace_ReturnsInvalidPath()
    {
        var result = await _sut.BootstrapAsync("   ");

        Assert.False(result.Success);
        Assert.Equal(BootstrapFailureKind.InvalidPath, result.FailureKind);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task BootstrapAsync_WhenPathIsRelative_ReturnsInvalidPath()
    {
        var result = await _sut.BootstrapAsync("relative/path");

        Assert.False(result.Success);
        Assert.Equal(BootstrapFailureKind.InvalidPath, result.FailureKind);
        Assert.NotNull(result.Error);
    }

    // ── Directory not found ───────────────────────────────────────────────────

    [Fact]
    public async Task BootstrapAsync_WhenDirectoryDoesNotExist_ReturnsDirectoryNotFound()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), $"nirm-nonexistent-{Guid.NewGuid():N}");

        var result = await _sut.BootstrapAsync(nonExistent);

        Assert.False(result.Success);
        Assert.Equal(BootstrapFailureKind.DirectoryNotFound, result.FailureKind);
        Assert.NotNull(result.Error);
    }

    // ── Filesystem error during AOS scaffold ──────────────────────────────────

    [Fact]
    public async Task BootstrapAsync_WhenAosPathBlockedByFile_ReturnsFileSystemError()
    {
        // Arrange: create a temp directory and pre-run git init so the git step succeeds.
        // Then place a file named ".aos" where the scaffold directory would be created.
        var root = Path.Combine(Path.GetTempPath(), $"nirm-fs-error-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        _tempPaths.Add(root);

        await RunGitInitAsync(root);

        // A file at the .aos path prevents Directory.CreateDirectory from succeeding.
        var aosPath = Path.Combine(root, ".aos");
        await File.WriteAllTextAsync(aosPath, "blocked");

        // Act
        var result = await _sut.BootstrapAsync(root);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(BootstrapFailureKind.FileSystemError, result.FailureKind);
        Assert.NotNull(result.Error);
    }

    // ── Successful bootstrap has None failure kind ────────────────────────────

    [Fact]
    public async Task BootstrapAsync_WhenSuccessful_HasNoneFailureKind()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nirm-failure-success-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        _tempPaths.Add(root);

        var result = await _sut.BootstrapAsync(root);

        Assert.True(result.Success);
        Assert.Equal(BootstrapFailureKind.None, result.FailureKind);
        Assert.Null(result.Error);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task RunGitInitAsync(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "init",
            WorkingDirectory = path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync();
    }
}
