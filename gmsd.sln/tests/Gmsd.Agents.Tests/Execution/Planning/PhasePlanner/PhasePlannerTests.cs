using FluentAssertions;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using PhasePlannerClass = Gmsd.Agents.Execution.Planning.PhasePlanner.PhasePlanner;
using PhasePlannerTypes = Gmsd.Agents.Execution.Planning.PhasePlanner;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Aos.Public;
using Moq;
using System.Text.Json;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Planning.PhasePlanner;

public class PhasePlannerTests
{
    private readonly FakeLlmProvider _fakeLlmProvider;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly PhasePlannerClass _sut;

    public PhasePlannerTests()
    {
        _fakeLlmProvider = new FakeLlmProvider();
        _workspaceMock = new Mock<IWorkspace>();
        _workspaceMock.Setup(x => x.AosRootPath).Returns(Path.Combine(Path.GetTempPath(), ".aos"));
        _sut = new PhasePlannerClass(_fakeLlmProvider, _workspaceMock.Object);
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithNullBrief_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.CreateTaskPlanAsync(null!, "RUN-001"));
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithNullRunId_ThrowsArgumentException()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.CreateTaskPlanAsync(brief, null!));
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithEmptyRunId_ThrowsArgumentException()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateTaskPlanAsync(brief, string.Empty));
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithValidBrief_ReturnsValidTaskPlan()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.IsValid.Should().BeTrue();
        result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTaskPlanAsync_PopulatesPlanId()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.PlanId.Should().NotBeNullOrEmpty();
        result.PlanId.Should().StartWith("PLAN-");
    }

    [Fact]
    public async Task CreateTaskPlanAsync_PopulatesPhaseId()
    {
        // Arrange
        var brief = CreateValidPhaseBrief("PH-TEST-001");
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.PhaseId.Should().Be("PH-TEST-001");
    }

    [Fact]
    public async Task CreateTaskPlanAsync_PopulatesRunId()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-TEST-001");

        // Assert
        result.RunId.Should().Be("RUN-TEST-001");
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithTwoTasks_ReturnsTwoTasks()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.Tasks.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithThreeTasks_ReturnsThreeTasks()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(3);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.Tasks.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithFourTasks_ReturnsInvalidPlan()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(4);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("Too many tasks"));
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WithZeroTasks_ReturnsInvalidPlan()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(0);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Tasks.Should().HaveCount(1);
        result.Tasks[0].Title.Should().Contain("Implement");
    }

    [Fact]
    public async Task CreateTaskPlanAsync_TasksHaveSequentialOrder()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(3);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.Tasks.Select(t => t.SequenceOrder).Should().BeInAscendingOrder();
        result.Tasks[0].SequenceOrder.Should().Be(1);
        result.Tasks[1].SequenceOrder.Should().Be(2);
        result.Tasks[2].SequenceOrder.Should().Be(3);
    }

    [Fact]
    public async Task CreateTaskPlanAsync_TasksHaveTaskIds()
    {
        // Arrange
        var brief = CreateValidPhaseBrief("PH-001");
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.Tasks[0].TaskId.Should().NotBeNullOrEmpty();
        result.Tasks[0].TaskId.Should().StartWith("TSK-");
    }

    [Fact]
    public async Task CreateTaskPlanAsync_TasksHaveRequiredFields()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        foreach (var task in result.Tasks)
        {
            task.Title.Should().NotBeNullOrEmpty();
            task.Description.Should().NotBeNullOrEmpty();
            task.PhaseId.Should().Be(brief.PhaseId);
        }
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WhenTaskMissingTitle_ReturnsInvalidPlan()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateInvalidLlmResponseMissingTitle();
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Tasks.Should().HaveCount(1);
        result.Tasks[0].Title.Should().Contain("Implement");
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WhenTaskMissingFileScopes_ReturnsInvalidPlan()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateInvalidLlmResponseMissingFileScopes();
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("no file scopes defined"));
    }

    [Fact]
    public async Task CreateTaskPlanAsync_WhenLlmThrowsException_ReturnsFallbackPlan()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        _fakeLlmProvider.EnqueueException(new LlmProviderException("test", "LLM service failed"));

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Tasks.Should().HaveCount(1);
        result.Tasks[0].Title.Should().Contain("Implement");
    }

    [Fact]
    public async Task CreateTaskPlanAsync_SetsCreatedAtTimestamp()
    {
        // Arrange
        var beforeTest = DateTimeOffset.UtcNow;
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");
        var afterTest = DateTimeOffset.UtcNow;

        // Assert
        result.CreatedAt.Should().BeOnOrAfter(beforeTest);
        result.CreatedAt.Should().BeOnOrBefore(afterTest);
    }

    [Fact]
    public async Task CreateTaskPlanAsync_SetsCompletedAtTimestamp()
    {
        // Arrange
        var beforeTest = DateTimeOffset.UtcNow;
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");
        var afterTest = DateTimeOffset.UtcNow;

        // Assert
        result.CompletedAt.Should().NotBeNull();
        result.CompletedAt.Should().BeOnOrAfter(beforeTest);
        result.CompletedAt.Should().BeOnOrBefore(afterTest);
    }

    [Fact]
    public async Task CreateTaskPlanAsync_TasksHaveTaskJsonPath()
    {
        // Arrange
        var brief = CreateValidPhaseBrief("PH-001");
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        var result = await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        result.Tasks[0].TaskJsonPath.Should().NotBeNullOrEmpty();
        result.Tasks[0].TaskJsonPath.Should().Contain("task.json");
    }

    [Fact]
    public async Task CreateTaskPlanAsync_RequestsStructuredSchema()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var llmResponse = CreateValidLlmResponse(2);
        _fakeLlmProvider.EnqueueTextResponse(llmResponse);

        // Act
        await _sut.CreateTaskPlanAsync(brief, "RUN-001");

        // Assert
        var request = _fakeLlmProvider.Requests.Single();
        request.StructuredOutputSchema.Should().NotBeNull();
        request.StructuredOutputSchema!.Name.Should().Be("phase_plan_v1");
    }

    private static PhasePlannerTypes.PhaseBrief CreateValidPhaseBrief(string phaseId = "PH-001")
    {
        return new PhasePlannerTypes.PhaseBrief
        {
            PhaseId = phaseId,
            PhaseName = "Test Phase",
            Description = "Test phase description",
            MilestoneId = "MS-001"
        };
    }

    private static string CreateValidLlmResponse(int taskCount)
    {
        var tasks = new List<object>();
        for (int i = 1; i <= taskCount; i++)
        {
            tasks.Add(new
            {
                id = $"TSK-10{i}",
                title = $"Task {i}",
                description = $"Description for task {i}",
                fileScopes = new[] { new { path = $"src/file{i}.cs" } },
                verificationSteps = new[] { "Run unit tests" }
            });
        }

        var response = new
        {
            planId = $"PLAN-{Guid.NewGuid():N}",
            phaseId = "PH-001",
            tasks
        };

        return JsonSerializer.Serialize(response);
    }

    private static string CreateInvalidLlmResponseMissingTitle()
    {
        var response = new
        {
            planId = "plan-1",
            phaseId = "PH-001",
            tasks = new[]
            {
                new
                {
                    id = "TSK-BAD",
                    title = "",
                    description = "Description without title",
                    fileScopes = new[] { new { path = "src/file.cs" } },
                    verificationSteps = new[] { "Run tests" }
                }
            }
        };
        return JsonSerializer.Serialize(response);
    }

    private static string CreateInvalidLlmResponseMissingFileScopes()
    {
        var response = new
        {
            planId = "plan-1",
            phaseId = "PH-001",
            tasks = new[]
            {
                new
                {
                    id = "TSK-NO-SCOPE",
                    title = "Task Without File Scopes",
                    description = "Description",
                    fileScopes = Array.Empty<object>(),
                    verificationSteps = new[] { "Compile" }
                }
            }
        };
        return JsonSerializer.Serialize(response);
    }
}
