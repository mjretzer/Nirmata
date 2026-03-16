using System.Text.Json;
using nirmata.Agents.Execution.Migration;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Migration;

public sealed class SchemaMigratorTests
{
    [Fact]
    public void DetectFormat_WithNewFormatTaskPlan_ReturnsNewFormat()
    {
        // Arrange
        var json = """
        {
          "schemaVersion": 1,
          "schemaId": "nirmata:aos:schema:task-plan:v1",
          "taskId": "TSK-0001",
          "fileScopes": [{"path": "src/"}]
        }
        """;
        var path = ".aos/spec/tasks/TSK-0001/plan.json";

        // Act
        var result = ArtifactFormatDetector.DetectFormat(path, json);

        // Assert
        Assert.True(result.IsNewFormat);
        Assert.Equal(ArtifactType.TaskPlan, result.ArtifactType);
        Assert.Equal(1, result.SchemaVersion);
        Assert.False(result.RequiresMigration);
    }

    [Fact]
    public void DetectFormat_WithOldFormatTaskPlan_ReturnsOldFormat()
    {
        // Arrange
        var json = """
        {
          "taskId": "TSK-0001",
          "fileScopes": ["src/", "tests/"]
        }
        """;
        var path = ".aos/spec/tasks/TSK-0001/plan.json";

        // Act
        var result = ArtifactFormatDetector.DetectFormat(path, json);

        // Assert
        Assert.False(result.IsNewFormat);
        Assert.Equal(ArtifactType.TaskPlan, result.ArtifactType);
        Assert.True(result.RequiresMigration);
    }

    [Fact]
    public void TransformTaskPlan_WithStringFileScopes_TransformsToObjectFormat()
    {
        // Arrange
        var oldJson = """
        {
          "taskId": "TSK-0001",
          "fileScopes": ["src/", "tests/"],
          "verificationSteps": []
        }
        """;

        // Act
        var newJson = ArtifactTransformer.TransformToNewFormat(oldJson, ArtifactType.TaskPlan);

        // Assert
        using var doc = JsonDocument.Parse(newJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("schemaVersion", out var schemaVersion));
        Assert.Equal(1, schemaVersion.GetInt32());

        Assert.True(root.TryGetProperty("schemaId", out var schemaId));
        Assert.Equal("nirmata:aos:schema:task-plan:v1", schemaId.GetString());

        Assert.True(root.TryGetProperty("fileScopes", out var fileScopes));
        Assert.Equal(JsonValueKind.Array, fileScopes.ValueKind);

        var scopes = fileScopes.EnumerateArray().ToList();
        Assert.Equal(2, scopes.Count);
        Assert.Equal("src/", scopes[0].GetProperty("path").GetString());
        Assert.Equal("tests/", scopes[1].GetProperty("path").GetString());
    }

    [Fact]
    public void TransformTaskPlan_WithObjectFileScopes_PreservesFormat()
    {
        // Arrange
        var oldJson = """
        {
          "taskId": "TSK-0001",
          "fileScopes": [{"path": "src/"}, {"path": "tests/"}],
          "verificationSteps": []
        }
        """;

        // Act
        var newJson = ArtifactTransformer.TransformToNewFormat(oldJson, ArtifactType.TaskPlan);

        // Assert
        using var doc = JsonDocument.Parse(newJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("fileScopes", out var fileScopes));
        var scopes = fileScopes.EnumerateArray().ToList();
        Assert.Equal(2, scopes.Count);
        Assert.Equal("src/", scopes[0].GetProperty("path").GetString());
        Assert.Equal("tests/", scopes[1].GetProperty("path").GetString());
    }

    [Fact]
    public void TransformVerifierOutput_AddsSchemaFields()
    {
        // Arrange
        var oldJson = """
        {
          "taskId": "TSK-0001",
          "runId": "RUN-0001",
          "status": "passed",
          "checks": []
        }
        """;

        // Act
        var newJson = ArtifactTransformer.TransformToNewFormat(oldJson, ArtifactType.VerifierOutput);

        // Assert
        using var doc = JsonDocument.Parse(newJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("schemaVersion", out var schemaVersion));
        Assert.Equal(1, schemaVersion.GetInt32());

        Assert.True(root.TryGetProperty("schemaId", out var schemaId));
        Assert.Equal("nirmata:aos:schema:verifier-output:v1", schemaId.GetString());

        Assert.True(root.TryGetProperty("status", out var status));
        Assert.Equal("passed", status.GetString());
    }

    [Fact]
    public void TransformFixPlan_AddsSchemaFields()
    {
        // Arrange
        var oldJson = """
        {
          "taskId": "TSK-0001",
          "fixes": [],
          "verificationSteps": []
        }
        """;

        // Act
        var newJson = ArtifactTransformer.TransformToNewFormat(oldJson, ArtifactType.FixPlan);

        // Assert
        using var doc = JsonDocument.Parse(newJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("schemaId", out var schemaId));
        Assert.Equal("nirmata:aos:schema:fix-plan:v1", schemaId.GetString());
    }

    [Fact]
    public void DetectFormat_WithDiagnosticArtifact_DetectsDiagnosticType()
    {
        // Arrange
        var json = """
        {
          "schemaVersion": 1,
          "schemaId": "nirmata:aos:schema:diagnostic:v1",
          "artifactPath": ".aos/spec/tasks/TSK-0001/plan.json",
          "failedSchemaId": "nirmata:aos:schema:task-plan:v1",
          "validationErrors": []
        }
        """;
        var path = ".aos/diagnostics/phase-planning/TSK-0001.diagnostic.json";

        // Act
        var result = ArtifactFormatDetector.DetectFormat(path, json);

        // Assert
        Assert.Equal(ArtifactType.Diagnostic, result.ArtifactType);
        Assert.True(result.IsNewFormat);
    }

    [Fact]
    public void DetectFormat_WithInvalidJson_ReturnsUnknown()
    {
        // Arrange
        var json = "{ invalid json }";
        var path = ".aos/spec/tasks/TSK-0001/plan.json";

        // Act
        var result = ArtifactFormatDetector.DetectFormat(path, json);

        // Assert
        Assert.Equal(ArtifactType.Unknown, result.ArtifactType);
        Assert.False(result.IsNewFormat);
    }

    [Fact]
    public void TransformPhasePlan_WithStringFileScopes_TransformsToObjectFormat()
    {
        // Arrange
        var oldJson = """
        {
          "tasks": [],
          "fileScopes": ["src/", "tests/"],
          "verificationSteps": []
        }
        """;

        // Act
        var newJson = ArtifactTransformer.TransformToNewFormat(oldJson, ArtifactType.PhasePlan);

        // Assert
        using var doc = JsonDocument.Parse(newJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("schemaId", out var schemaId));
        Assert.Equal("nirmata:aos:schema:phase-plan:v1", schemaId.GetString());

        Assert.True(root.TryGetProperty("fileScopes", out var fileScopes));
        var scopes = fileScopes.EnumerateArray().ToList();
        Assert.Equal(2, scopes.Count);
    }

    [Fact]
    public void TransformVerifierInput_TransformsOldCriteriaField()
    {
        // Arrange
        var oldJson = """
        {
          "taskId": "TSK-0001",
          "runId": "RUN-0001",
          "criteria": [{"id": "C1", "description": "Test"}],
          "fileScopes": []
        }
        """;

        // Act
        var newJson = ArtifactTransformer.TransformToNewFormat(oldJson, ArtifactType.VerifierInput);

        // Assert
        using var doc = JsonDocument.Parse(newJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("acceptanceCriteria", out var criteria));
        Assert.Equal(JsonValueKind.Array, criteria.ValueKind);
    }
}
