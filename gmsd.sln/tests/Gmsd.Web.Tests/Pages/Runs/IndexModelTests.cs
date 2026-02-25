using FluentAssertions;
using Gmsd.Agents.Models.Results;
using Gmsd.Agents.Persistence.State;
using Gmsd.Web.Pages.Runs;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Runs;

public class IndexModelTests
{
    private readonly Mock<IRunRepository> _runRepositoryMock;
    private readonly IndexModel _sut;

    public IndexModelTests()
    {
        _runRepositoryMock = new Mock<IRunRepository>();
        _sut = new IndexModel(_runRepositoryMock.Object);
    }

    [Fact]
    public async Task OnGetAsync_WhenNoRunsExist_ReturnsEmptyList()
    {
        _runRepositoryMock
            .Setup(x => x.ListAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RunResponse>());

        await _sut.OnGetAsync();

        _sut.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task OnGetAsync_WhenRunsExist_ReturnsRunsInDescendingOrderByStartedAt()
    {
        var now = DateTimeOffset.UtcNow;
        var runs = new[]
        {
            new RunResponse
            {
                RunId = "run-001",
                CorrelationId = "COR-001",
                Success = true,
                StartedAt = now.AddHours(-2),
                CompletedAt = now.AddHours(-1)
            },
            new RunResponse
            {
                RunId = "run-002",
                CorrelationId = "COR-002",
                Success = false,
                ErrorMessage = "Test error",
                StartedAt = now.AddHours(-1),
                CompletedAt = now
            },
            new RunResponse
            {
                RunId = "run-003",
                CorrelationId = "COR-003",
                Success = true,
                StartedAt = now,
                CompletedAt = now.AddMinutes(5)
            }
        };

        _runRepositoryMock
            .Setup(x => x.ListAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        await _sut.OnGetAsync();

        _sut.Runs.Should().HaveCount(3);
        _sut.Runs[0].RunId.Should().Be("run-003");
        _sut.Runs[1].RunId.Should().Be("run-002");
        _sut.Runs[2].RunId.Should().Be("run-001");
    }

    [Fact]
    public async Task OnGetAsync_WhenRunSucceeded_HasSuccessStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var runs = new[]
        {
            new RunResponse
            {
                RunId = "run-001",
                CorrelationId = "COR-001",
                Success = true,
                StartedAt = now,
                CompletedAt = now.AddMinutes(5)
            }
        };

        _runRepositoryMock
            .Setup(x => x.ListAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        await _sut.OnGetAsync();

        _sut.Runs[0].Success.Should().BeTrue();
        _sut.Runs[0].ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_WhenRunFailed_HasErrorMessage()
    {
        var now = DateTimeOffset.UtcNow;
        var runs = new[]
        {
            new RunResponse
            {
                RunId = "run-001",
                CorrelationId = "COR-001",
                Success = false,
                ErrorMessage = "Execution failed",
                ErrorCode = "ERR-001",
                StartedAt = now,
                CompletedAt = now.AddMinutes(5)
            }
        };

        _runRepositoryMock
            .Setup(x => x.ListAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        await _sut.OnGetAsync();

        _sut.Runs[0].Success.Should().BeFalse();
        _sut.Runs[0].ErrorMessage.Should().Be("Execution failed");
        _sut.Runs[0].ErrorCode.Should().Be("ERR-001");
    }

    [Fact]
    public async Task OnGetAsync_WhenRunHasNoSuccessFlagAndNoError_HasUnknownStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var runs = new[]
        {
            new RunResponse
            {
                RunId = "run-001",
                CorrelationId = "COR-001",
                Success = false,
                StartedAt = now,
                CompletedAt = now.AddMinutes(5)
            }
        };

        _runRepositoryMock
            .Setup(x => x.ListAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        await _sut.OnGetAsync();

        _sut.Runs[0].Success.Should().BeFalse();
        _sut.Runs[0].ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_CalculatesDurationCorrectly()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var completedAt = startedAt.AddHours(1).AddMinutes(30).AddSeconds(45);
        var runs = new[]
        {
            new RunResponse
            {
                RunId = "run-001",
                CorrelationId = "COR-001",
                Success = true,
                StartedAt = startedAt,
                CompletedAt = completedAt
            }
        };

        _runRepositoryMock
            .Setup(x => x.ListAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        await _sut.OnGetAsync();

        _sut.Runs[0].Duration.Should().Be(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(30)).Add(TimeSpan.FromSeconds(45)));
    }

    [Fact]
    public async Task OnGetAsync_WhenRepositoryThrows_PropagatesException()
    {
        var expectedException = new InvalidOperationException("Database unavailable");
        _runRepositoryMock
            .Setup(x => x.ListAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var act = async () => await _sut.OnGetAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Database unavailable");
    }

    [Fact]
    public async Task OnGetAsync_CallsRepositoryListAsync()
    {
        _runRepositoryMock
            .Setup(x => x.ListAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RunResponse>());

        await _sut.OnGetAsync();

        _runRepositoryMock.Verify(x => x.ListAsync(null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
