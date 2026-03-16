using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.Continuity.HistoryWriter;
using nirmata.Aos.Public.Models;
using nirmata.Aos.Engine.Evidence;
using nirmata.Aos.Public;
using Moq;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Continuity.HistoryWriter;

/// <summary>
/// Integration tests for the write-history command handler.
/// Verifies summary.md append with evidence links.
/// </summary>
public class WriteHistoryCommandHandlerTests
{
    private readonly Mock<IHistoryWriter> _historyWriterMock;
    private readonly WriteHistoryCommandHandler _sut;

    public WriteHistoryCommandHandlerTests()
    {
        _historyWriterMock = new Mock<IHistoryWriter>();
        _sut = new WriteHistoryCommandHandler(_historyWriterMock.Object);
    }

    private static string CreateValidRunId() => Guid.NewGuid().ToString("N").ToLowerInvariant();

    [Fact]
    public void Metadata_ShouldHaveCorrectCommandInfo()
    {
        _sut.Metadata.Group.Should().Be("core");
        _sut.Metadata.Command.Should().Be("write-history");
        _sut.Metadata.Id.Should().NotBeNullOrEmpty();
        _sut.Metadata.Description.Should().Contain("history");
        _sut.Metadata.Description.Should().Contain("summary.md");
        _sut.Metadata.Example.Should().Contain("write-history");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRunId_AppendsHistoryEntry()
    {
        // Arrange
        var runId = CreateValidRunId();
        var entry = CreateSampleHistoryEntry(runId);

        _historyWriterMock.Setup(x => x.Exists(runId, null)).Returns(false);
        _historyWriterMock.Setup(x => x.AppendAsync(runId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _historyWriterMock.Setup(x => x.SummaryPath).Returns("/tmp/aos/.aos/spec/summary.md");

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain($"History entry written for {runId}");
        result.Output.Should().Contain(entry.Timestamp);
        result.Output.Should().Contain(entry.Verification.Status);
        _historyWriterMock.Verify(x => x.AppendAsync(runId, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskId_AppendsTaskHistoryEntry()
    {
        // Arrange
        var runId = CreateValidRunId();
        var taskId = "TSK-000001";
        var entry = CreateSampleHistoryEntry(runId, taskId);

        _historyWriterMock.Setup(x => x.Exists(runId, taskId)).Returns(false);
        _historyWriterMock.Setup(x => x.AppendAsync(runId, taskId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _historyWriterMock.Setup(x => x.SummaryPath).Returns("/tmp/aos/.aos/spec/summary.md");

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId },
            Options = new Dictionary<string, string?> { { "task", taskId } }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain($"History entry written for {runId}/{taskId}");
        _historyWriterMock.Verify(x => x.AppendAsync(runId, taskId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNarrative_AppendsHistoryWithNarrative()
    {
        // Arrange
        var runId = CreateValidRunId();
        var narrative = "Completed implementation with all tests passing.";
        var entry = CreateSampleHistoryEntry(runId, narrative: narrative);

        _historyWriterMock.Setup(x => x.Exists(runId, null)).Returns(false);
        _historyWriterMock.Setup(x => x.AppendAsync(runId, null, narrative, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _historyWriterMock.Setup(x => x.SummaryPath).Returns("/tmp/aos/.aos/spec/summary.md");

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId },
            Options = new Dictionary<string, string?> { { "narrative", narrative } }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _historyWriterMock.Verify(x => x.AppendAsync(runId, null, narrative, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithAllOptions_AppendsCompleteHistoryEntry()
    {
        // Arrange
        var runId = CreateValidRunId();
        var taskId = "TSK-000001";
        var narrative = "Feature implementation complete.";
        var entry = CreateSampleHistoryEntry(runId, taskId, narrative);

        _historyWriterMock.Setup(x => x.Exists(runId, taskId)).Returns(false);
        _historyWriterMock.Setup(x => x.AppendAsync(runId, taskId, narrative, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _historyWriterMock.Setup(x => x.SummaryPath).Returns("/tmp/aos/.aos/spec/summary.md");

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId },
            Options = new Dictionary<string, string?>
            {
                { "task", taskId },
                { "narrative", narrative }
            }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain($"History entry written for {runId}/{taskId}");
        _historyWriterMock.Verify(x => x.AppendAsync(runId, taskId, narrative, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutRunId_ReturnsFailure()
    {
        // Arrange
        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = Array.Empty<string>()
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.ErrorOutput.Should().Contain("Run ID is required");
        result.Errors.Should().Contain(e => e.Code == "MissingRunId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("RUN-123")]
    [InlineData("1234567890123456789012345678901g")] // 32 chars but contains non-hex
    public async Task ExecuteAsync_WithInvalidRunId_ReturnsFailure(string invalidRunId)
    {
        // Arrange
        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { invalidRunId }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.ErrorOutput.Should().Contain("Invalid run ID format");
        result.Errors.Should().Contain(e => e.Code == "InvalidRunId");
    }

    [Fact]
    public async Task ExecuteAsync_WhenEntryExists_ReturnsFailure()
    {
        // Arrange
        var runId = CreateValidRunId();

        _historyWriterMock.Setup(x => x.Exists(runId, null)).Returns(true);

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.ErrorOutput.Should().Contain("History entry already exists");
        result.ErrorOutput.Should().Contain("Use --force to overwrite");
        result.Errors.Should().Contain(e => e.Code == "EntryExists");
    }

    [Fact]
    public async Task ExecuteAsync_WithForceOption_OverwritesExistingEntry()
    {
        // Arrange
        var runId = CreateValidRunId();
        var entry = CreateSampleHistoryEntry(runId);

        _historyWriterMock.Setup(x => x.Exists(runId, null)).Returns(true); // Entry exists
        _historyWriterMock.Setup(x => x.AppendAsync(runId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _historyWriterMock.Setup(x => x.SummaryPath).Returns("/tmp/aos/.aos/spec/summary.md");

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId },
            Options = new Dictionary<string, string?> { { "force", "true" } }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _historyWriterMock.Verify(x => x.AppendAsync(runId, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_OutputIncludesCommitHash_WhenAvailable()
    {
        // Arrange
        var runId = CreateValidRunId();
        var commitHash = "abc123def456";
        var entry = CreateSampleHistoryEntry(runId, commitHash: commitHash);

        _historyWriterMock.Setup(x => x.Exists(runId, null)).Returns(false);
        _historyWriterMock.Setup(x => x.AppendAsync(runId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _historyWriterMock.Setup(x => x.SummaryPath).Returns("/tmp/aos/.aos/spec/summary.md");

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain($"Commit: {commitHash}");
    }

    [Fact]
    public async Task ExecuteAsync_OutputExcludesCommitHash_WhenNotAvailable()
    {
        // Arrange
        var runId = CreateValidRunId();
        var entry = CreateSampleHistoryEntry(runId, commitHash: null);

        _historyWriterMock.Setup(x => x.Exists(runId, null)).Returns(false);
        _historyWriterMock.Setup(x => x.AppendAsync(runId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _historyWriterMock.Setup(x => x.SummaryPath).Returns("/tmp/aos/.aos/spec/summary.md");

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().NotContain("Commit:");
    }

    [Fact]
    public async Task ExecuteAsync_WhenEvidenceNotFound_ReturnsFailure()
    {
        // Arrange
        var runId = CreateValidRunId();

        _historyWriterMock.Setup(x => x.Exists(runId, null)).Returns(false);
        _historyWriterMock.Setup(x => x.AppendAsync(runId, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException($"Evidence not found for run '{runId}'."));

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.ErrorOutput.Should().Contain("Evidence not found");
        result.Errors.Should().Contain(e => e.Code == "EvidenceNotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WhenHistoryWriterThrows_ReturnsFailure()
    {
        // Arrange
        var runId = CreateValidRunId();

        _historyWriterMock.Setup(x => x.Exists(runId, null)).Returns(false);
        _historyWriterMock.Setup(x => x.AppendAsync(runId, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("State store unavailable"));

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.ErrorOutput.Should().Contain("History write failed");
        result.ErrorOutput.Should().Contain("State store unavailable");
        result.Errors.Should().Contain(e => e.Code == "HistoryWriteFailed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        var runId = CreateValidRunId();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _historyWriterMock.Setup(x => x.AppendAsync(runId, null, null, cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId },
            CancellationToken = cts.Token
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _sut.ExecuteAsync(context, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_SummaryPathIsIncludedInOutput()
    {
        // Arrange
        var runId = CreateValidRunId();
        var summaryPath = "/custom/path/to/summary.md";
        var entry = CreateSampleHistoryEntry(runId);

        _historyWriterMock.Setup(x => x.Exists(runId, null)).Returns(false);
        _historyWriterMock.Setup(x => x.AppendAsync(runId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _historyWriterMock.Setup(x => x.SummaryPath).Returns(summaryPath);

        var context = new CommandContext
        {
            Workspace = Mock.Of<IWorkspace>(),
            Arguments = new[] { runId }
        };

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain($"Summary: {summaryPath}");
    }

    private static HistoryEntry CreateSampleHistoryEntry(
        string runId,
        string? taskId = null,
        string? narrative = null,
        string? commitHash = null)
    {
        return new HistoryEntry
        {
            SchemaVersion = "1.0",
            Key = taskId != null ? $"{runId}/{taskId}" : runId,
            RunId = runId,
            TaskId = taskId,
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            Verification = new VerificationProof
            {
                Status = "passed",
                Method = "run-verifier"
            },
            CommitHash = commitHash,
            Evidence = new List<EvidencePointer>
            {
                new() { Type = "summary", Path = $".aos/evidence/runs/{runId}/summary.json" }
            },
            Narrative = narrative
        };
    }
}
