using FluentAssertions;
using Gmsd.Agents.Execution.Verification.Issues;
using Gmsd.Agents.Execution.Verification.UatVerifier;
using Gmsd.Aos.Public;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Verification;

/// <summary>
/// Tests for UAT verifier with LLM validation of acceptance criteria.
/// Verifies that verifier validates acceptance criteria and creates diagnostics on failure.
/// </summary>
public class UatVerifierLlmValidationTests
{
    private readonly Mock<IUatCheckRunner> _checkRunnerMock;
    private readonly Mock<IIssueWriter> _issueWriterMock;
    private readonly Mock<IUatResultWriter> _resultWriterMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly UatVerifier _sut;

    public UatVerifierLlmValidationTests()
    {
        _checkRunnerMock = new Mock<IUatCheckRunner>();
        _issueWriterMock = new Mock<IIssueWriter>();
        _resultWriterMock = new Mock<IUatResultWriter>();
        _workspaceMock = new Mock<IWorkspace>();

        _workspaceMock.Setup(x => x.RepositoryRootPath).Returns("/tmp/test-workspace");
        _workspaceMock.Setup(x => x.AosRootPath).Returns("/tmp/test-workspace/.aos");

        _sut = new UatVerifier(
            _checkRunnerMock.Object,
            _issueWriterMock.Object,
            _resultWriterMock.Object,
            _workspaceMock.Object);
    }

    [Fact]
    public async Task VerifyAsync_WithValidAcceptanceCriteria_ExecutesAllChecks()
    {
        // Arrange
        var criteria = new[]
        {
            new AcceptanceCriterion
            {
                Id = "criterion-001",
                Description = "Login form should render",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/components/LoginForm.tsx",
                IsRequired = true
            },
            new AcceptanceCriterion
            {
                Id = "criterion-002",
                Description = "Session manager should exist",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/services/SessionManager.ts",
                IsRequired = true
            }
        };

        var request = new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-001",
            AcceptanceCriteria = criteria.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.IsAny<AcceptanceCriterion>(),
                "/tmp/test-workspace",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((AcceptanceCriterion c, string _, CancellationToken _) => new UatCheckResult
            {
                CriterionId = c.Id,
                Passed = true,
                Message = $"Check passed for {c.Id}",
                CheckType = c.CheckType,
                IsRequired = c.IsRequired
            });

        _resultWriterMock.Setup(x => x.WriteResultAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<UatVerificationResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/path/to/result.json");

        // Act
        var result = await _sut.VerifyAsync(request);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.Checks.Should().HaveCount(2);
        _checkRunnerMock.Verify(
            x => x.RunCheckAsync(
                It.IsAny<AcceptanceCriterion>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task VerifyAsync_WithMalformedCriteria_HandlesGracefully()
    {
        // Arrange
        var criteria = new[]
        {
            new AcceptanceCriterion
            {
                Id = "criterion-001",
                Description = "Test criterion",
                CheckType = "invalid-check-type",
                IsRequired = true
            }
        };

        var request = new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-001",
            AcceptanceCriteria = criteria.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.IsAny<AcceptanceCriterion>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatCheckResult
            {
                CriterionId = "criterion-001",
                Passed = false,
                Message = "Unsupported check type: invalid-check-type",
                CheckType = "invalid-check-type",
                IsRequired = true
            });

        _resultWriterMock.Setup(x => x.WriteResultAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<UatVerificationResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/path/to/result.json");

        // Act
        var result = await _sut.VerifyAsync(request);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Checks.Should().HaveCount(1);
        result.Checks[0].Passed.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_WithRequiredCheckFailing_CreatesIssue()
    {
        // Arrange
        var criteria = new[]
        {
            new AcceptanceCriterion
            {
                Id = "criterion-001",
                Description = "Required file should exist",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/missing.ts",
                IsRequired = true
            }
        };

        var request = new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-001",
            AcceptanceCriteria = criteria.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.IsAny<AcceptanceCriterion>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatCheckResult
            {
                CriterionId = "criterion-001",
                Passed = false,
                Message = "File not found: src/missing.ts",
                CheckType = UatCheckTypes.FileExists,
                IsRequired = true
            });

        _issueWriterMock.Setup(x => x.CreateIssueAsync(
                "TASK-001",
                "RUN-001",
                It.IsAny<UatCheckResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueCreated
            {
                Id = "ISS-0001",
                FilePath = ".aos/spec/issues/ISS-0001.json"
            });

        _resultWriterMock.Setup(x => x.WriteResultAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<UatVerificationResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/path/to/result.json");

        // Act
        var result = await _sut.VerifyAsync(request);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.IssuesCreated.Should().HaveCount(1);
        result.IssuesCreated[0].IssueId.Should().Be("ISS-0001");
    }

    [Fact]
    public async Task VerifyAsync_WithOptionalCheckFailing_DoesNotCreateIssue()
    {
        // Arrange
        var criteria = new[]
        {
            new AcceptanceCriterion
            {
                Id = "criterion-001",
                Description = "Optional file should exist",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/optional.ts",
                IsRequired = false
            }
        };

        var request = new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-001",
            AcceptanceCriteria = criteria.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.IsAny<AcceptanceCriterion>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatCheckResult
            {
                CriterionId = "criterion-001",
                Passed = false,
                Message = "File not found: src/optional.ts",
                CheckType = UatCheckTypes.FileExists,
                IsRequired = false
            });

        _resultWriterMock.Setup(x => x.WriteResultAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<UatVerificationResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/path/to/result.json");

        // Act
        var result = await _sut.VerifyAsync(request);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.IssuesCreated.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyAsync_WritesResultArtifact()
    {
        // Arrange
        var criteria = new[]
        {
            new AcceptanceCriterion
            {
                Id = "criterion-001",
                Description = "Test",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "test.txt",
                IsRequired = true
            }
        };

        var request = new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-001",
            AcceptanceCriteria = criteria.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.IsAny<AcceptanceCriterion>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatCheckResult
            {
                CriterionId = "criterion-001",
                Passed = true,
                Message = "Check passed",
                CheckType = UatCheckTypes.FileExists,
                IsRequired = true
            });

        _resultWriterMock.Setup(x => x.WriteResultAsync(
                "TASK-001",
                "RUN-001",
                It.IsAny<UatVerificationResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/path/to/result.json");

        // Act
        await _sut.VerifyAsync(request);

        // Assert
        _resultWriterMock.Verify(
            x => x.WriteResultAsync(
                "TASK-001",
                "RUN-001",
                It.Is<UatVerificationResult>(r => r.IsPassed && r.RunId == "RUN-001"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
