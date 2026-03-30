using Microsoft.Extensions.Logging.Abstractions;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Services.Implementations;
using Xunit;

namespace nirmata.Services.Tests;

/// <summary>
/// Integration tests for the happy-path bootstrap scenarios in <see cref="WorkspaceBootstrapService"/>.
/// Verifies that bootstrapping a fresh directory produces the expected git repository and AOS scaffold.
/// These tests require git to be installed on the host machine.
/// </summary>
public sealed class WorkspaceBootstrapServiceSuccessTests : IDisposable
{
    private static readonly string[] ExpectedAosSubdirectories =
    [
        "spec", "state", "evidence", "codebase", "context", "cache", "schemas"
    ];

    private readonly string _root;
    private readonly WorkspaceBootstrapService _sut;

    public WorkspaceBootstrapServiceSuccessTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"nirm-bootstrap-success-{Guid.NewGuid():N}");
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

    // ── Fresh workspace (no git, no .aos) ─────────────────────────────────────

    [Fact]
    public async Task BootstrapAsync_OnFreshDirectory_ReturnsSuccess()
    {
        var result = await _sut.BootstrapAsync(_root);

        Assert.True(result.Success);
        Assert.Equal(BootstrapFailureKind.None, result.FailureKind);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task BootstrapAsync_OnFreshDirectory_ReportsGitRepositoryCreated()
    {
        var result = await _sut.BootstrapAsync(_root);

        Assert.True(result.Success);
        Assert.True(result.GitRepositoryCreated, "fresh directory must report git repository was created");
        Assert.True(Directory.Exists(Path.Combine(_root, ".git")), ".git/ must exist after bootstrap");
    }

    [Fact]
    public async Task BootstrapAsync_OnFreshDirectory_ReportsAosScaffoldCreated()
    {
        var result = await _sut.BootstrapAsync(_root);

        Assert.True(result.Success);
        Assert.True(result.AosScaffoldCreated, "fresh directory must report AOS scaffold was created");
    }

    [Fact]
    public async Task BootstrapAsync_OnFreshDirectory_CreatesAllAosSubdirectories()
    {
        await _sut.BootstrapAsync(_root);

        var aosRoot = Path.Combine(_root, ".aos");
        Assert.True(Directory.Exists(aosRoot), ".aos/ root must exist");

        foreach (var subdir in ExpectedAosSubdirectories)
        {
            var path = Path.Combine(aosRoot, subdir);
            Assert.True(Directory.Exists(path), $".aos/{subdir}/ must exist after bootstrap");
        }
    }

    // ── Pre-existing git repository ────────────────────────────────────────────

    [Fact]
    public async Task BootstrapAsync_WhenGitAlreadyExists_ReturnsSuccess()
    {
        // Arrange: pre-seed a git repository so bootstrap finds it
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        var result = await _sut.BootstrapAsync(_root);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task BootstrapAsync_WhenGitAlreadyExists_ReportsGitNotCreated()
    {
        // Arrange: pre-seed a git repository so bootstrap skips git init
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        var result = await _sut.BootstrapAsync(_root);

        Assert.True(result.Success);
        Assert.False(result.GitRepositoryCreated, "pre-existing git repository must not be re-created");
    }

    [Fact]
    public async Task BootstrapAsync_WhenGitAlreadyExists_StillCreatesAosScaffold()
    {
        // Arrange: pre-seed a git repository; .aos/ is absent
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        var result = await _sut.BootstrapAsync(_root);

        Assert.True(result.Success);
        Assert.True(result.AosScaffoldCreated, "AOS scaffold must be created even when git already exists");

        var aosRoot = Path.Combine(_root, ".aos");
        Assert.True(Directory.Exists(aosRoot), ".aos/ must exist after bootstrap");

        foreach (var subdir in ExpectedAosSubdirectories)
        {
            Assert.True(
                Directory.Exists(Path.Combine(aosRoot, subdir)),
                $".aos/{subdir}/ must exist when bootstrapping with pre-existing git");
        }
    }
}
