using FluentAssertions;
using nirmata.Agents.Execution.Verification.Issues;
using nirmata.Agents.Execution.Verification.UatVerifier;
using nirmata.Aos.Public;
using nirmata.Aos.Public.Services;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Verification;

public class UatVerifierTests
{
    private readonly Mock<IUatCheckRunner> _checkRunnerMock;
    private readonly Mock<IIssueWriter> _issueWriterMock;
    private readonly Mock<IUatResultWriter> _resultWriterMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly UatVerifier _sut;

    public UatVerifierTests()
    {
        _checkRunnerMock = new Mock<IUatCheckRunner>();
        _issueWriterMock = new Mock<IIssueWriter>();
        _resultWriterMock = new Mock<IUatResultWriter>();
        _workspaceMock = new Mock<IWorkspace>();

        _workspaceMock.Setup(x => x.RepositoryRootPath).Returns("/tmp/test-workspace");

        _sut = new UatVerifier(
            _checkRunnerMock.Object,
            _issueWriterMock.Object,
            _resultWriterMock.Object,
            _workspaceMock.Object);
    }

    [Fact]
    public async Task VerifyAsync_WhenAllRequiredChecksPass_ReturnsPassed()
    {
        var request = CreateVerificationRequest(new[]
        {
            new AcceptanceCriterion
            {
                Id = "criterion-001",
                Description = "File should exist",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/test.txt",
                IsRequired = true
            }
        });

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.Is<AcceptanceCriterion>(c => c.Id == "criterion-001"),
                "/tmp/test-workspace",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatCheckResult
            {
                CriterionId = "criterion-001",
                Passed = true,
                Message = "File exists: src/test.txt",
                CheckType = UatCheckTypes.FileExists,
                IsRequired = true
            });

        var result = await _sut.VerifyAsync(request);

        result.IsPassed.Should().BeTrue();
        result.RunId.Should().Be("RUN-001");
        result.Checks.Should().HaveCount(1);
        result.Checks[0].Passed.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_WhenRequiredCheckFails_ReturnsFailed()
    {
        var request = CreateVerificationRequest(new[]
        {
            new AcceptanceCriterion
            {
                Id = "criterion-001",
                Description = "File should exist",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/test.txt",
                IsRequired = true
            }
        });

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.Is<AcceptanceCriterion>(c => c.Id == "criterion-001"),
                "/tmp/test-workspace",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatCheckResult
            {
                CriterionId = "criterion-001",
                Passed = false,
                Message = "File not found: src/test.txt",
                CheckType = UatCheckTypes.FileExists,
                IsRequired = true
            });

        _issueWriterMock.Setup(x => x.CreateIssueAsync(
                "TASK-001",
                "RUN-001",
                It.Is<UatCheckResult>(r => r.CriterionId == "criterion-001"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueCreated
            {
                Id = "ISS-0001",
                FilePath = ".aos/spec/issues/ISS-0001.json"
            });

        var result = await _sut.VerifyAsync(request);

        result.IsPassed.Should().BeFalse();
        result.IssuesCreated.Should().HaveCount(1);
        result.IssuesCreated[0].IssueId.Should().Be("ISS-0001");
    }

    [Fact]
    public async Task VerifyAsync_WhenOnlyOptionalChecksFail_ReturnsPassed()
    {
        var request = CreateVerificationRequest(new[]
        {
            new AcceptanceCriterion
            {
                Id = "criterion-001",
                Description = "Required file should exist",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/required.txt",
                IsRequired = true
            },
            new AcceptanceCriterion
            {
                Id = "criterion-002",
                Description = "Optional file should exist",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/optional.txt",
                IsRequired = false
            }
        });

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.Is<AcceptanceCriterion>(c => c.Id == "criterion-001"),
                "/tmp/test-workspace",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatCheckResult
            {
                CriterionId = "criterion-001",
                Passed = true,
                Message = "File exists",
                CheckType = UatCheckTypes.FileExists,
                IsRequired = true
            });

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.Is<AcceptanceCriterion>(c => c.Id == "criterion-002"),
                "/tmp/test-workspace",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatCheckResult
            {
                CriterionId = "criterion-002",
                Passed = false,
                Message = "File not found",
                CheckType = UatCheckTypes.FileExists,
                IsRequired = false
            });

        var result = await _sut.VerifyAsync(request);

        result.IsPassed.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_WritesResultArtifact()
    {
        var request = CreateVerificationRequest(new[]
        {
            new AcceptanceCriterion
            {
                Id = "criterion-001",
                Description = "File should exist",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/test.txt",
                IsRequired = true
            }
        });

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.IsAny<AcceptanceCriterion>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UatCheckResult
            {
                CriterionId = "criterion-001",
                Passed = true,
                Message = "File exists",
                CheckType = UatCheckTypes.FileExists,
                IsRequired = true
            });

        _resultWriterMock.Setup(x => x.WriteResultAsync(
                "TASK-001",
                "RUN-001",
                It.IsAny<UatVerificationResult>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/path/to/uat-results.json");

        await _sut.VerifyAsync(request);

        _resultWriterMock.Verify(x => x.WriteResultAsync(
            "TASK-001",
            "RUN-001",
            It.Is<UatVerificationResult>(r => r.IsPassed && r.RunId == "RUN-001"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_MultipleChecks_AllAreExecuted()
    {
        var request = CreateVerificationRequest(new[]
        {
            new AcceptanceCriterion
            {
                Id = "criterion-001",
                Description = "File 1 should exist",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/file1.txt",
                IsRequired = true
            },
            new AcceptanceCriterion
            {
                Id = "criterion-002",
                Description = "File 2 should exist",
                CheckType = UatCheckTypes.FileExists,
                TargetPath = "src/file2.txt",
                IsRequired = true
            },
            new AcceptanceCriterion
            {
                Id = "criterion-003",
                Description = "Build should succeed",
                CheckType = UatCheckTypes.BuildSucceeds,
                IsRequired = true
            }
        });

        _checkRunnerMock.Setup(x => x.RunCheckAsync(
                It.IsAny<AcceptanceCriterion>(),
                "/tmp/test-workspace",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((AcceptanceCriterion c, string _, CancellationToken _) => new UatCheckResult
            {
                CriterionId = c.Id,
                Passed = true,
                Message = "Check passed",
                CheckType = c.CheckType,
                IsRequired = c.IsRequired
            });

        var result = await _sut.VerifyAsync(request);

        result.Checks.Should().HaveCount(3);
        _checkRunnerMock.Verify(x => x.RunCheckAsync(
            It.Is<AcceptanceCriterion>(c => c.Id == "criterion-001"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _checkRunnerMock.Verify(x => x.RunCheckAsync(
            It.Is<AcceptanceCriterion>(c => c.Id == "criterion-002"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _checkRunnerMock.Verify(x => x.RunCheckAsync(
            It.Is<AcceptanceCriterion>(c => c.Id == "criterion-003"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static UatVerificationRequest CreateVerificationRequest(AcceptanceCriterion[] criteria)
    {
        return new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-001",
            AcceptanceCriteria = criteria.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };
    }
}
