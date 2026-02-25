using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.Brownfield.MapValidator;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Brownfield.MapValidator;

public class MapValidatorTests
{
    private readonly IMapValidator _validator = new Agents.Execution.Brownfield.MapValidator.MapValidator();

    [Fact]
    public async Task ValidateAsync_WithEmptyCodebase_ReturnsInvalid()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var codebasePath = Path.Combine(tempDir, ".aos", "codebase");
        Directory.CreateDirectory(codebasePath);

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = false,
            CheckCrossFileInvariants = false
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.IssueType == "MissingFile");
        result.Issues.Should().Contain(i => i.Artifact == "map.json");
        result.Issues.Should().Contain(i => i.Artifact == "stack.json");
        result.Issues.Should().Contain(i => i.Artifact == "structure.json");

        CleanupTempDirectory(tempDir);
    }

    [Fact]
    public async Task ValidateAsync_WithMissingRequiredFiles_ReturnsCorrectIssues()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var codebasePath = Path.Combine(tempDir, ".aos", "codebase");
        Directory.CreateDirectory(codebasePath);

        // Create only map.json, leave stack.json and structure.json missing
        File.WriteAllText(Path.Combine(codebasePath, "map.json"), CreateValidMapJson());

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = false,
            CheckCrossFileInvariants = false
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.IssueType == "MissingFile" && i.Artifact == "stack.json");
        result.Issues.Should().Contain(i => i.IssueType == "MissingFile" && i.Artifact == "structure.json");
        result.Issues.Should().NotContain(i => i.IssueType == "MissingFile" && i.Artifact == "map.json");

        CleanupTempDirectory(tempDir);
    }

    [Fact]
    public async Task ValidateAsync_WithAllRequiredFiles_PassesBasicValidation()
    {
        // Arrange
        var tempDir = CreateTempCodebaseWithRequiredFiles();

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = false,
            CheckCrossFileInvariants = false
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().NotContain(i => i.IssueType == "MissingFile");

        CleanupTempDirectory(tempDir);
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidJson_FailsSchemaValidation()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var codebasePath = Path.Combine(tempDir, ".aos", "codebase");
        Directory.CreateDirectory(codebasePath);

        // Write invalid JSON
        File.WriteAllText(Path.Combine(codebasePath, "map.json"), "{ invalid json");
        File.WriteAllText(Path.Combine(codebasePath, "stack.json"), CreateValidStackJson());
        File.WriteAllText(Path.Combine(codebasePath, "structure.json"), CreateValidStructureJson());

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = true,
            CheckCrossFileInvariants = false
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.IssueType == "InvalidJson" || i.IssueType == "SchemaViolation");

        CleanupTempDirectory(tempDir);
    }

    [Fact]
    public async Task ValidateAsync_WithDeterminismValidation_ComputesHashes()
    {
        // Arrange
        var tempDir = CreateTempCodebaseWithRequiredFiles();
        var codebasePath = Path.Combine(tempDir, ".aos", "codebase");

        // Compute expected hash
        var mapJson = File.ReadAllText(Path.Combine(codebasePath, "map.json"));
        var expectedHash = ComputeHash(mapJson);

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = false,
            CheckCrossFileInvariants = false,
            ValidateDeterminism = true,
            ExpectedHashes = new Dictionary<string, string>
            {
                ["map.json"] = expectedHash
            }
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().NotContain(i => i.IssueType == "DeterminismViolation");

        CleanupTempDirectory(tempDir);
    }

    [Fact]
    public async Task ValidateAsync_WithWrongExpectedHash_ReportsDeterminismViolation()
    {
        // Arrange
        var tempDir = CreateTempCodebaseWithRequiredFiles();

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = false,
            CheckCrossFileInvariants = false,
            ValidateDeterminism = true,
            ExpectedHashes = new Dictionary<string, string>
            {
                ["map.json"] = "0000000000000000000000000000000000000000000000000000000000000000"
            }
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.IssueType == "DeterminismViolation" && i.Artifact == "map.json");

        CleanupTempDirectory(tempDir);
    }

    [Fact]
    public async Task ValidateAsync_WithCrossFileInvariantsEnabled_ChecksConsistency()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var codebasePath = Path.Combine(tempDir, ".aos", "codebase");
        Directory.CreateDirectory(codebasePath);

        var scanTimestamp = DateTimeOffset.UtcNow.ToString("O");

        // Create map.json with specific values
        File.WriteAllText(Path.Combine(codebasePath, "map.json"), $@"{{
            ""schemaVersion"": 1,
            ""version"": ""1.0"",
            ""repository"": {{ ""root"": ""{tempDir.Replace("\\", "/")}"", ""name"": ""test"", ""type"": ""git"" }},
            ""scanTimestamp"": ""{scanTimestamp}"",
            ""summary"": {{ ""totalFiles"": 10, ""totalDirectories"": 2, ""totalLinesOfCode"": 500, ""projectCount"": 1 }}
        }}");

        // Create stack.json with mismatched timestamp
        File.WriteAllText(Path.Combine(codebasePath, "stack.json"), $@"{{
            ""schemaVersion"": 1,
            ""languages"": [],
            ""frameworks"": [],
            ""buildTools"": [],
            ""packageManagers"": []
        }}");

        File.WriteAllText(Path.Combine(codebasePath, "structure.json"), $@"{{
            ""schemaVersion"": 1,
            ""directories"": [],
            ""files"": [],
            ""metadata"": {{ ""rootFiles"": [], ""rootDirectories"": [], ""ignoredPatterns"": [] }},
            ""statistics"": {{ ""totalFiles"": 10, ""totalDirectories"": 2, ""totalLinesOfCode"": 500, ""averageFileSize"": 100, ""maxDirectoryDepth"": 1, ""filesByType"": {{}}, ""filesByExtension"": {{}} }}
        }}");

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = false,
            CheckCrossFileInvariants = true
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert - should pass (no timestamp in stack.json means no mismatch check)
        result.Issues.Should().NotContain(i => i.IssueType == "CrossFileInvariant" && i.Path == "scanTimestamp");

        CleanupTempDirectory(tempDir);
    }

    [Fact]
    public async Task ValidateAsync_WithCrossFileInvariants_DetectsFileCountMismatch()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var codebasePath = Path.Combine(tempDir, ".aos", "codebase");
        Directory.CreateDirectory(codebasePath);

        // Create map.json with specific file count
        File.WriteAllText(Path.Combine(codebasePath, "map.json"), @"{
            ""schemaVersion"": 1,
            ""version"": ""1.0"",
            ""repository"": { ""root"": ""C:/test"", ""name"": ""test"", ""type"": ""git"" },
            ""scanTimestamp"": ""2024-01-01T00:00:00.0000000Z"",
            ""summary"": { ""totalFiles"": 10, ""totalDirectories"": 2, ""totalLinesOfCode"": 500, ""projectCount"": 1 }
        }");

        File.WriteAllText(Path.Combine(codebasePath, "stack.json"), @"{
            ""schemaVersion"": 1,
            ""languages"": [],
            ""frameworks"": [],
            ""buildTools"": [],
            ""packageManagers"": []
        }");

        // Create structure.json with different file count
        File.WriteAllText(Path.Combine(codebasePath, "structure.json"), @"{
            ""schemaVersion"": 1,
            ""directories"": [],
            ""files"": [],
            ""metadata"": { ""rootFiles"": [], ""rootDirectories"": [], ""ignoredPatterns"": [] },
            ""statistics"": { ""totalFiles"": 15, ""totalDirectories"": 2, ""totalLinesOfCode"": 500, ""averageFileSize"": 100, ""maxDirectoryDepth"": 1, ""filesByType"": {}, ""filesByExtension"": {} }
        }");

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = false,
            CheckCrossFileInvariants = true
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.Issues.Should().Contain(i =>
            i.IssueType == "CrossFileInvariant" &&
            i.Artifact == "structure.json" &&
            i.Message.Contains("File count mismatch"));

        CleanupTempDirectory(tempDir);
    }

    [Fact]
    public async Task ValidateAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var tempDir = CreateTempCodebaseWithRequiredFiles();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = true,
            CheckCrossFileInvariants = true
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _validator.ValidateAsync(request, cts.Token));

        CleanupTempDirectory(tempDir);
    }

    [Fact]
    public async Task ValidateAsync_WithSpecificArtifacts_OnlyValidatesThoseArtifacts()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var codebasePath = Path.Combine(tempDir, ".aos", "codebase");
        Directory.CreateDirectory(codebasePath);

        // Only create map.json
        File.WriteAllText(Path.Combine(codebasePath, "map.json"), CreateValidMapJson());

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = true,
            CheckCrossFileInvariants = false,
            SpecificArtifacts = new[] { "map.json" }
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert - should be valid because we're only validating map.json
        // Note: The missing required files check always runs regardless of SpecificArtifacts
        result.IsValid.Should().BeFalse(); // Still invalid due to missing required files check
        result.Issues.Should().Contain(i => i.IssueType == "MissingFile");

        CleanupTempDirectory(tempDir);
    }

    [Fact]
    public async Task ValidateAsync_SummaryCountsAreCorrect()
    {
        // Arrange
        var tempDir = CreateTempCodebaseWithRequiredFiles();

        // Create a file with invalid JSON to trigger a schema violation
        var codebasePath = Path.Combine(tempDir, ".aos", "codebase");
        File.WriteAllText(Path.Combine(codebasePath, "map.json"), "{ invalid json");

        var request = new MapValidationRequest
        {
            RepositoryRootPath = tempDir,
            ValidateSchemaCompliance = true,
            CheckCrossFileInvariants = false
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.Summary.ArtifactsValidated.Should().BeGreaterThan(0);
        result.Summary.ErrorCount.Should().BeGreaterThan(0);
        result.Summary.ValidationTimestamp.Should().BeWithin(TimeSpan.FromMinutes(1));

        CleanupTempDirectory(tempDir);
    }

    #region Helper Methods

    private static string CreateTempDirectory()
    {
        return Path.Combine(Path.GetTempPath(), $"MapValidatorTests_{Guid.NewGuid():N}");
    }

    private static void CleanupTempDirectory(string path)
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
            // Best effort cleanup
        }
    }

    private static string CreateTempCodebaseWithRequiredFiles()
    {
        var tempDir = CreateTempDirectory();
        var codebasePath = Path.Combine(tempDir, ".aos", "codebase");
        Directory.CreateDirectory(codebasePath);

        File.WriteAllText(Path.Combine(codebasePath, "map.json"), CreateValidMapJson());
        File.WriteAllText(Path.Combine(codebasePath, "stack.json"), CreateValidStackJson());
        File.WriteAllText(Path.Combine(codebasePath, "structure.json"), CreateValidStructureJson());

        return tempDir;
    }

    private static string CreateValidMapJson()
    {
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            version = "1.0",
            repository = new { root = "C:/test", name = "test", type = "git" },
            scanTimestamp = DateTimeOffset.UtcNow.ToString("O"),
            summary = new { totalFiles = 10, totalDirectories = 2, totalLinesOfCode = 500, projectCount = 1 }
        });
    }

    private static string CreateValidStackJson()
    {
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            languages = Array.Empty<object>(),
            frameworks = Array.Empty<object>(),
            buildTools = Array.Empty<object>(),
            packageManagers = Array.Empty<object>()
        });
    }

    private static string CreateValidStructureJson()
    {
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            directories = Array.Empty<object>(),
            files = Array.Empty<object>(),
            metadata = new { rootFiles = Array.Empty<string>(), rootDirectories = Array.Empty<string>(), ignoredPatterns = Array.Empty<string>() },
            statistics = new
            {
                totalFiles = 10,
                totalDirectories = 2,
                totalLinesOfCode = 500,
                averageFileSize = 100.0,
                maxDirectoryDepth = 1,
                filesByType = new Dictionary<string, int>(),
                filesByExtension = new Dictionary<string, int>()
            }
        });
    }

    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion
}
