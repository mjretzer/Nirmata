using FluentAssertions;
using Gmsd.Agents.Execution.Validation;
using Gmsd.Agents.Tests.Helpers;
using System.Text.Json;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Validation;

/// <summary>
/// Unit tests for all schema validators (Task 9.1).
/// Tests validate that each schema validator correctly validates artifacts against their canonical schemas.
/// </summary>
public class SchemaValidatorUnitTests
{
    [Fact]
    public void ValidateTaskPlan_WithMinimalValidPayload_Succeeds()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-001",
              "title": "Test Task",
              "description": "Test Description",
              "fileScopes": [],
              "steps": [],
              "verificationSteps": []
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-001/plan.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateTaskPlan_WithCompletePayload_Succeeds()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-002",
              "title": "Complete Task",
              "description": "Complete Task Description",
              "fileScopes": [
                {
                  "path": "src/Component.cs",
                  "scopeType": "file",
                  "description": "Main component"
                }
              ],
              "steps": [
                {
                  "stepId": "STEP-001",
                  "description": "Implement feature",
                  "expectedOutcome": "Feature works"
                }
              ],
              "verificationSteps": [
                {
                  "verificationType": "unit-test",
                  "description": "Run unit tests",
                  "expectedOutcome": "All tests pass"
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-002/plan.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidatePhasePlan_WithValidPayload_Succeeds()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-001",
              "phaseId": "PH-001",
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
            ".aos/spec/phases/PH-001/plan.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateVerifierInput_WithValidPayload_Succeeds()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-001",
              "runId": "RUN-001",
              "acceptanceCriteria": [
                {
                  "criterionId": "AC-001",
                  "description": "Feature should work",
                  "expectedOutcome": "Feature works correctly"
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierInput(
            ".aos/evidence/runs/RUN-001/verifier-input.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateVerifierOutput_WithValidPayload_Succeeds()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "isPassed": true,
              "runId": "RUN-001",
              "checks": [
                {
                  "criterionId": "AC-001",
                  "passed": true,
                  "isRequired": true,
                  "message": "Criterion passed"
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierOutput(
            ".aos/evidence/runs/RUN-001/uat-results.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateFixPlan_WithValidPayload_Succeeds()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "fixes": [
                {
                  "issueId": "ISS-001",
                  "description": "Fix the issue",
                  "proposedChanges": [
                    {
                      "file": "src/Fix.cs",
                      "changeDescription": "Apply fix"
                    }
                  ],
                  "tests": ["Verify fix works"]
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateFixPlan(
            ".aos/spec/tasks/TSK-FIX-001/plan.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateDiagnostic_WithValidPayload_Succeeds()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "schemaId": "gmsd:aos:schema:diagnostic:v1",
              "artifactPath": ".aos/spec/tasks/TSK-001/plan.json",
              "failedSchemaId": "gmsd:aos:schema:task-plan:v1",
              "failedSchemaVersion": 1,
              "timestamp": "2026-02-19T18:07:00Z",
              "phase": "task-execution",
              "context": {
                "readBoundary": "test"
              },
              "validationErrors": [
                {
                  "path": "$.fileScopes",
                  "message": "fileScopes must be an array",
                  "expected": "array",
                  "actual": "string"
                }
              ],
              "repairSuggestions": [
                "Ensure fileScopes is an array of objects"
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateDiagnostic(
            ".aos/diagnostics/task-execution/TSK-001.diagnostic.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateTaskPlan_WithMissingRequiredField_Fails()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-003",
              "title": "Incomplete Task"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-003/plan.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidatePhasePlan_WithMissingPhaseId_Fails()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-002",
              "tasks": []
            }
            """;

        var result = ArtifactContractValidator.ValidatePhasePlan(
            ".aos/spec/phases/PH-002/plan.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateVerifierOutput_WithInvalidIsPassed_Fails()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "isPassed": "not-a-boolean",
              "runId": "RUN-002",
              "checks": []
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierOutput(
            ".aos/evidence/runs/RUN-002/uat-results.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateFixPlan_WithEmptyFixes_Fails()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 1,
              "fixes": []
            }
            """;

        var result = ArtifactContractValidator.ValidateFixPlan(
            ".aos/spec/tasks/TSK-FIX-002/plan.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateTaskPlan_WithInvalidJson_Fails()
    {
        using var tempDir = new TempDirectory();
        var invalidJson = "{ invalid json }";

        var result = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-004/plan.json",
            invalidJson,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateTaskPlan_WithEmptyJson_Fails()
    {
        using var tempDir = new TempDirectory();
        var emptyJson = "";

        var result = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-005/plan.json",
            emptyJson,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateTaskPlan_WithWrongSchemaVersion_Fails()
    {
        using var tempDir = new TempDirectory();
        var payload = """
            {
              "schemaVersion": 99,
              "taskId": "TSK-006",
              "title": "Wrong Version",
              "description": "Test",
              "fileScopes": [],
              "steps": [],
              "verificationSteps": []
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-006/plan.json",
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "test-validator");

        result.IsValid.Should().BeFalse();
    }
}
