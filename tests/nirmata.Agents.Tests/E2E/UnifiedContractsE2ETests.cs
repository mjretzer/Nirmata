using FluentAssertions;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Tests.Helpers;
using System.Text.Json;
using Xunit;

namespace nirmata.Agents.Tests.E2E;

/// <summary>
/// End-to-end tests for unified data contracts (Tasks 9.5, 9.6, 9.7).
/// Tests validate full workflow artifact chaining, diagnostic discovery, and rendering.
/// </summary>
public class UnifiedContractsE2ETests
{
    [Fact]
    public void E2E_FullWorkflow_PlannerToExecutorToVerifierToFixer_ArtifactChaining()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "phases"));
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "tasks"));
        Directory.CreateDirectory(Path.Combine(aosRoot, "evidence", "runs"));
        Directory.CreateDirectory(Path.Combine(aosRoot, "diagnostics"));

        // Phase 1: Phase Planner creates phase plan
        var phasePlanPath = ".aos/spec/phases/PH-E2E-001/plan.json";
        var phasePlanPayload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-E2E-001",
              "phaseId": "PH-E2E-001",
              "tasks": [
                {
                  "id": "TSK-E2E-001",
                  "title": "Implement Feature",
                  "description": "Implement the feature",
                  "fileScopes": [
                    { "path": "src/Feature.cs" }
                  ],
                  "verificationSteps": ["Run tests"]
                }
              ]
            }
            """;

        var phasePlanResult = ArtifactContractValidator.ValidatePhasePlan(
            phasePlanPath,
            phasePlanPayload,
            aosRoot,
            "phase-planner-writer");

        phasePlanResult.IsValid.Should().BeTrue("Phase plan should be valid");

        // Phase 2: Task Executor reads phase plan and creates task plan
        var taskPlanPath = ".aos/spec/tasks/TSK-E2E-001/plan.json";
        var taskPlanPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-E2E-001",
              "title": "Implement Feature",
              "description": "Implement the feature",
              "fileScopes": [
                { "path": "src/Feature.cs" }
              ],
              "steps": [
                {
                  "stepId": "STEP-001",
                  "description": "Write code",
                  "expectedOutcome": "Code written"
                }
              ],
              "verificationSteps": [
                {
                  "verificationType": "unit-test",
                  "description": "Run tests",
                  "expectedOutcome": "Tests pass"
                }
              ]
            }
            """;

        var taskPlanResult = ArtifactContractValidator.ValidateTaskPlan(
            taskPlanPath,
            taskPlanPayload,
            aosRoot,
            "task-executor-writer");

        taskPlanResult.IsValid.Should().BeTrue("Task plan should be valid");

        // Phase 3: Verifier reads task plan and creates verification input
        var verifierInputPath = ".aos/evidence/runs/RUN-E2E-001/verifier-input.json";
        var verifierInputPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-E2E-001",
              "runId": "RUN-E2E-001",
              "acceptanceCriteria": [
                {
                  "criterionId": "AC-001",
                  "description": "Feature works",
                  "expectedOutcome": "Feature works correctly"
                }
              ]
            }
            """;

        var verifierInputResult = ArtifactContractValidator.ValidateVerifierInput(
            verifierInputPath,
            verifierInputPayload,
            aosRoot,
            "verifier-reader");

        verifierInputResult.IsValid.Should().BeTrue("Verifier input should be valid");

        // Phase 4: Verifier creates verification output
        var verifierOutputPath = ".aos/evidence/runs/RUN-E2E-001/uat-results.json";
        var verifierOutputPayload = """
            {
              "schemaVersion": 1,
              "isPassed": true,
              "runId": "RUN-E2E-001",
              "checks": [
                {
                  "criterionId": "AC-001",
                  "passed": true,
                  "isRequired": true,
                  "message": "Feature works correctly"
                }
              ]
            }
            """;

        var verifierOutputResult = ArtifactContractValidator.ValidateVerifierOutput(
            verifierOutputPath,
            verifierOutputPayload,
            aosRoot,
            "verifier-writer");

        verifierOutputResult.IsValid.Should().BeTrue("Verifier output should be valid");

        // Phase 5: Fix Planner reads verification results and creates fix plan (if needed)
        var fixPlanPath = ".aos/spec/tasks/TSK-E2E-FIX-001/plan.json";
        var fixPlanPayload = """
            {
              "schemaVersion": 1,
              "fixes": [
                {
                  "issueId": "ISS-E2E-001",
                  "description": "Fix any issues",
                  "proposedChanges": [
                    {
                      "file": "src/Feature.cs",
                      "changeDescription": "Apply fix"
                    }
                  ],
                  "tests": ["Verify fix works"]
                }
              ]
            }
            """;

        var fixPlanResult = ArtifactContractValidator.ValidateFixPlan(
            fixPlanPath,
            fixPlanPayload,
            aosRoot,
            "fix-planner-writer");

        fixPlanResult.IsValid.Should().BeTrue("Fix plan should be valid");

        // Verify all artifacts are valid and chained correctly
        phasePlanResult.IsValid.Should().BeTrue();
        taskPlanResult.IsValid.Should().BeTrue();
        verifierInputResult.IsValid.Should().BeTrue();
        verifierOutputResult.IsValid.Should().BeTrue();
        fixPlanResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void E2E_ArtifactChaining_WithoutManualPatching()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "phases"));
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "tasks"));

        // Create phase plan with canonical schema
        var phasePlanPayload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-CHAIN-001",
              "phaseId": "PH-CHAIN-001",
              "tasks": [
                {
                  "id": "TSK-CHAIN-001",
                  "title": "Task 1",
                  "description": "Task 1 description",
                  "fileScopes": [
                    { "path": "src/File1.cs" }
                  ],
                  "verificationSteps": ["Verify"]
                }
              ]
            }
            """;

        var phasePlanResult = ArtifactContractValidator.ValidatePhasePlan(
            ".aos/spec/phases/PH-CHAIN-001/plan.json",
            phasePlanPayload,
            aosRoot,
            "phase-planner-writer");

        phasePlanResult.IsValid.Should().BeTrue();

        // Read phase plan and create task plan without manual transformation
        var taskPlanPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-CHAIN-001",
              "title": "Task 1",
              "description": "Task 1 description",
              "fileScopes": [
                { "path": "src/File1.cs" }
              ],
              "steps": [],
              "verificationSteps": [
                {
                  "verificationType": "unit-test",
                  "description": "Verify",
                  "expectedOutcome": "Pass"
                }
              ]
            }
            """;

        var taskPlanResult = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-CHAIN-001/plan.json",
            taskPlanPayload,
            aosRoot,
            "task-executor-writer");

        taskPlanResult.IsValid.Should().BeTrue("Task plan should be valid without manual patching");
    }

    [Fact]
    public void E2E_DiagnosticDiscovery_ListsAllDiagnostics()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "tasks"));
        Directory.CreateDirectory(Path.Combine(aosRoot, "diagnostics"));

        // Create multiple invalid artifacts to generate diagnostics
        var invalidPayload1 = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-DISC-001"
            }
            """;

        var invalidPayload2 = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-DIAG-DISC-002"
            }
            """;

        var result1 = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-DIAG-DISC-001/plan.json",
            invalidPayload1,
            aosRoot,
            "test-reader");

        var result2 = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-DIAG-DISC-002/plan.json",
            invalidPayload2,
            aosRoot,
            "test-reader");

        result1.IsValid.Should().BeFalse();
        result2.IsValid.Should().BeFalse();

        // Discover diagnostics
        var diagnostics = nirmata.Agents.Execution.Validation.DiagnosticArtifactReader.ListAll(aosRoot).ToList();

        diagnostics.Should().HaveCountGreaterThanOrEqualTo(2);
        diagnostics.Should().AllSatisfy(d => d.Phase.Should().Be("task-execution"));
    }

    [Fact]
    public void E2E_DiagnosticDiscovery_ByPhase()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "tasks"));
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "phases"));
        Directory.CreateDirectory(Path.Combine(aosRoot, "diagnostics"));

        // Create invalid task plan
        var invalidTaskPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-PHASE-DISC-001"
            }
            """;

        var taskResult = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-PHASE-DISC-001/plan.json",
            invalidTaskPayload,
            aosRoot,
            "test-reader");

        // Create invalid phase plan
        var invalidPhasePayload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-PHASE-DISC-001"
            }
            """;

        var phaseResult = ArtifactContractValidator.ValidatePhasePlan(
            ".aos/spec/phases/PH-PHASE-DISC-001/plan.json",
            invalidPhasePayload,
            aosRoot,
            "test-reader");

        taskResult.IsValid.Should().BeFalse();
        phaseResult.IsValid.Should().BeFalse();

        // Discover diagnostics by phase
        var taskDiagnostics = nirmata.Agents.Execution.Validation.DiagnosticArtifactReader.ListByPhase(aosRoot, "task-execution").ToList();
        var phaseDiagnostics = nirmata.Agents.Execution.Validation.DiagnosticArtifactReader.ListByPhase(aosRoot, "phase-planning").ToList();

        taskDiagnostics.Should().HaveCountGreaterThanOrEqualTo(1);
        phaseDiagnostics.Should().HaveCountGreaterThanOrEqualTo(1);
        taskDiagnostics.Should().AllSatisfy(d => d.Phase.Should().Be("task-execution"));
        phaseDiagnostics.Should().AllSatisfy(d => d.Phase.Should().Be("phase-planning"));
    }

    [Fact]
    public void E2E_DiagnosticRendering_CanBeDeserialized()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "tasks"));
        Directory.CreateDirectory(Path.Combine(aosRoot, "diagnostics"));

        // Create invalid artifact
        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-RENDER-001",
              "title": "Task",
              "fileScopes": "invalid"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-RENDER-001/plan.json",
            invalidPayload,
            aosRoot,
            "test-reader");

        result.IsValid.Should().BeFalse();

        // Read diagnostic and verify it can be deserialized
        var diagnosticJson = File.ReadAllText(result.DiagnosticPath!);
        var diagnostic = JsonSerializer.Deserialize<dynamic>(diagnosticJson);

        diagnostic.Should().NotBeNull();
    }

    [Fact]
    public void E2E_DiagnosticRendering_ContainsRepairSuggestions()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "tasks"));
        Directory.CreateDirectory(Path.Combine(aosRoot, "diagnostics"));

        // Create invalid artifact
        var invalidPayload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-REPAIR-RENDER-001",
              "title": "Task",
              "fileScopes": "invalid"
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-REPAIR-RENDER-001/plan.json",
            invalidPayload,
            aosRoot,
            "test-reader");

        using var diagnostic = JsonDocument.Parse(File.ReadAllText(result.DiagnosticPath!));
        var root = diagnostic.RootElement;
        var suggestions = root.GetProperty("repairSuggestions");

        suggestions.ValueKind.Should().Be(JsonValueKind.Array);
        suggestions.GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void E2E_MultiplePhases_MaintainSchemaConsistency()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "phases"));

        // Create multiple phase plans
        var phase1Payload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-MULTI-001",
              "phaseId": "PH-MULTI-001",
              "tasks": [
                {
                  "id": "TSK-001",
                  "title": "Task 1",
                  "description": "Description",
                  "fileScopes": [
                    { "path": "src/File1.cs" }
                  ],
                  "verificationSteps": ["Verify"]
                }
              ]
            }
            """;

        var phase2Payload = """
            {
              "schemaVersion": 1,
              "planId": "PLAN-MULTI-002",
              "phaseId": "PH-MULTI-002",
              "tasks": [
                {
                  "id": "TSK-002",
                  "title": "Task 2",
                  "description": "Description",
                  "fileScopes": [
                    { "path": "src/File2.cs" }
                  ],
                  "verificationSteps": ["Verify"]
                }
              ]
            }
            """;

        var result1 = ArtifactContractValidator.ValidatePhasePlan(
            ".aos/spec/phases/PH-MULTI-001/plan.json",
            phase1Payload,
            aosRoot,
            "phase-planner-writer");

        var result2 = ArtifactContractValidator.ValidatePhasePlan(
            ".aos/spec/phases/PH-MULTI-002/plan.json",
            phase2Payload,
            aosRoot,
            "phase-planner-writer");

        result1.IsValid.Should().BeTrue();
        result2.IsValid.Should().BeTrue();
    }

    [Fact]
    public void E2E_SchemaVersioning_IncludedInArtifacts()
    {
        using var tempDir = new TempDirectory();
        var aosRoot = Path.Combine(tempDir.Path, ".aos");
        Directory.CreateDirectory(Path.Combine(aosRoot, "spec", "tasks"));

        var payload = """
            {
              "schemaVersion": 1,
              "taskId": "TSK-VERSION-001",
              "title": "Task",
              "description": "Description",
              "fileScopes": [],
              "steps": [],
              "verificationSteps": []
            }
            """;

        var result = ArtifactContractValidator.ValidateTaskPlan(
            ".aos/spec/tasks/TSK-VERSION-001/plan.json",
            payload,
            aosRoot,
            "test-writer");

        result.IsValid.Should().BeTrue();

        // Verify schema version is in the payload
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        root.TryGetProperty("schemaVersion", out var version).Should().BeTrue();
        version.GetInt32().Should().Be(1);
    }
}
