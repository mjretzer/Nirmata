using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.Backlog.TodoCapturer;
using Gmsd.Agents.Tests.Fakes;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Backlog.TodoCapturer;

public class TodoCapturerTests : IDisposable
{
    private readonly FakeWorkspace _workspace;
    private readonly Agents.Execution.Backlog.TodoCapturer.TodoCapturer _sut;

    public TodoCapturerTests()
    {
        _workspace = new FakeWorkspace();
        _sut = new Agents.Execution.Backlog.TodoCapturer.TodoCapturer(
            new FakeDeterministicJsonSerializer());

        // Create directories
        Directory.CreateDirectory(Path.Combine(_workspace.AosRootPath, "context", "todos"));
        Directory.CreateDirectory(Path.Combine(_workspace.AosRootPath, "state"));
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task CaptureAsync_WithValidRequest_CreatesTodoFile()
    {
        // Arrange
        var request = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "Implement user authentication",
            Source = "src/Controllers/AuthController.cs",
            Priority = "high"
        };

        // Act
        var result = await _sut.CaptureAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.TodoId.Should().NotBeNullOrEmpty();
        result.FilePath.Should().NotBeNullOrEmpty();
        File.Exists(result.FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task CaptureAsync_GeneratesCorrectTodoFileContent()
    {
        // Arrange
        var request = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "Implement user authentication",
            Source = "src/Controllers/AuthController.cs",
            Priority = "high"
        };

        // Act
        var result = await _sut.CaptureAsync(request);

        // Assert
        var json = await File.ReadAllTextAsync(result.FilePath!);
        var todo = JsonSerializer.Deserialize<JsonElement>(json);

        todo.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        todo.GetProperty("id").GetString().Should().Be(result.TodoId);
        todo.GetProperty("description").GetString().Should().Be("Implement user authentication");
        todo.GetProperty("source").GetString().Should().Be("src/Controllers/AuthController.cs");
        todo.GetProperty("priority").GetString().Should().Be("high");
        todo.GetProperty("status").GetString().Should().Be("captured");
        todo.GetProperty("capturedAt").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CaptureAsync_GeneratesSequentialTodoIds()
    {
        // Arrange
        var request1 = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "First TODO"
        };

        var request2 = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "Second TODO"
        };

        // Act
        var result1 = await _sut.CaptureAsync(request1);
        var result2 = await _sut.CaptureAsync(request2);

        // Assert
        result1.TodoId.Should().Be("TODO-001");
        result2.TodoId.Should().Be("TODO-002");
    }

    [Fact]
    public async Task CaptureAsync_WithExplicitTodoId_UsesProvidedId()
    {
        // Arrange
        var request = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "Custom ID TODO",
            TodoId = "TODO-CUSTOM"
        };

        // Act
        var result = await _sut.CaptureAsync(request);

        // Assert
        result.TodoId.Should().Be("TODO-CUSTOM");
        result.FilePath.Should().EndWith("TODO-CUSTOM.json");
    }

    [Fact]
    public async Task CaptureAsync_WritesCaptureEventToEventsNdjson()
    {
        // Arrange
        var request = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "TODO with event",
            Source = "conversation-123",
            Priority = "medium",
            WriteEvent = true
        };

        // Act
        var result = await _sut.CaptureAsync(request);

        // Assert
        result.EventWritten.Should().BeTrue();

        var eventsPath = Path.Combine(_workspace.AosRootPath, "state", "events.ndjson");
        File.Exists(eventsPath).Should().BeTrue();

        var lines = await File.ReadAllLinesAsync(eventsPath);
        lines.Should().HaveCount(1);

        var evt = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        evt.GetProperty("eventType").GetString().Should().Be("capture");
        evt.GetProperty("todoId").GetString().Should().Be(result.TodoId);
        evt.GetProperty("source").GetString().Should().Be("conversation-123");
        evt.GetProperty("priority").GetString().Should().Be("medium");
        evt.GetProperty("schemaVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task CaptureAsync_WithWriteEventFalse_DoesNotCreateEventsFile()
    {
        // Arrange
        var request = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "TODO without event",
            WriteEvent = false
        };

        // Act
        var result = await _sut.CaptureAsync(request);

        // Assert
        result.EventWritten.Should().BeFalse();

        var eventsPath = Path.Combine(_workspace.AosRootPath, "state", "events.ndjson");
        File.Exists(eventsPath).Should().BeFalse();
    }

    [Theory]
    [InlineData("low", "low")]
    [InlineData("LOW", "low")]
    [InlineData("medium", "medium")]
    [InlineData("MEDIUM", "medium")]
    [InlineData("high", "high")]
    [InlineData("HIGH", "high")]
    [InlineData("urgent", "urgent")]
    [InlineData("URGENT", "urgent")]
    [InlineData("invalid", "medium")]
    [InlineData("", "medium")]
    public async Task CaptureAsync_NormalizesPriorityLevels(string input, string expected)
    {
        // Arrange
        var request = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "Priority test",
            Priority = input
        };

        // Act
        var result = await _sut.CaptureAsync(request);

        // Assert
        var json = await File.ReadAllTextAsync(result.FilePath!);
        var todo = JsonSerializer.Deserialize<JsonElement>(json);
        todo.GetProperty("priority").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task CaptureAsync_WithoutSource_OmitsSourceField()
    {
        // Arrange
        var request = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "TODO without source"
        };

        // Act
        var result = await _sut.CaptureAsync(request);

        // Assert
        var json = await File.ReadAllTextAsync(result.FilePath!);
        var todo = JsonSerializer.Deserialize<JsonElement>(json);

        // Source should be null when not provided
        todo.GetProperty("source").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task CaptureAsync_MultipleTodos_AppendsToEventsNdjson()
    {
        // Arrange
        var request1 = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "First TODO"
        };

        var request2 = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "Second TODO"
        };

        // Act
        await _sut.CaptureAsync(request1);
        await _sut.CaptureAsync(request2);

        // Assert
        var eventsPath = Path.Combine(_workspace.AosRootPath, "state", "events.ndjson");
        var lines = await File.ReadAllLinesAsync(eventsPath);
        lines.Should().HaveCount(2);

        var evt1 = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        var evt2 = JsonSerializer.Deserialize<JsonElement>(lines[1]);

        evt1.GetProperty("todoId").GetString().Should().Be("TODO-001");
        evt2.GetProperty("todoId").GetString().Should().Be("TODO-002");
    }

    [Fact]
    public async Task CaptureAsync_DoesNotModifyCursorState()
    {
        // Arrange - Create a state.json file
        var stateDir = Path.Combine(_workspace.AosRootPath, "state");
        var state = new
        {
            schemaVersion = 1,
            cursor = new
            {
                milestoneId = "M-001",
                milestoneStatus = "active",
                phaseId = "PH-001",
                phaseStatus = "in-progress",
                taskId = "TSK-001",
                taskStatus = "active",
                stepId = "STEP-001",
                stepStatus = "in-progress"
            }
        };

        var stateJson = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        var statePath = Path.Combine(stateDir, "state.json");
        await File.WriteAllTextAsync(statePath, stateJson);

        var originalState = await File.ReadAllTextAsync(statePath);

        var request = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "TODO that should not affect cursor"
        };

        // Act
        await _sut.CaptureAsync(request);

        // Assert - State file should remain unchanged
        var finalState = await File.ReadAllTextAsync(statePath);
        finalState.Should().Be(originalState);
    }

    [Fact]
    public async Task CaptureAsync_CreatesTodosDirectoryIfMissing()
    {
        // Arrange - Delete the todos directory
        var todosDir = Path.Combine(_workspace.AosRootPath, "context", "todos");
        if (Directory.Exists(todosDir))
        {
            Directory.Delete(todosDir, true);
        }

        var request = new TodoCaptureRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            Description = "TODO in new directory"
        };

        // Act
        var result = await _sut.CaptureAsync(request);

        // Assert
        Directory.Exists(todosDir).Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        File.Exists(result.FilePath).Should().BeTrue();
    }
}
