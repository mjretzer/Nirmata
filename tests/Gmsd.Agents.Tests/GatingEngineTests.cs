using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane;
using Xunit;

namespace Gmsd.Agents.Tests;

public class GatingEngineTests
{
    private readonly GatingEngine _sut = new();

    [Fact]
    public async Task EvaluateAsync_WhenProjectMissing_ReturnsInterviewer()
    {
        var context = new GatingContext
        {
            HasProject = false,
            HasRoadmap = false,
            HasPlan = false
        };

        var result = await _sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Interviewer");
        result.Reason.Should().Be("Project specification not found");
    }

    [Fact]
    public async Task EvaluateAsync_WhenProjectExistsButRoadmapMissing_ReturnsRoadmapper()
    {
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = false,
            HasPlan = false
        };

        var result = await _sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Roadmapper");
        result.Reason.Should().Be("Roadmap not defined for project");
    }

    [Fact]
    public async Task EvaluateAsync_WhenProjectAndRoadmapExistButPlanMissing_ReturnsPlanner()
    {
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = false,
            CurrentCursor = "milestone-1"
        };

        var result = await _sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Planner");
        result.Reason.Should().Be("No plan exists for current cursor position");
    }

    [Fact]
    public async Task EvaluateAsync_WhenPlanExists_ReturnsExecutor()
    {
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
            CurrentCursor = "task-1"
        };

        var result = await _sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Executor");
        result.Reason.Should().Be("Ready to execute task plan");
    }

    [Fact]
    public async Task EvaluateAsync_WhenExecutionCompletedAndVerificationPending_ReturnsVerifier()
    {
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
            LastExecutionStatus = "completed",
            LastVerificationStatus = null
        };

        var result = await _sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Verifier");
        result.Reason.Should().Be("Execution complete, awaiting verification");
    }

    [Fact]
    public async Task EvaluateAsync_WhenVerificationFailed_ReturnsFixPlanner()
    {
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
            LastExecutionStatus = "completed",
            LastVerificationStatus = "failed"
        };

        var result = await _sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("FixPlanner");
        result.Reason.Should().Be("Verification failed, fix planning required");
    }

    [Fact]
    public async Task EvaluateAsync_Priority_Order_Is_Correct()
    {
        // Even with all conditions that could trigger different phases,
        // missing project should take precedence
        var context = new GatingContext
        {
            HasProject = false,
            HasRoadmap = true,
            HasPlan = true,
            LastVerificationStatus = "failed"
        };

        var result = await _sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Interviewer");
    }

    [Fact]
    public async Task EvaluateAsync_IncludesContextData()
    {
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasPlan = true,
            CurrentCursor = "task-123"
        };

        var result = await _sut.EvaluateAsync(context);

        result.ContextData.Should().ContainKey("hasPlan");
        result.ContextData.Should().ContainKey("cursor");
    }
}
