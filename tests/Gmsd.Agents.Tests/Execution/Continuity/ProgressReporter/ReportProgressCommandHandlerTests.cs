using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.Continuity.ProgressReporter;
using Gmsd.Aos.Engine.Commands.Base;
using Gmsd.Aos.Public;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Continuity.ProgressReporter;

/// <summary>
/// Integration tests for the report-progress command handler.
/// Verifies deterministic output from the command pipeline.
/// </summary>
public class ReportProgressCommandHandlerTests
{
    private readonly Mock<IProgressReporter> _progressReporterMock;
    private readonly ReportProgressCommandHandler _sut;

    public ReportProgressCommandHandlerTests()
    {
        _progressReporterMock = new Mock<IProgressReporter>();
        _sut = new ReportProgressCommandHandler(_progressReporterMock.Object);
    }

    [Fact]
    public void Metadata_ShouldHaveCorrectCommandInfo()
    {
        _sut.Metadata.Group.Should().Be("core");
        _sut.Metadata.Command.Should().Be("report-progress");
        _sut.Metadata.Id.Should().NotBeNullOrEmpty();
        _sut.Metadata.Description.Should().Contain("progress");
        _sut.Metadata.Example.Should().Contain("report-progress");
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultFormat_ReturnsJsonProgressReport()
    {
        // Arrange
        var expectedReport = CreateSampleProgressReport();
        _progressReporterMock.Setup(x => x.ReportAsync("json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var context = CommandContext.Create(Mock.Of<IWorkspace>());

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Output.Should().NotBeNullOrEmpty();

        // Verify it's valid JSON
        var json = result.Output!;
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("schemaVersion").GetString().Should().Be("1.0");
        doc.RootElement.GetProperty("cursor").Should().NotBeNull();
        doc.RootElement.GetProperty("blockers").Should().NotBeNull();
        doc.RootElement.GetProperty("nextCommand").Should().NotBeNull();
        doc.RootElement.GetProperty("timestamp").Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonFormatOption_ReturnsJsonProgressReport()
    {
        // Arrange
        var expectedReport = CreateSampleProgressReport();
        _progressReporterMock.Setup(x => x.ReportAsync("json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var context = CommandContext.Create(Mock.Of<IWorkspace>(), ct: default);
        var contextWithOptions = new CommandContext
        {
            Workspace = context.Workspace,
            CancellationToken = context.CancellationToken,
            Options = new Dictionary<string, string?> { { "format", "json" } }
        };

        // Act
        var result = await _sut.ExecuteAsync(contextWithOptions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().NotBeNullOrEmpty();
        var json = result.Output!;
        json.Should().Contain("\"cursor\"");
        json.Should().Contain("\"blockers\"");
    }

    [Fact]
    public async Task ExecuteAsync_WithMarkdownFormatOption_ReturnsMarkdownProgressReport()
    {
        // Arrange
        var expectedReport = CreateSampleProgressReport();
        var markdownOutput = "# Progress Report\n\nTest markdown";
        _progressReporterMock.Setup(x => x.ReportAsync("markdown", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);
        _progressReporterMock.Setup(x => x.ReportAsStringAsync("markdown", It.IsAny<CancellationToken>()))
            .ReturnsAsync(markdownOutput);

        var context = CommandContext.Create(Mock.Of<IWorkspace>(), ct: default);
        var contextWithOptions = new CommandContext
        {
            Workspace = context.Workspace,
            CancellationToken = context.CancellationToken,
            Options = new Dictionary<string, string?> { { "format", "markdown" } }
        };

        // Act
        var result = await _sut.ExecuteAsync(contextWithOptions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Be(markdownOutput);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidFormat_ReturnsFailure()
    {
        // Arrange
        var context = CommandContext.Create(Mock.Of<IWorkspace>(), ct: default);
        var contextWithOptions = new CommandContext
        {
            Workspace = context.Workspace,
            CancellationToken = context.CancellationToken,
            Options = new Dictionary<string, string?> { { "format", "xml" } }
        };

        // Act
        var result = await _sut.ExecuteAsync(contextWithOptions);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.ErrorOutput.Should().Contain("Invalid format 'xml'");
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be("InvalidFormat");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyFormatOption_UsesDefaultJson()
    {
        // Arrange
        var expectedReport = CreateSampleProgressReport();
        _progressReporterMock.Setup(x => x.ReportAsync("json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var context = CommandContext.Create(Mock.Of<IWorkspace>(), ct: default);
        var contextWithOptions = new CommandContext
        {
            Workspace = context.Workspace,
            CancellationToken = context.CancellationToken,
            Options = new Dictionary<string, string?> { { "format", "" } }
        };

        // Act
        var result = await _sut.ExecuteAsync(contextWithOptions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _progressReporterMock.Verify(x => x.ReportAsync("json", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FormatIsCaseInsensitive()
    {
        // Arrange
        var expectedReport = CreateSampleProgressReport();
        _progressReporterMock.Setup(x => x.ReportAsync("json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var context = CommandContext.Create(Mock.Of<IWorkspace>(), ct: default);
        var contextWithOptions = new CommandContext
        {
            Workspace = context.Workspace,
            CancellationToken = context.CancellationToken,
            Options = new Dictionary<string, string?> { { "format", "JSON" } }
        };

        // Act
        var result = await _sut.ExecuteAsync(contextWithOptions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _progressReporterMock.Verify(x => x.ReportAsync("json", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_JsonOutputIsDeterministic_SameInputProducesSameStructure()
    {
        // Arrange
        var expectedReport = CreateSampleProgressReport();
        _progressReporterMock.Setup(x => x.ReportAsync("json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var context = CommandContext.Create(Mock.Of<IWorkspace>());

        // Act
        var result1 = await _sut.ExecuteAsync(context);
        var result2 = await _sut.ExecuteAsync(context);

        // Assert - verify structure is consistent
        var json1 = result1.Output!;
        var json2 = result2.Output!;

        var doc1 = JsonDocument.Parse(json1);
        var doc2 = JsonDocument.Parse(json2);

        doc1.RootElement.GetProperty("schemaVersion").GetString()
            .Should().Be(doc2.RootElement.GetProperty("schemaVersion").GetString());

        doc1.RootElement.GetProperty("hasActiveExecution").GetBoolean()
            .Should().Be(doc2.RootElement.GetProperty("hasActiveExecution").GetBoolean());
    }

    [Fact]
    public async Task ExecuteAsync_JsonOutputContainsExpectedCursorFields()
    {
        // Arrange
        var expectedReport = new ProgressReport
        {
            SchemaVersion = "1.0",
            Cursor = new ProgressCursor
            {
                Phase = "Implementation",
                Milestone = "M1",
                Task = "TSK-0001",
                Step = "step-1"
            },
            Blockers = Array.Empty<Blocker>(),
            NextCommand = new NextCommand { Command = "continue" },
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            HasActiveExecution = true
        };

        _progressReporterMock.Setup(x => x.ReportAsync("json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var context = CommandContext.Create(Mock.Of<IWorkspace>());

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        var json = result.Output!;
        var doc = JsonDocument.Parse(json);
        var cursor = doc.RootElement.GetProperty("cursor");

        cursor.GetProperty("phase").GetString().Should().Be("Implementation");
        cursor.GetProperty("milestone").GetString().Should().Be("M1");
        cursor.GetProperty("task").GetString().Should().Be("TSK-0001");
        cursor.GetProperty("step").GetString().Should().Be("step-1");
    }

    [Fact]
    public async Task ExecuteAsync_JsonOutputContainsBlockersArray()
    {
        // Arrange
        var expectedReport = new ProgressReport
        {
            SchemaVersion = "1.0",
            Cursor = new ProgressCursor { Task = "TSK-0001" },
            Blockers = new[]
            {
                new Blocker
                {
                    Type = "step-blocked",
                    Severity = "high",
                    Description = "Step is blocked",
                    Task = "TSK-0001"
                }
            },
            NextCommand = new NextCommand { Command = "resume" },
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            HasActiveExecution = true
        };

        _progressReporterMock.Setup(x => x.ReportAsync("json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var context = CommandContext.Create(Mock.Of<IWorkspace>());

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        var json = result.Output!;
        var doc = JsonDocument.Parse(json);
        var blockers = doc.RootElement.GetProperty("blockers");

        blockers.GetArrayLength().Should().Be(1);
        var firstBlocker = blockers[0];
        firstBlocker.GetProperty("type").GetString().Should().Be("step-blocked");
        firstBlocker.GetProperty("severity").GetString().Should().Be("high");
        firstBlocker.GetProperty("task").GetString().Should().Be("TSK-0001");
    }

    [Fact]
    public async Task ExecuteAsync_JsonOutputContainsNextCommandWithArgs()
    {
        // Arrange
        var expectedReport = new ProgressReport
        {
            SchemaVersion = "1.0",
            Cursor = new ProgressCursor { Task = "TSK-0001" },
            Blockers = Array.Empty<Blocker>(),
            NextCommand = new NextCommand
            {
                Command = "execute",
                Args = new Dictionary<string, object?>
                {
                    { "step", "step-1" },
                    { "task", "TSK-0001" }
                },
                Reason = "Step is ready to execute"
            },
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            HasActiveExecution = true
        };

        _progressReporterMock.Setup(x => x.ReportAsync("json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var context = CommandContext.Create(Mock.Of<IWorkspace>());

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        var json = result.Output!;
        var doc = JsonDocument.Parse(json);
        var nextCommand = doc.RootElement.GetProperty("nextCommand");

        nextCommand.GetProperty("command").GetString().Should().Be("execute");
        nextCommand.GetProperty("reason").GetString().Should().Be("Step is ready to execute");

        var args = nextCommand.GetProperty("args");
        args.GetProperty("step").GetString().Should().Be("step-1");
        args.GetProperty("task").GetString().Should().Be("TSK-0001");
    }

    [Fact]
    public async Task ExecuteAsync_WhenProgressReporterThrows_ReturnsFailure()
    {
        // Arrange
        _progressReporterMock.Setup(x => x.ReportAsync("json", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("State store unavailable"));

        var context = CommandContext.Create(Mock.Of<IWorkspace>());

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.ErrorOutput.Should().Contain("Progress report generation failed");
        result.ErrorOutput.Should().Contain("State store unavailable");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _progressReporterMock.Setup(x => x.ReportAsync("json", cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var context = CommandContext.Create(Mock.Of<IWorkspace>(), cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _sut.ExecuteAsync(context, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_NullPropertiesInReport_AreExcludedFromJson()
    {
        // Arrange
        var expectedReport = new ProgressReport
        {
            SchemaVersion = "1.0",
            Cursor = new ProgressCursor(),
            Blockers = Array.Empty<Blocker>(),
            NextCommand = new NextCommand { Command = "start" },
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            HasActiveExecution = false,
            RunId = null
        };

        _progressReporterMock.Setup(x => x.ReportAsync("json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var context = CommandContext.Create(Mock.Of<IWorkspace>());

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        var json = result.Output!;
        json.Should().NotContain("runId"); // Null values should be excluded
    }

    private static ProgressReport CreateSampleProgressReport()
    {
        return new ProgressReport
        {
            SchemaVersion = "1.0",
            Cursor = new ProgressCursor
            {
                Phase = "Implementation",
                Task = "TSK-0001"
            },
            Blockers = Array.Empty<Blocker>(),
            NextCommand = new NextCommand { Command = "continue" },
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            HasActiveExecution = true
        };
    }
}
