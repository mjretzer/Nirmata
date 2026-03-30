using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using nirmata.Services.Implementations;
using Xunit;

namespace nirmata.Services.Tests;

/// <summary>
/// Integration tests for <see cref="WorkspaceBootstrapService"/> idempotency guarantees.
/// Verifies that re-running bootstrap on the same root preserves existing git history
/// and only fills missing AOS scaffold artifacts without touching existing ones.
/// These tests require git to be installed on the host machine.
/// </summary>
public sealed class WorkspaceBootstrapServiceIdempotencyTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceBootstrapService _sut;

    public WorkspaceBootstrapServiceIdempotencyTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"nirm-bootstrap-idempotency-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _sut = new WorkspaceBootstrapService(NullLogger<WorkspaceBootstrapService>.Instance);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root))
            return;

        // Git marks object files as read-only on Windows; reset attributes before deleting.
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* best-effort */ }
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task BootstrapAsync_WhenReRunOnFullyInitializedWorkspace_ReturnsNoChanges()
    {
        // Arrange: bootstrap once to fully initialize
        var first = await _sut.BootstrapAsync(_root);
        Assert.True(first.Success);

        // Act: re-run bootstrap on the same root
        var second = await _sut.BootstrapAsync(_root);

        // Assert: bootstrap succeeds and reports no new artifacts created
        Assert.True(second.Success);
        Assert.False(second.GitRepositoryCreated, "git repository must not be re-created when .git/ already exists");
        Assert.False(second.AosScaffoldCreated, "AOS scaffold must not be re-created when all directories already exist");
    }

    [Fact]
    public async Task BootstrapAsync_WhenGitExistsButAosDirsMissing_OnlyFillsMissingDirectories()
    {
        // Arrange: bootstrap once, then remove two AOS subdirectories
        var first = await _sut.BootstrapAsync(_root);
        Assert.True(first.Success);

        var specDir = Path.Combine(_root, ".aos", "spec");
        var stateDir = Path.Combine(_root, ".aos", "state");
        Directory.Delete(specDir, recursive: true);
        Directory.Delete(stateDir, recursive: true);

        // Act: re-run bootstrap
        var second = await _sut.BootstrapAsync(_root);

        // Assert: git was not re-initialized, missing AOS dirs were recreated
        Assert.True(second.Success);
        Assert.False(second.GitRepositoryCreated, "existing git repository must be preserved");
        Assert.True(second.AosScaffoldCreated, "missing AOS directories must be recreated");
        Assert.True(Directory.Exists(specDir), "spec/ must be recreated");
        Assert.True(Directory.Exists(stateDir), "state/ must be recreated");
    }

    [Fact]
    public async Task BootstrapAsync_WhenReRunAfterFilesWrittenToScaffold_PreservesExistingFiles()
    {
        // Arrange: bootstrap once, write a custom file inside the scaffold
        var first = await _sut.BootstrapAsync(_root);
        Assert.True(first.Success);

        var customFile = Path.Combine(_root, ".aos", "spec", "project.json");
        const string customContent = """{ "custom": true }""";
        await File.WriteAllTextAsync(customFile, customContent);

        // Act: re-run bootstrap
        var second = await _sut.BootstrapAsync(_root);

        // Assert: custom file is untouched
        Assert.True(second.Success);
        Assert.True(File.Exists(customFile), "existing file inside scaffold must not be deleted");
        Assert.Equal(customContent, await File.ReadAllTextAsync(customFile));
    }

    [Fact]
    public async Task BootstrapAsync_WhenReRunOnWorkspaceWithCommits_PreservesGitHistory()
    {
        // Arrange: bootstrap, commit a file, then re-run bootstrap
        var first = await _sut.BootstrapAsync(_root);
        Assert.True(first.Success);

        await RunGitAsync("config user.email test@example.com");
        await RunGitAsync("config user.name TestUser");
        await File.WriteAllTextAsync(Path.Combine(_root, "README.md"), "# Test");
        await RunGitAsync("add README.md");
        await RunGitAsync("commit -m \"initial commit\"");

        var commitsBefore = await CountCommitsAsync();
        Assert.True(commitsBefore > 0, "test setup: at least one commit must exist before re-bootstrap");

        // Act: re-run bootstrap
        var second = await _sut.BootstrapAsync(_root);

        // Assert: git history is unchanged
        Assert.True(second.Success);
        Assert.False(second.GitRepositoryCreated, "git init must not be re-run when history exists");
        Assert.Equal(commitsBefore, await CountCommitsAsync());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task RunGitAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync();
    }

    private async Task<int> CountCommitsAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-list --count HEAD",
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        return p.ExitCode == 0 && int.TryParse(output.Trim(), out var count) ? count : 0;
    }
}
