using FluentAssertions;
using Gmsd.Agents.Execution.Validation;
using Gmsd.Agents.Tests.Helpers;
using System.Text.Json;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Validation;

/// <summary>
/// Integration tests for reader validation with diagnostics (Task 9.3).
/// Tests validate that readers correctly validate artifacts and generate diagnostic artifacts on failure.
/// </summary>
public class ReaderValidationIntegrationTests
{
    [Fact]
    public void ReaderValidation_TaskPlan_ValidArtifact_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-READER-001/plan.json";

        var validPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-READER-001",
              "title": "Valid Task",
              "description": "Valid task description",
              "fileScopes": [
                { "path": "src/File.cs" }
              ],
              "steps": [],
              "verificationSteps": []
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            validPayload,
            aosRoot,
            "task-executor-reader");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ReaderValidation_TaskPlan_InvalidArtifact_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-READER-002/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-READER-002",
              "title": "Invalid Task",
              "fileScopes": "should-be-array"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "task-executor-reader");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.DiagnosticPath!).Should().BeTrue();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ReaderValidation_PhasePlan_ValidArtifact_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/phases/PH-READER-001/plan.json";

        var validPayload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-READER-001",
              "phaseId": "PH-READER-001",
              "tasks": [
                {
                  "id": "TSK-001",
                  "title": "Phase Task",
                  "description": "Task in phase",
                  "fileScopes": [
                    { "path": "src/File.cs" }
                  ],
                  "verificationSteps": ["Verify"]
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidatePhasePlan(
            artifactPath,
            validPayload,
            aosRoot,
            "phase-reader");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
    }

    [Fact]
    public void ReaderValidation_PhasePlan_InvalidArtifact_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/phases/PH-READER-002/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-READER-002",
              "tasks": "should-be-array"
            }
            """;

        var result = ArtifactContractValidator.ValidatePhasePlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "phase-reader");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.DiagnosticPath!).Should().BeTrue();
    }

    [Fact]
    public void ReaderValidation_FixPlan_ValidArtifact_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-FIX-READER-001/plan.json";

        var validPayload = """
            {
              "schemaVersion": 1,
              "fixes": [
                {
                  "issueId": "ISS-001",
                  "description": "Fix issue",
                  "proposedChanges": [
                    {
                      "file": "src/Fix.cs",
                      "changeDescription": "Apply fix"
                    }
                  ],
                  "tests": ["Verify fix"]
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateFixPlan(
            artifactPath,
            validPayload,
            aosRoot,
            "fix-planner-reader");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
    }

    [Fact]
    public void ReaderValidation_FixPlan_InvalidArtifact_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-FIX-READER-002/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "fixes": "should-be-array"
            }
            """;

        var result = ArtifactContractValidator.ValidateFixPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "fix-planner-reader");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ReaderValidation_VerifierOutput_ValidArtifact_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/evidence/runs/RUN-READER-001/uat-results.json";

        var validPayload = """
            {
              "schemaVersion": 1,
              "isPassed": true,
              "runId": "RUN-READER-001",
              "checks": [
                {
                  "criterionId": "AC-001",
                  "passed": true,
                  "isRequired": true,
                  "message": "Passed"
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierOutput(
            artifactPath,
            validPayload,
            aosRoot,
            "verifier-reader");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
    }

    [Fact]
    public void ReaderValidation_VerifierOutput_InvalidArtifact_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/evidence/runs/RUN-READER-002/uat-results.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "isPassed": "not-boolean",
              "runId": "RUN-READER-002",
              "checks": []
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierOutput(
            artifactPath,
            invalidPayload,
            aosRoot,
            "verifier-reader");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ReaderValidation_DiagnosticContainsReadBoundary()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-READER-003/plan.json";
        var readBoundary = "custom-reader-boundary";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-READER-003"
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
        var recordedBoundary = context.GetProperty("readBoundary").GetString();

        recordedBoundary.Should().Be(readBoundary);
    }

    [Fact]
    public void ReaderValidation_DiagnosticStoredInPhaseDirectory()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-READER-004/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-READER-004"
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
    public void ReaderValidation_MultipleErrors_AllIncludedInDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-READER-005/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-READER-005",
              "fileScopes": "invalid",
              "steps": "invalid"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-reader");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var errors = root.GetProperty("validationErrors");

        errors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void ReaderValidation_VerifierInput_ValidArtifact_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/evidence/runs/RUN-READER-003/verifier-input.json";

        var validPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-001",
              "runId": "RUN-READER-003",
              "acceptanceCriteria": [
                {
                  "criterionId": "AC-001",
                  "description": "Feature works",
                  "expectedOutcome": "Feature works correctly"
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierInput(
            artifactPath,
            validPayload,
            aosRoot,
            "verifier-reader");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
    }

    [Fact]
    public void ReaderValidation_VerifierInput_InvalidArtifact_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/evidence/runs/RUN-READER-004/verifier-input.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-001",
              "acceptanceCriteria": "should-be-array"
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierInput(
            artifactPath,
            invalidPayload,
            aosRoot,
            "verifier-reader");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
    }
}
