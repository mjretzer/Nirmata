using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gmsd.Agents.Execution.Execution.SubagentRuns;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Engine.Evidence.TaskEvidence;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;

namespace Gmsd.Agents.Execution.Execution.TaskExecutor;

/// <summary>
/// Executes task plans with strict file scope enforcement and evidence capture.
/// </summary>
public sealed class TaskExecutor : ITaskExecutor
{
    private readonly IRunLifecycleManager _runLifecycleManager;
    private readonly ISubagentOrchestrator _subagentOrchestrator;
    private readonly IWorkspace _workspace;
    private readonly IStateStore _stateStore;
    private readonly ILogger<TaskExecutor> _logger;

    public TaskExecutor(
        IRunLifecycleManager runLifecycleManager,
        ISubagentOrchestrator subagentOrchestrator,
        IWorkspace workspace,
        IStateStore stateStore,
        ILogger<TaskExecutor> logger)
    {
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _subagentOrchestrator = subagentOrchestrator ?? throw new ArgumentNullException(nameof(subagentOrchestrator));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runId = string.Empty;
        var modifiedFiles = new List<string>();

        try
        {
            var planPath = Path.Combine(request.TaskDirectory, "plan.json");
            
            if (!File.Exists(planPath))
            {
                return CreateFailureResult(runId, $"Plan file not found: {planPath}");
            }

            var planJson = await File.ReadAllTextAsync(planPath, ct);
            var plan = JsonSerializer.Deserialize<TaskPlanModel>(planJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (plan == null)
            {
                return CreateFailureResult(runId, "Failed to parse plan.json");
            }

            var scopeValidation = ValidateFileScopes(plan, request.AllowedFileScope);
            if (!scopeValidation.IsValid)
            {
                return CreateFailureResult(runId, scopeValidation.ErrorMessage!);
            }

            var runContext = await _runLifecycleManager.StartRunAsync(ct);
            runId = runContext.RunId;

            // Append execution start event to events.ndjson (Task 2.6)
            AppendTaskExecutionEvent(request.TaskId, runId, "task_execution_started", new Dictionary<string, object>
            {
                ["taskId"] = request.TaskId,
                ["runId"] = runId,
                ["planSteps"] = plan.Steps.Count,
                ["fileScopes"] = plan.FileScopes.Select(f => f.Path).ToList()
            });

            _logger.LogInformation(
                "Started task execution for {TaskId} with run {RunId}",
                request.TaskId,
                runId);

            foreach (var step in plan.Steps)
            {
                if (step.TargetPath != null)
                {
                    if (!IsPathInAllowedScope(step.TargetPath, request.AllowedFileScope))
                    {
                        var errorMessage = $"File '{step.TargetPath}' is outside allowed scope: {string.Join(", ", request.AllowedFileScope)}";
                        _logger.LogError(
                            "Scope violation for task {TaskId}, run {RunId}: {ErrorMessage}",
                            request.TaskId,
                            runId,
                            errorMessage);
                        
                        await _runLifecycleManager.FinishRunAsync(runId, false, new Dictionary<string, object>
                        {
                            ["error"] = errorMessage,
                            ["scopeViolation"] = true,
                            ["targetPath"] = step.TargetPath,
                            ["allowedScope"] = request.AllowedFileScope.ToList()
                        }, ct);

                        return CreateFailureResult(runId, errorMessage);
                    }
                }
            }

            // Delegate to SubagentOrchestrator for actual execution (Task 3.6)
            var subagentRequest = new SubagentRunRequest
            {
                RunId = runId,
                TaskId = request.TaskId,
                SubagentConfig = "task_executor",
                ContextPackIds = Array.Empty<string>(), // Context packs can be loaded from plan if needed
                AllowedFileScope = request.AllowedFileScope,
                ParentRunId = request.ParentRunId,
                CorrelationId = request.CorrelationId
            };

            var subagentResult = await _subagentOrchestrator.RunSubagentAsync(subagentRequest, ct);

            // If subagent failed, propagate the failure (Task 3.6 - error propagation)
            if (!subagentResult.Success)
            {
                await _runLifecycleManager.FinishRunAsync(runId, false, new Dictionary<string, object>
                {
                    ["error"] = subagentResult.ErrorMessage ?? "Subagent execution failed",
                    ["errorCategory"] = subagentResult.ResultData.GetValueOrDefault("errorCategory", "subagent_failure"),
                    ["subagentRunId"] = subagentResult.RunId
                }, ct);

                // Append execution failure event
                AppendTaskExecutionEvent(request.TaskId, runId, "task_execution_failed", new Dictionary<string, object>
                {
                    ["taskId"] = request.TaskId,
                    ["runId"] = runId,
                    ["success"] = false,
                    ["error"] = subagentResult.ErrorMessage ?? "Subagent execution failed",
                    ["errorCategory"] = subagentResult.ResultData.GetValueOrDefault("errorCategory", "subagent_failure")
                });

                return CreateFailureResult(runId, subagentResult.ErrorMessage ?? "Subagent execution failed");
            }

            // Collect modified files from subagent result
            modifiedFiles.AddRange(subagentResult.ModifiedFiles);

            var normalizedOutput = CreateNormalizedOutput(plan, modifiedFiles);
            var deterministicHash = ComputeDeterministicHash(normalizedOutput);

            var resultData = new Dictionary<string, object>
            {
                ["taskId"] = request.TaskId,
                ["planSteps"] = plan.Steps.Count,
                ["fileScopes"] = plan.FileScopes.Select(f => f.Path).ToList(),
                ["normalizedOutput"] = normalizedOutput,
                ["deterministicHash"] = deterministicHash
            };

            await _runLifecycleManager.FinishRunAsync(runId, true, resultData, ct);

            // Update task evidence pointer (Task 2.5)
            UpdateTaskEvidencePointer(request.TaskId, runId, modifiedFiles);

            // Append execution completion event (Task 2.6)
            AppendTaskExecutionEvent(request.TaskId, runId, "task_execution_completed", new Dictionary<string, object>
            {
                ["taskId"] = request.TaskId,
                ["runId"] = runId,
                ["success"] = true,
                ["deterministicHash"] = deterministicHash,
                ["filesModified"] = modifiedFiles.Count
            });

            _logger.LogInformation(
                "Completed task execution for {TaskId} with run {RunId}",
                request.TaskId,
                runId);

            return new TaskExecutionResult
            {
                Success = true,
                RunId = runId,
                NormalizedOutput = normalizedOutput,
                DeterministicHash = deterministicHash,
                ModifiedFiles = modifiedFiles.AsReadOnly(),
                EvidenceArtifacts = new[] { $".aos/evidence/runs/{runId}/" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Task execution failed for {TaskId}",
                request.TaskId);

            if (!string.IsNullOrEmpty(runId))
            {
                await _runLifecycleManager.FinishRunAsync(runId, false, new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["exceptionType"] = ex.GetType().Name
                }, ct);

                // Append execution failure event (Task 2.6)
                AppendTaskExecutionEvent(request.TaskId, runId, "task_execution_failed", new Dictionary<string, object>
                {
                    ["taskId"] = request.TaskId,
                    ["runId"] = runId,
                    ["success"] = false,
                    ["error"] = ex.Message,
                    ["exceptionType"] = ex.GetType().Name
                });
            }

            return CreateFailureResult(runId, ex.Message);
        }
    }

    private static (bool IsValid, string? ErrorMessage) ValidateFileScopes(
        TaskPlanModel plan,
        IReadOnlyList<string> allowedFileScope)
    {
        if (allowedFileScope == null || allowedFileScope.Count == 0)
        {
            return (false, "No allowed file scope specified in request");
        }

        foreach (var scope in plan.FileScopes)
        {
            if (!IsPathInAllowedScope(scope.Path, allowedFileScope))
            {
                return (false, $"Plan file scope '{scope.Path}' is outside allowed scope: {string.Join(", ", allowedFileScope)}");
            }
        }

        return (true, null);
    }

    private static bool IsPathInAllowedScope(string path, IReadOnlyList<string> allowedScopes)
    {
        var normalizedPath = NormalizePath(path);

        foreach (var scope in allowedScopes)
        {
            var normalizedScope = NormalizePath(scope);

            if (normalizedPath == normalizedScope)
            {
                return true;
            }

            if (normalizedScope.EndsWith("/") || normalizedScope.EndsWith("\\"))
            {
                if (normalizedPath.StartsWith(normalizedScope, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                var scopeWithSeparator = normalizedScope + "/";
                if (normalizedPath.StartsWith(scopeWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var scopeWithBackslash = normalizedScope + "\\";
                if (normalizedPath.StartsWith(scopeWithBackslash, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static string CreateNormalizedOutput(TaskPlanModel plan, List<string> modifiedFiles)
    {
        var output = new
        {
            action = "task_execution",
            files = modifiedFiles,
            summary = new
            {
                planTitle = plan.Title,
                stepCount = plan.Steps.Count,
                fileScopeCount = plan.FileScopes.Count,
                filesModified = modifiedFiles.Count
            }
        };

        return JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string ComputeDeterministicHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static TaskExecutionResult CreateFailureResult(string runId, string errorMessage)
    {
        return new TaskExecutionResult
        {
            Success = false,
            RunId = runId ?? string.Empty,
            ErrorMessage = errorMessage,
            ModifiedFiles = Array.Empty<string>(),
            EvidenceArtifacts = Array.Empty<string>()
        };
    }

    /// <summary>
    /// Updates the task evidence pointer (latest.json) after successful task execution.
    /// </summary>
    private void UpdateTaskEvidencePointer(string taskId, string runId, List<string> modifiedFiles)
    {
        try
        {
            var diffstat = new AosTaskEvidenceLatestWriter.TaskEvidenceDiffstat(
                FilesChanged: modifiedFiles.Count,
                Insertions: 0, // To be calculated if needed
                Deletions: 0  // To be calculated if needed
            );

            AosTaskEvidenceLatestWriter.WriteLatest(
                aosRootPath: _workspace.AosRootPath,
                taskId: taskId,
                runId: runId,
                gitCommit: null, // Git commit can be added later if needed
                diffstat: diffstat
            );

            _logger.LogDebug(
                "Updated task evidence pointer for {TaskId} with run {RunId}",
                taskId,
                runId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to update task evidence pointer for {TaskId}",
                taskId);
            // Don't fail the entire execution if evidence pointer update fails
        }
    }

    /// <summary>
    /// Appends a task execution event to the events.ndjson log.
    /// </summary>
    private void AppendTaskExecutionEvent(string taskId, string runId, string eventType, Dictionary<string, object> data)
    {
        try
        {
            var eventPayload = new Dictionary<string, object>
            {
                ["eventType"] = eventType,
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
                ["taskId"] = taskId,
                ["runId"] = runId
            };

            // Merge additional data
            foreach (var kvp in data)
            {
                if (!eventPayload.ContainsKey(kvp.Key))
                {
                    eventPayload[kvp.Key] = kvp.Value;
                }
            }

            var jsonElement = JsonSerializer.SerializeToDocument(eventPayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }).RootElement;

            _stateStore.AppendEvent(jsonElement);

            _logger.LogDebug(
                "Appended state event {EventType} for task {TaskId}",
                eventType,
                taskId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to append state event {EventType} for task {TaskId}",
                eventType,
                taskId);
            // Don't fail the entire execution if event append fails
        }
    }
}
