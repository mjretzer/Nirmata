using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using nirmata.Data.Dto.Models.Codebase;
using nirmata.Services.Implementations;
using Xunit;

namespace nirmata.Services.Tests;

/// <summary>
/// Tests for <see cref="CodebaseService"/>.
/// Each test creates an isolated temp workspace directory and tears it down afterwards.
/// No mocking required — the service reads real files.
/// </summary>
public sealed class CodebaseServiceTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly string _codebaseDir;
    private readonly CodebaseService _sut = new();

    public CodebaseServiceTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "nirm-tests", Guid.NewGuid().ToString("N"));
        _codebaseDir = Path.Combine(_workspaceRoot, ".aos", "codebase");
        Directory.CreateDirectory(_codebaseDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
            Directory.Delete(_workspaceRoot, recursive: true);
    }

    // ── GetInventoryAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetInventoryAsync_AllArtifactsMissing_ReturnsEveryArtifactWithMissingStatus()
    {
        // Arrange: empty codebase dir — no files at all.

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Artifacts);
        Assert.All(result.Artifacts, a => Assert.Equal(CodebaseArtifactStatus.Missing, a.Status));
    }

    [Fact]
    public async Task GetInventoryAsync_NoManifest_ExistingArtifactIsStale()
    {
        // Arrange: write map.json but no manifest → cannot verify hash → stale.
        await WriteJsonAsync("map.json", new { summary = "hello" });

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert
        var mapArtifact = Assert.Single(result.Artifacts, a => a.Id == "map");
        Assert.Equal(CodebaseArtifactStatus.Stale, mapArtifact.Status);
    }

    [Fact]
    public async Task GetInventoryAsync_ManifestHashMatches_ArtifactIsReady()
    {
        // Arrange: write stack.json and a matching hash-manifest.json.
        var content = """{"languages":[{"name":"C#"}]}""";
        await File.WriteAllTextAsync(Path.Combine(_codebaseDir, "stack.json"), content);
        var hash = ComputeSha256Hex(content);
        await WriteManifestAsync(new Dictionary<string, string> { ["stack.json"] = hash });

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert
        var artifact = Assert.Single(result.Artifacts, a => a.Id == "stack");
        Assert.Equal(CodebaseArtifactStatus.Ready, artifact.Status);
    }

    [Fact]
    public async Task GetInventoryAsync_ManifestHashMismatch_ArtifactIsStale()
    {
        // Arrange: write conventions.json but use the wrong hash.
        await WriteJsonAsync("conventions.json", new { rule = "camelCase" });
        await WriteManifestAsync(new Dictionary<string, string> { ["conventions.json"] = "deadbeef" });

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert
        var artifact = Assert.Single(result.Artifacts, a => a.Id == "conventions");
        Assert.Equal(CodebaseArtifactStatus.Stale, artifact.Status);
    }

    [Fact]
    public async Task GetInventoryAsync_InvalidJsonFile_ArtifactIsError()
    {
        // Arrange: write a broken JSON file for "architecture".
        await File.WriteAllTextAsync(Path.Combine(_codebaseDir, "architecture.json"), "{ NOT VALID JSON }}}");

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert
        var artifact = Assert.Single(result.Artifacts, a => a.Id == "architecture");
        Assert.Equal(CodebaseArtifactStatus.Error, artifact.Status);
    }

    [Fact]
    public async Task GetInventoryAsync_ExtractsLanguagesFromStackJson()
    {
        // Arrange
        var stackJson = """{"languages":[{"name":"C#"},{"name":"TypeScript"}],"frameworks":[]}""";
        await File.WriteAllTextAsync(Path.Combine(_codebaseDir, "stack.json"), stackJson);

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert
        Assert.Contains("C#", result.Languages);
        Assert.Contains("TypeScript", result.Languages);
    }

    [Fact]
    public async Task GetInventoryAsync_ExtractsStackFromStackJson()
    {
        // Arrange
        var stackJson = """{"languages":[],"frameworks":[{"name":".NET 10"},{"name":"React"}]}""";
        await File.WriteAllTextAsync(Path.Combine(_codebaseDir, "stack.json"), stackJson);

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert
        Assert.Contains(".NET 10", result.Stack);
        Assert.Contains("React", result.Stack);
    }

    [Fact]
    public async Task GetInventoryAsync_MissingStackJson_ReturnsEmptyLanguagesAndStack()
    {
        // Arrange: no stack.json at all.

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert
        Assert.Empty(result.Languages);
        Assert.Empty(result.Stack);
    }

    [Fact]
    public async Task GetInventoryAsync_ArtifactsIncludeStableFields()
    {
        // Arrange: write map.json so at least one artifact has a file.
        await WriteJsonAsync("map.json", new { summary = "ok" });

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert: every record has id, type, status, path.
        Assert.All(result.Artifacts, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Id));
            Assert.False(string.IsNullOrWhiteSpace(a.Type));
            Assert.False(string.IsNullOrWhiteSpace(a.Status));
            Assert.False(string.IsNullOrWhiteSpace(a.Path));
        });
    }

    [Fact]
    public async Task GetInventoryAsync_ExistingArtifact_HasLastUpdatedSet()
    {
        // Arrange
        await WriteJsonAsync("map.json", new { summary = "ok" });

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert
        var mapArtifact = Assert.Single(result.Artifacts, a => a.Id == "map");
        Assert.NotNull(mapArtifact.LastUpdated);
    }

    [Fact]
    public async Task GetInventoryAsync_MissingArtifact_HasNullLastUpdated()
    {
        // Arrange: empty codebase dir.

        // Act
        var result = await _sut.GetInventoryAsync(_workspaceRoot);

        // Assert: no artifact should have a LastUpdated when the file is absent.
        var mapArtifact = Assert.Single(result.Artifacts, a => a.Id == "map");
        Assert.Null(mapArtifact.LastUpdated);
    }

    // ── GetArtifactAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetArtifactAsync_UnknownArtifactId_ReturnsNull()
    {
        // Act — unsupported id, caller should return 404.
        var result = await _sut.GetArtifactAsync(_workspaceRoot, "unsupported-artifact-id");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetArtifactAsync_KnownIdFileAbsent_ReturnsMissingStatus()
    {
        // Act
        var result = await _sut.GetArtifactAsync(_workspaceRoot, "structure");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("structure", result.Id);
        Assert.Equal(CodebaseArtifactStatus.Missing, result.Status);
        Assert.Null(result.Payload);
    }

    [Fact]
    public async Task GetArtifactAsync_KnownIdFilePresent_ReturnsPayloadAndStatus()
    {
        // Arrange
        var content = """{"node_count":42}""";
        await File.WriteAllTextAsync(Path.Combine(_codebaseDir, "structure.json"), content);

        // Act
        var result = await _sut.GetArtifactAsync(_workspaceRoot, "structure");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("structure", result.Id);
        Assert.True(result.Status is CodebaseArtifactStatus.Ready or CodebaseArtifactStatus.Stale);
        Assert.NotNull(result.Payload);
    }

    [Fact]
    public async Task GetArtifactAsync_HashMatchesManifest_ReturnsReadyStatus()
    {
        // Arrange
        var content = """{"count":1}""";
        await File.WriteAllTextAsync(Path.Combine(_codebaseDir, "testing.json"), content);
        var hash = ComputeSha256Hex(content);
        await WriteManifestAsync(new Dictionary<string, string> { ["testing.json"] = hash });

        // Act
        var result = await _sut.GetArtifactAsync(_workspaceRoot, "testing");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(CodebaseArtifactStatus.Ready, result.Status);
    }

    [Fact]
    public async Task GetArtifactAsync_CacheArtifact_SymbolsIdResolvesCorrectly()
    {
        // Arrange: the "symbols" artifact lives under cache/symbols.json.
        var cacheDir = Path.Combine(_codebaseDir, "cache");
        Directory.CreateDirectory(cacheDir);
        await File.WriteAllTextAsync(Path.Combine(cacheDir, "symbols.json"), """{"entries":[]}""");

        // Act
        var result = await _sut.GetArtifactAsync(_workspaceRoot, "symbols");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("symbols", result.Id);
        Assert.NotEqual(CodebaseArtifactStatus.Missing, result.Status);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task WriteJsonAsync(string relPath, object obj)
    {
        var path = Path.Combine(_codebaseDir, relPath);
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(obj));
    }

    private async Task WriteManifestAsync(Dictionary<string, string> files)
    {
        var manifest = new { files };
        await File.WriteAllTextAsync(
            Path.Combine(_codebaseDir, "hash-manifest.json"),
            JsonSerializer.Serialize(manifest));
    }

    private static string ComputeSha256Hex(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
