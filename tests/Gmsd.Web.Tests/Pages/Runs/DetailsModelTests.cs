using FluentAssertions;
using Gmsd.Agents.Models.Results;
using Gmsd.Agents.Persistence.State;
using Gmsd.Aos.Public;
using Gmsd.Web.Pages.Runs;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Runs;

public class DetailsModelTests
{
    private readonly Mock<IRunRepository> _runRepositoryMock;
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly Mock<ILogger<DetailsModel>> _loggerMock;
    private readonly DetailsModel _sut;

    public DetailsModelTests()
    {
        _runRepositoryMock = new Mock<IRunRepository>();
        _workspaceMock = new Mock<IWorkspace>();
        _loggerMock = new Mock<ILogger<DetailsModel>>();
        _sut = new DetailsModel(_runRepositoryMock.Object, _workspaceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task OnGetAsync_WithValidRunId_ReturnsPageResult()
    {
        var runId = "run-001";
        var run = new RunResponse
        {
            RunId = runId,
            CorrelationId = "COR-001",
            Success = true,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow
        };

        _runRepositoryMock
            .Setup(x => x.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        _workspaceMock.Setup(x => x.AosRootPath).Returns("/tmp/test-aos");

        var result = await _sut.OnGetAsync(runId);

        result.Should().BeOfType<PageResult>();
    }

    [Fact]
    public async Task OnGetAsync_WithValidRunId_LoadsRunData()
    {
        var runId = "run-001";
        var now = DateTimeOffset.UtcNow;
        var run = new RunResponse
        {
            RunId = runId,
            CorrelationId = "COR-001",
            Success = true,
            StartedAt = now.AddMinutes(-10),
            CompletedAt = now
        };

        _runRepositoryMock
            .Setup(x => x.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        _workspaceMock.Setup(x => x.AosRootPath).Returns("/tmp/test-aos");

        await _sut.OnGetAsync(runId);

        _sut.Run.Should().NotBeNull();
        _sut.Run!.RunId.Should().Be(runId);
        _sut.Run.CorrelationId.Should().Be("COR-001");
        _sut.Run.Success.Should().BeTrue();
    }

    [Fact]
    public async Task OnGetAsync_WithNullRunId_ReturnsPageResultWithNotFoundMessage()
    {
        var result = await _sut.OnGetAsync(string.Empty);

        result.Should().BeOfType<PageResult>();
        _sut.Run.Should().BeNull();
        _sut.NotFoundMessage.Should().Be("No run ID specified.");
    }

    [Fact]
    public async Task OnGetAsync_WithWhitespaceRunId_ReturnsPageResultWithNotFoundMessage()
    {
        var result = await _sut.OnGetAsync("   ");

        result.Should().BeOfType<PageResult>();
        _sut.Run.Should().BeNull();
        _sut.NotFoundMessage.Should().Be("No run ID specified.");
    }

    [Fact]
    public async Task OnGetAsync_WithNonExistentRun_ReturnsPageResultWithNotFoundMessage()
    {
        var runId = "non-existent-run";

        _runRepositoryMock
            .Setup(x => x.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunResponse?)null);

        var result = await _sut.OnGetAsync(runId);

        result.Should().BeOfType<PageResult>();
        _sut.Run.Should().BeNull();
        _sut.NotFoundMessage.Should().Be($"Run '{runId}' was not found.");
    }

    [Fact]
    public async Task OnGetAsync_WithSuccessfulRun_SetsSuccessStatus()
    {
        var runId = "run-001";
        var run = new RunResponse
        {
            RunId = runId,
            CorrelationId = "COR-001",
            Success = true,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow
        };

        _runRepositoryMock
            .Setup(x => x.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        _workspaceMock.Setup(x => x.AosRootPath).Returns("/tmp/test-aos");

        await _sut.OnGetAsync(runId);

        _sut.Run.Should().NotBeNull();
        _sut.Run!.Success.Should().BeTrue();
        _sut.Run.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task OnGetAsync_WithFailedRun_SetsErrorStatusAndMessage()
    {
        var runId = "run-001";
        var run = new RunResponse
        {
            RunId = runId,
            CorrelationId = "COR-001",
            Success = false,
            ErrorMessage = "Execution failed",
            ErrorCode = "ERR-001",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow
        };

        _runRepositoryMock
            .Setup(x => x.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        _workspaceMock.Setup(x => x.AosRootPath).Returns("/tmp/test-aos");

        await _sut.OnGetAsync(runId);

        _sut.Run.Should().NotBeNull();
        _sut.Run!.Success.Should().BeFalse();
        _sut.Run.ErrorMessage.Should().Be("Execution failed");
        _sut.Run.ErrorCode.Should().Be("ERR-001");
    }

    [Fact]
    public async Task OnGetAsync_SetsArtifactPathsCorrectly()
    {
        var runId = "run-001";
        var aosRoot = "/tmp/test-aos";
        var run = new RunResponse
        {
            RunId = runId,
            CorrelationId = "COR-001",
            Success = true,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        _runRepositoryMock
            .Setup(x => x.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        _workspaceMock.Setup(x => x.AosRootPath).Returns(aosRoot);

        await _sut.OnGetAsync(runId);

        _sut.SummaryArtifactPath.Should().Be(Path.Combine(aosRoot, "evidence", "runs", runId, "summary.json"));
        _sut.CommandsArtifactPath.Should().Be(Path.Combine(aosRoot, "evidence", "runs", runId, "commands.json"));
    }

    [Fact]
    public async Task OnGetAsync_CalculatesDurationCorrectly()
    {
        var runId = "run-001";
        var startedAt = DateTimeOffset.UtcNow;
        var completedAt = startedAt.AddHours(1).AddMinutes(30).AddSeconds(45);
        var run = new RunResponse
        {
            RunId = runId,
            CorrelationId = "COR-001",
            Success = true,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };

        _runRepositoryMock
            .Setup(x => x.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        _workspaceMock.Setup(x => x.AosRootPath).Returns("/tmp/test-aos");

        await _sut.OnGetAsync(runId);

        _sut.Run.Should().NotBeNull();
        _sut.Run!.Duration.Should().Be(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(30)).Add(TimeSpan.FromSeconds(45)));
    }

    [Fact]
    public async Task OnGetAsync_WhenRepositoryThrows_PropagatesException()
    {
        var runId = "run-001";
        var expectedException = new InvalidOperationException("Database unavailable");

        _runRepositoryMock
            .Setup(x => x.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var act = async () => await _sut.OnGetAsync(runId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Database unavailable");
    }

    [Fact]
    public async Task OnGetAsync_CallsRepositoryGetAsync()
    {
        var runId = "run-001";
        var run = new RunResponse
        {
            RunId = runId,
            CorrelationId = "COR-001",
            Success = true,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        _runRepositoryMock
            .Setup(x => x.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        _workspaceMock.Setup(x => x.AosRootPath).Returns("/tmp/test-aos");

        await _sut.OnGetAsync(runId);

        _runRepositoryMock.Verify(x => x.GetAsync(runId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_InitializesEmptyLogsList()
    {
        var runId = "run-001";
        var aosRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var run = new RunResponse
        {
            RunId = runId,
            CorrelationId = "COR-001",
            Success = true,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        _runRepositoryMock
            .Setup(x => x.GetAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        _workspaceMock.Setup(x => x.AosRootPath).Returns(aosRoot);

        await _sut.OnGetAsync(runId);

        _sut.Logs.Should().NotBeNull();
        _sut.Logs.Should().BeEmpty();

        // Cleanup
        try
        {
            if (Directory.Exists(aosRoot))
            {
                Directory.Delete(aosRoot, true);
            }
        }
        catch { }
    }
}
