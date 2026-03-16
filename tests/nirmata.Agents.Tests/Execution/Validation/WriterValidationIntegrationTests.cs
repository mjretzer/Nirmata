using FluentAssertions;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Tests.Helpers;
using System.Text.Json;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Validation;

/// <summary>
/// Integration tests for writer validation with diagnostics (Task 9.4).
/// Tests validate that writers correctly validate artifacts before persistence and generate diagnostic artifacts on failure.
/// </summary>
public class WriterValidationIntegrationTests
{
    [Fact]
    public void WriterValidation_TaskPlan_ValidArtifact_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-WRITER-001/plan.json";

        var validPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-WRITER-001",
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
            "task-executor-writer");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
    }

    [Fact]
    public void WriterValidation_TaskPlan_InvalidArtifact_GeneratesDiagnosticBeforePersistence()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-WRITER-002/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-WRITER-002",
              "title": "Invalid Task",
              "fileScopes": "should-be-array"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "task-executor-writer");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.DiagnosticPath!).Should().BeTrue();
    }

    [Fact]
    public void WriterValidation_PhasePlan_ValidArtifact_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/phases/PH-WRITER-001/plan.json";

        var validPayload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-WRITER-001",
              "phaseId": "PH-WRITER-001",
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
            "phase-planner-writer");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WriterValidation_PhasePlan_InvalidArtifact_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/phases/PH-WRITER-002/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-WRITER-002",
              "phaseId": "PH-WRITER-002",
              "tasks": "should-be-array"
            }
            """;

        var result = ArtifactContractValidator.ValidatePhasePlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "phase-planner-writer");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.DiagnosticPath!).Should().BeTrue();
    }

    [Fact]
    public void WriterValidation_FixPlan_ValidArtifact_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-FIX-WRITER-001/plan.json";

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
            "fix-planner-writer");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WriterValidation_FixPlan_InvalidArtifact_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-FIX-WRITER-002/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "fixes": []
            }
            """;

        var result = ArtifactContractValidator.ValidateFixPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "fix-planner-writer");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void WriterValidation_VerifierOutput_ValidArtifact_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/evidence/runs/RUN-WRITER-001/uat-results.json";

        var validPayload = """
            {
              "schemaVersion": 1,
              "isPassed": true,
              "runId": "RUN-WRITER-001",
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
            "verifier-writer");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WriterValidation_VerifierOutput_InvalidArtifact_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/evidence/runs/RUN-WRITER-002/uat-results.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "isPassed": true,
              "runId": "RUN-WRITER-002",
              "checks": "should-be-array"
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierOutput(
            artifactPath,
            invalidPayload,
            aosRoot,
            "verifier-writer");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void WriterValidation_DiagnosticIncludesWriterBoundary()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-WRITER-003/plan.json";
        var writerBoundary = "custom-writer-boundary";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-WRITER-003"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            writerBoundary);

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var context = root.GetProperty("context");
        var recordedBoundary = context.GetProperty("readBoundary").GetString();

        recordedBoundary.Should().Be(writerBoundary);
    }

    [Fact]
    public void WriterValidation_DiagnosticStoredInCorrectPhaseDirectory()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-WRITER-004/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-WRITER-004"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-writer");

        var diagnosticPath = result.DiagnosticPath!;
        diagnosticPath.Should().Contain(".aos/diagnostics/task-execution/");
        diagnosticPath.Should().EndWith(".diagnostic.json");
    }

    [Fact]
    public void WriterValidation_DiagnosticIncludesRepairSuggestions()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-WRITER-005/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-WRITER-005",
              "title": "Task",
              "fileScopes": "invalid"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-writer");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var suggestions = root.GetProperty("repairSuggestions");

        suggestions.ValueKind.Should().Be(JsonValueKind.Array);
        suggestions.GetArrayLength().Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void WriterValidation_VerifierInput_ValidArtifact_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/evidence/runs/RUN-WRITER-003/verifier-input.json";

        var validPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-001",
              "runId": "RUN-WRITER-003",
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
            "verifier-writer");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WriterValidation_VerifierInput_InvalidArtifact_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/evidence/runs/RUN-WRITER-004/verifier-input.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-001",
              "runId": "RUN-WRITER-004",
              "acceptanceCriteria": "should-be-array"
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierInput(
            artifactPath,
            invalidPayload,
            aosRoot,
            "verifier-writer");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void WriterValidation_MultipleArtifacts_EachGeneratesOwnDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");

        var invalidPayload1 = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-WRITER-006"
            }
            """;

        var invalidPayload2 = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-WRITER-007"
            }
            """;

        var result1 = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-WRITER-006/plan.json",
            invalidPayload1,
            aosRoot,
            "test-writer");

        var result2 = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-WRITER-007/plan.json",
            invalidPayload2,
            aosRoot,
            "test-writer");

        result1.IsValid.Should().BeFalse();
        result2.IsValid.Should().BeFalse();
        result1.DiagnosticPath.Should().NotBe(result2.DiagnosticPath);
        File.Exists(result1.DiagnosticPath!).Should().BeTrue();
        File.Exists(result2.DiagnosticPath!).Should().BeTrue();
    }

    [Fact]
    public void WriterValidation_DiagnosticIncludesValidationErrorDetails()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = ".aos/spec/tasks/TSK-WRITER-008/plan.json";

        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-WRITER-008",
              "title": "Task",
              "fileScopes": null
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "test-writer");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var errors = root.GetProperty("validationErrors");

        errors.GetArrayLength().Should().BeGreaterThan(0);
        var firstError = errors[0];
        firstError.TryGetProperty("path", out _).Should().BeTrue();
        firstError.TryGetProperty("message", out _).Should().BeTrue();
    }
}
