using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.Backlog.TodoReviewer;
using Gmsd.Agents.Tests.Fakes;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Backlog.TodoReviewer;

public class TodoReviewerTests : IDisposable
{
    private readonly FakeWorkspace _workspace;
    private readonly Agents.Execution.Backlog.TodoReviewer.TodoReviewer _sut;

    public TodoReviewerTests()
    {
        _workspace = new FakeWorkspace();
        _sut = new Agents.Execution.Backlog.TodoReviewer.TodoReviewer(
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
    public async Task ListTodosAsync_ReturnsCapturedTodos()
    {
        // Arrange
        CreateTodoFile("TODO-001", "First TODO", status: "captured");
        CreateTodoFile("TODO-002", "Second TODO", status: "reviewing");
        CreateTodoFile("TODO-003", "Third TODO", status: "promoted");

        var request = new TodoReviewRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath
        };

        // Act
        var result = await _sut.ListTodosAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Todos.Should().HaveCount(2); // captured + reviewing, not promoted
        result.Todos.Select(t => t.Id).Should().ContainInOrder("TODO-001", "TODO-002");
    }

    [Fact]
    public async Task ListTodosAsync_WithPriorityFilter_ReturnsMatching()
    {
        // Arrange
        CreateTodoFile("TODO-001", "High priority", priority: "high");
        CreateTodoFile("TODO-002", "Low priority", priority: "low");

        var request = new TodoReviewRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            PriorityFilter = "high"
        };

        // Act
        var result = await _sut.ListTodosAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Todos.Should().HaveCount(1);
        result.Todos[0].Id.Should().Be("TODO-001");
    }

    [Fact]
    public async Task PromoteToTaskAsync_CreatesTaskAndUpdatesTodo()
    {
        // Arrange
        CreateTodoFile("TODO-001", "Implement feature X");

        var request = new TodoPromotionRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            TodoId = "TODO-001",
            Title = "Feature X Implementation",
            WriteEvent = true
        };

        // Act
        var result = await _sut.PromoteToTaskAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.TodoId.Should().Be("TODO-001");
        result.CreatedType.Should().Be("task");
        result.CreatedId.Should().StartWith("TSK-");
        result.EventWritten.Should().BeTrue();

        // Verify task file exists
        File.Exists(result.CreatedFilePath).Should().BeTrue();

        // Verify TODO status updated
        var todoPath = Path.Combine(_workspace.AosRootPath, "context", "todos", "TODO-001.json");
        var todoJson = await File.ReadAllTextAsync(todoPath);
        var todo = JsonSerializer.Deserialize<JsonElement>(todoJson);
        todo.GetProperty("status").GetString().Should().Be("promoted");
    }

    [Fact]
    public async Task PromoteToTaskAsync_CreatesTaskWithCorrectContent()
    {
        // Arrange
        CreateTodoFile("TODO-001", "Original description", source: "src/File.cs");

        var request = new TodoPromotionRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            TodoId = "TODO-001",
            Title = "Custom Task Title",
            AdditionalDescription = "Additional context"
        };

        // Act
        var result = await _sut.PromoteToTaskAsync(request);

        // Assert
        var taskJson = await File.ReadAllTextAsync(result.CreatedFilePath!);
        var task = JsonSerializer.Deserialize<JsonElement>(taskJson);

        task.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        task.GetProperty("id").GetString().Should().Be(result.CreatedId);
        task.GetProperty("type").GetString().Should().Be("general");
        task.GetProperty("title").GetString().Should().Be("Custom Task Title");
        task.GetProperty("description").GetString().Should().Contain("Original description");
        task.GetProperty("description").GetString().Should().Contain("Additional context");
        task.GetProperty("status").GetString().Should().Be("planned");
        task.GetProperty("sourceTodoId").GetString().Should().Be("TODO-001");
    }

    [Fact]
    public async Task PromoteToTaskAsync_WritesReviewEvent()
    {
        // Arrange
        CreateTodoFile("TODO-001", "Test TODO");

        var request = new TodoPromotionRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            TodoId = "TODO-001",
            WriteEvent = true
        };

        // Act
        await _sut.PromoteToTaskAsync(request);

        // Assert
        var eventsPath = Path.Combine(_workspace.AosRootPath, "state", "events.ndjson");
        File.Exists(eventsPath).Should().BeTrue();

        var lines = await File.ReadAllLinesAsync(eventsPath);
        lines.Should().HaveCount(1);

        var evt = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        evt.GetProperty("eventType").GetString().Should().Be("review");
        evt.GetProperty("todoId").GetString().Should().Be("TODO-001");
        evt.GetProperty("decision").GetString().Should().Be("promote-task");
        evt.GetProperty("targetId").GetString().Should().StartWith("TSK-");
    }

    [Fact]
    public async Task PromoteToPhaseAsync_CreatesPhaseAndUpdatesTodo()
    {
        // Arrange
        CreateTodoFile("TODO-001", "Major roadmap item");

        var request = new TodoPromotionRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            TodoId = "TODO-001",
            Title = "Phase 1: Foundation",
            WriteEvent = true
        };

        // Act
        var result = await _sut.PromoteToPhaseAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.TodoId.Should().Be("TODO-001");
        result.CreatedType.Should().Be("phase");
        result.CreatedId.Should().StartWith("PH-");

        // Verify phase file exists
        File.Exists(result.CreatedFilePath).Should().BeTrue();

        // Verify TODO status updated
        var todoPath = Path.Combine(_workspace.AosRootPath, "context", "todos", "TODO-001.json");
        var todoJson = await File.ReadAllTextAsync(todoPath);
        var todo = JsonSerializer.Deserialize<JsonElement>(todoJson);
        todo.GetProperty("status").GetString().Should().Be("promoted");
    }

    [Fact]
    public async Task PromoteToPhaseAsync_WritesReviewEvent()
    {
        // Arrange
        CreateTodoFile("TODO-001", "Test TODO for phase");

        var request = new TodoPromotionRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            TodoId = "TODO-001",
            WriteEvent = true
        };

        // Act
        await _sut.PromoteToPhaseAsync(request);

        // Assert
        var eventsPath = Path.Combine(_workspace.AosRootPath, "state", "events.ndjson");
        var lines = await File.ReadAllLinesAsync(eventsPath);

        var evt = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        evt.GetProperty("decision").GetString().Should().Be("promote-roadmap");
        evt.GetProperty("targetId").GetString().Should().StartWith("PH-");
    }

    [Fact]
    public async Task DiscardAsync_WithArchive_MovesToArchiveDirectory()
    {
        // Arrange
        CreateTodoFile("TODO-001", "Discard me");

        var request = new TodoDiscardRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            TodoId = "TODO-001",
            Archive = true,
            Rationale = "No longer needed",
            WriteEvent = true
        };

        // Act
        var result = await _sut.DiscardAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ArchivePath.Should().NotBeNull();
        File.Exists(result.ArchivePath).Should().BeTrue();

        // Original should be gone
        var originalPath = Path.Combine(_workspace.AosRootPath, "context", "todos", "TODO-001.json");
        File.Exists(originalPath).Should().BeFalse();
    }

    [Fact]
    public async Task DiscardAsync_WithoutArchive_MarksAsDiscarded()
    {
        // Arrange
        CreateTodoFile("TODO-001", "Discard me but keep");

        var request = new TodoDiscardRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            TodoId = "TODO-001",
            Archive = false,
            WriteEvent = false
        };

        // Act
        var result = await _sut.DiscardAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ArchivePath.Should().BeNull();

        // File should still exist but status changed
        var originalPath = Path.Combine(_workspace.AosRootPath, "context", "todos", "TODO-001.json");
        File.Exists(originalPath).Should().BeTrue();

        var json = await File.ReadAllTextAsync(originalPath);
        var todo = JsonSerializer.Deserialize<JsonElement>(json);
        todo.GetProperty("status").GetString().Should().Be("discarded");
    }

    [Fact]
    public async Task DiscardAsync_WritesReviewEvent()
    {
        // Arrange
        CreateTodoFile("TODO-001", "TODO to discard");

        var request = new TodoDiscardRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            TodoId = "TODO-001",
            Archive = true,
            Rationale = "Duplicate of TODO-002",
            WriteEvent = true
        };

        // Act
        await _sut.DiscardAsync(request);

        // Assert
        var eventsPath = Path.Combine(_workspace.AosRootPath, "state", "events.ndjson");
        var lines = await File.ReadAllLinesAsync(eventsPath);

        var evt = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        evt.GetProperty("decision").GetString().Should().Be("discard");
        evt.GetProperty("rationale").GetString().Should().Be("Duplicate of TODO-002");
    }

    [Fact]
    public async Task ListTodosAsync_DiscardedTodos_NotInDefaultResults()
    {
        // Arrange
        CreateTodoFile("TODO-001", "Active TODO");
        CreateTodoFile("TODO-002", "Discarded TODO", status: "discarded");

        var request = new TodoReviewRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath
        };

        // Act
        var result = await _sut.ListTodosAsync(request);

        // Assert
        result.Todos.Should().HaveCount(1);
        result.Todos[0].Id.Should().Be("TODO-001");
    }

    [Fact]
    public async Task PromoteToTaskAsync_NonExistentTodo_ReturnsFailure()
    {
        // Arrange
        var request = new TodoPromotionRequest
        {
            WorkspaceRoot = _workspace.RepositoryRootPath,
            TodoId = "TODO-NONEXISTENT"
        };

        // Act
        var result = await _sut.PromoteToTaskAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    private void CreateTodoFile(string todoId, string description, string? status = null, string priority = "medium", string? source = null)
    {
        var todosDir = Path.Combine(_workspace.AosRootPath, "context", "todos");
        Directory.CreateDirectory(todosDir);

        var todo = new Dictionary<string, object>
        {
            ["schemaVersion"] = 1,
            ["id"] = todoId,
            ["description"] = description,
            ["capturedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["priority"] = priority,
            ["status"] = status ?? "captured"
        };

        if (source != null)
            todo["source"] = source;

        var json = JsonSerializer.Serialize(todo, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        File.WriteAllText(Path.Combine(todosDir, $"{todoId}.json"), json);
    }
}
