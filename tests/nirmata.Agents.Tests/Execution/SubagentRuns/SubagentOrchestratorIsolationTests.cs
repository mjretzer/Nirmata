using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.Execution.SubagentRuns;
using nirmata.Agents.Execution.Execution.TaskExecutor;
using nirmata.Agents.Execution.ToolCalling;
using nirmata.Agents.Models.Runtime;
using nirmata.Agents.Persistence.Runs;
using nirmata.Aos.Public;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution;

public class SubagentOrchestratorIsolationTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IToolCallingLoop> _toolCallingLoopMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<ILogger<SubagentOrchestrator>> _loggerMock;
    private readonly SubagentOrchestrator _sut;

    public SubagentOrchestratorIsolationTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _toolCallingLoopMock = new Mock<IToolCallingLoop>();
        _workspaceMock = new Mock<IWorkspace>();
        _loggerMock = new Mock<ILogger<SubagentOrchestrator>>();

        _workspaceMock.Setup(w => w.AosRootPath).Returns(Path.GetTempPath());

        // Setup default tool calling loop behavior
        _toolCallingLoopMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ToolCallingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCallingResult
            {
                FinalMessage = ToolCallingMessage.Assistant("Task completed successfully"),
                ConversationHistory = new List<ToolCallingMessage>
                {
                    ToolCallingMessage.System("You are a subagent"),
                    ToolCallingMessage.User("Execute the assigned task using available tools."),
                    ToolCallingMessage.Assistant("Task completed successfully")
                },
                IterationCount = 1,
                CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
                Usage = new ToolCallingUsageStats
                {
                    TotalPromptTokens = 100,
                    TotalCompletionTokens = 50,
                    IterationCount = 1
                }
            });

        _sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _toolCallingLoopMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task RunSubagentAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        Func<Task> act = async () => await _sut.RunSubagentAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RunSubagentAsync_CreatesDistinctRunRecord_PerInvocation()
    {
        SetupRunLifecycleManager("RUN-001");
        var request = CreateRequest();

        var result = await _sut.RunSubagentAsync(request);

        result.RunId.Should().Be("RUN-001");
        _runLifecycleManagerMock.Verify(x => x.StartRunAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSubagentAsync_RecordsSubagentStartCommand()
    {
        SetupRunLifecycleManager("RUN-001");
        var request = CreateRequest();

        await _sut.RunSubagentAsync(request);

        _runLifecycleManagerMock.Verify(
            x => x.RecordCommandAsync("RUN-001", "subagent", "start", "running", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunSubagentAsync_RecordsSubagentCompletionCommand()
    {
        SetupRunLifecycleManager("RUN-001");
        var request = CreateRequest();

        await _sut.RunSubagentAsync(request);

        _runLifecycleManagerMock.Verify(
            x => x.RecordCommandAsync("RUN-001", "subagent", "complete", "success", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunSubagentAsync_CreatesIsolatedWorkingDirectory()
    {
        SetupRunLifecycleManager("RUN-001");
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(tempRoot);

        var request = CreateRequest();

        await _sut.RunSubagentAsync(request);

        var tempDirs = Directory.GetDirectories(Path.Combine(tempRoot, "temp"), "subagent-*");
        tempDirs.Should().HaveCountGreaterThanOrEqualTo(1);

        try
        {
            DeleteDirectory(tempRoot);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to clean up temp directory {tempRoot}: {ex.Message}");
        }
    }

    private static void DeleteDirectory(string targetDir)
    {
        if (!Directory.Exists(targetDir)) return;

        string[] files = Directory.GetFiles(targetDir);
        string[] dirs = Directory.GetDirectories(targetDir);

        foreach (string file in files)
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (string dir in dirs)
        {
            DeleteDirectory(dir);
        }

        Directory.Delete(targetDir, false);
    }

    [Fact]
    public async Task RunSubagentAsync_PopulatesRunMetadata_WithRequestDetails()
    {
        SetupRunLifecycleManager("RUN-001");
        var request = CreateRequest(taskId: "TSK-123", subagentConfig: "custom_config");

        await _sut.RunSubagentAsync(request);

        _runLifecycleManagerMock.Verify(
            x => x.FinishRunAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.Is<Dictionary<string, object>>(d => 
                    d.ContainsKey("taskId") && 
                    d["taskId"].ToString() == "TSK-123"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunSubagentAsync_IncludesBudgetInMetadata()
    {
        SetupRunLifecycleManager("RUN-001");
        var request = CreateRequest(budget: new SubagentBudget
        {
            MaxIterations = 50,
            MaxToolCalls = 100,
            MaxExecutionTimeSeconds = 300,
            MaxTokens = 8000
        });

        await _sut.RunSubagentAsync(request);

        _runLifecycleManagerMock.Verify(
            x => x.FinishRunAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.Is<Dictionary<string, object>>(d => d.ContainsKey("metrics")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunSubagentAsync_WithParentRunId_TracksParentRelationship()
    {
        SetupRunLifecycleManager("RUN-002");
        var request = CreateRequest(parentRunId: "RUN-001");

        var result = await _sut.RunSubagentAsync(request);

        result.Should().NotBeNull();
    }

    private SubagentRunRequest CreateRequest(
        string taskId = "TSK-001",
        string subagentConfig = "task_executor",
        string? parentRunId = null,
        SubagentBudget? budget = null)
    {
        return new SubagentRunRequest
        {
            RunId = "RUN-TEST",
            TaskId = taskId,
            SubagentConfig = subagentConfig,
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            ParentRunId = parentRunId,
            CorrelationId = "corr-123",
            Budget = budget ?? new SubagentBudget()
        };
    }

    private void SetupRunLifecycleManager(string runId)
    {
        var context = new RunContext { RunId = runId, StartedAt = DateTimeOffset.UtcNow };
        _runLifecycleManagerMock
            .Setup(x => x.StartRunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        _runLifecycleManagerMock
            .Setup(x => x.RecordCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _runLifecycleManagerMock
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }
}
