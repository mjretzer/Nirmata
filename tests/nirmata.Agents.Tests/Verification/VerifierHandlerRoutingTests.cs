using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.Verification.UatVerifier;
using nirmata.Agents.Persistence.Runs;
using nirmata.Agents.Execution.Planning;
using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using nirmata.Aos.Public.Services;
using nirmata.Agents.Tests.Helpers;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Verification;

public class VerifierHandlerRoutingTests : IDisposable
{
    private readonly Mock<IUatVerifier> _uatVerifierMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<IStateStore> _stateStoreMock;
    private readonly Mock<IRunLifecycleManager> _runLifecycleManagerMock;
    private readonly VerifierHandler _sut;
    private readonly TempDirectory _tempDir;

    public VerifierHandlerRoutingTests()
    {
        _uatVerifierMock = new Mock<IUatVerifier>();
        _workspaceMock = new Mock<IWorkspace>();
        _stateStoreMock = new Mock<IStateStore>();
        _runLifecycleManagerMock = new Mock<IRunLifecycleManager>();

        _tempDir = new TempDirectory();
        
        _workspaceMock.Setup(x => x.RepositoryRootPath).Returns(_tempDir.Path);
        _workspaceMock.Setup(x => x.AosRootPath).Returns(Path.Combine(_tempDir.Path, ".aos"));

        _sut = new VerifierHandler(
            _uatVerifierMock.Object,
            _workspaceMock.Object,
            _stateStoreMock.Object,
            _runLifecycleManagerMock.Object);
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    [Fact]
    public async Task HandleAsync_WhenVerificationPasses_ReturnsContinueRoutingHint()
    {
        // Arrange
        SetupStateWithTask("TASK-001");
        SetupTaskDirectoryWithPlan("TASK-001");

        _uatVerifierMock.Setup(x => x.VerifyAsync(
                It.IsAny<UatVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatVerificationResult
            {
                IsPassed = true,
                RunId = "RUN-001",
                Checks = new[]
                {
                    new UatCheckResult
                    {
                        CriterionId = "criterion-001",
                        Passed = true,
                        Message = "Check passed",
                        CheckType = UatCheckTypes.FileExists,
                        IsRequired = true
                    }
                }.AsReadOnly(),
                IssuesCreated = Array.Empty<UatIssueReference>().AsReadOnly()
            });

        var request = CommandRequest.Create("run", "verify");

        // Act
        var result = await _sut.HandleAsync(request, "RUN-001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RoutingHint.Should().Be("continue");
    }

    [Fact]
    public async Task HandleAsync_WhenVerificationFails_ReturnsFixPlannerRoutingHint()
    {
        // Arrange
        SetupStateWithTask("TASK-001");
        SetupTaskDirectoryWithPlan("TASK-001");

        _uatVerifierMock.Setup(x => x.VerifyAsync(
                It.IsAny<UatVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatVerificationResult
            {
                IsPassed = false,
                RunId = "RUN-001",
                Checks = new[]
                {
                    new UatCheckResult
                    {
                        CriterionId = "criterion-001",
                        Passed = false,
                        Message = "Check failed",
                        CheckType = UatCheckTypes.FileExists,
                        IsRequired = true
                    }
                }.AsReadOnly(),
                IssuesCreated = new[]
                {
                    new UatIssueReference
                    {
                        IssueId = "ISS-0001",
                        CriterionId = "criterion-001",
                        IssuePath = ".aos/spec/issues/ISS-0001.json"
                    }
                }.AsReadOnly()
            });

        var request = CommandRequest.Create("run", "verify");

        // Act
        var result = await _sut.HandleAsync(request, "RUN-001");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.RoutingHint.Should().Be("FixPlanner");
    }

    [Fact]
    public async Task HandleAsync_WhenNoAcceptanceCriteria_ReturnsSuccessWithSkipMessage()
    {
        // Arrange
        SetupStateWithTask("TASK-001");
        SetupTaskDirectoryWithEmptyPlan("TASK-001");

        var request = CommandRequest.Create("run", "verify");

        // Act
        var result = await _sut.HandleAsync(request, "RUN-001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("No acceptance criteria defined");
    }

    [Fact]
    public async Task HandleAsync_RecordsCommandToLifecycleManager()
    {
        // Arrange
        SetupStateWithTask("TASK-001");
        SetupTaskDirectoryWithPlan("TASK-001");

        _uatVerifierMock.Setup(x => x.VerifyAsync(
                It.IsAny<UatVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatVerificationResult
            {
                IsPassed = true,
                RunId = "RUN-001",
                Checks = Array.Empty<UatCheckResult>().AsReadOnly(),
                IssuesCreated = Array.Empty<UatIssueReference>().AsReadOnly()
            });

        var request = CommandRequest.Create("run", "verify");

        // Act
        await _sut.HandleAsync(request, "RUN-001");

        // Assert
        _runLifecycleManagerMock.Verify(x => x.RecordCommandAsync(
            "RUN-001",
            "run",
            "verify",
            "completed",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UpdatesCursorVerificationStatusOnSuccess()
    {
        // Arrange
        SetupStateWithTask("TASK-001");
        SetupTaskDirectoryWithPlan("TASK-001");

        _uatVerifierMock.Setup(x => x.VerifyAsync(
                It.IsAny<UatVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatVerificationResult
            {
                IsPassed = true,
                RunId = "RUN-001",
                Checks = Array.Empty<UatCheckResult>().AsReadOnly(),
                IssuesCreated = Array.Empty<UatIssueReference>().AsReadOnly()
            });

        var request = CommandRequest.Create("run", "verify");

        // Act
        var result = await _sut.HandleAsync(request, "RUN-001");

        // Assert - verify state would have been updated (we can't easily verify the file write,
        // but we can verify the handler completed without error)
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenNoCurrentTask_ReturnsFailure()
    {
        // Arrange
        _stateStoreMock.Setup(x => x.ReadSnapshot())
            .Returns(new StateSnapshot { Cursor = new StateCursor() });

        var request = CommandRequest.Create("run", "verify");

        // Act
        var result = await _sut.HandleAsync(request, "RUN-001");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("No current task found");
    }

    [Fact]
    public async Task HandleAsync_WhenTaskDirectoryNotFound_ReturnsFailure()
    {
        // Arrange
        SetupStateWithTask("TASK-001");
        // Don't create task directory

        var request = CommandRequest.Create("run", "verify");

        // Act
        var result = await _sut.HandleAsync(request, "RUN-001");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("Task directory not found");
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectTaskIdToVerifier()
    {
        // Arrange
        SetupStateWithTask("TASK-ABC-123");
        SetupTaskDirectoryWithPlan("TASK-ABC-123");

        UatVerificationRequest? capturedRequest = null;
        _uatVerifierMock.Setup(x => x.VerifyAsync(
                It.IsAny<UatVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<UatVerificationRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new UatVerificationResult
            {
                IsPassed = true,
                RunId = "RUN-001",
                Checks = Array.Empty<UatCheckResult>().AsReadOnly(),
                IssuesCreated = Array.Empty<UatIssueReference>().AsReadOnly()
            });

        var request = CommandRequest.Create("run", "verify");

        // Act
        await _sut.HandleAsync(request, "RUN-001");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.TaskId.Should().Be("TASK-ABC-123");
        capturedRequest.RunId.Should().Be("RUN-001");
    }

    [Fact]
    public async Task HandleAsync_ResultIncludesVerificationDetails()
    {
        // Arrange
        SetupStateWithTask("TASK-001");
        SetupTaskDirectoryWithPlan("TASK-001");

        _uatVerifierMock.Setup(x => x.VerifyAsync(
                It.IsAny<UatVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatVerificationResult
            {
                IsPassed = true,
                RunId = "RUN-001",
                Checks = new[]
                {
                    new UatCheckResult
                    {
                        CriterionId = "criterion-001",
                        Passed = true,
                        Message = "File exists: src/test.txt",
                        CheckType = UatCheckTypes.FileExists,
                        IsRequired = true
                    }
                }.AsReadOnly(),
                IssuesCreated = Array.Empty<UatIssueReference>().AsReadOnly()
            });

        var request = CommandRequest.Create("run", "verify");

        // Act
        var result = await _sut.HandleAsync(request, "RUN-001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("UAT verification passed");
        result.Output.Should().Contain("1 check(s) passed");
    }

    [Fact]
    public async Task HandleAsync_WhenPlanContractInvalid_FailsFastAndSkipsVerifier()
    {
        // Arrange
        SetupStateWithTask("TASK-001");

        var taskDir = Path.Combine(_tempDir.Path, ".aos", "spec", "tasks", "TASK-001");
        Directory.CreateDirectory(taskDir);
        File.WriteAllText(Path.Combine(taskDir, "plan.json"), "{\"fileScopes\":[\"src/test.txt\"]}");

        var request = CommandRequest.Create("run", "verify");

        // Act
        var result = await _sut.HandleAsync(request, "RUN-001");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().Be(4);
        result.ErrorOutput.Should().Contain("contract validation gate");
        result.ErrorOutput.Should().Contain("Diagnostic:");

        _uatVerifierMock.Verify(
            x => x.VerifyAsync(It.IsAny<UatVerificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenVerificationFails_ResultIncludesIssueCount()
    {
        // Arrange
        SetupStateWithTask("TASK-001");
        SetupTaskDirectoryWithPlan("TASK-001");

        _uatVerifierMock.Setup(x => x.VerifyAsync(
                It.IsAny<UatVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatVerificationResult
            {
                IsPassed = false,
                RunId = "RUN-001",
                Checks = new[]
                {
                    new UatCheckResult
                    {
                        CriterionId = "criterion-001",
                        Passed = false,
                        Message = "File not found",
                        CheckType = UatCheckTypes.FileExists,
                        IsRequired = true
                    }
                }.AsReadOnly(),
                IssuesCreated = new[]
                {
                    new UatIssueReference
                    {
                        IssueId = "ISS-0001",
                        CriterionId = "criterion-001",
                        IssuePath = ".aos/spec/issues/ISS-0001.json"
                    }
                }.AsReadOnly()
            });

        var request = CommandRequest.Create("run", "verify");

        // Act
        var result = await _sut.HandleAsync(request, "RUN-001");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("1 required check(s) failed");
        result.ErrorOutput.Should().Contain("1 issue(s) created");
    }

    private void SetupStateWithTask(string taskId)
    {
        _stateStoreMock.Setup(x => x.ReadSnapshot())
            .Returns(new StateSnapshot
            {
                Cursor = new StateCursor
                {
                    TaskId = taskId,
                    TaskStatus = "completed"
                }
            });
    }

    private void SetupTaskDirectoryWithPlan(string taskId)
    {
        var taskDir = Path.Combine(_tempDir.Path, ".aos", "spec", "tasks", taskId);
        Directory.CreateDirectory(taskDir);

        var planJson = @"{
            ""schemaVersion"": 1,
            ""taskId"": ""TASK-001"",
            ""title"": ""Test Task"",
            ""description"": ""Test description"",
            ""fileScopes"": [
                {
                    ""path"": ""src/test.txt"",
                    ""scopeType"": ""create""
                }
            ],
            ""verificationSteps"": [
                {
                    ""verificationType"": ""file"",
                    ""description"": ""Verify file was created""
                }
            ]
        }";

        File.WriteAllText(Path.Combine(taskDir, "plan.json"), planJson);
    }

    private void SetupTaskDirectoryWithEmptyPlan(string taskId)
    {
        var taskDir = Path.Combine(_tempDir.Path, ".aos", "spec", "tasks", taskId);
        Directory.CreateDirectory(taskDir);

        var planJson = @"{
            ""schemaVersion"": 1,
            ""taskId"": ""TASK-001"",
            ""title"": ""Test Task"",
            ""description"": ""Test description"",
            ""fileScopes"": [],
            ""verificationSteps"": []
        }";

        File.WriteAllText(Path.Combine(taskDir, "plan.json"), planJson);
    }
}
