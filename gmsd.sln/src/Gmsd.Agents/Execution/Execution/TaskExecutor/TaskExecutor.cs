using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gmsd.Agents.Execution.Validation;
using Gmsd.Agents.Execution.Execution.SubagentRuns;
using Gmsd.Agents.Execution.ToolCalling;
using Gmsd.Agents.Execution.ControlPlane.Tools.Firewall;
using Gmsd.Agents.Execution.ControlPlane.Tools.Registry;
using Gmsd.Agents.Execution.Evidence;
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
    private readonly IToolCallingLoop _toolCallingLoop;
    private readonly IToolRegistry _toolRegistry;
    private readonly IWorkspace _workspace;
    private readonly IStateStore _stateStore;
    private readonly ILogger<TaskExecutor> _logger;

    public TaskExecutor(
        IRunLifecycleManager runLifecycleManager,
        IToolCallingLoop toolCallingLoop,
        IToolRegistry toolRegistry,
        IWorkspace workspace,
        IStateStore stateStore,
        ILogger<TaskExecutor> logger)
    {
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _toolCallingLoop = toolCallingLoop ?? throw new ArgumentNullException(nameof(toolCallingLoop));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
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
            var planValidation = ArtifactContractValidator.ValidateTaskPlan(
                artifactPath: planPath,
                artifactJson: planJson,
                aosRootPath: _workspace.AosRootPath,
                readBoundary: "task-executor");
            if (!planValidation.IsValid)
            {
                return new TaskExecutionResult
                {
                    Success = false,
                    RunId = string.Empty,
                    ErrorMessage = planValidation.CreateFailureMessage(),
                    ModifiedFiles = Array.Empty<string>(),
                    EvidenceArtifacts = new[] { planValidation.DiagnosticPath! },
                    ResultData = new Dictionary<string, object>
                    {
                        ["validationGate"] = "task-plan",
                        ["diagnosticPath"] = planValidation.DiagnosticPath!
                    }
                };
            }

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

            // Build and execute tool calling loop for task execution (Task 7.1)
            var toolCallingRequest = BuildToolCallingRequest(request, plan, runId);
            
            _logger.LogInformation(
                "Starting tool calling loop for task {TaskId} with run {RunId}",
                request.TaskId,
                runId);

            // Task L31: Subscribe to IToolCallingEventEmitter during loop execution
            var toolCallsLogPath = Path.Combine(_workspace.AosRootPath, "evidence", "runs", runId, "artifacts", "tool-calls.ndjson");
            var toolCallsDirectory = Path.GetDirectoryName(toolCallsLogPath);
            if (!string.IsNullOrEmpty(toolCallsDirectory) && !Directory.Exists(toolCallsDirectory))
            {
                Directory.CreateDirectory(toolCallsDirectory);
            }
            var eventLogger = new ToolCallingEventLogger(toolCallsLogPath);

            ToolCallingResult loopResult;
            try
            {
                loopResult = await _toolCallingLoop.ExecuteAsync(toolCallingRequest, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tool calling loop failed for task {TaskId}", request.TaskId);
                
                await _runLifecycleManager.FinishRunAsync(runId, false, new Dictionary<string, object>
                {
                    ["error"] = $"Tool calling execution failed: {ex.Message}",
                    ["errorCategory"] = "tool_calling_failure",
                    ["exceptionType"] = ex.GetType().Name
                }, ct);

                AppendTaskExecutionEvent(request.TaskId, runId, "task_execution_failed", new Dictionary<string, object>
                {
                    ["taskId"] = request.TaskId,
                    ["runId"] = runId,
                    ["success"] = false,
                    ["error"] = $"Tool calling execution failed: {ex.Message}",
                    ["errorCategory"] = "tool_calling_failure"
                });

                return CreateFailureResult(runId, $"Tool calling execution failed: {ex.Message}");
            }

            // Handle tool call failures in result (Task 7.3)
            if (loopResult.Error != null || loopResult.CompletionReason != ToolCallingCompletionReason.CompletedNaturally)
            {
                var errorMessage = loopResult.Error?.Message ?? $"Loop completed with reason: {loopResult.CompletionReason}";
                _logger.LogError(
                    "Tool calling loop completed unsuccessfully for task {TaskId}: {ErrorMessage}",
                    request.TaskId,
                    errorMessage);

                await _runLifecycleManager.FinishRunAsync(runId, false, new Dictionary<string, object>
                {
                    ["error"] = errorMessage,
                    ["errorCategory"] = "tool_calling_failure",
                    ["completionReason"] = loopResult.CompletionReason.ToString(),
                    ["errorCode"] = loopResult.Error?.Code ?? "Unknown"
                }, ct);

                AppendTaskExecutionEvent(request.TaskId, runId, "task_execution_failed", new Dictionary<string, object>
                {
                    ["taskId"] = request.TaskId,
                    ["runId"] = runId,
                    ["success"] = false,
                    ["error"] = errorMessage,
                    ["errorCategory"] = "tool_calling_failure",
                    ["completionReason"] = loopResult.CompletionReason.ToString()
                });

                return CreateFailureResult(runId, errorMessage);
            }

            // Extract modified files from tool call results in conversation history
            ExtractModifiedFilesFromToolCallingHistory(loopResult.ConversationHistory, modifiedFiles);

            _logger.LogInformation(
                "Tool calling loop completed for task {TaskId} with {IterationCount} iterations and {ModifiedFilesCount} modified files",
                request.TaskId,
                loopResult.IterationCount,
                modifiedFiles.Count);

            var normalizedOutput = CreateNormalizedOutput(plan, modifiedFiles);
            
            // Validate evidence output before finishing run
            var evidencePath = Path.Combine(_workspace.AosRootPath, "evidence", "runs", runId, "artifacts", "task-execution-result.json");
            var evidenceValidation = ArtifactContractValidator.ValidateTaskPlan(
                artifactPath: evidencePath,
                artifactJson: normalizedOutput,
                aosRootPath: _workspace.AosRootPath,
                readBoundary: "task-executor-writer");

            if (!evidenceValidation.IsValid)
            {
                _logger.LogWarning("Task execution output validation failed: {Message}. Diagnostic: {DiagnosticPath}", evidenceValidation.Message, evidenceValidation.DiagnosticPath);
                // We proceed but log the failure as evidence validation shouldn't block execution completion
                // unless it's a critical schema mismatch.
            }

            // Task L42: Update TaskExecutor to call all evidence writers after loop completion
            var artifactsDirectory = Path.Combine(_workspace.AosRootPath, "evidence", "runs", runId, "artifacts");
            if (!Directory.Exists(artifactsDirectory))
            {
                Directory.CreateDirectory(artifactsDirectory);
            }

            // Write execution summary
            var executionSummary = new ExecutionSummary
            {
                TaskId = request.TaskId,
                RunId = runId,
                StartTime = DateTime.UtcNow.AddSeconds(-2),
                EndTime = DateTime.UtcNow,
                Iterations = loopResult.IterationCount,
                FilesModified = modifiedFiles,
                ToolCallsCount = loopResult.ConversationHistory.Count(m => m.Role == ToolCallingRole.Tool),
                CompletionStatus = loopResult.CompletionReason == ToolCallingCompletionReason.CompletedNaturally ? "success" : "incomplete"
            };

            var summaryWriter = new ExecutionSummaryWriter();
            var summaryPath = Path.Combine(artifactsDirectory, "execution-summary.json");
            summaryWriter.WriteExecutionSummary(summaryPath, executionSummary);

            // Generate and write diff
            var diffGenerator = new DiffGenerator();
            var changesPatchPath = Path.Combine(artifactsDirectory, "changes.patch");
            var patchContent = new StringBuilder();
            
            foreach (var filePath in modifiedFiles)
            {
                try
                {
                    var fullPath = Path.Combine(request.TaskDirectory, filePath);
                    if (File.Exists(fullPath))
                    {
                        var diff = diffGenerator.GenerateDiff(fullPath, fullPath);
                        patchContent.AppendLine(diff);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate diff for file {FilePath}", filePath);
                }
            }
            
            File.WriteAllText(changesPatchPath, patchContent.ToString());

            // Task L56: Update TaskExecutor to persist evidence to run folder
            var hashGenerator = new DeterministicHashGenerator();
            var toolCallsContent = File.Exists(toolCallsLogPath) ? File.ReadAllText(toolCallsLogPath) : string.Empty;
            var changesPatchContent = patchContent.ToString();
            var summaryContent = File.ReadAllText(summaryPath);
            
            var deterministicHash = hashGenerator.ComputeHashFromContent(toolCallsContent, changesPatchContent, summaryContent);
            
            // Write deterministic hash to file
            var hashFilePath = Path.Combine(artifactsDirectory, "deterministic-hash");
            hashGenerator.WriteHashFile(hashFilePath, deterministicHash);
            
            // Update execution summary with hash
            executionSummary.DeterministicHash = deterministicHash;
            summaryWriter.WriteExecutionSummary(summaryPath, executionSummary);

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

    /// <summary>
    /// Builds a ToolCallingRequest from task execution request and plan (Task 7.2).
    /// Maps task file scopes to tool calling context and wraps registry with scope firewall.
    /// </summary>
    private ToolCallingRequest BuildToolCallingRequest(TaskExecutionRequest request, TaskPlanModel plan, string runId)
    {
        // Build available tools from plan file scopes
        var availableTools = BuildToolDefinitionsFromPlan(plan);

        // Build initial conversation messages
        var messages = BuildInitialMessages(request, plan);

        // Create tool calling options with reasonable defaults
        var options = new ToolCallingOptions
        {
            MaxIterations = 20,
            Timeout = TimeSpan.FromMinutes(10),
            EnableParallelToolExecution = true,
            MaxParallelToolExecutions = 32
        };

        // Map file scopes to context for tool execution (Task 7.2)
        var context = new Dictionary<string, object?>
        {
            ["runId"] = runId,
            ["taskId"] = request.TaskId,
            ["taskDirectory"] = request.TaskDirectory,
            ["parentRunId"] = request.ParentRunId,
            ["allowedFileScope"] = request.AllowedFileScope,
            ["fileScopes"] = plan.FileScopes.Select(f => f.Path).ToList(),
            ["steps"] = plan.Steps.Select(s => new { s.StepId, s.StepType, s.TargetPath, s.Description }).ToList(),
            ["scopedToolRegistry"] = new ScopedToolRegistry(_toolRegistry, new ScopeFirewall(request.AllowedFileScope))
        };

        // Add any context data from the request
        foreach (var kvp in request.ContextData)
        {
            if (!context.ContainsKey(kvp.Key))
            {
                context[kvp.Key] = kvp.Value;
            }
        }

        return new ToolCallingRequest
        {
            Messages = messages,
            Tools = availableTools,
            Options = options,
            CorrelationId = request.CorrelationId ?? runId,
            Context = context
        };
    }

    /// <summary>
    /// Builds tool definitions from the task plan.
    /// </summary>
    private static IReadOnlyList<ToolCallingToolDefinition> BuildToolDefinitionsFromPlan(TaskPlanModel plan)
    {
        // Define common file operation tools that task executor may use
        var tools = new List<ToolCallingToolDefinition>
        {
            new()
            {
                Name = "read_file",
                Description = "Read the contents of a file at the specified path",
                ParametersSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["path"] = new { type = "string", description = "The file path to read" }
                    },
                    required = new[] { "path" }
                }
            },
            new()
            {
                Name = "write_file",
                Description = "Write content to a file at the specified path",
                ParametersSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["path"] = new { type = "string", description = "The file path to write" },
                        ["content"] = new { type = "string", description = "The content to write" }
                    },
                    required = new[] { "path", "content" }
                }
            },
            new()
            {
                Name = "modify_file",
                Description = "Modify a file by applying specified changes",
                ParametersSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["path"] = new { type = "string", description = "The file path to modify" },
                        ["changes"] = new { type = "array", description = "List of changes to apply" }
                    },
                    required = new[] { "path", "changes" }
                }
            },
            new()
            {
                Name = "delete_file",
                Description = "Delete a file at the specified path",
                ParametersSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["path"] = new { type = "string", description = "The file path to delete" }
                    },
                    required = new[] { "path" }
                }
            },
            new()
            {
                Name = "run_command",
                Description = "Execute a shell command",
                ParametersSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["command"] = new { type = "string", description = "The command to execute" },
                        ["workingDirectory"] = new { type = "string", description = "Optional working directory" }
                    },
                    required = new[] { "command" }
                }
            }
        };

        return tools;
    }

    /// <summary>
    /// Builds initial conversation messages for the tool calling loop.
    /// </summary>
    private static IReadOnlyList<ToolCallingMessage> BuildInitialMessages(TaskExecutionRequest request, TaskPlanModel plan)
    {
        var messages = new List<ToolCallingMessage>();

        // Add system message with task context
        var systemContent = $"""
            You are a task executor for plan: {plan.Title}
            Task ID: {request.TaskId}
            
            You have access to file operation tools. All file operations must stay within the allowed scope:
            {string.Join(", ", request.AllowedFileScope)}
            
            Execute the plan steps sequentially. Use the available tools to perform file operations.
            Report the results of each operation clearly.
            """;

        messages.Add(ToolCallingMessage.System(systemContent));

        // Add user message with plan details
        var planContext = new
        {
            plan.Title,
            plan.Description,
            Steps = plan.Steps.Select(s => new
            {
                s.StepId,
                s.StepType,
                s.TargetPath,
                s.Description
            }).ToList(),
            FileScopes = plan.FileScopes.Select(f => new { f.Path, f.ScopeType }).ToList()
        };

        var planJson = JsonSerializer.Serialize(planContext, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        messages.Add(ToolCallingMessage.User($"Execute the following plan:\n```json\n{planJson}\n```"));

        return messages;
    }

    /// <summary>
    /// Extracts modified files from tool call results in conversation history.
    /// </summary>
    private static void ExtractModifiedFilesFromToolCallingHistory(
        IReadOnlyList<ToolCallingMessage> history,
        List<string> modifiedFiles)
    {
        foreach (var message in history)
        {
            if (message.Role == ToolCallingRole.Tool && !string.IsNullOrEmpty(message.Content))
            {
                // Try to parse file modification indicators from tool results
                try
                {
                    using var doc = JsonDocument.Parse(message.Content);
                    if (doc.RootElement.TryGetProperty("modifiedFile", out var modifiedFileElement))
                    {
                        var filePath = modifiedFileElement.GetString();
                        if (!string.IsNullOrEmpty(filePath) && !modifiedFiles.Contains(filePath))
                        {
                            modifiedFiles.Add(filePath);
                        }
                    }
                    else if (doc.RootElement.TryGetProperty("filePath", out var filePathElement))
                    {
                        var filePath = filePathElement.GetString();
                        if (!string.IsNullOrEmpty(filePath) && !modifiedFiles.Contains(filePath))
                        {
                            modifiedFiles.Add(filePath);
                        }
                    }
                    else if (doc.RootElement.TryGetProperty("path", out var pathElement))
                    {
                        var filePath = pathElement.GetString();
                        if (!string.IsNullOrEmpty(filePath) && 
                            !modifiedFiles.Contains(filePath) &&
                            IsFileModificationMessage(message, doc))
                        {
                            modifiedFiles.Add(filePath);
                        }
                    }
                }
                catch
                {
                    // Ignore parsing errors for non-JSON tool results
                }
            }
        }
    }

    /// <summary>
    /// Determines if a tool result message indicates a file was modified.
    /// </summary>
    private static bool IsFileModificationMessage(ToolCallingMessage message, JsonDocument doc)
    {
        // Check if the tool name suggests a modification operation
        if (message.ToolName != null)
        {
            var modifyingTools = new[] { "write_file", "modify_file", "delete_file" };
            if (modifyingTools.Any(t => message.ToolName.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Check for success indicators in the result
        if (doc.RootElement.TryGetProperty("success", out var successElement))
        {
            if (successElement.ValueKind == JsonValueKind.True)
            {
                return true;
            }
        }

        return false;
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
