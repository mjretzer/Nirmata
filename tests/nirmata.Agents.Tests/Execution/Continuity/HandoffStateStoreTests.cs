using FluentAssertions;
using nirmata.Agents.Execution.Continuity;
using nirmata.Aos.Contracts.State;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Continuity;

public class HandoffStateStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly HandoffStateStore _sut;

    public HandoffStateStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"handoff-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sut = new HandoffStateStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Exists_WhenNoHandoff_ReturnsFalse()
    {
        _sut.Exists().Should().BeFalse();
    }

    [Fact]
    public void Exists_AfterWriteHandoff_ReturnsTrue()
    {
        var handoff = CreateValidHandoffState();

        _sut.WriteHandoff(handoff);

        _sut.Exists().Should().BeTrue();
    }

    [Fact]
    public void ReadHandoff_WhenNoFile_ThrowsFileNotFoundException()
    {
        var act = () => _sut.ReadHandoff();

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ReadHandoff_AfterWriteHandoff_ReturnsEquivalentData()
    {
        var handoff = CreateValidHandoffState();
        _sut.WriteHandoff(handoff);

        var result = _sut.ReadHandoff();

        result.Should().NotBeNull();
        result.SchemaVersion.Should().Be(handoff.SchemaVersion);
        result.SourceRunId.Should().Be(handoff.SourceRunId);
        result.Timestamp.Should().Be(handoff.Timestamp);
        result.Cursor.TaskId.Should().Be(handoff.Cursor.TaskId);
        result.TaskContext.TaskId.Should().Be(handoff.TaskContext.TaskId);
        result.NextCommand.Name.Should().Be(handoff.NextCommand.Name);
    }

    [Fact]
    public void WriteHandoff_CreatesDeterministicJson()
    {
        var handoff = CreateValidHandoffState();

        _sut.WriteHandoff(handoff);

        var handoffPath = Path.Combine(_tempDir, ".aos", "state", "handoff.json");
        File.Exists(handoffPath).Should().BeTrue();
        var json = File.ReadAllText(handoffPath);
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"sourceRunId\"");
        json.Should().Contain("\"cursor\"");
        json.Should().Contain("\"taskContext\"");
    }

    [Fact]
    public void DeleteHandoff_WhenExists_RemovesFile()
    {
        var handoff = CreateValidHandoffState();
        _sut.WriteHandoff(handoff);

        _sut.DeleteHandoff();

        _sut.Exists().Should().BeFalse();
    }

    [Fact]
    public void DeleteHandoff_WhenNotExists_DoesNotThrow()
    {
        var act = () => _sut.DeleteHandoff();

        act.Should().NotThrow();
    }

    [Fact]
    public void HandoffPath_ReturnsCorrectPath()
    {
        var expectedPath = Path.Combine(_tempDir, ".aos", "state", "handoff.json");

        _sut.HandoffPath.Should().Be(expectedPath);
    }

    private static HandoffState CreateValidHandoffState() => new()
    {
        SchemaVersion = "1.0",
        Timestamp = DateTimeOffset.UtcNow.ToString("O"),
        SourceRunId = "RUN-20260101-120000-abc123",
        Cursor = new StateCursor
        {
            TaskId = "TSK-0001",
            PhaseId = "Implementation",
            MilestoneId = "M1"
        },
        TaskContext = new TaskContext
        {
            TaskId = "TSK-0001",
            Status = "paused"
        },
        Scope = new ScopeConstraints(),
        NextCommand = new NextCommand
        {
            Name = "continue",
            Group = "orchestrator"
        }
    };
}
