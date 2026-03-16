using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using nirmata.Agents.Execution.ToolCalling;
using nirmata.Agents.Persistence.Runs;
using nirmata.Aos.Contracts.Tools;
using nirmata.Aos.Public;
using Microsoft.Extensions.Logging;

namespace nirmata.Agents.Execution.Execution.SubagentRuns;

/// <summary>
/// Orchestrates subagent runs with fresh context isolation and per-invocation RUN record creation.
/// </summary>
public sealed class SubagentOrchestrator : ISubagentOrchestrator
{
    private readonly IRunLifecycleManager _runLifecycleManager;
    private readonly IToolCallingLoop _toolCallingLoop;
    private readonly IWorkspace _workspace;
    private readonly ILogger<SubagentOrchestrator> _logger;

    public SubagentOrchestrator(
        IRunLifecycleManager runLifecycleManager,
        IToolCallingLoop toolCallingLoop,
        IWorkspace workspace,
        ILogger<SubagentOrchestrator> logger)
    {
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _toolCallingLoop = toolCallingLoop ?? throw new ArgumentNullException(nameof(toolCallingLoop));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SubagentRunResult> RunSubagentAsync(SubagentRunRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runId = string.Empty;
        var modifiedFiles = new List<string>();
        var toolCalls = new List<SubagentToolCall>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Create a distinct RUN record for this subagent invocation (Task 3.3)
            var runContext = await _runLifecycleManager.StartRunAsync(ct);
            runId = runContext.RunId;

            // Record subagent start command (Task 3.4 - evidence capture)
            await _runLifecycleManager.RecordCommandAsync(runId, "subagent", "start", "running", ct);

            // Load context packs with budget enforcement (Task 3.2)
            var contextPackResult = await LoadContextPacksAsync(request, runId, ct);
            if (!contextPackResult.Success)
            {
                throw new SubagentContextLoadException(
                    $"Failed to load context packs: {contextPackResult.ErrorMessage}",
                    contextPackResult.FailedPackId);
            }

            // Initialize fresh context isolation (Task 3.1)
            var isolatedContext = await CreateIsolatedExecutionContext(request, contextPackResult.Packs);
            
            _logger.LogInformation(
                "Initialized fresh context isolation for run {RunId} with {PackCount} context packs",
                runId,
                contextPackResult.Packs.Count);

            // Store subagent-specific metadata in the run
            var runMetadata = new Dictionary<string, object>
            {
                ["taskId"] = request.TaskId,
                ["subagentConfig"] = request.SubagentConfig,
                ["contextPackIds"] = request.ContextPackIds.ToList(),
                ["parentRunId"] = request.ParentRunId ?? string.Empty,
                ["correlationId"] = request.CorrelationId ?? string.Empty,
                ["workingDirectory"] = isolatedContext.WorkingDirectory,
                ["budget"] = new
                {
                    maxIterations = request.Budget.MaxIterations,
                    maxToolCalls = request.Budget.MaxToolCalls,
                    maxExecutionTimeSeconds = request.Budget.MaxExecutionTimeSeconds,
                    maxTokens = request.Budget.MaxTokens
                }
            };

            await _runLifecycleManager.UpdateRunMetadataAsync(runId, runMetadata, ct);

            // Commit initial state to git for diff tracking (Task 5.2)
            await CommitInitialStateAsync(isolatedContext.WorkingDirectory);

            // Execute subagent logic with budget enforcement (Task 3.2)
            var executionResult = await ExecuteSubagentLogicWithBudgetAsync(
                request, 
                isolatedContext, 
                modifiedFiles, 
                toolCalls, 
                ct);

            stopwatch.Stop();

            // Generate normalized output and deterministic hash (Task 5.4, 5.5)
            var normalizedOutput = CreateNormalizedOutput(request, modifiedFiles, toolCalls);
            var deterministicHash = ComputeDeterministicHash(normalizedOutput);

            // Prepare result data for run record
            var resultData = new Dictionary<string, object>
            {
                ["taskId"] = request.TaskId,
                ["success"] = executionResult.Success,
                ["normalizedOutput"] = normalizedOutput,
                ["deterministicHash"] = deterministicHash,
                ["modifiedFiles"] = modifiedFiles,
                ["toolCallCount"] = toolCalls.Count,
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds,
                ["metrics"] = new
                {
                    iterationCount = executionResult.Iterations,
                    toolCallCount = toolCalls.Count,
                    executionTimeMs = stopwatch.ElapsedMilliseconds,
                    tokensConsumed = executionResult.TokensConsumed
                }
            };

            if (!executionResult.Success && executionResult.ErrorMessage != null)
            {
                resultData["error"] = executionResult.ErrorMessage;
                resultData["errorCategory"] = executionResult.ErrorCategory ?? "execution_failed";
            }

            // Record subagent execution completion command (Task 3.4 - evidence capture)
            await _runLifecycleManager.RecordCommandAsync(
                runId,
                "subagent",
                executionResult.Success ? "complete" : "fail",
                executionResult.Success ? "success" : "failed",
                ct);

            // Write tool calls to evidence folder (Task 3.4 - tool call evidence)
            await WriteToolCallsEvidenceAsync(runId, toolCalls, ct);

            // Capture and write file diffs to evidence folder (Task 5.2)
            await WriteFileDiffsEvidenceAsync(runId, isolatedContext.WorkingDirectory, ct);

            // Close the RUN record with final status
            await _runLifecycleManager.FinishRunAsync(runId, executionResult.Success, resultData, ct);

            _logger.LogInformation(
                "Completed subagent run {RunId} for task {TaskId} in {ElapsedMs}ms",
                runId,
                request.TaskId,
                stopwatch.ElapsedMilliseconds);

            return new SubagentRunResult
            {
                Success = executionResult.Success,
                RunId = runId,
                TaskId = request.TaskId,
                NormalizedOutput = normalizedOutput,
                DeterministicHash = deterministicHash,
                ErrorMessage = executionResult.ErrorMessage,
                ModifiedFiles = modifiedFiles.AsReadOnly(),
                ToolCalls = toolCalls.AsReadOnly(),
                EvidenceArtifacts = new[] { $".aos/evidence/runs/{runId}/" },
                Metrics = new SubagentExecutionMetrics
                {
                    IterationCount = executionResult.Iterations,
                    ToolCallCount = toolCalls.Count,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    TokensConsumed = executionResult.TokensConsumed
                },
                ResultData = resultData
            };
        }
        catch (SubagentBudgetExceededException ex)
        {
            stopwatch.Stop();
            return await HandleFailureAsync(runId, request.TaskId, ex, "budget_exceeded", stopwatch.ElapsedMilliseconds, ct);
        }
        catch (SubagentScopeViolationException ex)
        {
            stopwatch.Stop();
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorCategory"] = "scope_violation",
                ["targetPath"] = ex.TargetPath,
                ["allowedScopes"] = ex.AllowedScopes,
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds
            };
            return await HandleFailureAsync(runId, request.TaskId, ex, "scope_violation", stopwatch.ElapsedMilliseconds, ct, errorData);
        }
        catch (SubagentTimeoutException ex)
        {
            stopwatch.Stop();
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorCategory"] = "timeout",
                ["timeoutSeconds"] = ex.TimeoutSeconds,
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds
            };
            return await HandleFailureAsync(runId, request.TaskId, ex, "timeout", stopwatch.ElapsedMilliseconds, ct, errorData);
        }
        catch (SubagentContextLoadException ex)
        {
            stopwatch.Stop();
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorCategory"] = "context_load_failed",
                ["contextPackId"] = ex.ContextPackId ?? string.Empty,
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds
            };
            return await HandleFailureAsync(runId, request.TaskId, ex, "context_load_failed", stopwatch.ElapsedMilliseconds, ct, errorData);
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            var errorData = new Dictionary<string, object>
            {
                ["error"] = "Subagent execution was cancelled",
                ["errorCategory"] = "cancelled",
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds
            };
            await HandleFailureAsync(runId, request.TaskId, ex, "cancelled", stopwatch.ElapsedMilliseconds, ct, errorData);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorCategory"] = "unexpected",
                ["exceptionType"] = ex.GetType().Name,
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds,
                ["stackTrace"] = ex.StackTrace ?? string.Empty
            };
            return await HandleFailureAsync(runId, request.TaskId, ex, "unexpected", stopwatch.ElapsedMilliseconds, ct, errorData);
        }
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

    private static string CreateNormalizedOutput(SubagentRunRequest request, List<string> modifiedFiles, List<SubagentToolCall> toolCalls)
    {
        var output = new
        {
            action = "subagent_execution",
            subagentConfig = request.SubagentConfig,
            files = modifiedFiles.OrderBy(f => f).ToList(),
            toolCalls = toolCalls.Select(t => new
            {
                toolName = t.ToolName
            }).ToList(),
            summary = new
            {
                taskId = request.TaskId,
                filesModified = modifiedFiles.Count,
                toolCallCount = toolCalls.Count
            }
        };

        return JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    private static string ComputeDeterministicHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task InitializeGitInIsolatedContextAsync(string workingDirectory)
    {
        try
        {
            await RunProcessAsync("git", "init", workingDirectory);
            await RunProcessAsync("git", "config user.email \"subagent@nirmata.ai\"", workingDirectory);
            await RunProcessAsync("git", "config user.name \"Subagent\"", workingDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize git in isolated context at {WorkingDirectory}", workingDirectory);
        }
    }

    private async Task CommitInitialStateAsync(string workingDirectory)
    {
        try
        {
            await RunProcessAsync("git", "add .", workingDirectory);
            await RunProcessAsync("git", "commit -m \"Initial state\"", workingDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to commit initial state in isolated context at {WorkingDirectory}", workingDirectory);
        }
    }

    private async Task WriteFileDiffsEvidenceAsync(string runId, string workingDirectory, CancellationToken ct)
    {
        try
        {
            var diffResult = await RunProcessAsync("git", "diff HEAD", workingDirectory);
            if (string.IsNullOrWhiteSpace(diffResult.Output))
            {
                return;
            }

            var evidenceFolder = Path.Combine(_workspace.AosRootPath, "evidence", "runs", runId);
            var diffPath = Path.Combine(evidenceFolder, "changes.diff");

            await File.WriteAllTextAsync(diffPath, diffResult.Output, ct);
            _logger.LogDebug("Wrote file diffs to evidence folder for run {RunId}", runId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write file diffs evidence for run {RunId}", runId);
        }
    }

    private async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string command, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }

    private static SubagentRunResult CreateFailureResult(string? runId, string taskId, string errorMessage)
    {
        return new SubagentRunResult
        {
            Success = false,
            RunId = runId ?? string.Empty,
            TaskId = taskId,
            ErrorMessage = errorMessage,
            ModifiedFiles = Array.Empty<string>(),
            ToolCalls = Array.Empty<SubagentToolCall>(),
            EvidenceArtifacts = Array.Empty<string>()
        };
    }

    /// <summary>
    /// Writes tool call evidence to the run's evidence folder (Task 3.4).
    /// </summary>
    private async Task WriteToolCallsEvidenceAsync(string runId, List<SubagentToolCall> toolCalls, CancellationToken ct)
    {
        if (toolCalls.Count == 0)
        {
            return;
        }

        try
        {
            var evidenceFolder = Path.Combine(_workspace.AosRootPath, "evidence", "runs", runId);
            var toolCallsPath = Path.Combine(evidenceFolder, "tool-calls.json");

            var toolCallsData = new
            {
                schemaVersion = 1,
                runId,
                recordedAt = DateTimeOffset.UtcNow,
                count = toolCalls.Count,
                calls = toolCalls.Select(t => new
                {
                    toolName = t.ToolName,
                    arguments = t.Arguments,
                    result = t.Result,
                    timestamp = t.Timestamp
                }).ToList()
            };

            var json = JsonSerializer.Serialize(toolCallsData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await File.WriteAllTextAsync(toolCallsPath, json, ct);

            _logger.LogDebug(
                "Wrote {Count} tool calls to evidence folder for run {RunId}",
                toolCalls.Count,
                runId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to write tool calls evidence for run {RunId}",
                runId);
            // Don't fail the entire execution if evidence writing fails
        }
    }

    /// <summary>
    /// Handles failure by recording error details, closing the run, and returning a failure result.
    /// </summary>
    private async Task<SubagentRunResult> HandleFailureAsync(
        string runId,
        string taskId,
        Exception ex,
        string errorCategory,
        long executionTimeMs,
        CancellationToken ct,
        Dictionary<string, object>? additionalData = null)
    {
        _logger.LogError(
            ex,
            "Subagent run {RunId} failed for task {TaskId} with category {ErrorCategory}: {ErrorMessage}",
            runId,
            taskId,
            errorCategory,
            ex.Message);

        if (!string.IsNullOrEmpty(runId))
        {
            // Record failure command (Task 3.4 - evidence capture)
            await _runLifecycleManager.RecordCommandAsync(runId, "subagent", "fail", "failed", ct);

            // Build error data for run record
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorCategory"] = errorCategory,
                ["executionTimeMs"] = executionTimeMs
            };

            // Merge additional data if provided
            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    if (!errorData.ContainsKey(kvp.Key))
                    {
                        errorData[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Close the run record with failure status
            await _runLifecycleManager.FinishRunAsync(runId, false, errorData, ct);
        }

        // Create failure result with error category for upstream handling
        var failureResult = CreateFailureResult(runId, taskId, ex.Message);
        failureResult.ResultData["errorCategory"] = errorCategory;
        failureResult.ResultData["executionTimeMs"] = executionTimeMs;

        return failureResult;
    }

    private sealed class SubagentExecutionResult
    {
        public bool Success { get; init; }
        public int Iterations { get; init; }
        public int TokensConsumed { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ErrorCategory { get; init; }
        public string? FinalMessage { get; init; }
        public IReadOnlyList<ToolCallingMessage> ConversationHistory { get; init; } = Array.Empty<ToolCallingMessage>();
    }

    /// <summary>
    /// Loads context packs with budget enforcement (Task 3.2).
    /// </summary>
    private async Task<ContextPackLoadResult> LoadContextPacksAsync(SubagentRunRequest request, string runId, CancellationToken ct)
    {
        var packs = new List<ContextPack>();
        var totalBytes = 0;
        var maxTotalBytes = request.Budget.MaxTokens * 4; // Rough approximation: ~4 bytes per token

        foreach (var packId in request.ContextPackIds)
        {
            try
            {
                // Attempt to load context pack from workspace
                var packPath = Path.Combine(_workspace.AosRootPath, "context", "packs", $"{packId}.json");
                
                if (!File.Exists(packPath))
                {
                    _logger.LogWarning("Context pack {PackId} not found at {PackPath}", packId, packPath);
                    return new ContextPackLoadResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"Context pack '{packId}' not found",
                        FailedPackId = packId
                    };
                }

                var packJson = await File.ReadAllTextAsync(packPath, ct);
                var packBytes = Encoding.UTF8.GetByteCount(packJson);

                // Check budget before adding pack
                if (totalBytes + packBytes > maxTotalBytes)
                {
                    _logger.LogError(
                        "Context pack {PackId} ({PackBytes} bytes) exceeds budget. Total would be {TotalBytes}/{MaxBytes}",
                        packId,
                        packBytes,
                        totalBytes + packBytes,
                        maxTotalBytes);

                    throw new SubagentBudgetExceededException(
                        $"Context pack '{packId}' would exceed token budget. " +
                        $"Pack size: {packBytes} bytes, Budget: {maxTotalBytes} bytes.");
                }

                var pack = JsonSerializer.Deserialize<ContextPack>(packJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (pack == null)
                {
                    return new ContextPackLoadResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"Failed to parse context pack '{packId}'",
                        FailedPackId = packId
                    };
                }

                packs.Add(pack);
                totalBytes += packBytes;

                _logger.LogDebug(
                    "Loaded context pack {PackId} ({PackBytes} bytes) for run {RunId}. Total: {TotalBytes}/{MaxBytes}",
                    packId,
                    packBytes,
                    runId,
                    totalBytes,
                    maxTotalBytes);
            }
            catch (SubagentBudgetExceededException)
            {
                throw; // Re-throw budget exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load context pack {PackId}", packId);
                return new ContextPackLoadResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Error loading context pack '{packId}': {ex.Message}",
                    FailedPackId = packId
                };
            }
        }

        // Log budget utilization
        var utilizationPercent = maxTotalBytes > 0 ? (totalBytes * 100.0 / maxTotalBytes) : 0;
        _logger.LogInformation(
            "Loaded {PackCount} context packs for run {RunId}. Total: {TotalBytes} bytes ({Utilization:F1}% of budget)",
            packs.Count,
            runId,
            totalBytes,
            utilizationPercent);

        return new ContextPackLoadResult 
        { 
            Success = true, 
            Packs = packs,
            TotalBytes = totalBytes
        };
    }

    /// <summary>
    /// Creates an isolated execution context for the subagent (Task 3.1).
    /// </summary>
    private async Task<IsolatedExecutionContext> CreateIsolatedExecutionContext(SubagentRunRequest request, List<ContextPack> packs)
    {
        // Create fresh isolated context - no shared state from parent or previous runs
        var isolatedContext = new IsolatedExecutionContext
        {
            TaskId = request.TaskId,
            SubagentConfig = request.SubagentConfig,
            AllowedFileScope = request.AllowedFileScope.ToList(),
            ContextPacks = packs,
            EnvironmentVariables = new Dictionary<string, string>(), // Fresh env - no inheritance
            WorkingDirectory = Path.Combine(_workspace.AosRootPath, "temp", $"subagent-{Guid.NewGuid():N}"),
            StartTime = DateTimeOffset.UtcNow
        };

        // Create isolated working directory
        try
        {
            Directory.CreateDirectory(isolatedContext.WorkingDirectory);
            
            // Initialize git repository for diff tracking (Task 5.2)
            await InitializeGitInIsolatedContextAsync(isolatedContext.WorkingDirectory);

            // Copy context pack files to isolated directory
            foreach (var pack in packs)
            {
                if (pack.Files != null)
                {
                    foreach (var file in pack.Files)
                    {
                        var targetPath = Path.Combine(isolatedContext.WorkingDirectory, file.RelativePath);
                        var targetDir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                        File.WriteAllText(targetPath, file.Content);
                    }
                }
            }

            _logger.LogDebug(
                "Created isolated execution context at {WorkingDirectory} for task {TaskId}",
                isolatedContext.WorkingDirectory,
                request.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create isolated execution context");
            throw new SubagentContextLoadException(
                $"Failed to initialize isolated context: {ex.Message}");
        }

        return isolatedContext;
    }

    /// <summary>
    /// Executes subagent logic using the tool calling loop (Task 6.1).
    /// Maps SubagentBudget to ToolCallingOptions (Task 6.2).
    /// Passes context pack tools to the loop (Task 6.3).
    /// Handles loop completion/failure translation (Task 6.4).
    /// </summary>
    private async Task<SubagentExecutionResult> ExecuteSubagentLogicWithBudgetAsync(
        SubagentRunRequest request,
        IsolatedExecutionContext context,
        List<string> modifiedFiles,
        List<SubagentToolCall> toolCalls,
        CancellationToken ct)
    {
        var iterationCount = 0;
        var tokensConsumed = 0;
        var startTime = DateTimeOffset.UtcNow;
        var maxExecutionTime = TimeSpan.FromSeconds(request.Budget.MaxExecutionTimeSeconds);

        // Build tool calling options from subagent budget (Task 6.2)
        var toolCallingOptions = new ToolCallingOptions
        {
            MaxIterations = request.Budget.MaxIterations,
            MaxToolCalls = request.Budget.MaxToolCalls,
            Timeout = maxExecutionTime,
            MaxTotalTokens = request.Budget.MaxTokens,
            EnableParallelToolExecution = true,
            MaxParallelToolExecutions = 32
        };

        // Build available tools from context packs (Task 6.3)
        var availableTools = BuildToolDefinitionsFromContextPacks(context.ContextPacks);

        // Build initial conversation messages
        var messages = BuildInitialMessages(request, context);

        // Create tool calling request
        var toolCallingRequest = new ToolCallingRequest
        {
            Messages = messages,
            Tools = availableTools,
            Options = toolCallingOptions,
            CorrelationId = request.CorrelationId ?? request.RunId,
            Context = new Dictionary<string, object?>
            {
                ["runId"] = request.RunId,
                ["taskId"] = request.TaskId,
                ["subagentConfig"] = request.SubagentConfig,
                ["parentRunId"] = request.ParentRunId,
                ["allowedFileScope"] = request.AllowedFileScope,
                ["workingDirectory"] = context.WorkingDirectory
            }
        };

        try
        {
            // Execute the tool calling loop (Task 6.1)
            var result = await _toolCallingLoop.ExecuteAsync(toolCallingRequest, ct);

            // Translate loop results to subagent results (Task 6.4)
            iterationCount = result.IterationCount;
            tokensConsumed = result.Usage?.TotalTokens ?? 0;

            // Extract tool calls from conversation history
            ExtractToolCallsFromHistory(result.ConversationHistory, toolCalls);

            // Extract modified files from tool call results
            ExtractModifiedFilesFromHistory(result.ConversationHistory, modifiedFiles);

            // Handle completion reason translation
            var (success, errorMessage, errorCategory) = TranslateCompletionReason(result.CompletionReason, result.Error);

            return new SubagentExecutionResult
            {
                Success = success,
                Iterations = iterationCount,
                TokensConsumed = tokensConsumed,
                ErrorMessage = errorMessage,
                ErrorCategory = errorCategory,
                FinalMessage = result.FinalMessage.Content,
                ConversationHistory = result.ConversationHistory
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool calling loop failed for subagent run {RunId}", request.RunId);
            return new SubagentExecutionResult
            {
                Success = false,
                Iterations = iterationCount,
                TokensConsumed = tokensConsumed,
                ErrorMessage = $"Tool calling execution failed: {ex.Message}",
                ErrorCategory = "tool_execution_failed"
            };
        }
    }

    /// <summary>
    /// Builds tool definitions from context packs (Task 6.3).
    /// </summary>
    private static IReadOnlyList<ToolCallingToolDefinition> BuildToolDefinitionsFromContextPacks(List<ContextPack> packs)
    {
        var tools = new List<ToolCallingToolDefinition>();

        foreach (var pack in packs)
        {
            if (pack.Tools != null)
            {
                foreach (var tool in pack.Tools)
                {
                    tools.Add(new ToolCallingToolDefinition
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        ParametersSchema = tool.ParametersSchema ?? new { type = "object", properties = new Dictionary<string, object>(), required = Array.Empty<string>() }
                    });
                }
            }
        }

        return tools;
    }

    /// <summary>
    /// Builds initial conversation messages from request and context.
    /// </summary>
    private static IReadOnlyList<ToolCallingMessage> BuildInitialMessages(SubagentRunRequest request, IsolatedExecutionContext context)
    {
        var messages = new List<ToolCallingMessage>();

        // Build context pack information
        var contextPacksInfo = new StringBuilder();
        foreach (var pack in context.ContextPacks)
        {
            contextPacksInfo.AppendLine($"- {pack.Name} ({pack.Id})");
            if (pack.Files.Count > 0)
            {
                contextPacksInfo.AppendLine("  Files:");
                foreach (var file in pack.Files)
                {
                    contextPacksInfo.AppendLine($"    - {file.RelativePath}");
                }
            }
        }

        // Add system message with comprehensive context (Task 2.1, 2.3, 2.4)
        var systemContent = $"""
            You are a subagent executing task: {request.TaskId}
            Configuration: {request.SubagentConfig}
            Working Directory: {context.WorkingDirectory}
            
            # BOUNDED CONTEXT
            You have access to the following context packs that provide necessary information and tools:
            {contextPacksInfo}

            # ALLOWED FILE SCOPE
            Your operations are strictly limited to the following paths:
            {string.Join("\n", request.AllowedFileScope.Select(s => $"- {s}"))}
            
            CRITICAL: Any attempt to read or write files outside this scope will be blocked and recorded as a security violation.
            
            # EXECUTION GUIDELINES
            1. OBJECTIVE: Complete the assigned task efficiently and correctly.
            2. SCOPE: Stay within your assigned file scope. Do not attempt to access or modify files elsewhere.
            3. TOOLS: Use the provided tools to interact with the environment. Prefer tools over manual reasoning for factual checks.
            4. ERRORS: If a tool returns an error, analyze the output, adjust your approach, and retry if appropriate.
            5. SUMMARY: Provide a concise but complete summary of what you did when finished.
            
            # VERIFICATION REQUIREMENTS
            Before completing the task, you MUST verify your work:
            1. CODE QUALITY: Ensure modified code is syntactically correct and follows project standards.
            2. FUNCTIONAL VERIFICATION: Run relevant tests or build commands using the `process_runner` tool to confirm your changes work as expected.
            3. SIDE EFFECTS: Verify that your changes did not break related functionality.
            4. PERSISTENCE: Use `file_read` to confirm that your `file_write` operations were successful and the content is correct.
            """;

        messages.Add(ToolCallingMessage.System(systemContent));

        // Add user message with task context and plan (Task 2.2)
        var userContent = new StringBuilder();
        userContent.AppendLine($"Execute the following task: {request.TaskId}");
        
        if (request.ContextData.TryGetValue("taskPlan", out var taskPlan))
        {
            userContent.AppendLine("\n# TASK PLAN");
            userContent.AppendLine(taskPlan?.ToString() ?? "No plan provided.");
        }

        if (request.ContextData.Count > 0)
        {
            var otherContext = request.ContextData
                .Where(kvp => kvp.Key != "taskPlan")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
            if (otherContext.Count > 0)
            {
                var contextJson = JsonSerializer.Serialize(otherContext, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
                userContent.AppendLine("\n# ADDITIONAL CONTEXT");
                userContent.AppendLine(contextJson);
            }
        }

        messages.Add(ToolCallingMessage.User(userContent.ToString()));

        return messages;
    }

    /// <summary>
    /// Extracts tool calls and their results from conversation history.
    /// </summary>
    private static void ExtractToolCallsFromHistory(
        IReadOnlyList<ToolCallingMessage> history,
        List<SubagentToolCall> toolCalls)
    {
        // Dictionary to track assistant tool calls by their ID
        var assistantToolCalls = new Dictionary<string, (string Name, string Arguments)>();

        foreach (var message in history)
        {
            if (message.Role == ToolCallingRole.Assistant && message.ToolCalls?.Count > 0)
            {
                foreach (var tc in message.ToolCalls)
                {
                    assistantToolCalls[tc.Id] = (tc.Name, tc.ArgumentsJson);
                }
            }
            else if (message.Role == ToolCallingRole.Tool && !string.IsNullOrEmpty(message.ToolCallId))
            {
                if (assistantToolCalls.TryGetValue(message.ToolCallId, out var tcInfo))
                {
                    toolCalls.Add(new SubagentToolCall
                    {
                        ToolName = tcInfo.Name,
                        Arguments = tcInfo.Arguments,
                        Result = message.Content,
                        Timestamp = DateTimeOffset.UtcNow
                    });
                    
                    // Remove from dictionary so we don't process it twice if there are multiple tool messages
                    assistantToolCalls.Remove(message.ToolCallId);
                }
            }
        }

        // Add any tool calls that didn't get a result (though this shouldn't happen in a successful run)
        foreach (var kvp in assistantToolCalls)
        {
            toolCalls.Add(new SubagentToolCall
            {
                ToolName = kvp.Value.Name,
                Arguments = kvp.Value.Arguments,
                Result = null,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Extracts modified files from tool result messages in conversation history.
    /// </summary>
    private static void ExtractModifiedFilesFromHistory(
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
                    var doc = JsonDocument.Parse(message.Content);
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
                }
                catch
                {
                    // Ignore parsing errors for non-JSON tool results
                }
            }
        }
    }

    /// <summary>
    /// Translates ToolCallingCompletionReason to subagent success/error (Task 6.4).
    /// </summary>
    private static (bool success, string? errorMessage, string? errorCategory) TranslateCompletionReason(
        ToolCallingCompletionReason reason,
        ToolCallingError? error)
    {
        return reason switch
        {
            ToolCallingCompletionReason.CompletedNaturally => (true, null, null),
            ToolCallingCompletionReason.MaxIterationsReached => (false, error?.Message ?? "Maximum iterations reached", "max_iterations"),
            ToolCallingCompletionReason.Timeout => (false, error?.Message ?? "Execution timed out", "timeout"),
            ToolCallingCompletionReason.Error => (false, error?.Message ?? "An error occurred during execution", "tool_error"),
            ToolCallingCompletionReason.Cancelled => (false, error?.Message ?? "Execution was cancelled", "cancelled"),
            _ => (false, $"Unknown completion reason: {reason}", "unknown")
        };
    }

    /// <summary>
    /// Result of context pack loading operation.
    /// </summary>
    private sealed class ContextPackLoadResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string? FailedPackId { get; init; }
        public List<ContextPack> Packs { get; init; } = new();
        public int TotalBytes { get; init; }
    }

    /// <summary>
    /// Represents a loaded context pack.
    /// </summary>
    private sealed class ContextPack
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public List<ContextPackFile> Files { get; init; } = new();
        public Dictionary<string, object> Metadata { get; init; } = new();
        public List<ContextPackTool>? Tools { get; init; }
    }

    /// <summary>
    /// Represents a tool definition within a context pack.
    /// </summary>
    private sealed class ContextPackTool
    {
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public object? ParametersSchema { get; init; }
    }

    /// <summary>
    /// Represents a file within a context pack.
    /// </summary>
    private sealed class ContextPackFile
    {
        public string RelativePath { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents an isolated execution context for a subagent run.
    /// </summary>
    private sealed class IsolatedExecutionContext
    {
        public string TaskId { get; init; } = string.Empty;
        public string SubagentConfig { get; init; } = string.Empty;
        public List<string> AllowedFileScope { get; init; } = new();
        public List<ContextPack> ContextPacks { get; init; } = new();
        public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
        public string WorkingDirectory { get; init; } = string.Empty;
        public DateTimeOffset StartTime { get; init; }
    }
}
