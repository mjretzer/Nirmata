using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Tests.Helpers;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Validation;

public class ArtifactContractValidatorTests
{
    [Fact]
    public void ValidateTaskPlan_WithCanonicalPayload_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var artifactPath = Path.Combine(tempDir.Path, ".aos", "spec", "tasks", "TSK-001", "plan.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "taskId": "TSK-001",
              "title": "Implement feature",
              "description": "Implement feature details",
              "fileScopes": [
                {
                  "path": "src/Feature.cs"
                }
              ],
              "steps": [],
              "verificationSteps": []
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "task-executor-handler");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
    }

    [Fact]
    public void ValidateTaskPlan_WithUnsupportedSchemaVersion_ReturnsInvalidAndWritesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(tempDir.Path, ".aos", "spec", "tasks", "TSK-001", "plan.json");

        var payload =
            """
            {
              "schemaVersion": 2,
              "taskId": "TSK-001",
              "title": "Implement feature",
              "description": "Implement feature details",
              "fileScopes": [
                {
                  "path": "src/Feature.cs"
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            payload,
            aosRoot,
            "verifier-handler");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.DiagnosticPath!).Should().BeTrue();

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        diagnostic.RootElement.GetProperty("failedSchemaId").GetString().Should().Be("nirmata:aos:schema:task-plan:v1");
        diagnostic.RootElement.GetProperty("failedSchemaVersion").GetInt32().Should().Be(2);
    }

    [Fact]
    public void ValidateTaskPlan_WithMalformedFileScopesStringEntries_ReturnsInvalidAndWritesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var artifactPath = Path.Combine(tempDir.Path, ".aos", "spec", "tasks", "TSK-001", "plan.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "taskId": "TSK-001",
              "title": "Implement feature",
              "description": "Implement feature details",
              "fileScopes": ["src/Feature.cs"]
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "atomic-git-committer-handler");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.DiagnosticPath!).Should().BeTrue();
    }

    [Fact]
    public void ValidateTaskPlan_WithMissingFileScopePath_ReturnsInvalidAndWritesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var artifactPath = Path.Combine(tempDir.Path, ".aos", "spec", "tasks", "TSK-001", "plan.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "taskId": "TSK-001",
              "title": "Implement feature",
              "description": "Implement feature details",
              "fileScopes": [
                {
                  "scopeType": "modify"
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "task-executor-handler");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.DiagnosticPath!).Should().BeTrue();
    }

    [Fact]
    public void ValidatePhasePlan_WithCanonicalPayload_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var artifactPath = Path.Combine(tempDir.Path, ".aos", "spec", "phases", "PH-001", "plan.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "planId": "PLAN-001",
              "phaseId": "PH-001",
              "tasks": [
                {
                  "id": "TSK-001",
                  "title": "Task 1",
                  "description": "Description for task 1",
                  "fileScopes": [
                    { "path": "src/File1.cs" }
                  ],
                  "verificationSteps": ["Step 1"]
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidatePhasePlan(
            artifactPath,
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "phase-planner-writer");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateVerifierOutput_WithCanonicalPayload_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var artifactPath = Path.Combine(tempDir.Path, ".aos", "evidence", "runs", "RUN-001", "artifacts", "uat-results.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "isPassed": true,
              "runId": "RUN-001",
              "checks": [
                {
                  "criterionId": "crit-001",
                  "passed": true,
                  "isRequired": true,
                  "message": "Check passed"
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierOutput(
            artifactPath,
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "verifier-writer");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateFixPlan_WithCanonicalPayload_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var artifactPath = Path.Combine(tempDir.Path, ".aos", "spec", "tasks", "TSK-FIX-001", "plan.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "fixes": [
                {
                  "issueId": "ISS-001",
                  "description": "Fixing the reported issue in the system.",
                  "proposedChanges": [
                    {
                      "file": "src/Fix.cs",
                      "changeDescription": "Applying fix to the file"
                    }
                  ],
                  "tests": ["Verify that the fix works as expected"]
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateFixPlan(
            artifactPath,
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "fix-planner-writer");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateFixPlan_WithMalformedFileScopes_ReturnsInvalid()
    {
        using var tempDir = new TempDirectory();
        var artifactPath = Path.Combine(tempDir.Path, ".aos", "spec", "tasks", "TSK-FIX-001", "plan.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "fixes": [
                {
                  "issueId": "ISS-001",
                  "description": "Fixing",
                  "proposedChanges": [],
                  "tests": []
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateFixPlan(
            artifactPath,
            payload,
            Path.Combine(tempDir.Path, ".aos"),
            "fix-planner-writer");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
    }

    // Reader Validation Tests (Task 4.6)

    [Fact]
    public void ValidateTaskPlan_ReaderValidation_WithValidPayload_ReturnsValidWithNoDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(aosRoot, "spec", "tasks", "TSK-READER-001", "plan.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "taskId": "TSK-READER-001",
              "title": "Read and validate task plan",
              "description": "Validate task plan from reader",
              "fileScopes": [
                {
                  "path": "src/Reader.cs"
                }
              ],
              "verificationSteps": []
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            payload,
            aosRoot,
            "task-executor-reader");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
        result.Message.Should().BeNull();
    }

    [Fact]
    public void ValidateTaskPlan_ReaderValidation_WithInvalidPayload_GeneratesDiagnosticArtifact()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(aosRoot, "spec", "tasks", "TSK-READER-002", "plan.json");

        var invalidPayload =
            """
            {
              "schemaVersion": 1,
              "taskId": "TSK-READER-002",
              "title": "Invalid task plan",
              "fileScopes": "invalid-should-be-array"
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

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        root.GetProperty("failedSchemaId").GetString().Should().Be("nirmata:aos:schema:task-plan:v1");
        root.GetProperty("phase").GetString().Should().Be("task-execution");
        root.GetProperty("context").GetProperty("readBoundary").GetString().Should().Be("task-executor-reader");
        root.GetProperty("validationErrors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void ValidatePhasePlan_ReaderValidation_WithValidPayload_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(aosRoot, "spec", "phases", "PH-READER-001", "plan.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "planId": "PLAN-READER-001",
              "phaseId": "PH-READER-001",
              "tasks": [
                {
                  "id": "TSK-001",
                  "title": "Phase task",
                  "description": "Task in phase",
                  "fileScopes": [
                    { "path": "src/Phase.cs" }
                  ],
                  "verificationSteps": ["Verify phase"]
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidatePhasePlan(
            artifactPath,
            payload,
            aosRoot,
            "phase-reader");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
    }

    [Fact]
    public void ValidatePhasePlan_ReaderValidation_WithMissingRequiredField_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(aosRoot, "spec", "phases", "PH-READER-002", "plan.json");

        var invalidPayload =
            """
            {
              "schemaVersion": 1,
              "planId": "PLAN-READER-002",
              "tasks": []
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

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        root.GetProperty("failedSchemaId").GetString().Should().Be("nirmata:aos:schema:phase-plan:v1");
        root.GetProperty("phase").GetString().Should().Be("phase-planning");
    }

    [Fact]
    public void ValidateFixPlan_ReaderValidation_WithValidPayload_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(aosRoot, "spec", "tasks", "TSK-FIX-READER-001", "fix-plan.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "fixes": [
                {
                  "issueId": "ISS-READER-001",
                  "description": "Fix for reader validation",
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
            payload,
            aosRoot,
            "fix-planner-reader");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
    }

    [Fact]
    public void ValidateFixPlan_ReaderValidation_WithInvalidPayload_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(aosRoot, "spec", "tasks", "TSK-FIX-READER-002", "fix-plan.json");

        var invalidPayload =
            """
            {
              "schemaVersion": 1,
              "fixes": "invalid-should-be-array"
            }
            """;

        var result = ArtifactContractValidator.ValidateFixPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "fix-planner-reader");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.DiagnosticPath!).Should().BeTrue();

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        root.GetProperty("failedSchemaId").GetString().Should().Be("nirmata:aos:schema:fix-plan:v1");
        root.GetProperty("phase").GetString().Should().Be("fix-planning");
        root.GetProperty("context").GetProperty("readBoundary").GetString().Should().Be("fix-planner-reader");
    }

    [Fact]
    public void ValidateVerifierOutput_ReaderValidation_WithValidPayload_ReturnsValid()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(aosRoot, "evidence", "runs", "RUN-READER-001", "uat-results.json");

        var payload =
            """
            {
              "schemaVersion": 1,
              "isPassed": true,
              "runId": "RUN-READER-001",
              "checks": [
                {
                  "criterionId": "crit-reader-001",
                  "passed": true,
                  "isRequired": true,
                  "message": "Reader validation passed"
                }
              ]
            }
            """;

        var result = ArtifactContractValidator.ValidateVerifierOutput(
            artifactPath,
            payload,
            aosRoot,
            "verifier-reader");

        result.IsValid.Should().BeTrue();
        result.DiagnosticPath.Should().BeNull();
    }

    [Fact]
    public void ValidateVerifierOutput_ReaderValidation_WithInvalidPayload_GeneratesDiagnostic()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(aosRoot, "evidence", "runs", "RUN-READER-002", "uat-results.json");

        var invalidPayload =
            """
            {
              "schemaVersion": 1,
              "isPassed": "invalid-should-be-boolean",
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
        File.Exists(result.DiagnosticPath!).Should().BeTrue();

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        root.GetProperty("failedSchemaId").GetString().Should().Be("nirmata:aos:schema:verifier-output:v1");
        root.GetProperty("phase").GetString().Should().Be("uat-verification");
    }

    [Fact]
    public void ReaderValidation_DiagnosticArtifact_ContainsRepairSuggestions()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(aosRoot, "spec", "tasks", "TSK-REPAIR-001", "plan.json");

        var invalidPayload =
            """
            {
              "schemaVersion": 1,
              "taskId": "TSK-REPAIR-001",
              "title": "Task with repair suggestions",
              "fileScopes": null
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "task-executor-reader");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        root.TryGetProperty("repairSuggestions", out var suggestions).Should().BeTrue();
        suggestions.GetArrayLength().Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void ReaderValidation_DiagnosticArtifact_StoredInCorrectLocation()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        var artifactPath = Path.Combine(aosRoot, "spec", "tasks", "TSK-LOCATION-001", "plan.json");

        var invalidPayload =
            """
            {
              "schemaVersion": 1,
              "taskId": "TSK-LOCATION-001",
              "title": "Task",
              "description": "Missing fileScopes"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            artifactPath,
            invalidPayload,
            aosRoot,
            "task-executor-reader");

        result.IsValid.Should().BeFalse();
        result.DiagnosticPath.Should().NotBeNullOrWhiteSpace();

        var diagnosticPath = result.DiagnosticPath!;
        diagnosticPath.Should().Contain(".aos");
        diagnosticPath.Should().Contain("diagnostics");
        diagnosticPath.Should().Contain("task-execution");
        diagnosticPath.Should().EndWith(".diagnostic.json");
        File.Exists(diagnosticPath).Should().BeTrue();
    }
}
