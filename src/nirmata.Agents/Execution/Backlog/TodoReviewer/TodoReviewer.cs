using System.Text.Json;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Backlog.TodoReviewer;

/// <summary>
/// Implementation of the TODO Reviewer.
/// Reviews captured TODOs and promotes them to tasks or roadmap phases, or discards them.
/// </summary>
public sealed class TodoReviewer : ITodoReviewer
{
    private readonly IDeterministicJsonSerializer _jsonSerializer;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions NdjsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public TodoReviewer(IDeterministicJsonSerializer jsonSerializer)
    {
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
    }

    public async Task<TodoReviewResult> ListTodosAsync(TodoReviewRequest request, CancellationToken ct = default)
    {
        try
        {
            var todosDir = Path.Combine(request.WorkspaceRoot, ".aos", "context", "todos");
            if (!Directory.Exists(todosDir))
            {
                return new TodoReviewResult
                {
                    IsSuccess = true,
                    Todos = Array.Empty<TodoItem>(),
                    TotalCount = 0
                };
            }

            var todoFiles = Directory.GetFiles(todosDir, "TODO-*.json");
            var todos = new List<TodoItem>();

            foreach (var file in todoFiles)
            {
                ct.ThrowIfCancellationRequested();

                var json = await File.ReadAllTextAsync(file, ct);
                var todoFile = JsonSerializer.Deserialize<TodoFile>(json, JsonOptions);

                if (todoFile == null) continue;

                // Apply status filter if provided, else show active TODOs
                var targetStatuses = request.StatusFilter != null
                    ? new[] { request.StatusFilter.ToLowerInvariant() }
                    : new[] { "captured", "reviewing" };

                if (!targetStatuses.Contains(todoFile.Status?.ToLowerInvariant() ?? ""))
                {
                    continue;
                }

                // Apply priority filter if provided
                if (request.PriorityFilter != null &&
                    !string.Equals(todoFile.Priority, request.PriorityFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                todos.Add(new TodoItem
                {
                    Id = todoFile.Id ?? Path.GetFileNameWithoutExtension(file),
                    Description = todoFile.Description ?? "",
                    Source = todoFile.Source,
                    CapturedAt = todoFile.CapturedAt ?? "",
                    Priority = todoFile.Priority ?? "medium",
                    Status = todoFile.Status ?? "captured",
                    FilePath = file
                });
            }

            return new TodoReviewResult
            {
                IsSuccess = true,
                Todos = todos.OrderBy(t => t.Id).ToList(),
                TotalCount = todoFiles.Length
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TodoReviewResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<TodoPromotionResult> PromoteToTaskAsync(TodoPromotionRequest request, CancellationToken ct = default)
    {
        try
        {
            // Read the TODO file
            var todoPath = Path.Combine(request.WorkspaceRoot, ".aos", "context", "todos", $"{request.TodoId}.json");
            if (!File.Exists(todoPath))
            {
                return new TodoPromotionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"TODO '{request.TodoId}' not found"
                };
            }

            var todoJson = await File.ReadAllTextAsync(todoPath, ct);
            var todo = JsonSerializer.Deserialize<TodoFile>(todoJson, JsonOptions);

            if (todo == null)
            {
                return new TodoPromotionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to parse TODO '{request.TodoId}'"
                };
            }

            // Create tasks directory
            var tasksDir = Path.Combine(request.WorkspaceRoot, ".aos", "spec", "tasks");
            Directory.CreateDirectory(tasksDir);

            // Generate task ID
            var existingTasks = Directory.GetFiles(tasksDir, "TSK-*.json");
            var nextNumber = existingTasks.Length + 1;
            var taskId = $"TSK-{nextNumber:D4}";

            var taskDir = Path.Combine(tasksDir, taskId);
            Directory.CreateDirectory(taskDir);

            // Create task.json
            var title = request.Title ?? todo.Description ?? $"Task from {request.TodoId}";
            var description = request.AdditionalDescription != null
                ? $"{todo.Description}\n\nAdditional context: {request.AdditionalDescription}"
                : todo.Description;

            var task = new TaskFile
            {
                SchemaVersion = 1,
                Id = taskId,
                Type = "general",
                Title = title,
                Description = description ?? "",
                Status = "planned",
                SourceTodoId = request.TodoId
            };

            var taskPath = Path.Combine(taskDir, "task.json");
            var taskJson = _jsonSerializer.SerializeToString(task, JsonOptions);
            await File.WriteAllTextAsync(taskPath, taskJson, ct);

            // Update TODO status to promoted
            var updatedTodo = todo with { Status = "promoted" };
            var updatedTodoJson = _jsonSerializer.SerializeToString(updatedTodo, JsonOptions);
            await File.WriteAllTextAsync(todoPath, updatedTodoJson, ct);

            // Write review event
            bool eventWritten = false;
            if (request.WriteEvent)
            {
                eventWritten = await WriteReviewEventAsync(
                    request.WorkspaceRoot,
                    request.TodoId,
                    "promote-task",
                    taskId,
                    $"Promoted to task {taskId}",
                    ct);
            }

            return new TodoPromotionResult
            {
                IsSuccess = true,
                TodoId = request.TodoId,
                CreatedId = taskId,
                CreatedType = "task",
                CreatedFilePath = taskPath,
                EventWritten = eventWritten
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TodoPromotionResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<TodoPromotionResult> PromoteToPhaseAsync(TodoPromotionRequest request, CancellationToken ct = default)
    {
        try
        {
            // Read the TODO file
            var todoPath = Path.Combine(request.WorkspaceRoot, ".aos", "context", "todos", $"{request.TodoId}.json");
            if (!File.Exists(todoPath))
            {
                return new TodoPromotionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"TODO '{request.TodoId}' not found"
                };
            }

            var todoJson = await File.ReadAllTextAsync(todoPath, ct);
            var todo = JsonSerializer.Deserialize<TodoFile>(todoJson, JsonOptions);

            if (todo == null)
            {
                return new TodoPromotionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to parse TODO '{request.TodoId}'"
                };
            }

            // Create phases directory
            var phasesDir = Path.Combine(request.WorkspaceRoot, ".aos", "spec", "phases");
            Directory.CreateDirectory(phasesDir);

            // Generate phase ID
            var existingPhases = Directory.GetFiles(phasesDir, "PH-*.json");
            var nextNumber = existingPhases.Length + 1;
            var phaseId = $"PH-{nextNumber:D3}";

            // Create phase.json
            var title = request.Title ?? todo.Description ?? $"Phase from {request.TodoId}";
            var description = request.AdditionalDescription != null
                ? $"{todo.Description}\n\nAdditional context: {request.AdditionalDescription}"
                : todo.Description;

            var phase = new PhaseFile
            {
                SchemaVersion = 1,
                Id = phaseId,
                Title = title,
                Description = description ?? "",
                Status = "planned",
                SourceTodoId = request.TodoId
            };

            var phasePath = Path.Combine(phasesDir, $"{phaseId}.json");
            var phaseJson = _jsonSerializer.SerializeToString(phase, JsonOptions);
            await File.WriteAllTextAsync(phasePath, phaseJson, ct);

            // Update TODO status to promoted
            var updatedTodo = todo with { Status = "promoted" };
            var updatedTodoJson = _jsonSerializer.SerializeToString(updatedTodo, JsonOptions);
            await File.WriteAllTextAsync(todoPath, updatedTodoJson, ct);

            // Write review event
            bool eventWritten = false;
            if (request.WriteEvent)
            {
                eventWritten = await WriteReviewEventAsync(
                    request.WorkspaceRoot,
                    request.TodoId,
                    "promote-roadmap",
                    phaseId,
                    $"Promoted to phase {phaseId}",
                    ct);
            }

            return new TodoPromotionResult
            {
                IsSuccess = true,
                TodoId = request.TodoId,
                CreatedId = phaseId,
                CreatedType = "phase",
                CreatedFilePath = phasePath,
                EventWritten = eventWritten
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TodoPromotionResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<TodoDiscardResult> DiscardAsync(TodoDiscardRequest request, CancellationToken ct = default)
    {
        try
        {
            // Read the TODO file
            var todoPath = Path.Combine(request.WorkspaceRoot, ".aos", "context", "todos", $"{request.TodoId}.json");
            if (!File.Exists(todoPath))
            {
                return new TodoDiscardResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"TODO '{request.TodoId}' not found"
                };
            }

            var todoJson = await File.ReadAllTextAsync(todoPath, ct);
            var todo = JsonSerializer.Deserialize<TodoFile>(todoJson, JsonOptions);

            if (todo == null)
            {
                return new TodoDiscardResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to parse TODO '{request.TodoId}'"
                };
            }

            string? archivePath = null;

            if (request.Archive)
            {
                // Move to archive
                var archiveDir = Path.Combine(request.WorkspaceRoot, ".aos", "context", "todos", "archive");
                Directory.CreateDirectory(archiveDir);

                archivePath = Path.Combine(archiveDir, $"{request.TodoId}.json");
                File.Move(todoPath, archivePath, overwrite: true);
            }
            else
            {
                // Just mark as discarded
                var updatedTodo = todo with
                {
                    Status = "discarded"
                };
                var updatedTodoJson = _jsonSerializer.SerializeToString(updatedTodo, JsonOptions);
                await File.WriteAllTextAsync(todoPath, updatedTodoJson, ct);
            }

            // Write review event
            bool eventWritten = false;
            if (request.WriteEvent)
            {
                eventWritten = await WriteReviewEventAsync(
                    request.WorkspaceRoot,
                    request.TodoId,
                    "discard",
                    null,
                    request.Rationale ?? "Discarded during review",
                    ct);
            }

            return new TodoDiscardResult
            {
                IsSuccess = true,
                TodoId = request.TodoId,
                ArchivePath = archivePath,
                EventWritten = eventWritten
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TodoDiscardResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<bool> WriteReviewEventAsync(
        string workspaceRoot,
        string todoId,
        string decision,
        string? targetId,
        string rationale,
        CancellationToken ct)
    {
        try
        {
            var eventsPath = Path.Combine(workspaceRoot, ".aos", "state", "events.ndjson");
            var eventsDir = Path.GetDirectoryName(eventsPath);

            if (!string.IsNullOrEmpty(eventsDir) && !Directory.Exists(eventsDir))
            {
                Directory.CreateDirectory(eventsDir);
            }

            var evt = new ReviewEvent
            {
                SchemaVersion = 1,
                EventType = "review",
                TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
                TodoId = todoId,
                Decision = decision,
                TargetId = targetId,
                Rationale = rationale
            };

            var line = JsonSerializer.Serialize(evt, NdjsonOptions);

            if (File.Exists(eventsPath))
            {
                using var stream = new FileStream(eventsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

                if (stream.Length > 0)
                {
                    stream.Seek(-1, SeekOrigin.End);
                    var last = stream.ReadByte();
                    if (last != '\n')
                    {
                        stream.Seek(0, SeekOrigin.End);
                        stream.WriteByte((byte)'\n');
                    }
                }

                stream.Seek(0, SeekOrigin.End);
                var lineBytes = System.Text.Encoding.UTF8.GetBytes(line + '\n');
                await stream.WriteAsync(lineBytes, ct);
            }
            else
            {
                await File.WriteAllTextAsync(eventsPath, line + '\n', ct);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record TodoFile
    {
        public int SchemaVersion { get; init; }
        public string? Id { get; init; }
        public string? Description { get; init; }
        public string? Source { get; init; }
        public string? CapturedAt { get; init; }
        public string? Priority { get; init; }
        public string? Status { get; init; }
    }

    private sealed record TaskFile
    {
        public int SchemaVersion { get; init; }
        public string? Id { get; init; }
        public string? Type { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public string? Status { get; init; }
        public string? SourceTodoId { get; init; }
    }

    private sealed record PhaseFile
    {
        public int SchemaVersion { get; init; }
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public string? Status { get; init; }
        public string? SourceTodoId { get; init; }
    }

    private sealed record ReviewEvent
    {
        public int SchemaVersion { get; init; }
        public string? EventType { get; init; }
        public string? TimestampUtc { get; init; }
        public string? TodoId { get; init; }
        public string? Decision { get; init; }
        public string? TargetId { get; init; }
        public string? Rationale { get; init; }
    }
}
