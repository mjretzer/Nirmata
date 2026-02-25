using FluentAssertions;
using Gmsd.Agents.Execution.Validation;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Agents.Tests.Helpers;
using System.Text.Json;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Validation;

/// <summary>
/// Unit tests for diagnostic artifact generation (Task 9.2).
/// Tests validate that diagnostic artifacts are correctly generated with proper structure,
/// repair suggestions, and metadata.
/// </summary>
public class DiagnosticArtifactGenerationTests
{
    [Fact]
    public void ValidateTaskPlan_OnFailure_GeneratesDiagnosticArtifact()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-001/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-001",
              "title": "Invalid Task",
              "fileScopes": "should-be-array"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.DiagnosticPath!).Should().BeTrue();
    }

    [Fact]
    public void DiagnosticArtifact_ContainsRequiredFields()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-002/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-002",
              "title": "Test"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;

        root.TryGetProperty("schemaVersion", out _).Should().BeTrue();
        root.TryGetProperty("schemaId", out _).Should().BeTrue();
        root.TryGetProperty("artifactPath", out _).Should().BeTrue();
        root.TryGetProperty("failedSchemaId", out _).Should().BeTrue();
        root.TryGetProperty("failedSchemaVersion", out _).Should().BeTrue();
        root.TryGetProperty("timestamp", out _).Should().BeTrue();
        root.TryGetProperty("phase", out _).Should().BeTrue();
        root.TryGetProperty("context", out _).Should().BeTrue();
        root.TryGetProperty("validationErrors", out _).Should().BeTrue();
        root.TryGetProperty("repairSuggestions", out _).Should().BeTrue();
    }

    [Fact]
    public void DiagnosticArtifact_IncludesValidationErrors()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-003/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-003",
              "fileScopes": null
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var errors = root.GetProperty("validationErrors");

        errors.ValueKind.Should().Be(JsonValueKind.Array);
        errors.GetArrayLength().Should().BeGreaterThan(0);

        var firstError = errors[0];
        firstError.TryGetProperty("path", out _).Should().BeTrue();
        firstError.TryGetProperty("message", out _).Should().BeTrue();
    }

    [Fact]
    public void DiagnosticArtifact_IncludesRepairSuggestions()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-004/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-004",
              "title": "Task",
              "fileScopes": "invalid"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var suggestions = root.GetProperty("repairSuggestions");

        suggestions.ValueKind.Should().Be(JsonValueKind.Array);
        suggestions.GetArrayLength().Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void DiagnosticArtifact_StoredInCorrectLocation()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-005/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-005"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        var diagnosticPath = result.DiagnosticPath!;
        diagnosticPath.Should().Contain(".aos/diagnostics/task-execution/");
        diagnosticPath.Should().EndWith(".diagnostic.json");
    }

    [Fact]
    public void DiagnosticArtifact_IncludesPhaseInformation()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-006/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-006"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var phase = root.GetProperty("phase").GetString();

        phase.Should().Be("task-execution");
    }

    [Fact]
    public void DiagnosticArtifact_IncludesContextInformation()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-007/plan.json";
        var readBoundary = "custom-reader-boundary";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-007"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            readBoundary);

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var context = root.GetProperty("context");

        context.TryGetProperty("readBoundary", out var boundary).Should().BeTrue();
        boundary.GetString().Should().Be(readBoundary);
    }

    [Fact]
    public void DiagnosticArtifact_IncludesTimestamp()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-008/plan.json";
        var beforeValidation = DateTimeOffset.UtcNow;

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-008"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        var afterValidation = DateTimeOffset.UtcNow;

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var timestamp = DateTimeOffset.Parse(root.GetProperty("timestamp").GetString()!);

        timestamp.Should().BeOnOrAfter(beforeValidation);
        timestamp.Should().BeOnOrBefore(afterValidation);
    }

    [Fact]
    public void DiagnosticArtifact_ForPhasePlan_IncludesCorrectPhase()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/phases/PH-DIAG-001/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-001"
            }
            """;

        var result = ArtifactContractValidator.ValidatePhasePlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var phase = root.GetProperty("phase").GetString();

        phase.Should().Be("phase-planning");
    }

    [Fact]
    public void DiagnosticArtifact_ForFixPlan_IncludesCorrectPhase()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-FIX-DIAG-001/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "fixes": "invalid"
            }
            """;

        var result = ArtifactContractValidator.ValidateFixPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var phase = root.GetProperty("phase").GetString();

        phase.Should().Be("fix-planning");
    }

    [Fact]
    public void DiagnosticArtifact_ForVerifierOutput_IncludesCorrectPhase()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/evidence/runs/RUN-DIAG-001/uat-results.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "isPassed": "invalid",
              "runId": "RUN-001",
              "checks": []
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierOutput(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var phase = root.GetProperty("phase").GetString();

        phase.Should().Be("uat-verification");
    }

    [Fact]
    public void DiagnosticArtifact_IncludesFailedSchemaId()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-009/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-009"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var failedSchemaId = root.GetProperty("failedSchemaId").GetString();

        failedSchemaId.Should().Be("gmsd:aos:schema:task-plan:v1");
    }

    [Fact]
    public void DiagnosticArtifact_IncludesFailedSchemaVersion()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-010/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-010"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var failedSchemaVersion = root.GetProperty("failedSchemaVersion").GetInt32();

        failedSchemaVersion.Should().Be(1);
    }

    [Fact]
    public void DiagnosticArtifact_IncludesArtifactPath()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-DIAG-011/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-011"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var recordedPath = root.GetProperty("artifactPath").GetString();

        recordedPath.Should().Be(artifactPath);
    }
}
