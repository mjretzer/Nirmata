using System;
using System.IO;
using System.Text.Json;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests.Engine.Workspace;

public class AosWorkspaceBootstrapperRepairTests : IDisposable
{
    private readonly string _testWorkspaceRoot;

    public AosWorkspaceBootstrapperRepairTests()
    {
        _testWorkspaceRoot = Path.Combine(Path.GetTempPath(), $"aos-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testWorkspaceRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testWorkspaceRoot))
        {
            Directory.Delete(_testWorkspaceRoot, recursive: true);
        }
    }

    [Fact]
    public void Repair_WithValidWorkspace_ReturnsSuccess()
    {
        // Arrange
        AosWorkspaceBootstrapper.EnsureInitialized(_testWorkspaceRoot);

        // Act
        var result = AosWorkspaceBootstrapper.Repair(_testWorkspaceRoot);

        // Assert
        Assert.Equal(AosWorkspaceRepairOutcome.Success, result.Outcome);
        Assert.NotNull(result.Duration);
        Assert.True(result.Duration.Value.TotalMilliseconds >= 0);
    }

    [Fact]
    public void Repair_RebuildIndexFiles_CreatesValidIndexes()
    {
        // Arrange
        AosWorkspaceBootstrapper.EnsureInitialized(_testWorkspaceRoot);
        var aosRootPath = Path.Combine(_testWorkspaceRoot, ".aos");

        // Create a sample task file
        var tasksDir = Path.Combine(aosRootPath, "spec", "tasks");
        var sampleTaskPath = Path.Combine(tasksDir, "sample-task.json");
        var sampleTask = new { id = "task-001", name = "Sample Task", schemaVersion = 1 };
        File.WriteAllText(sampleTaskPath, JsonSerializer.Serialize(sampleTask));

        // Act
        var result = AosWorkspaceBootstrapper.Repair(_testWorkspaceRoot);

        // Assert
        Assert.Equal(AosWorkspaceRepairOutcome.Success, result.Outcome);
        var indexPath = Path.Combine(tasksDir, "index.json");
        Assert.True(File.Exists(indexPath));

        using var stream = File.OpenRead(indexPath);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }

    [Fact]
    public void Repair_WithMissingFiles_SeedsBaselineArtifacts()
    {
        // Arrange
        AosWorkspaceBootstrapper.EnsureInitialized(_testWorkspaceRoot);
        var aosRootPath = Path.Combine(_testWorkspaceRoot, ".aos");
        var projectJsonPath = Path.Combine(aosRootPath, "spec", "project.json");

        // Delete a required file
        File.Delete(projectJsonPath);
        Assert.False(File.Exists(projectJsonPath));

        // Act
        var result = AosWorkspaceBootstrapper.Repair(_testWorkspaceRoot);

        // Assert
        Assert.Equal(AosWorkspaceRepairOutcome.Success, result.Outcome);
        Assert.True(File.Exists(projectJsonPath));
    }

    [Fact]
    public void Repair_ValidatesArtifactSchemas()
    {
        // Arrange
        AosWorkspaceBootstrapper.EnsureInitialized(_testWorkspaceRoot);
        var aosRootPath = Path.Combine(_testWorkspaceRoot, ".aos");

        // Create an artifact without schemaVersion
        var invalidArtifactPath = Path.Combine(aosRootPath, "spec", "invalid-artifact.json");
        File.WriteAllText(invalidArtifactPath, JsonSerializer.Serialize(new { id = "invalid" }));

        // Act
        var result = AosWorkspaceBootstrapper.Repair(_testWorkspaceRoot);

        // Assert
        Assert.Equal(AosWorkspaceRepairOutcome.Success, result.Outcome);
        Assert.NotEmpty(result.SchemaValidationIssues);
        Assert.Contains("Missing schemaVersion", result.SchemaValidationIssues[0]);
    }

    [Fact]
    public void CheckCompliance_WithValidWorkspace_ReturnsCompliant()
    {
        // Arrange
        AosWorkspaceBootstrapper.EnsureInitialized(_testWorkspaceRoot);

        // Act
        var report = AosWorkspaceBootstrapper.CheckCompliance(_testWorkspaceRoot);

        // Assert
        Assert.True(report.IsCompliant);
        Assert.Empty(report.MissingDirectories);
        Assert.Empty(report.MissingFiles);
    }
}
