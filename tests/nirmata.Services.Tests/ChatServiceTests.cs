<<<<<<< C:/Users/James Lestler/Desktop/Projects/Nirmata/tests/nirmata.Services.Tests/ChatServiceTests.cs
using Moq;
using nirmata.Services.Implementations;
using nirmata.Services.Interfaces;
using Xunit;

namespace nirmata.Services.Tests;

/// <summary>
/// Verifies that <see cref="ChatService"/> fails closed on unknown workspaces —
/// returning <see langword="null"/> without dispatching to the orchestrator gate service.
/// </summary>
public sealed class ChatServiceTests
{
    private static readonly Guid _unknownId = Guid.NewGuid();
    private static readonly string _knownRoot = Path.Combine(Path.GetTempPath(), "nirm-chat-svc-tests");

    // Use Strict so any unexpected call to the gate service throws immediately,
    // proving no command dispatch was attempted.
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly Mock<IOrchestratorGateService> _gateService = new(MockBehavior.Strict);
    private readonly ChatService _sut;

    public ChatServiceTests()
    {
        _sut = new ChatService(_workspaceService.Object, _gateService.Object);
    }

    // ── GetSnapshotAsync — unknown workspace ──────────────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_UnknownWorkspace_ReturnsNull()
    {
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.GetSnapshotAsync(_unknownId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSnapshotAsync_UnknownWorkspace_DoesNotCallGateService()
    {
        // Arrange: workspace is unknown → service returns null.
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        await _sut.GetSnapshotAsync(_unknownId);

        // Assert: strict mock throws if GetGateAsync was called — explicit verify adds clarity.
        _gateService.Verify(
            s => s.GetGateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gateService.Verify(
            s => s.GetTimelineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── ProcessTurnAsync — unknown workspace ──────────────────────────────────

    [Fact]
    public async Task ProcessTurnAsync_UnknownWorkspace_ReturnsNull()
    {
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.ProcessTurnAsync(_unknownId, "execute-plan");

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessTurnAsync_UnknownWorkspace_DoesNotDispatchCommand()
    {
        // Arrange: workspace is unknown — dispatch must not reach the gate service.
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act: use an explicit command token to confirm classification does not cause dispatch.
        await _sut.ProcessTurnAsync(_unknownId, "execute-plan");

        // Assert: strict mock would throw on any call; explicit verify proves the invariant.
        _gateService.Verify(
            s => s.GetGateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gateService.Verify(
            s => s.GetTimelineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessTurnAsync_UnknownWorkspace_DoesNotDispatchFreeformInput()
    {
        // Same guarantee holds for non-command (conversational) input.
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await _sut.ProcessTurnAsync(_unknownId, "what is the current status?");

        _gateService.Verify(
            s => s.GetGateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
=======
using Moq;
using nirmata.Services.Implementations;
using nirmata.Services.Interfaces;
using nirmata.Data.Dto.Models.Chat;
using nirmata.Data.Dto.Models.OrchestratorGate;
using Xunit;

namespace nirmata.Services.Tests;

/// <summary>
/// Verifies that <see cref="ChatService"/> fails closed on unknown workspaces —
/// returning <see langword="null"/> without dispatching to the orchestrator gate service.
/// </summary>
public sealed class ChatServiceTests
{
    private static readonly Guid _unknownId = Guid.NewGuid();
    private static readonly Guid _knownId = Guid.NewGuid();
    private static readonly string _knownRoot = Path.Combine(Path.GetTempPath(), "nirm-chat-svc-tests");

    // Use Strict so any unexpected call to the gate service throws immediately,
    // proving no command dispatch was attempted.
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly Mock<IOrchestratorGateService> _gateService = new(MockBehavior.Strict);
    private readonly ChatService _sut;

    public ChatServiceTests()
    {
        _sut = new ChatService(_workspaceService.Object, _gateService.Object);
    }

    // ── GetSnapshotAsync — unknown workspace ──────────────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_UnknownWorkspace_ReturnsNull()
    {
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.GetSnapshotAsync(_unknownId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSnapshotAsync_UnknownWorkspace_DoesNotCallGateService()
    {
        // Arrange: workspace is unknown → service returns null.
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        await _sut.GetSnapshotAsync(_unknownId);

        // Assert: strict mock throws if GetGateAsync was called — explicit verify adds clarity.
        _gateService.Verify(
            s => s.GetGateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gateService.Verify(
            s => s.GetTimelineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── ProcessTurnAsync — unknown workspace ──────────────────────────────────

    [Fact]
    public async Task ProcessTurnAsync_UnknownWorkspace_ReturnsNull()
    {
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.ProcessTurnAsync(_unknownId, "execute-plan");

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessTurnAsync_UnknownWorkspace_DoesNotDispatchCommand()
    {
        // Arrange: workspace is unknown — dispatch must not reach the gate service.
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act: use an explicit command token to confirm classification does not cause dispatch.
        await _sut.ProcessTurnAsync(_unknownId, "execute-plan");

        // Assert: strict mock would throw on any call; explicit verify proves the invariant.
        _gateService.Verify(
            s => s.GetGateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gateService.Verify(
            s => s.GetTimelineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessTurnAsync_UnknownWorkspace_DoesNotDispatchFreeformInput()
    {
        // Same guarantee holds for non-command (conversational) input.
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await _sut.ProcessTurnAsync(_unknownId, "what is the current status?");

        _gateService.Verify(
            s => s.GetGateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Command classification and normalization ─────────────────────────────

    [Fact]
    public async Task ProcessTurnAsync_KnownWorkspace_NormalizesAosPrefix()
    {
        // Arrange
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_knownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_knownRoot);

        var gateDto = new OrchestratorGateDto
        {
            Runnable = true,
            Checks = [],
            RecommendedAction = "execute-plan"
        };
        _gateService
            .Setup(s => s.GetGateAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateDto);
        _gateService
            .Setup(s => s.GetTimelineAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestratorTimelineDto?)null);

        // Act
        var result = await _sut.ProcessTurnAsync(_knownId, "aos plan-phase");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("assistant", result.Role);
        Assert.Contains("Command received: `plan-phase`", result.Content);
        Assert.Equal(gateDto, result.Gate);
        Assert.Equal("execute-plan", result.NextCommand);
        Assert.Equal("orchestrator", result.AgentId);
        Assert.True(result.Timestamp > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task ProcessTurnAsync_KnownWorkspace_ClassifiesCommandInput()
    {
        // Arrange
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_knownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_knownRoot);

        var gateDto = new OrchestratorGateDto
        {
            Runnable = true,
            Checks = [],
            RecommendedAction = "execute-plan"
        };
        _gateService
            .Setup(s => s.GetGateAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateDto);
        _gateService
            .Setup(s => s.GetTimelineAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestratorTimelineDto?)null);

        // Act
        var result = await _sut.ProcessTurnAsync(_knownId, "plan-phase");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("assistant", result.Role);
        Assert.Contains("Command received: `plan-phase`", result.Content);
        Assert.Equal(gateDto, result.Gate);
        Assert.Equal("execute-plan", result.NextCommand);
    }

    [Fact]
    public async Task ProcessTurnAsync_KnownWorkspace_ClassifiesConversationalInput()
    {
        // Arrange
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_knownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_knownRoot);

        var gateDto = new OrchestratorGateDto
        {
            Runnable = true,
            Checks = [],
            RecommendedAction = "status"
        };
        _gateService
            .Setup(s => s.GetGateAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateDto);
        _gateService
            .Setup(s => s.GetTimelineAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestratorTimelineDto?)null);

        // Act
        var result = await _sut.ProcessTurnAsync(_knownId, "what is the current status?");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("assistant", result.Role);
        Assert.Contains("Understood: \"what is the current status?\"", result.Content);
        Assert.Contains("recommended next step is `status`", result.Content);
        Assert.Equal(gateDto, result.Gate);
        Assert.Equal("status", result.NextCommand);
    }

    [Fact]
    public async Task ProcessTurnAsync_KnownWorkspace_HandlesStatusCommandSpecially()
    {
        // Arrange
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_knownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_knownRoot);

        var gateDto = new OrchestratorGateDto
        {
            Runnable = true,
            TaskId = "TSK-001",
            TaskTitle = "Implement chat tests",
            Checks = [],
            RecommendedAction = "verify-work"
        };
        _gateService
            .Setup(s => s.GetGateAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateDto);
        _gateService
            .Setup(s => s.GetTimelineAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestratorTimelineDto?)null);

        // Act
        var result = await _sut.ProcessTurnAsync(_knownId, "status");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("assistant", result.Role);
        Assert.Contains("**Workspace Status**", result.Content);
        Assert.Contains("Active task: `TSK-001` — Implement chat tests", result.Content);
        Assert.Contains("Recommended action: `verify-work`", result.Content);
        Assert.Equal(gateDto, result.Gate);
        Assert.Equal("verify-work", result.NextCommand);
    }

    // ── Response shape parity with OrchestratorMessage ────────────────────────

    [Fact]
    public async Task ProcessTurnAsync_KnownWorkspace_ReturnsCorrectOrchestratorMessageShape()
    {
        // Arrange
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_knownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_knownRoot);

        var gateDto = new OrchestratorGateDto
        {
            Runnable = false,
            TaskId = "TSK-002",
            TaskTitle = "Fix failing tests",
            Checks = [
                new() { 
                    Id = "test-coverage", 
                    Kind = "evidence", 
                    Label = "Test Coverage", 
                    Detail = "Test coverage insufficient", 
                    Status = "fail" 
                }
            ],
            RecommendedAction = "plan-fix"
        };
        var timelineDto = new OrchestratorTimelineDto
        {
            Steps = [
                new() { Id = "step-1", Label = "Run tests", Status = "completed" },
                new() { Id = "step-2", Label = "Analyze results", Status = "failed" }
            ]
        };

        _gateService
            .Setup(s => s.GetGateAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateDto);
        _gateService
            .Setup(s => s.GetTimelineAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(timelineDto);

        // Act
        var result = await _sut.ProcessTurnAsync(_knownId, "aos status");

        // Assert - verify all required OrchestratorMessageDto fields are populated
        Assert.NotNull(result);
        
        // Required fields
        Assert.Equal("assistant", result.Role);
        Assert.NotNull(result.Content);
        Assert.True(result.Timestamp > DateTimeOffset.MinValue);
        
        // Optional but important fields
        Assert.Equal(gateDto, result.Gate);
        Assert.Equal(timelineDto, result.Timeline);
        Assert.Equal("plan-fix", result.NextCommand);
        Assert.Equal("orchestrator", result.AgentId);
        
        // Collections should be initialized
        Assert.NotNull(result.Artifacts);
        Assert.NotNull(result.Logs);
        
        // Verify content includes expected elements
        Assert.Contains("**Workspace Status**", result.Content);
        Assert.Contains("TSK-002", result.Content);
    }

    [Fact]
    public async Task GetSnapshotAsync_KnownWorkspace_ReturnsCorrectSnapshotShape()
    {
        // Arrange
        _workspaceService
            .Setup(s => s.ResolveRootAsync(_knownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_knownRoot);

        var gateDto = new OrchestratorGateDto
        {
            Runnable = true,
            Checks = [],
            RecommendedAction = "execute-plan"
        };
        _gateService
            .Setup(s => s.GetGateAsync(_knownRoot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateDto);

        // Act
        var result = await _sut.GetSnapshotAsync(_knownId);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Messages);
        Assert.Empty(result.Messages); // New workspace should have empty message thread
        Assert.NotNull(result.CommandSuggestions);
        Assert.True(result.CommandSuggestions.Count > 0);
        Assert.NotNull(result.QuickActions);
        Assert.True(result.QuickActions.Count > 0);
        
        // Verify suggestions include the recommended action
        var recommendedSuggestion = result.CommandSuggestions.FirstOrDefault(s => 
            s.Command == "execute-plan");
        Assert.NotNull(recommendedSuggestion);
        Assert.Contains("Recommended", recommendedSuggestion.Description);
        
        // Verify quick actions include the recommended action
        var recommendedAction = result.QuickActions.FirstOrDefault(a => 
            a.Command == "execute-plan");
        Assert.NotNull(recommendedAction);
        Assert.Equal("execute-plan", recommendedAction.Label);
    }
}
>>>>>>> C:/Users/James Lestler/.windsurf/worktrees/Nirmata/Nirmata-8a8f29ae/tests/nirmata.Services.Tests/ChatServiceTests.cs
