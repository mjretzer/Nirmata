using System.Text.Json;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Backlog.TodoCapturer;

/// <summary>
/// Implementation of the TODO Capturer.
/// Captures TODOs from execution context to .aos/context/todos/ without affecting the cursor.
/// </summary>
public sealed class TodoCapturer : ITodoCapturer
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

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoCapturer"/> class.
    /// </summary>
    public TodoCapturer(IDeterministicJsonSerializer jsonSerializer)
    {
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
    }

    /// <inheritdoc />
    public async Task<TodoCaptureResult> CaptureAsync(TodoCaptureRequest request, CancellationToken ct = default)
    {
        try
        {
            // Generate or use provided TODO ID
            var todoId = request.TodoId ?? GenerateTodoId(request.WorkspaceRoot);
            var capturedAt = DateTimeOffset.UtcNow.ToString("O");

            // Create TODO directory if needed
            var todosDir = Path.Combine(request.WorkspaceRoot, ".aos", "context", "todos");
            if (!Directory.Exists(todosDir))
            {
                Directory.CreateDirectory(todosDir);
            }

            // Create TODO file
            var todoFile = new TodoFile
            {
                SchemaVersion = 1,
                Id = todoId,
                Description = request.Description,
                Source = request.Source,
                CapturedAt = capturedAt,
                Priority = NormalizePriority(request.Priority),
                Status = "captured"
            };

            var filePath = Path.Combine(todosDir, $"{todoId}.json");
            var json = _jsonSerializer.SerializeToString(todoFile, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);

            // Write capture event if requested
            bool eventWritten = false;
            if (request.WriteEvent)
            {
                eventWritten = await WriteCaptureEventAsync(request.WorkspaceRoot, todoFile, ct);
            }

            return new TodoCaptureResult
            {
                IsSuccess = true,
                TodoId = todoId,
                FilePath = filePath,
                EventWritten = eventWritten
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TodoCaptureResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static string GenerateTodoId(string workspaceRoot)
    {
        var todosDir = Path.Combine(workspaceRoot, ".aos", "context", "todos");
        
        if (!Directory.Exists(todosDir))
        {
            return "TODO-001";
        }

        var existingTodos = Directory.GetFiles(todosDir, "TODO-*.json");
        var nextNumber = existingTodos.Length + 1;
        return $"TODO-{nextNumber:D3}";
    }

    private static string NormalizePriority(string priority)
    {
        return priority?.ToLowerInvariant() switch
        {
            "low" => "low",
            "medium" => "medium",
            "high" => "high",
            "urgent" => "urgent",
            _ => "medium"
        };
    }

    private async Task<bool> WriteCaptureEventAsync(
        string workspaceRoot,
        TodoFile todo,
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

            var evt = new CaptureEvent
            {
                SchemaVersion = 1,
                EventType = "capture",
                TimestampUtc = todo.CapturedAt,
                TodoId = todo.Id,
                Source = todo.Source,
                Priority = todo.Priority
            };

            var line = JsonSerializer.Serialize(evt, NdjsonOptions);

            // Append with proper LF handling
            if (File.Exists(eventsPath))
            {
                using var stream = new FileStream(eventsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

                // Ensure trailing LF if file is not empty
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

    private sealed record CaptureEvent
    {
        public int SchemaVersion { get; init; }
        public string? EventType { get; init; }
        public string? TimestampUtc { get; init; }
        public string? TodoId { get; init; }
        public string? Source { get; init; }
        public string? Priority { get; init; }
    }
}
