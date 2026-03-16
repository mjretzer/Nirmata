using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace nirmata.Agents.Tests.Execution.CodebaseScanner;

public class CodebaseIntelligencePackWriterTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanner _scanner;
    private readonly Agents.Execution.Brownfield.CodebaseScanner.CodebaseIntelligencePackWriter _writer;

    public CodebaseIntelligencePackWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"codebase-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _scanner = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanner();
        _writer = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseIntelligencePackWriter();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task WriteAllAsync_WithSuccessfulScan_WritesAllRequiredFiles()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act
        var writtenFiles = await _writer.WriteAllAsync(result, outputDir);

        // Assert
        writtenFiles.Should().NotBeEmpty();
        writtenFiles.Should().Contain(f => f.EndsWith("map.json"));
        writtenFiles.Should().Contain(f => f.EndsWith("stack.json"));
        writtenFiles.Should().Contain(f => f.EndsWith("structure.json"));
        writtenFiles.Should().Contain(f => f.EndsWith("architecture.json"));
        writtenFiles.Should().Contain(f => f.EndsWith("conventions.json"));
        writtenFiles.Should().Contain(f => f.EndsWith("testing.json"));
        writtenFiles.Should().Contain(f => f.EndsWith("integrations.json"));
        writtenFiles.Should().Contain(f => f.EndsWith("concerns.json"));
        writtenFiles.Should().Contain(f => f.EndsWith("symbols.json"));
        writtenFiles.Should().Contain(f => f.EndsWith("file-graph.json"));
    }

    [Fact]
    public async Task WriteAllAsync_ProducesValidJsonFiles()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act
        var writtenFiles = await _writer.WriteAllAsync(result, outputDir);

        // Assert - all files should be valid JSON
        foreach (var file in writtenFiles)
        {
            var content = await File.ReadAllTextAsync(file);
            var exception = Record.Exception(() => JsonDocument.Parse(content));
            exception.Should().BeNull($"File {Path.GetFileName(file)} should be valid JSON");
        }
    }

    [Fact]
    public async Task WriteAllAsync_MapJson_HasRequiredSchemaVersion()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act
        await _writer.WriteAllAsync(result, outputDir);
        var mapJson = await File.ReadAllTextAsync(Path.Combine(outputDir, "map.json"));
        var doc = JsonDocument.Parse(mapJson);

        // Assert
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("repository").GetProperty("name").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("scanTimestamp").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task WriteAllAsync_StackJson_ContainsLanguagesAndFrameworks()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act
        await _writer.WriteAllAsync(result, outputDir);
        var stackJson = await File.ReadAllTextAsync(Path.Combine(outputDir, "stack.json"));
        var doc = JsonDocument.Parse(stackJson);

        // Assert
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("languages").EnumerateArray().Should().NotBeEmpty();
        doc.RootElement.GetProperty("frameworks").EnumerateArray().Should().NotBeEmpty();
        doc.RootElement.GetProperty("buildTools").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteAllAsync_StructureJson_ContainsDirectoriesAndFiles()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act
        await _writer.WriteAllAsync(result, outputDir);
        var structureJson = await File.ReadAllTextAsync(Path.Combine(outputDir, "structure.json"));
        var doc = JsonDocument.Parse(structureJson);

        // Assert
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("directories").EnumerateArray().Should().NotBeEmpty();
        doc.RootElement.GetProperty("files").EnumerateArray().Should().NotBeEmpty();
        doc.RootElement.GetProperty("statistics").GetProperty("totalFiles").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WriteAllAsync_ArchitectureJson_ContainsLayersAndPatterns()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act
        await _writer.WriteAllAsync(result, outputDir);
        var archJson = await File.ReadAllTextAsync(Path.Combine(outputDir, "architecture.json"));
        var doc = JsonDocument.Parse(archJson);

        // Assert
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("layers").EnumerateArray().Should().NotBeNull();
        doc.RootElement.GetProperty("patterns").EnumerateArray().Should().NotBeNull();
        doc.RootElement.GetProperty("entryPoints").EnumerateArray().Should().NotBeNull();
    }

    [Fact]
    public async Task WriteAllAsync_TestingJson_ContainsTestProjects()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act
        await _writer.WriteAllAsync(result, outputDir);
        var testingJson = await File.ReadAllTextAsync(Path.Combine(outputDir, "testing.json"));
        var doc = JsonDocument.Parse(testingJson);

        // Assert
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("testProjects").EnumerateArray().Should().NotBeNull();
        doc.RootElement.GetProperty("frameworks").EnumerateArray().Should().NotBeNull();
    }

    [Fact]
    public async Task WriteAllAsync_FileGraphJson_ContainsNodesAndEdges()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");
        var cacheDir = Path.Combine(outputDir, "cache");

        // Act
        await _writer.WriteAllAsync(result, outputDir);
        var graphJson = await File.ReadAllTextAsync(Path.Combine(cacheDir, "file-graph.json"));
        var doc = JsonDocument.Parse(graphJson);

        // Assert
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("nodes").EnumerateArray().Should().NotBeNull();
        doc.RootElement.GetProperty("edges").EnumerateArray().Should().NotBeNull();
        doc.RootElement.GetProperty("metadata").GetProperty("nodeCount").GetInt32().Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task WriteAllAsync_ProducesDeterministicOutput()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir1 = Path.Combine(_tempDirectory, "codebase1");
        var outputDir2 = Path.Combine(_tempDirectory, "codebase2");

        // Act - write twice with same input
        await _writer.WriteAllAsync(result, outputDir1);
        await _writer.WriteAllAsync(result, outputDir2);

        // Assert - compare key files
        var files1 = Directory.GetFiles(outputDir1, "*.json", SearchOption.AllDirectories).OrderBy(f => f).ToList();
        var files2 = Directory.GetFiles(outputDir2, "*.json", SearchOption.AllDirectories).OrderBy(f => f).ToList();

        files1.Count.Should().Be(files2.Count);

        for (var i = 0; i < files1.Count; i++)
        {
            var content1 = await File.ReadAllTextAsync(files1[i]);
            var content2 = await File.ReadAllTextAsync(files2[i]);
            content1.Should().Be(content2, $"Files should be identical: {Path.GetFileName(files1[i])}");
        }
    }

    [Fact]
    public async Task WriteAllAsync_CreatesCacheDirectory()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");
        var cacheDir = Path.Combine(outputDir, "cache");

        // Act
        await _writer.WriteAllAsync(result, outputDir);

        // Assert
        Directory.Exists(cacheDir).Should().BeTrue();
        File.Exists(Path.Combine(cacheDir, "symbols.json")).Should().BeTrue();
        File.Exists(Path.Combine(cacheDir, "file-graph.json")).Should().BeTrue();
    }

    [Fact]
    public async Task WriteAllAsync_WithFailedScan_ThrowsInvalidOperationException()
    {
        // Arrange
        var failedResult = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanResult
        {
            IsSuccess = false,
            ErrorMessage = "Test failure"
        };
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _writer.WriteAllAsync(failedResult, outputDir));
        exception.Message.Should().Contain("Cannot write intelligence pack for failed scan");
    }

    [Fact]
    public async Task WriteAllAsync_ProducesValidUtf8Json()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act
        var writtenFiles = await _writer.WriteAllAsync(result, outputDir);

        // Assert - verify UTF-8 encoding without BOM
        foreach (var file in writtenFiles)
        {
            var bytes = await File.ReadAllBytesAsync(file);
            bytes.Should().NotBeEmpty();

            // Should not have UTF-8 BOM
            if (bytes.Length >= 3)
            {
                (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF).Should().BeFalse($"File {Path.GetFileName(file)} should not have BOM");
            }

            // Should end with newline
            bytes[^1].Should().Be((byte)'\n', $"File {Path.GetFileName(file)} should end with newline");
        }
    }

    [Fact]
    public async Task WriteAllAsync_WithCancellationToken_StopsWhenCancelled()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _writer.WriteAllAsync(result, outputDir, cts.Token));
    }

    [Fact]
    public async Task WriteAllAsync_JsonFilesHaveSortedKeys()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act
        await _writer.WriteAllAsync(result, outputDir);
        var mapJson = await File.ReadAllTextAsync(Path.Combine(outputDir, "map.json"));
        var doc = JsonDocument.Parse(mapJson);

        // Assert - verify property order is alphabetical
        var propertyNames = doc.RootElement.EnumerateObject()
            .Select(p => p.Name)
            .ToList();

        var sortedNames = propertyNames.OrderBy(n => n, StringComparer.Ordinal).ToList();
        propertyNames.Should().Equal(sortedNames, "JSON keys should be alphabetically sorted");
    }

    [Fact]
    public async Task WriteAllAsync_GeneratesHashManifest()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");

        // Act
        await _writer.WriteAllAsync(result, outputDir);

        // Assert
        var manifestPath = Path.Combine(outputDir, "hash-manifest.json");
        File.Exists(manifestPath).Should().BeTrue();

        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var doc = JsonDocument.Parse(manifestJson);

        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("algorithm").GetString().Should().Be("SHA256");
        doc.RootElement.GetProperty("generatedAt").GetString().Should().NotBeNullOrEmpty();

        var files = doc.RootElement.GetProperty("files");
        files.GetProperty("map.json").GetString().Should().NotBeNullOrEmpty();
        files.GetProperty("stack.json").GetString().Should().NotBeNullOrEmpty();
        files.GetProperty("structure.json").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task VerifyDeterminismAsync_WithValidManifest_ReturnsTrue()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");
        await _writer.WriteAllAsync(result, outputDir);

        // Act
        var (isValid, mismatches) = await Agents.Execution.Brownfield.CodebaseScanner.CodebaseIntelligencePackWriter.VerifyDeterminismAsync(outputDir);

        // Assert
        isValid.Should().BeTrue();
        mismatches.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyDeterminismAsync_WithMissingFile_ReturnsFalse()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir = Path.Combine(_tempDirectory, "codebase");
        await _writer.WriteAllAsync(result, outputDir);

        // Delete a file
        File.Delete(Path.Combine(outputDir, "stack.json"));

        // Act
        var (isValid, mismatches) = await Agents.Execution.Brownfield.CodebaseScanner.CodebaseIntelligencePackWriter.VerifyDeterminismAsync(outputDir);

        // Assert
        isValid.Should().BeFalse();
        mismatches.Should().Contain(m => m.Contains("stack.json") && m.Contains("missing"));
    }

    [Fact]
    public async Task VerifyDeterminismAsync_WithMissingManifest_ReturnsFalse()
    {
        // Arrange
        var outputDir = Path.Combine(_tempDirectory, "empty");
        Directory.CreateDirectory(outputDir);

        // Act
        var (isValid, mismatches) = await Agents.Execution.Brownfield.CodebaseScanner.CodebaseIntelligencePackWriter.VerifyDeterminismAsync(outputDir);

        // Assert
        isValid.Should().BeFalse();
        mismatches.Should().Contain("hash-manifest.json not found");
    }

    [Fact]
    public async Task WriteAllAsync_HashManifestIsDeterministic()
    {
        // Arrange
        var request = new Agents.Execution.Brownfield.CodebaseScanner.CodebaseScanRequest
        {
            RepositoryPath = GetRepositoryRoot()
        };
        var result = await _scanner.ScanAsync(request);
        var outputDir1 = Path.Combine(_tempDirectory, "codebase1");
        var outputDir2 = Path.Combine(_tempDirectory, "codebase2");

        // Act - write twice
        await _writer.WriteAllAsync(result, outputDir1);
        await _writer.WriteAllAsync(result, outputDir2);

        // Assert - verify both manifests have same structure (timestamps will differ)
        var manifest1 = await File.ReadAllTextAsync(Path.Combine(outputDir1, "hash-manifest.json"));
        var manifest2 = await File.ReadAllTextAsync(Path.Combine(outputDir2, "hash-manifest.json"));

        var doc1 = JsonDocument.Parse(manifest1);
        var doc2 = JsonDocument.Parse(manifest2);

        // Same files should be listed
        var files1 = doc1.RootElement.GetProperty("files").EnumerateObject().Select(p => p.Name).OrderBy(n => n);
        var files2 = doc2.RootElement.GetProperty("files").EnumerateObject().Select(p => p.Name).OrderBy(n => n);
        files1.Should().Equal(files2);

        // Same algorithm
        doc1.RootElement.GetProperty("algorithm").GetString().Should().Be(doc2.RootElement.GetProperty("algorithm").GetString());

        // Both have same schema version
        doc1.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(doc2.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    private static string GetRepositoryRoot()
    {
        // Start from current directory and walk up to find repo root
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);

        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "nirmata.slnx")) ||
                File.Exists(Path.Combine(dir.FullName, "nirmata.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        // Fallback to current directory
        return currentDir;
    }
}
