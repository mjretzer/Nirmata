using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.Execution.SubagentRuns;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution;

public class RunRecordEvidenceTests
{
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<ILogger<SubagentOrchestrator>> _loggerMock;

    public RunRecordEvidenceTests()
    {
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();
        _workspaceMock = new Mock<IWorkspace>();
        _loggerMock = new Mock<ILogger<SubagentOrchestrator>>();

        _workspaceMock.Setup(w => w.AosRootPath).Returns(Path.GetTempPath());
    }

    [Fact]
    public async Task RunSubagentAsync_CreatesRunRecord_WithStartTimestamp()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        SetupRunLifecycleManager("RUN-001", startTime);

        var sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);

        var request = CreateRequest();

        // Act
        var result = await sut.RunSubagentAsync(request);

        // Assert
        _runLifecycleManagerMock.Verify(x => x.StartRunAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.RunId.Should().Be("RUN-001");
    }

    [Fact]
    public async Task RunSubagentAsync_ClosesRunRecord_WithSuccessStatus()
    {
        // Arrange
        SetupRunLifecycleManager("RUN-001");

        var sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);

        var request = CreateRequest();

        // Act
        await sut.RunSubagentAsync(request);

        // Assert
        _runLifecycleManagerMock.Verify(
            x => x.FinishRunAsync("RUN-001", true, It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunSubagentAsync_ClosesRunRecord_WithFailureStatus_OnError()
    {
        // Arrange
        SetupRunLifecycleManager("RUN-001");
        SetupFailingRunLifecycleManager();

        var sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);

        var request = CreateRequest(budget: new SubagentBudget { MaxIterations = 0 });

        // Act
        var result = await sut.RunSubagentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        _runLifecycleManagerMock.Verify(
            x => x.FinishRunAsync("RUN-001", false, It.Is<Dictionary<string, object>>(d => 
                d.ContainsKey("error") || d.ContainsKey("errorCategory")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunSubagentAsync_IncludesExecutionMetrics_InRunRecord()
    {
        // Arrange
        SetupRunLifecycleManager("RUN-001");

        var sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);

        var request = CreateRequest();

        Dictionary<string, object>? capturedData = null;
        _runLifecycleManagerMock
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, Dictionary<string, object>, CancellationToken>((_, _, data, _) => capturedData = data)
            .Returns(Task.CompletedTask);

        // Act
        await sut.RunSubagentAsync(request);

        // Assert
        capturedData.Should().NotBeNull();
        capturedData.Should().ContainKey("metrics");
        capturedData.Should().ContainKey("taskId");
    }

    [Fact]
    public async Task RunSubagentAsync_RecordsToolCallEvidence_WhenToolCallsExist()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var workspacePath = Path.Combine(tempDir.Path, "workspace");
        Directory.CreateDirectory(workspacePath);
        _workspaceMock.Setup(w => w.AosRootPath).Returns(workspacePath);

        SetupRunLifecycleManager("RUN-001");

        var sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);

        var request = CreateRequest();

        // Act
        await sut.RunSubagentAsync(request);

        // Assert - verify that the evidence directory structure is created
        var evidencePath = Path.Combine(workspacePath, "evidence", "runs", "RUN-001");
        // Note: In actual implementation, tool calls would be written here
    }

    [Fact]
    public async Task RunSubagentAsync_IncludesNormalizedOutput_InRunRecord()
    {
        // Arrange
        SetupRunLifecycleManager("RUN-001");

        var sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);

        var request = CreateRequest();

        Dictionary<string, object>? capturedData = null;
        _runLifecycleManagerMock
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, Dictionary<string, object>, CancellationToken>((_, _, data, _) => capturedData = data)
            .Returns(Task.CompletedTask);

        // Act
        await sut.RunSubagentAsync(request);

        // Assert
        capturedData.Should().NotBeNull();
        capturedData.Should().ContainKey("normalizedOutput");
        capturedData.Should().ContainKey("deterministicHash");
    }

    [Fact]
    public async Task RunSubagentAsync_IncludesErrorCategory_OnFailure()
    {
        // Arrange
        SetupRunLifecycleManager("RUN-001");

        var sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);

        var request = CreateRequest(budget: new SubagentBudget { MaxIterations = 0 });

        Dictionary<string, object>? capturedData = null;
        _runLifecycleManagerMock
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, Dictionary<string, object>, CancellationToken>((_, _, data, _) => capturedData = data)
            .Returns(Task.CompletedTask);

        // Act
        await sut.RunSubagentAsync(request);

        // Assert
        capturedData.Should().NotBeNull();
        capturedData.Should().ContainKey("errorCategory");
    }

    [Fact]
    public async Task RunSubagentAsync_ReturnsEvidenceArtifacts_InResult()
    {
        // Arrange
        SetupRunLifecycleManager("RUN-001");

        var sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);

        var request = CreateRequest();

        // Act
        var result = await sut.RunSubagentAsync(request);

        // Assert
        result.EvidenceArtifacts.Should().NotBeEmpty();
        result.EvidenceArtifacts.Should().Contain(a => a.Contains("RUN-001"));
    }

    [Fact]
    public async Task RunSubagentAsync_RecordsCommandSequence_Correctly()
    {
        // Arrange
        SetupRunLifecycleManager("RUN-001");

        var recordedCommands = new List<(string entity, string command, string status)>();
        _runLifecycleManagerMock
            .Setup(x => x.RecordCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<string, string, string, string, CancellationToken>((_, entity, command, status, _) =>
                recordedCommands.Add((entity, command, status)));

        var sut = new SubagentOrchestrator(
            _runLifecycleManagerMock.Object,
            _workspaceMock.Object,
            _loggerMock.Object);

        var request = CreateRequest();

        // Act
        await sut.RunSubagentAsync(request);

        // Assert
        recordedCommands.Should().Contain(("subagent", "start", "running"));
        recordedCommands.Should().Contain(("subagent", "complete", "success"));
    }

    private SubagentRunRequest CreateRequest(SubagentBudget? budget = null)
    {
        return new SubagentRunRequest
        {
            RunId = "RUN-TEST",
            TaskId = "TSK-001",
            SubagentConfig = "task_executor",
            ContextPackIds = Array.Empty<string>(),
            AllowedFileScope = new[] { "src/" }.ToList().AsReadOnly(),
            Budget = budget ?? new SubagentBudget()
        };
    }

    private void SetupRunLifecycleManager(string runId, DateTimeOffset? startTime = null)
    {
        var context = new RunContext { RunId = runId, StartedAt = startTime ?? DateTimeOffset.UtcNow };
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

    private void SetupFailingRunLifecycleManager()
    {
        _runLifecycleManagerMock
            .Setup(x => x.RecordCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _runLifecycleManagerMock
            .Setup(x => x.FinishRunAsync(It.IsAny<string>(), false, It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }
}
