using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ControlPlane;

public class GatingEngineConfirmationTests
{
    private readonly IDestructivenessAnalyzer _analyzer = new DestructivenessAnalyzer();

    [Fact]
    public async Task EvaluateAsync_WithExecutorPhase_RequiresConfirmation()
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
        result.RequiresConfirmation.Should().BeTrue();
        result.ProposedAction.Should().NotBeNull();
        result.ProposedAction!.RiskLevel.Should().Be(RiskLevel.WriteDestructive);
    }

    [Fact]
    public async Task EvaluateAsync_WithInterviewerPhase_DoesNotRequireConfirmation()
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
        result.RequiresConfirmation.Should().BeFalse();
        result.ProposedAction.Should().NotBeNull();
        result.ProposedAction!.RiskLevel.Should().Be(RiskLevel.WriteSafe);
    }

    [Fact]
    public async Task EvaluateAsync_WithVerifierPhase_DoesNotRequireConfirmation()
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
        result.RequiresConfirmation.Should().BeFalse();
        result.ProposedAction.Should().NotBeNull();
        result.ProposedAction!.RiskLevel.Should().Be(RiskLevel.Read);
    }

    [Fact]
    public async Task EvaluateAsync_ProvidesDetailedReasoning()
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
        result.Reasoning.Should().NotBeNullOrEmpty();
        result.Reasoning.Should().Contain("Planner");
    }

    [Fact]
    public async Task EvaluateAsync_ProposedAction_ContainsPhaseDescription()
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
        result.ProposedAction.Should().NotBeNull();
        result.ProposedAction!.Phase.Should().Be("Roadmapper");
        result.ProposedAction.Description.Should().NotBeNullOrEmpty();
        result.ProposedAction.SideEffects.Should().Contain("file_system");
    }

    [Fact]
    public async Task EvaluateAsync_ProposedAction_ContainsAffectedResources()
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
        result.ProposedAction!.AffectedResources.Should().Contain(".aos/spec/project.json");
    }

    [Fact]
    public async Task EvaluateAsync_WithResponderPhase_DoesNotRequireConfirmation()
    {
        // Arrange - Responder is the default when no specific phase triggered
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "TSK-001",
            LastExecutionStatus = "in_progress",  // Not completed, not failed - unusual state
            LastVerificationStatus = null,
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert - should fall through to Responder for unknown states
        result.TargetPhase.Should().Be("Responder");
        result.RequiresConfirmation.Should().BeFalse();
        result.ProposedAction!.RiskLevel.Should().Be(RiskLevel.Read);
    }

    [Fact]
    public async Task EvaluateAsync_ProposedAction_ForFixPlanner_HasCorrectSideEffects()
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
            ParentTaskId = "TSK-001"
        };

        // Act
        var result = await sut.EvaluateAsync(context);

        // Assert
        result.TargetPhase.Should().Be("FixPlanner");
        result.ProposedAction.Should().NotBeNull();
        result.ProposedAction!.SideEffects.Should().Contain("file_system");
    }

    [Fact]
    public async Task EvaluateAsync_CodebaseMapperPhase_DoesNotRequireConfirmation()
    {
        // Arrange — brownfield preflight is WriteSafe, should not require confirmation
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
        result.RequiresConfirmation.Should().BeFalse();
        result.ProposedAction!.RiskLevel.Should().Be(RiskLevel.WriteSafe);
    }
}
