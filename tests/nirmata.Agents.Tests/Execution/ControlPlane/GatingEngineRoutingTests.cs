using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ControlPlane;

public class GatingEngineRoutingTests
{
    private readonly IDestructivenessAnalyzer _analyzer = new DestructivenessAnalyzer();

    [Fact]
    public async Task EvaluateAsync_WithVerificationFailed_RoutesToFixPlanner()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
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
    public async Task EvaluateAsync_WithExecutionFailed_RoutesToFixPlanner()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
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
    public async Task EvaluateAsync_WithVerificationFailed_IncludesIssueIdsInContext()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var issueIds = new[] { "ISS-0001", "ISS-0002", "ISS-0003" };
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
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
            HasPlan = true,
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
    public async Task EvaluateAsync_WithExecutionFailed_IncludesParentTaskIdInContext()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
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

    [Fact]
    public async Task EvaluateAsync_WithMissingParentTaskId_UsesUnknownPlaceholder()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
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
            HasPlan = true,
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

    [Fact]
    public async Task EvaluateAsync_WithVerificationPassed_RoutesToExecutor()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "completed",
            LastVerificationStatus = "passed",
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Executor");
    }

    [Fact]
    public async Task EvaluateAsync_WithNoProject_RoutesToInterviewer()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = false,
            HasRoadmap = false,
            HasPlan = false,
            CurrentCursor = null,
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Interviewer");
    }

    [Fact]
    public async Task EvaluateAsync_WithProjectButNoRoadmap_RoutesToRoadmapper()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = false,
            HasPlan = false,
            CurrentCursor = null,
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Roadmapper");
    }

    [Fact]
    public async Task EvaluateAsync_WithRoadmapButNoPlan_RoutesToPlanner()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = false,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = null,
            LastVerificationStatus = null
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("Planner");
    }

    [Fact]
    public async Task EvaluateAsync_ReadyToExecute_RoutesToExecutor()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
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

    [Fact]
    public async Task EvaluateAsync_WithExecutionCompletedButNotVerified_RoutesToVerifier()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
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

    [Fact]
    public async Task EvaluateAsync_WithVerificationFailed_HasCorrectGatingResultStructure()
    {
        // Arrange
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
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
}
