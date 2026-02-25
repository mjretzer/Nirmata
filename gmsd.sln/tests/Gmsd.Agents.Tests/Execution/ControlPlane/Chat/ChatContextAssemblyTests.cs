using Xunit;
using Moq;
using Gmsd.Agents.Execution.ControlPlane.Chat;
using Gmsd.Agents.Execution.Preflight;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Aos.Public;
using Gmsd.Aos.Contracts.State;
using System.Text.Json;

namespace Gmsd.Agents.Tests.Execution.ControlPlane.Chat;

public class ChatContextAssemblyTests
{
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly FakeStateStore _fakeStateStore;
    private readonly Mock<ICommandRegistry> _commandRegistryMock;

    public ChatContextAssemblyTests()
    {
        _workspaceMock = new Mock<IWorkspace>();
        _fakeStateStore = new FakeStateStore();
        _commandRegistryMock = new Mock<ICommandRegistry>();
    }

    private ChatContextAssembly CreateSut()
    {
        return new ChatContextAssembly(
            _workspaceMock.Object,
            _fakeStateStore,
            _commandRegistryMock.Object);
    }

    [Fact]
    public async Task AssembleAsync_WithNullWorkspace_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChatContextAssembly(null!, _fakeStateStore, _commandRegistryMock.Object));
    }

    [Fact]
    public async Task AssembleAsync_WithNullStateStore_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChatContextAssembly(_workspaceMock.Object, null!, _commandRegistryMock.Object));
    }

    [Fact]
    public async Task AssembleAsync_WithNullCommandRegistry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ChatContextAssembly(_workspaceMock.Object, _fakeStateStore, null!));
    }

    [Fact]
    public async Task AssembleAsync_ReturnsStateContext()
    {
        SetupBasicState();

        var result = await CreateSut().AssembleAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.State);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AssembleAsync_WithCursorInSnapshot_PopulatesState()
    {
        _fakeStateStore.SetSnapshot(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "PH-0001",
                TaskId = "T-001",
                TaskStatus = "in_progress"
            }
        });
        _commandRegistryMock.Setup(r => r.GetAllCommands()).Returns(new List<CommandRegistration>());

        var result = await CreateSut().AssembleAsync();

        Assert.Equal("PH-0001", result.State.CurrentPhaseId);
        Assert.Equal("T-001", result.State.CurrentTaskId);
        Assert.Equal("in_progress", result.State.LastRunStatus);
        Assert.Equal("T-001", result.State.Cursor); // TaskId takes precedence
    }

    [Fact]
    public async Task AssembleAsync_WithOnlyPhaseCursor_PopulatesCursor()
    {
        _fakeStateStore.SetSnapshot(new StateSnapshot
        {
            Cursor = new StateCursor
            {
                PhaseId = "PH-0002",
                TaskId = null,
                TaskStatus = null
            }
        });
        _commandRegistryMock.Setup(r => r.GetAllCommands()).Returns(new List<CommandRegistration>());

        var result = await CreateSut().AssembleAsync();

        Assert.Equal("PH-0002", result.State.CurrentPhaseId);
        Assert.Equal("PH-0002", result.State.Cursor); // PhaseId when no TaskId
    }

    [Fact]
    public async Task AssembleAsync_WithNullCursor_ReturnsNullCursor()
    {
        SetupBasicState();

        var result = await CreateSut().AssembleAsync();

        Assert.Null(result.State.Cursor);
        Assert.Null(result.State.CurrentPhaseId);
        Assert.Null(result.State.CurrentTaskId);
    }

    [Fact]
    public async Task AssembleAsync_WithCommands_PopulatesAvailableCommands()
    {
        SetupBasicState();
        var commands = new List<CommandRegistration>
        {
            new() { Name = "run", Group = "workflow", SideEffect = SideEffect.Write, Description = "Run a task" },
            new() { Name = "status", Group = "query", SideEffect = SideEffect.ReadOnly, Description = "Check status" }
        };
        _commandRegistryMock.Setup(r => r.GetAllCommands()).Returns(commands);

        var result = await CreateSut().AssembleAsync();

        Assert.Equal(2, result.AvailableCommands.Count);
        Assert.Contains(result.AvailableCommands, c => c.Name == "run" && c.Syntax == "/run");
        Assert.Contains(result.AvailableCommands, c => c.Name == "status" && c.Description == "Check status");
    }

    [Fact]
    public async Task AssembleAsync_WithNoCommands_ReturnsEmptyList()
    {
        SetupBasicState();
        _commandRegistryMock.Setup(r => r.GetAllCommands()).Returns(new List<CommandRegistration>());

        var result = await CreateSut().AssembleAsync();

        Assert.Empty(result.AvailableCommands);
    }

    [Fact]
    public async Task AssembleAsync_WithNullCommandDescription_UsesDefault()
    {
        SetupBasicState();
        var commands = new List<CommandRegistration>
        {
            new() { Name = "test", Group = "test", SideEffect = SideEffect.None, Description = null }
        };
        _commandRegistryMock.Setup(r => r.GetAllCommands()).Returns(commands);

        var result = await CreateSut().AssembleAsync();

        Assert.Equal("No description available", result.AvailableCommands[0].Description);
    }

    [Fact]
    public async Task AssembleAsync_CachesResult()
    {
        SetupBasicState();
        var sut = CreateSut();

        var result1 = await sut.AssembleAsync();
        var result2 = await sut.AssembleAsync();

        // Results should be the same cached object
        Assert.Same(result1, result2);
    }

    [Fact]
    public async Task AssembleAsync_UsesDefaultMaxContextTokens()
    {
        var sut = CreateSut();

        Assert.Equal(2000, sut.MaxContextTokens);
    }

    [Fact]
    public async Task AssembleAsync_CanSetCustomMaxContextTokens()
    {
        var sut = new ChatContextAssembly(
            _workspaceMock.Object,
            _fakeStateStore,
            _commandRegistryMock.Object)
        {
            MaxContextTokens = 1000
        };

        Assert.Equal(1000, sut.MaxContextTokens);
    }

    [Fact]
    public async Task AssembleAsync_WhenStateStoreThrows_ReturnsDegradedContext()
    {
        // Create a throwing state store
        var throwingStore = new ThrowingStateStore();
        _commandRegistryMock.Setup(r => r.GetAllCommands()).Returns(new List<CommandRegistration>());
        var sut = new ChatContextAssembly(_workspaceMock.Object, throwingStore, _commandRegistryMock.Object);

        var result = await sut.AssembleAsync();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotNull(result.State); // Should still have empty state
        Assert.NotNull(result.AvailableCommands); // Should still have commands
    }

    [Fact]
    public async Task AssembleAsync_WithRunEvents_PopulatesRecentRuns()
    {
        SetupBasicState();
        var events = new List<StateEventEntry>
        {
            CreateEventEntry("run.started", "run-001", DateTime.UtcNow.AddMinutes(-5)),
            CreateEventEntry("run.completed", "run-001", DateTime.UtcNow.AddMinutes(-4)),
            CreateEventEntry("run.failed", "run-002", DateTime.UtcNow.AddMinutes(-2))
        };
        _fakeStateStore.SetEvents(events);

        var result = await CreateSut().AssembleAsync();

        Assert.Equal(3, result.RecentRuns.Count);
        Assert.Contains(result.RecentRuns, r => r.RunId == "run-001" && r.Status == "run.started");
        Assert.Contains(result.RecentRuns, r => r.RunId == "run-002" && r.Status == "run.failed");
    }

    [Fact]
    public async Task AssembleAsync_WithNonRunEvents_FiltersThemOut()
    {
        SetupBasicState();
        var events = new List<StateEventEntry>
        {
            CreateEventEntry("run.started", "run-001", DateTime.UtcNow),
            CreateEventEntry("state.changed", "state-001", DateTime.UtcNow), // Should be filtered
            CreateEventEntry("run.completed", "run-001", DateTime.UtcNow)
        };
        _fakeStateStore.SetEvents(events);

        var result = await CreateSut().AssembleAsync();

        Assert.Equal(2, result.RecentRuns.Count);
        Assert.DoesNotContain(result.RecentRuns, r => r.RunId == "state-001");
    }

    [Fact]
    public async Task AssembleAsync_WithEmptyEvents_ReturnsEmptyRecentRuns()
    {
        SetupBasicState();
        _fakeStateStore.SetEvents(new List<StateEventEntry>());

        var result = await CreateSut().AssembleAsync();

        Assert.Empty(result.RecentRuns);
    }

    [Fact]
    public async Task AssembleAsync_WithNoEventsSet_ReturnsEmptyRecentRuns()
    {
        SetupBasicState();
        // Don't set any events

        var result = await CreateSut().AssembleAsync();

        Assert.Empty(result.RecentRuns);
    }

    private void SetupBasicState()
    {
        _fakeStateStore.Reset();
        _fakeStateStore.SetSnapshot(new StateSnapshot());
        _commandRegistryMock.Setup(r => r.GetAllCommands()).Returns(new List<CommandRegistration>());
    }

    private static StateEventEntry CreateEventEntry(string eventType, string eventId, DateTime? timestamp = null)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            eventType,
            eventId,
            timestamp = timestamp ?? DateTime.UtcNow
        });
        return new StateEventEntry { Payload = payload };
    }

    private class ThrowingStateStore : IStateStore
    {
        public void EnsureWorkspaceInitialized() { }
        public StateSnapshot ReadSnapshot() => throw new InvalidOperationException("Store error");
        public void AppendEvent(JsonElement payload) { }
        public StateEventTailResponse TailEvents(StateEventTailRequest request) => new() { Items = new List<StateEventEntry>() };
    }
}
