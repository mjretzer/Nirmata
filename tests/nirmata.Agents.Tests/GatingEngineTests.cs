using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane;
using Xunit;

namespace nirmata.Agents.Tests;

public class GatingEngineTests
{
    private readonly IDestructivenessAnalyzer _analyzer = new DestructivenessAnalyzer();

    [Fact]
    public async Task EvaluateAsync_WhenProjectMissing_ReturnsInterviewer()
    {
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = false,
            HasRoadmap = false,
            HasTaskPlan = false
        };

        var result = await sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Interviewer");
        result.Reason.Should().Be("Project specification not found");
    }

    [Fact]
    public async Task EvaluateAsync_WhenProjectExistsButCodebaseMissing_ReturnsCodebaseMapper()
    {
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = false,
            HasTaskPlan = false,
            HasCodebaseIntelligence = false
        };

        var result = await sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("CodebaseMapper");
    }

    [Fact]
    public async Task EvaluateAsync_WhenProjectExistsButRoadmapMissing_ReturnsRoadmapper()
    {
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = false,
            HasTaskPlan = false,
            HasCodebaseIntelligence = true
        };

        var result = await sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Roadmapper");
        result.Reason.Should().Be("Roadmap not defined for project");
    }

    [Fact]
    public async Task EvaluateAsync_WhenProjectAndRoadmapExistButTaskPlanMissing_ReturnsPlanner()
    {
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = false,
            HasCodebaseIntelligence = true,
            CurrentCursor = "milestone-1"
        };

        var result = await sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Planner");
        result.Reason.Should().Be("No task plan exists for current cursor position");
    }

    [Fact]
    public async Task EvaluateAsync_WhenTaskPlanExists_ReturnsExecutor()
    {
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "task-1"
        };

        var result = await sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Executor");
        result.Reason.Should().Be("Ready to execute the plan");
    }

    [Fact]
    public async Task EvaluateAsync_WhenExecutionCompletedAndVerificationPending_ReturnsVerifier()
    {
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            LastExecutionStatus = "completed",
            LastVerificationStatus = null
        };

        var result = await sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Verifier");
        result.Reason.Should().Be("Execution complete, awaiting verification");
    }

    [Fact]
    public async Task EvaluateAsync_WhenVerificationFailed_ReturnsFixPlanner()
    {
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            LastExecutionStatus = "completed",
            LastVerificationStatus = "failed"
        };

        var result = await sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("FixPlanner");
        result.Reason.Should().Be("Verification failed, fix planning required");
    }

    [Fact]
    public async Task EvaluateAsync_Priority_Order_Is_Correct()
    {
        // Even with all conditions that could trigger different phases,
        // missing project should take precedence
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = false,
            HasRoadmap = true,
            HasTaskPlan = true,
            LastVerificationStatus = "failed"
        };

        var result = await sut.EvaluateAsync(context);

        result.TargetPhase.Should().Be("Interviewer");
    }

    [Fact]
    public async Task EvaluateAsync_IncludesContextData()
    {
        var sut = new GatingEngine(_analyzer);
        var context = new GatingContext
        {
            HasProject = true,
            HasRoadmap = true,
            HasTaskPlan = true,
            HasCodebaseIntelligence = true,
            CurrentCursor = "task-123"
        };

        var result = await sut.EvaluateAsync(context);

        result.ContextData.Should().ContainKey("cursor");
    }
}
