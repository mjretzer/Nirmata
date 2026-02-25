using System.Text.Json;
using Gmsd.Agents.Execution.Execution.TaskExecutor;
using Gmsd.Agents.Execution.Validation;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Command handler for the Executor phase of the orchestrator workflow.
/// Coordinates task execution with scope enforcement and evidence capture.
/// </summary>
public sealed class TaskExecutorHandler
{
    private readonly ITaskExecutor _taskExecutor;
    private readonly IWorkspace _workspace;
    private readonly IStateStore _stateStore;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskExecutorHandler"/> class.
    /// </summary>
    public TaskExecutorHandler(
        ITaskExecutor taskExecutor,
        IWorkspace workspace,
        IStateStore stateStore)
    {
        _taskExecutor = taskExecutor ?? throw new ArgumentNullException(nameof(taskExecutor));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    /// <summary>
    /// Handles the executor phase command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Extract task ID from request inputs
            var taskId = ExtractTaskId(request);
            if (string.IsNullOrEmpty(taskId))
            {
                return CommandRouteResult.Failure(1, "Task ID is required for execution.");
            }

            // Read the current state to determine the current task
            var snapshot = _stateStore.ReadSnapshot();
            var currentTaskId = snapshot?.Cursor?.TaskId;

            if (string.IsNullOrEmpty(currentTaskId))
            {
                return CommandRouteResult.Failure(2, "No current task found in state cursor.");
            }

            // Build the task directory path
            var taskDirectory = Path.Combine(
                _workspace.RepositoryRootPath,
                ".aos",
                "spec",
                "tasks",
                currentTaskId);

            if (!Directory.Exists(taskDirectory))
            {
                return CommandRouteResult.Failure(3, $"Task directory not found: {taskDirectory}");
            }

            // Read plan.json to extract file scopes
            var planPath = Path.Combine(taskDirectory, "plan.json");
            if (!File.Exists(planPath))
            {
                return CommandRouteResult.Failure(4, $"Plan file not found: {planPath}");
            }

            var planJson = await File.ReadAllTextAsync(planPath, ct);
            var planValidation = ArtifactContractValidator.ValidateTaskPlan(
                artifactPath: planPath,
                artifactJson: planJson,
                aosRootPath: _workspace.AosRootPath,
                readBoundary: "task-executor-handler");
            if (!planValidation.IsValid)
            {
                return CommandRouteResult.Failure(
                    6,
                    $"Task execution blocked by contract validation gate. {planValidation.CreateFailureMessage()}");
            }

            var allowedScopes = ExtractFileScopes(planJson);

            // Create execution request
            var executionRequest = new TaskExecutionRequest
            {
                TaskId = currentTaskId,
                TaskDirectory = taskDirectory,
                AllowedFileScope = allowedScopes,
                CorrelationId = runId,
                ParentRunId = runId
            };

            // Execute the task
            var result = await _taskExecutor.ExecuteAsync(executionRequest, ct);

            // Update cursor state with execution status for routing (Task 4.3)
            await UpdateCursorExecutionStatusAsync(result.Success, ct);

            if (result.Success)
            {
                return CommandRouteResult.Success(
                    $"Task execution completed successfully for {currentTaskId}. " +
                    $"Run ID: {result.RunId}. " +
                    $"Modified {result.ModifiedFiles.Count} file(s).");
            }
            else
            {
                return CommandRouteResult.Failure(
                    5,
                    $"Task execution failed for {currentTaskId}: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Task executor handler failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the cursor execution status in state.json for routing decisions.
    /// Success → Verifier, Failure → FixPlanner
    /// </summary>
    private async Task UpdateCursorExecutionStatusAsync(bool success, CancellationToken ct)
    {
        try
        {
            var statePath = Path.Combine(_workspace.AosRootPath, "state", "state.json");
            if (!File.Exists(statePath))
            {
                return;
            }

            var stateJson = await File.ReadAllTextAsync(statePath, ct);
            var state = JsonSerializer.Deserialize<StateSnapshot>(stateJson, JsonOptions);
            if (state?.Cursor == null)
            {
                return;
            }

            var updatedCursor = new StateCursor
            {
                MilestoneId = state.Cursor.MilestoneId,
                MilestoneStatus = state.Cursor.MilestoneStatus,
                PhaseId = state.Cursor.PhaseId,
                PhaseStatus = state.Cursor.PhaseStatus,
                TaskId = state.Cursor.TaskId,
                TaskStatus = success ? "completed" : "failed",
                StepId = state.Cursor.StepId,
                StepStatus = state.Cursor.StepStatus
            };

            var updatedState = new StateSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                Cursor = updatedCursor
            };

            var updatedStateJson = JsonSerializer.Serialize(updatedState, JsonOptions);
            await File.WriteAllTextAsync(statePath, updatedStateJson, ct);
        }
        catch
        {
            // Don't fail the execution if state update fails
            // The event log still contains the execution result
        }
    }

    private static string ExtractTaskId(CommandRequest request)
    {
        // Try to extract from options if present
        if (request.Options.TryGetValue("task-id", out var taskIdValue) && !string.IsNullOrEmpty(taskIdValue))
        {
            return taskIdValue;
        }

        // Try to extract from arguments if present
        foreach (var arg in request.Arguments)
        {
            if (arg.StartsWith("--task-id=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[10..];
            }
        }

        // Return empty - will use current cursor task
        return string.Empty;
    }

    private static IReadOnlyList<string> ExtractFileScopes(string planJson)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(planJson);

            var scopes = new List<string>();

            if (document.RootElement.TryGetProperty("fileScopes", out var fileScopesElement) &&
                fileScopesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var scopeElement in fileScopesElement.EnumerateArray())
                {
                    if (scopeElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                        scopeElement.TryGetProperty("path", out var pathElement) &&
                        pathElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var scopePath = pathElement.GetString();
                        if (!string.IsNullOrWhiteSpace(scopePath))
                        {
                            scopes.Add(scopePath);
                        }
                    }
                }
            }

            return scopes.AsReadOnly();
        }
        catch
        {
            // Return empty list if parsing fails - TaskExecutor will validate
            return Array.Empty<string>();
        }
    }
}
