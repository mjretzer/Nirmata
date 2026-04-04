using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ControlPlane;

public class GatingEngineRoutingTests
{
    private readonly IDestructivenessAnalyzer _analyzer = new DestructivenessAnalyzer();

    // ── Gate 1: Missing project → Interviewer ──

    [Fact]
    public async Task EvaluateAsync_WithNoProject_RoutesToInterviewer()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = false,
            HasRoadmap = false,
            HasTaskPlan = false,
            CurrentCursor = null,
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Interviewer");
    }

    // ── Gate 2: Brownfield codebase preflight → CodebaseMapper ──

    [Fact]
    public async Task EvaluateAsync_WithProjectButNoCodebase_RoutesToCodebaseMapper()
    {
        // Arrange — project exists but codebase intelligence is absent
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = false,
            HasTaskPlan = false,
            HasCodebaseIntelligence = false,
            CurrentCursor = null,
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("CodebaseMapper");
        result.Reason.Should().Contain("absent");
    }

    [Fact]
    public async Task EvaluateAsync_WithProjectAndStaleCodebase_RoutesToCodebaseMapper()
    {
        // Arrange — project exists but codebase intelligence is stale
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = false,
            HasCodebaseIntelligence = true,
            IsCodebaseStale = true,
            CurrentCursor = null,
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("CodebaseMapper");
        result.Reason.Should().Contain("stale");
    }

    [Fact]
    public async Task EvaluateAsync_WithAbsentCodebase_ButRoadmapAndTaskPlanExist_SkipsCodebaseMapper()
    {
        // Arrange — codebase intelligence absent but roadmap and task plans exist;
        // the brownfield gate should only fire when the next step is roadmap generation
        // or phase planning, not when execution is ready.
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = false,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert — should NOT route to CodebaseMapper; should fall through to Executor
        result.TargetPhase.Should().NotBe("CodebaseMapper");
        result.TargetPhase.Should().Be("Executor");
    }

    [Fact]
    public async Task EvaluateAsync_WithStaleCodebase_ButRoadmapAndTaskPlanExist_SkipsCodebaseMapper()
    {
        // Arrange — codebase intelligence is stale but roadmap and task plans exist;
        // stale codebase should not block execution when past the planning stage.
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            IsCodebaseStale = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert — should NOT route to CodebaseMapper; should fall through to Executor
        result.TargetPhase.Should().NotBe("CodebaseMapper");
        result.TargetPhase.Should().Be("Executor");
    }

    [Fact]
    public async Task EvaluateAsync_WithAbsentCodebase_NoRoadmap_RoutesToCodebaseMapper()
    {
        // Arrange — codebase absent and no roadmap: next step would be roadmap generation,
        // so the brownfield gate should fire.
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = false,
            HasTaskPlan = false,
            HasCodebaseIntelligence = false,
            CurrentCursor = null,
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("CodebaseMapper");
    }

    [Fact]
    public async Task EvaluateAsync_WithStaleCodebase_NoTaskPlan_RoutesToCodebaseMapper()
    {
        // Arrange — codebase stale and no task plan: next step would be phase planning,
        // so the brownfield gate should fire.
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = false,
            HasCodebaseIntelligence = true,
            IsCodebaseStale = true,
            CurrentCursor = "PH-0001",
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("CodebaseMapper");
        result.Reason.Should().Contain("stale");
    }

    // ── Gate 3: Missing roadmap → Roadmapper ──

    [Fact]
    public async Task EvaluateAsync_WithProjectButNoRoadmap_RoutesToRoadmapper()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = false,
            HasTaskPlan = false,
            HasCodebaseIntelligence = true,
            CurrentCursor = null,
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Roadmapper");
    }

    // ── Gate 4: Missing task plan → Planner ──

    [Fact]
    public async Task EvaluateAsync_WithRoadmapButNoTaskPlan_RoutesToPlanner()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = false,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Planner");
    }

    // ── Gate 5: Execution completed, verification pending → Verifier ──

    [Fact]
    public async Task EvaluateAsync_WithExecutionCompletedButNotVerified_RoutesToVerifier()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = null,
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Verifier");
    }

    // ── Gate 6: Verification failed → FixPlanner ──

    [Fact]
    public async Task EvaluateAsync_WithVerificationFailed_RoutesToFixPlanner()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = "failed",
            IssueIds = new[] { "ISS-0001", "ISS-0002" },
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("FixPlanner");
        result.Reason.Should().Contain("Verification failed");
    }

    [Fact]
    public async Task EvaluateAsync_WithVerificationFailed_IncludesIssueIdsInContext()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var issueIds = new[] { "ISS-0001", "ISS-0002", "ISS-0003" };
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = "failed",
            IssueIds = issueIds,
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.ContextData.Should().ContainKey("issueIds");
        result.ContextData["issueIds"].Should().BeEquivalentTo(issueIds);
    }

    [Fact]
    public async Task EvaluateAsync_WithVerificationFailed_IncludesParentTaskIdInContext()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = "failed",
            IssueIds = new[] { "ISS-0001" },
            ParentTaskId = "TSK-PARENT-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.ContextData.Should().ContainKey("parentTaskId");
        result.ContextData["parentTaskId"].Should().Be("TSK-PARENT-001");
    }

    [Fact]
    public async Task EvaluateAsync_WithFixPlanReadyToExecute_RoutesToExecutor()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-FIX-001",
            LastExecutionStatus = "fix-planned",
            LastVerificationStatus = "ready-to-execute"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Executor");
        result.Reason.Should().Contain("ready to rerun");
    }

    [Fact]
    public async Task EvaluateAsync_WithVerificationFailed_HasCorrectGatingResultStructure()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = "failed",
            IssueIds = new[] { "ISS-0001", "ISS-0002" },
            ParentTaskId = "TSK-PARENT"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("FixPlanner");
        result.Reason.Should().NotBeNullOrEmpty();
        result.ContextData.Should().ContainKeys("verificationStatus", "parentTaskId", "issueIds");
    }

    [Fact]
    public async Task EvaluateAsync_WithMissingParentTaskId_UsesUnknownPlaceholder()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = "failed",
            IssueIds = new[] { "ISS-0001" },
            ParentTaskId = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.ContextData.Should().ContainKey("parentTaskId");
        result.ContextData["parentTaskId"].Should().Be("unknown");
    }

    [Fact]
    public async Task EvaluateAsync_WithEmptyIssueIds_PassesEmptyList()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = "failed",
            IssueIds = Array.Empty<string>(),
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.ContextData.Should().ContainKey("issueIds");
        var issueIds = result.ContextData["issueIds"] as IReadOnlyList<string>;
        issueIds.Should().BeEmpty();
    }

    // ── Gate 7: Execution failed → FixPlanner (recovery) ──

    [Fact]
    public async Task EvaluateAsync_WithExecutionFailed_RoutesToFixPlanner()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "failed",
            LastVerificationStatus = null,
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("FixPlanner");
        result.Reason.Should().Contain("execution failed");
    }

    [Fact]
    public async Task EvaluateAsync_WithExecutionFailed_IncludesParentTaskIdInContext()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "failed",
            LastVerificationStatus = null,
            ParentTaskId = "TSK-EXEC-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.ContextData.Should().ContainKey("parentTaskId");
        result.ContextData["parentTaskId"].Should().Be("TSK-EXEC-001");
    }

    // ── Gate 8: Verification passed → progression ──

    [Fact]
    public async Task EvaluateAsync_WithVerificationPassed_MoreTasksInPhase_RoutesToExecutor()
    {
        // Arrange — phase and milestone not complete, so next task should execute
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = "passed",
            IsPhaseComplete = false,
            IsMilestoneComplete = false,
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Executor");
    }

    [Fact]
    public async Task EvaluateAsync_WithVerificationPassed_PhaseComplete_RoutesToPlanner()
    {
        // Arrange — phase is complete, more phases remain → plan next phase
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = "passed",
            IsPhaseComplete = true,
            IsMilestoneComplete = false,
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Planner");
        result.Reason.Should().Contain("next phase");
    }

    [Fact]
    public async Task EvaluateAsync_WithVerificationPassed_MilestoneComplete_RoutesToMilestoneProgression()
    {
        // Arrange — milestone is complete → milestone progression
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = "passed",
            IsPhaseComplete = true,
            IsMilestoneComplete = true,
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("MilestoneProgression");
    }

    // ── Gate 9: Ready to execute → Executor ──

    [Fact]
    public async Task EvaluateAsync_ReadyToExecute_RoutesToExecutor()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = null,
            LastVerificationStatus = null,
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Executor");
    }

    // ── Gate 10: Default → Responder ──

    [Fact]
    public async Task EvaluateAsync_UnknownState_RoutesToResponder()
    {
        // Arrange — in_progress is not a terminal execution state, falls through to default
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "in_progress",
            LastVerificationStatus = null,
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Responder");
    }
}
